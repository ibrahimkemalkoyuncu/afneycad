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

        // Layer adı → katalog ID eşlemesi (TS 1258 LU değerleri FixtureLibraryService'ten gelir)
        private static readonly (string layerKeyword, string fixtureId)[] s_layerMap =
        {
            ("LAV",      "LV-001"),  // Lavabo: 1.5 LU
            ("WASHB",    "LV-001"),
            ("WC",       "WC-001"),  // Klozet: 3.0 LU
            ("KLOZET",   "WC-001"),
            ("TOILET",   "WC-001"),
            ("PISUVAR",  "WC-003"),  // Pisuvar: 2.0 LU
            ("URINAL",   "WC-003"),
            ("DUŞ",      "DU-001"),  // Duş: 2.0 LU
            ("SHOWER",   "DU-001"),
            ("KÜVET",    "KV-001"),  // Küvet: 3.0 LU
            ("BATHTUB",  "KV-001"),
            ("EVIYE",    "EV-001"),  // Eviye: 2.0 LU
            ("SINK",     "EV-001"),
            ("BIDE",     "BI-001"),  // Bide: 1.0 LU
            ("BIDET",    "BI-001"),
            ("CAMASIR",  "CM-001"),  // Çamaşır Makinesi: 1.5 LU
            ("WASHING",  "CM-001"),
            ("BULASIK",  "BM-001"),  // Bulaşık Makinesi: 1.5 LU
            ("DISH",     "BM-001"),
            ("SUZGEC",   "DS-001"),  // Döşeme Süzgeci: 0.5 LU
            ("DRAIN",    "DS-001"),
            ("GIDER",    "DS-001"),
        };

        private void AnalyzeAndAddFixtures(RoomEntity mahal)
        {
            var lib = new FixtureLibraryService();
            var entities = _database.GetAllEntities().ToList();

            foreach (var ent in entities)
            {
                if (!IsPointInPolygon(ent.GetBoundingBox().Center, mahal.BoundaryPoints)) continue;

                string layer = (ent.Layer ?? "").ToUpperInvariant()
                                                 .Replace("İ", "I").Replace("Ş", "S")
                                                 .Replace("Ç", "C").Replace("Ğ", "G")
                                                 .Replace("Ü", "U").Replace("Ö", "O");

                string? fixtureId = null;
                foreach (var (keyword, id) in s_layerMap)
                {
                    if (layer.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        fixtureId = id;
                        break;
                    }
                }

                if (fixtureId == null) continue;

                var fixtureEntity = lib.CreateEntity(fixtureId, ent.GetBoundingBox().Center);
                _database.AddEntity(fixtureEntity);
                mahal.Fixtures.Add(fixtureEntity);
                mahal.TotalLoadUnits += fixtureEntity.LoadUnits;
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
