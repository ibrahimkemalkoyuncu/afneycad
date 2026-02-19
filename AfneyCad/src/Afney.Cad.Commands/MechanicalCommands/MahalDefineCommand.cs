using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
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
        NE: Gelişmiş Mahal Tanımlama Komutu
        NEDEN: Mimari proje üzerinde oda sınırlarını ve içindeki cihazları bulmak için.
    */
    public class MahalDefineCommand : ICadCommand
    {
        private readonly CadDatabase _database;
        private readonly SmartBoundaryService _boundaryService;
        private readonly Action<RoomEntity> _onCompleted;
        private RoomEntity? _lastCreatedMahal;

        public string CommandName => "MAHAL_TANIMLA";
        public Vector3D? ActivePoint => null;

        public event Action<string>? OnFeedback;
        public event Action? OnCompleted;

        public MahalDefineCommand(CadDatabase database, Action<RoomEntity> onCompleted)
        {
            _database = database;
            _boundaryService = new SmartBoundaryService(database);
            _onCompleted = onCompleted;
        }

        /*
           NE: Komutu Başlat (Start)
           NEDEN: Mahal analiz sürecini başlatmak ve kullanıcıdan odanın merkezinde bir noktaya tıklamasını istemek için.
        */
        public void Start()
        {
            Serilog.Log.Information("KOMUT BAŞLATILDI: MAHAL_TANIMLA");
            OnFeedback?.Invoke("MAHAL ANALİZİ: Odanın merkezinde bir noktaya tıklayın.");
        }

        /*
           NE: Tıklama Olayı (OnPointerPressed)
           NEDEN: Tıklanan noktadan etrafa ışınlar atarak kapalı bölge sınırlarını tespit etmek ve odayı (Room) sanal olarak oluşturup vitrifiye analizini tetiklemek için.
        */
        public void OnPointerPressed(Vector3D point)
        {
            Serilog.Log.Information("MAHAL SECIMI: Tıklanan Nokta: {Point}", point);
            try
            {
                var points = _boundaryService.FindBoundary(point);
                if (points == null || points.Count < 3)
                {
                    Serilog.Log.Warning("MAHAL HATASI: Kapalı alan bulunamadı.");
                    OnFeedback?.Invoke("HATA: Kapalı bir oda sınırı bulunamadı. Lütfen dört tarafı kapalı bir alana tıklayın.");
                    return;
                }

                Serilog.Log.Information("MAHAL BULUNDU: {Count} nokta tespit edildi.", points.Count);
                
                // MAHAL OLUŞTUR (RoomEntity Kullanıyoruz)
                _lastCreatedMahal = new RoomEntity(points, "Yeni Mahal");
                
                // Vitrifiye Analizi
                AnalyzeAndAddFixtures(_lastCreatedMahal);
                
                // Callback
                _onCompleted?.Invoke(_lastCreatedMahal);
            }
            catch (Exception ex)
            {
                OnFeedback?.Invoke($"HATA: Mahal analizi sırasında kritik hata. {ex.Message}");
                Serilog.Log.Error(ex, "Mahal analizi hatası");
            }
        }

        public void FinalizeMahal(string name, string type)
        {
            if (_lastCreatedMahal != null)
            {
                _lastCreatedMahal.RoomName = name;
                
                // RoomType Enum Dönüşümü
                if (Enum.TryParse<Afney.Cad.Mechanical.Enums.RoomType>(type, true, out var rType))
                    _lastCreatedMahal.Type = rType;
                else
                    _lastCreatedMahal.Type = Afney.Cad.Mechanical.Enums.RoomType.StandardRoom;

                _database.AddEntity(_lastCreatedMahal);
                OnFeedback?.Invoke($"BAŞARILI: {_lastCreatedMahal.RoomName} tanımlandı. ΣLU: {_lastCreatedMahal.TotalLoadUnits:F2}");
            }
            OnCompleted?.Invoke();
        }

        private void AnalyzeAndAddFixtures(RoomEntity mahal)
        {
            var entities = _database.GetAllEntities().ToList();
            foreach (var ent in entities)
            {
                if (IsPointInPolygon(ent.GetBoundingBox().Center, mahal.BoundaryPoints))
                {
                    string layer = ent.Layer ?? "";
                    SanitaryFixtureEntity? fixtureEntity = null;

                    // Layer ismine göre cihaz tespiti
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
                        _database.AddEntity(fixtureEntity);
                        mahal.Fixtures.Add(fixtureEntity);
                        mahal.TotalLoadUnits += fixtureEntity.LoadUnits;
                    }
                }
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

        public void OnPointerMoved(Vector3D point) { }
        public void OnKeyDown(InputKey key) { if (key == InputKey.Escape) Cancel(); }
        public void Draw(IRenderContext context) { }
        public void Cancel() { OnCompleted?.Invoke(); }
    }
}
