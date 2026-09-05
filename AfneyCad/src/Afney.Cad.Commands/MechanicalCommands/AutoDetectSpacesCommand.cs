using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Commands.MechanicalCommands
{
    /*
        NE: Otonom Tüm Mahalleri Bul Komutu
        NEDEN: SpaceDetectionEngine'i çalıştırarak çizimdeki tüm kapalı alanları (odaları)
               kullanıcı tıklaması olmadan otomatik tespit etmek için.
    */
    public class AutoDetectSpacesCommand : ICadCommand
    {
        private readonly CadDatabase _database;
        private readonly TransactionManager _transactionManager;
        private readonly SpaceDetectionEngine _detectionEngine;
        private CompositeOperation? _pendingComposite;

        public string CommandName => "OTO_MAHAL_TANIMLA";
        public Vector3D? ActivePoint => null;

        public event Action<string>? OnFeedback;
        public event Action? OnCompleted;

        public AutoDetectSpacesCommand(CadDatabase database, TransactionManager transactionManager)
        {
            _database = database;
            _transactionManager = transactionManager;
            _detectionEngine = new SpaceDetectionEngine(database);
        }

        public void Start()
        {
            OnFeedback?.Invoke("OTO MAHAL: Tüm mimari taranıyor, lütfen bekleyin...");
            try
            {
                var spaces = _detectionEngine.DetectAllSpaces();
                
                if (spaces.Count == 0)
                {
                    OnFeedback?.Invoke("OTO MAHAL: Çizimde kapalı yüzey (oda sınırı) bulunamadı.");
                    OnCompleted?.Invoke();
                    return;
                }

                var composite = new CompositeOperation("Otonom Mahal Algılama");
                _pendingComposite = composite;

                int createdRooms = 0;
                for (int i = 0; i < spaces.Count; i++)
                {
                    var points = spaces[i];
                    var room = new RoomEntity(points, $"Oda_{i + 1}");

                    // Vitrifiye Analizi
                    AnalyzeAndAddFixtures(room);

                    composite.Add(new AddEntityOperation(_database, room));
                    createdRooms++;
                }

                _pendingComposite = null;
                _transactionManager.Submit(composite);

                OnFeedback?.Invoke($"OTO MAHAL: {createdRooms} adet oda otomatik bulundu ve eklendi.");
                Serilog.Log.Information("OTO MAHAL: {Count} alan bulundu.", createdRooms);
            }
            catch (Exception ex)
            {
                OnFeedback?.Invoke($"HATA: Otonom mahal analizi hatası. {ex.Message}");
                Serilog.Log.Error(ex, "Otonom mahal analizi hatası");
            }
            finally
            {
                OnCompleted?.Invoke();
            }
        }

        private void AnalyzeAndAddFixtures(RoomEntity mahal)
        {
            var entities = _database.GetAllEntities().ToList();
            var newFixtures = new List<SanitaryFixtureEntity>();

            foreach (var ent in entities)
            {
                if (ent is SanitaryFixtureEntity) continue;

                if (IsPointInPolygon(ent.GetBoundingBox().Center, mahal.BoundaryPoints))
                {
                    string layer = ent.Layer ?? "";
                    SanitaryFixtureEntity? fixtureEntity = null;

                    if (layer.Contains("LAV", StringComparison.OrdinalIgnoreCase))
                    {
                        fixtureEntity = new SanitaryFixtureEntity(ent.GetBoundingBox().Center, "Washbasin", 0.5);
                    }
                    else if (layer.Contains("WC", StringComparison.OrdinalIgnoreCase) || layer.Contains("KLOZET", StringComparison.OrdinalIgnoreCase))
                    {
                        fixtureEntity = new SanitaryFixtureEntity(ent.GetBoundingBox().Center, "WC", 1.0);
                    }
                    else if (layer.Contains("DUŞ", StringComparison.OrdinalIgnoreCase) || layer.Contains("SHOWER", StringComparison.OrdinalIgnoreCase))
                    {
                        fixtureEntity = new SanitaryFixtureEntity(ent.GetBoundingBox().Center, "Shower", 0.8);
                    }
                    else if (layer.Contains("EVIYE", StringComparison.OrdinalIgnoreCase) || layer.Contains("SINK", StringComparison.OrdinalIgnoreCase))
                    {
                        fixtureEntity = new SanitaryFixtureEntity(ent.GetBoundingBox().Center, "Sink", 1.0);
                    }

                    if (fixtureEntity != null)
                    {
                        newFixtures.Add(fixtureEntity);
                    }
                }
            }

            foreach(var fix in newFixtures)
            {
                _pendingComposite!.Add(new AddEntityOperation(_database, fix));
                mahal.Fixtures.Add(fix);
                mahal.TotalLoadUnits += fix.LoadUnits;
            }
            
            // Eğer içinde WC veya Lavabo varsa tipini akıllıca düzenle
            if (mahal.Fixtures.OfType<SanitaryFixtureEntity>().Any(f => f.FixtureType == "WC" || f.FixtureType == "Washbasin"))
            {
                mahal.RoomName = mahal.RoomName.Replace("Oda_", "Banyo_WC_");
                mahal.Type = Afney.Cad.Mechanical.Enums.RoomType.Bathroom;
            }
            else if (mahal.Fixtures.OfType<SanitaryFixtureEntity>().Any(f => f.FixtureType == "Sink"))
            {
                mahal.RoomName = mahal.RoomName.Replace("Oda_", "Mutfak_");
                mahal.Type = Afney.Cad.Mechanical.Enums.RoomType.Kitchen;
            }
        }

        private bool IsPointInPolygon(Vector3D p, List<Vector3D> poly)
        {
            bool isInside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) && (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    isInside = !isInside;
            }
            return isInside;
        }

        public void OnPointerPressed(Vector3D point) { }
        public void OnPointerMoved(Vector3D point) { }
        public void OnKeyDown(InputKey key) { if (key == InputKey.Escape) Cancel(); }
        public void Draw(IRenderContext context) { }
        public void Cancel() { OnCompleted?.Invoke(); }
    }
}
