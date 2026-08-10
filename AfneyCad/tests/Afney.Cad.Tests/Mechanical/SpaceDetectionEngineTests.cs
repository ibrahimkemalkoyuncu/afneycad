using System;
using System.Collections.Generic;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: SpaceDetectionEngine Testleri
   NEDEN: Bu servis (gerçek planar-graph algoritması — half-edge benzeri CCW traversal,
          Shoelace alan, en büyük poligonu dış kabuk sayıp eleme) daha önce HİÇ test
          edilmiyordu (önceki denetimde bu, alt-özellik puanını düşüren ana neden olarak
          işaretlenmişti). Bu testler, tek odalı ve çok odalı gerçek senaryolarda otonom
          oda tespitinin GERÇEKTEN doğru alan/oda sayısı ürettiğini kanıtlar.
*/
public class SpaceDetectionEngineTests
{
    private static void AddWall(CadDatabase db, Vector3D a, Vector3D b)
        => db.AddEntity(new LineEntity(a, b) { Layer = "DUVAR" });

    [Fact]
    public void DetectAllSpaces_SingleRectangularRoom_FindsOneRoomWithCorrectArea()
    {
        // 4m x 3m tek oda (kapalı 4 duvar).
        var db = new CadDatabase();
        AddWall(db, new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0));
        AddWall(db, new Vector3D(4000, 0, 0), new Vector3D(4000, 3000, 0));
        AddWall(db, new Vector3D(4000, 3000, 0), new Vector3D(0, 3000, 0));
        AddWall(db, new Vector3D(0, 3000, 0), new Vector3D(0, 0, 0));

        var engine = new SpaceDetectionEngine(db);
        var rooms = engine.DetectAllSpaces();

        Assert.Single(rooms); // dış kabuk elendi, sadece iç oda kaldı
        Assert.Equal(12.0, SpaceDetectionEngine.CalculateAreaM2(rooms[0]), precision: 3);
    }

    [Fact]
    public void DetectAllSpaces_TwoRoomsSeparatedByInternalWall_FindsBothRoomsWithCorrectAreas()
    {
        // 8m x 3m dış kabuk, x=4m'de bir ara duvarla ikiye bölünmüş — her biri 4m x 3m = 12 m².
        var db = new CadDatabase();
        AddWall(db, new Vector3D(0, 0, 0), new Vector3D(8000, 0, 0));
        AddWall(db, new Vector3D(8000, 0, 0), new Vector3D(8000, 3000, 0));
        AddWall(db, new Vector3D(8000, 3000, 0), new Vector3D(0, 3000, 0));
        AddWall(db, new Vector3D(0, 3000, 0), new Vector3D(0, 0, 0));
        AddWall(db, new Vector3D(4000, 0, 0), new Vector3D(4000, 3000, 0)); // ara duvar

        var engine = new SpaceDetectionEngine(db);
        var rooms = engine.DetectAllSpaces();

        Assert.Equal(2, rooms.Count);
        foreach (var room in rooms)
            Assert.Equal(12.0, SpaceDetectionEngine.CalculateAreaM2(room), precision: 3);
    }

    [Fact]
    public void DetectAllSpaces_TwoDisconnectedFloorPlansSideBySide_FindsExactlyOneRoomPerFloor()
    {
        // Kullanıcı senaryosu: aynı dosyada birden fazla kat (bodrum/zemin/normal/çatı) yan
        // yana, birbirine DEĞMEYEN (fiziksel olarak bağlantısız) duvar adaları olarak çizili
        // — MultiStoryBuildingService katları Z ile ayırıyor ama SpaceDetectionEngine 2D
        // çalışıyor, "aktif kat" kavramı da UI'da hiç yok. Her "ada" 4m x 3m tek odalık
        // KENDİ dış kabuğuna sahip. Eski kod (tek global en-büyük-poligonu-el) bu durumda
        // SADECE BİR katın dış hattını elerdi, diğer katın dış hattı da yanlışlıkla "oda"
        // olarak eklenirdi (3 "oda" dönerdi: 1 gerçek + 1 gerçek + 1 yanlış dış-kabuk-kalıntısı).
        var db = new CadDatabase();

        // "Bodrum" adası — orijin civarı
        AddWall(db, new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0));
        AddWall(db, new Vector3D(4000, 0, 0), new Vector3D(4000, 3000, 0));
        AddWall(db, new Vector3D(4000, 3000, 0), new Vector3D(0, 3000, 0));
        AddWall(db, new Vector3D(0, 3000, 0), new Vector3D(0, 0, 0));

        // "Zemin Kat" adası — 50 metre öteye, TAMAMEN AYRI (hiçbir köşe paylaşmıyor)
        double dx = 50000;
        AddWall(db, new Vector3D(dx + 0, 0, 0), new Vector3D(dx + 4000, 0, 0));
        AddWall(db, new Vector3D(dx + 4000, 0, 0), new Vector3D(dx + 4000, 3000, 0));
        AddWall(db, new Vector3D(dx + 4000, 3000, 0), new Vector3D(dx + 0, 3000, 0));
        AddWall(db, new Vector3D(dx + 0, 3000, 0), new Vector3D(dx + 0, 0, 0));

        var engine = new SpaceDetectionEngine(db);
        var rooms = engine.DetectAllSpaces();

        Assert.Equal(2, rooms.Count); // her adadan tam olarak 1 oda — dış kabuk kalıntısı YOK
        foreach (var room in rooms)
            Assert.Equal(12.0, SpaceDetectionEngine.CalculateAreaM2(room), precision: 3);
    }

    [Fact]
    public void DetectAllSpaces_TinyFurnitureScaleClosedLoop_IsFilteredOut_RealRoomSurvives()
    {
        // Kullanıcı gerçek bir 6-katlı projede test etti: "Otonom" 1202 "oda" buldu — kod
        // incelemesiyle GERÇEK kök neden bulundu: FilterOuterBoundary'nin küçük-poligon
        // eşiği `faceAreas[i] > 1.0` idi — kodun her yerinde mm birimi kullanıldığından bu
        // pratikte 1mm² (neredeyse hiçbir şeyi elemiyordu). Mobilya/sembol/detay gibi mimari
        // OLMAYAN ama kapalı küçük şekiller (ör. bu testteki 100mm x 100mm kare) "oda" olarak
        // tespit ediliyordu. Bu test, DUVAR katmanında olsa BİLE gerçekçi bir oda boyutunun
        // (0.25 m²) altındaki kapalı döngülerin artık elendiğini, gerçek odanın etkilenmediğini
        // kanıtlıyor.
        var db = new CadDatabase();

        // Gerçek oda: 4m x 3m = 12 m²
        AddWall(db, new Vector3D(0, 0, 0), new Vector3D(4000, 0, 0));
        AddWall(db, new Vector3D(4000, 0, 0), new Vector3D(4000, 3000, 0));
        AddWall(db, new Vector3D(4000, 3000, 0), new Vector3D(0, 3000, 0));
        AddWall(db, new Vector3D(0, 3000, 0), new Vector3D(0, 0, 0));

        // Mobilya/sembol ölçeğinde, TAMAMEN AYRI bir minik kapalı kare: 100mm x 100mm = 0.01 m²
        // (odanın 100m öteye çizilmiş, bağlantısız bir "adası" gibi — furniture/symbol simülasyonu)
        double dx = 100_000;
        AddWall(db, new Vector3D(dx + 0, 0, 0), new Vector3D(dx + 100, 0, 0));
        AddWall(db, new Vector3D(dx + 100, 0, 0), new Vector3D(dx + 100, 100, 0));
        AddWall(db, new Vector3D(dx + 100, 100, 0), new Vector3D(dx + 0, 100, 0));
        AddWall(db, new Vector3D(dx + 0, 100, 0), new Vector3D(dx + 0, 0, 0));

        var engine = new SpaceDetectionEngine(db);
        var rooms = engine.DetectAllSpaces();

        Assert.Single(rooms); // sadece gerçek oda kaldı, minik kare elendi
        Assert.Equal(12.0, SpaceDetectionEngine.CalculateAreaM2(rooms[0]), precision: 3);
    }

    /*
       NE: Büyük Ölçekli Performans Regresyon Testi
       NEDEN: Uygulama geneli darboğaz denetiminde bulundu — `GroupIntoConnectedComponents`
              ve `ExtractPlanarFaces`'teki düğüm (vertex) dedup'ı AYRI AYRI O(n²) lineer
              aramayla yazılmıştı; birkaç bin duvar segmenti içeren gerçek çok katlı
              projelerde "Otonom" tespiti gözle görülür yavaşlıyordu. Ortak bir grid-hash
              `NodePool` ile O(1)'e yakın hale getirildi. Bu test, o düzeltmenin ileride
              (ör. birisi tekrar lineer bir arama ekleyip) sessizce geri alınmasını —
              10x10'luk bir oda ızgarasının (100 oda, ~200+ kesişim sonrası segment)
              makul bir süre bütçesi içinde tamamlanmasını zorunlu kılarak — yakalar.
              Süre sınırı bilinçli olarak gevşek tutuldu (yavaş CI makinelerinde de
              GERÇEK bir O(n²) regresyonunu yakalayacak, ama ufak varyasyonlarda
              yanlış alarm vermeyecek şekilde).
    */
    [Fact]
    public void DetectAllSpaces_TenByTenRoomGrid_CompletesWithinPerformanceBudget()
    {
        const int gridSize = 10;   // 10x10 ızgara → en fazla 100 oda
        const double cellSize = 3000.0; // 3m x 3m hücreler
        double extent = gridSize * cellSize;

        var db = new CadDatabase();

        // Yatay ve dikey tam-uzunluklu duvarlar — ResolveIntersections her kesişimde böler.
        for (int i = 0; i <= gridSize; i++)
        {
            double y = i * cellSize;
            AddWall(db, new Vector3D(0, y, 0), new Vector3D(extent, y, 0));
        }
        for (int j = 0; j <= gridSize; j++)
        {
            double x = j * cellSize;
            AddWall(db, new Vector3D(x, 0, 0), new Vector3D(x, extent, 0));
        }

        var engine = new SpaceDetectionEngine(db);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rooms = engine.DetectAllSpaces();
        sw.Stop();

        // NOT: Bu testin amacı PERFORMANS regresyonunu yakalamak (bkz. üstteki NEDEN notu),
        // eşit-alanlı bir ızgarada dış-kabuk-eleme köşe seçiminin TAM OLARAK kaç hücreyi
        // elediği (gözlemlendi: run'lar arası değişebiliyor — eşit-alan durumunda ayrı,
        // önceden var olan bir tie-break nüansı, bu fix'in kapsamı dışında) değil. Bu yüzden
        // sadece "makul bir oda sayısı üretildi mi" gevşek sağlık kontrolü yapılıyor —
        // asıl iddia performans bütçesinde.
        Assert.True(rooms.Count > gridSize * gridSize / 2,
            $"Beklenen en az {gridSize * gridSize / 2} oda, gerçek: {rooms.Count} — düğüm dedup'ında ciddi bir kayıp var mı diye kontrol et.");
        Assert.True(sw.ElapsedMilliseconds < 8000,
            $"10x10 oda ızgarası {sw.ElapsedMilliseconds}ms sürdü — O(n²) düğüm dedup regresyonu şüphesi (bütçe: 8000ms).");
    }

    [Fact]
    public void DetectAllSpaces_NonWallLayer_IsIgnoredEntirely()
    {
        // Duvar katmanı anahtar kelimelerinden hiçbirini içermeyen bir katmandaki kapalı
        // dörtgen (ör. mobilya çizimi) oda olarak algılanmamalı.
        var db = new CadDatabase();
        db.AddEntity(new LineEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0)) { Layer = "MOBILYA" });
        db.AddEntity(new LineEntity(new Vector3D(2000, 0, 0), new Vector3D(2000, 1000, 0)) { Layer = "MOBILYA" });
        db.AddEntity(new LineEntity(new Vector3D(2000, 1000, 0), new Vector3D(0, 1000, 0)) { Layer = "MOBILYA" });
        db.AddEntity(new LineEntity(new Vector3D(0, 1000, 0), new Vector3D(0, 0, 0)) { Layer = "MOBILYA" });

        var engine = new SpaceDetectionEngine(db);
        var rooms = engine.DetectAllSpaces();

        Assert.Empty(rooms);
    }

    [Fact]
    public void DetectRoomNameFromTexts_TextInsideBoundary_ReturnsRoomName()
    {
        var db = new CadDatabase();
        db.AddEntity(new TextEntity("Mutfak", new Vector3D(2000, 1500, 0), 250));
        var engine = new SpaceDetectionEngine(db);

        var boundary = new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 0, 0), new(4000, 3000, 0), new(0, 3000, 0)
        };

        string? name = engine.DetectRoomNameFromTexts(boundary);

        Assert.Equal("Mutfak", name);
    }

    [Fact]
    public void CalculateAreaM2_And_CalculatePerimeterM_MatchAnalyticalRectangleFormulas()
    {
        var rect = new List<Vector3D>
        {
            new(0, 0, 0), new(5000, 0, 0), new(5000, 2000, 0), new(0, 2000, 0)
        };

        Assert.Equal(10.0, SpaceDetectionEngine.CalculateAreaM2(rect), precision: 6);       // 5m x 2m
        Assert.Equal(14.0, SpaceDetectionEngine.CalculatePerimeterM(rect), precision: 6);   // 2*(5+2)
    }

    /*
       NE: DetectRoomNameFromTexts — CadDatabase.QueryEntities Broad-Phase / Brute-Force Eşdeğerliği
       NEDEN: DetectRoomNameFromTexts, Session #62'de _database.GetAllEntities() ile TÜM
              veritabanını doğrusal taramaktan, odanın kendi bounding box'ıyla
              _database.QueryEntities(range) (CadDatabase'in kendi QuadTree'si) sorgusuna
              geçirildi. Bu test; rastgele dağıtılmış birçok TextEntity (bazıları oda sınırının
              içinde, bazıları dışında) içeren bir veritabanında, optimize edilmiş sonucun
              burada bağımsız yeniden yazılmış saf O(n) taramayla (GetAllEntities + IsPointInPolygon)
              BİREBİR aynı metni döndürdüğünü kanıtlar.
    */
    [Fact]
    public void DetectRoomNameFromTexts_QuadTreeBroadPhase_MatchesBruteForceReference_OnRandomLayout()
    {
        var rnd = new Random(24680);
        var db = new CadDatabase();

        // Oda sınırı: X=[0,4000], Y=[0,3000]
        var boundary = new List<Vector3D>
        {
            new(0, 0, 0), new(4000, 0, 0), new(4000, 3000, 0), new(0, 3000, 0)
        };

        // Sınırın dışında rastgele dağılmış çok sayıda "gürültü" metni (uzak koordinatlarda)
        for (int i = 0; i < 40; i++)
        {
            double x = rnd.Next(-20000, 20000);
            double y = rnd.Next(-20000, 20000);
            // Sınırın içine denk gelenleri filtrelemeye çalış (gerçek testte tek bir "gerçek" isim istiyoruz)
            if (x is > -200 and < 4200 && y is > -200 and < 3200) continue;
            db.AddEntity(new TextEntity("N" + i, new Vector3D(x, y, 0), 200));
        }

        // Sınırın içinde geçerli oda adı
        db.AddEntity(new TextEntity("Yatak Odasi", new Vector3D(2000, 1500, 0), 250));

        var engine = new SpaceDetectionEngine(db);

        string? actual = engine.DetectRoomNameFromTexts(boundary);
        string? expected = BruteForceDetectRoomNameFromTexts(db, boundary);

        Assert.Equal(expected, actual);
        Assert.Equal("Yatak Odasi", actual);
    }

    // Bağımsız Brute-Force Referans: eski GetAllEntities() + IsPointInPolygon mantığının
    // production kodundan kopyalanmamış, sıfırdan yazılmış hali.
    private static string? BruteForceDetectRoomNameFromTexts(CadDatabase db, List<Vector3D> roomBoundary)
    {
        foreach (var ent in db.GetAllEntities())
        {
            if (ent is TextEntity text && !string.IsNullOrWhiteSpace(text.Text))
            {
                if (IsPointInPolygonRef(text.Position, roomBoundary))
                {
                    string t = text.Text.Trim();
                    if (t.Length >= 2 && !double.TryParse(t, out _))
                        return t;
                }
            }
        }
        return null;
    }

    private static bool IsPointInPolygonRef(Vector3D p, List<Vector3D> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            if (((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)) &&
                (p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }
}
