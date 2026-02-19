using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Mimari Tanıma Servisi (ArchitecturalRecognitionService)
    NEDEN: FINE SANI standardında, dwg/dxf olarak gelen mimari kat planını analiz ederek "Anlamlı Veri" (Duvar, Kapı vb.) üretmek için.
    
    NASIL (Mühendislik Modu - Heuristic Layer Parsing):
    1. Layer isimlerini analiz eder (Pattern Matching: WALL, DUVAR, DOOR, KAPI vb.)
    2. Bu layer'lardaki nesneleri ArchitecturalObstacle olarak paketler.
    3. Blokları (Insert) çözümleyerek kapı/pencere yerlerini tespit eder.
*/
public class ArchitecturalRecognitionService
{
    private readonly CadDatabase _database;

    /*
       NE: ArchitecturalRecognitionService Yapıcı Metodu
       NEDEN: Veritabanı referansını alarak ham çizim verilerini analiz etmeye hazır hale getirir.
    */
    public ArchitecturalRecognitionService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Engelleri Tanı (RecognizeObstacles)
       NEDEN: Çizimdeki katman isimlerini ve nesne tiplerini mühendislik mantığıyla analiz ederek; duvar, kapı, pencere ve kolon gibi mimari unsurları saptamak için.
    */
    public List<ArchitecturalObstacle> RecognizeObstacles()
    {
        var obstacles = new List<ArchitecturalObstacle>();
        var entities = _database.GetAllEntities();
        
        Serilog.Log.Information("Mimari Analiz Başladı. Toplam Nesne: {Count}", entities.Count());

        foreach (var entity in entities)
        {
            var layerName = (entity.Layer ?? "0").ToUpper();
            ObstacleType? type = null;

            // 1. İsim Bazlı Tanıma (Güçlü Eşleşme) - GENİŞLETİLMİŞ
            if (layerName.Contains("WALL") || layerName.Contains("DUVAR") || 
                layerName.Contains("KABA") || layerName.Contains("SIVA") || 
                layerName.Contains("MIMARI") || layerName.Contains("DUVR"))
                type = ObstacleType.Wall;
            
            else if (layerName.Contains("DOOR") || layerName.Contains("KAPI") || 
                     layerName.Contains("KAPI") || layerName.Contains("KAPILAR") ||
                     layerName.Contains("DR") || layerName.Contains("DOORS"))
                type = ObstacleType.Door;
            
            else if (layerName.Contains("WIN") || layerName.Contains("PENCERE") || 
                     layerName.Contains("CAM") || layerName.Contains("WINDOW") ||
                     layerName.Contains("PENC") || layerName.Contains("PENCR"))
                type = ObstacleType.Window;
            
            else if (layerName.Contains("COL") || layerName.Contains("KOLON"))
                type = ObstacleType.Column;
            
            // 2. Renk ve Tip Bazlı Tanıma (Zayıf Eşleşme - Fallback)
            if (!type.HasValue)
            {
                 // Eğer nesne Line veya Polyline ise ve Kırmızı/Mavi/Yeşil değilse (Tesisat değilse) Duvar say.
                 if (entity is LineEntity || entity is LwPolylineEntity)
                 {
                     // Renk analizi: Genellikle Tesisat (1=Red, 3=Green, 4=Cyan, 5=Blue, 6=Magenta)
                     // Mimari (2=Yellow, 7=White, 8=Gray, 9=LtGray)
                     // Basitçe: Eğer layer adı "MEK" veya "TES" içermiyorsa duvar kabul et.
                     if (!layerName.Contains("MEK") && !layerName.Contains("TES") && !layerName.Contains("PIPE") && !layerName.Contains("BORU"))
                     {
                         type = ObstacleType.Wall;
                     }
                 }
            }

            if (type.HasValue)
            {
                var obs = new ArchitecturalObstacle
                {
                    Type = type.Value,
                    SourceEntityId = entity.Id,
                    OriginalLayer = entity.Layer ?? "0"
                };

                // Sınır Belirleme (Boundary Extraction)
                if (entity is LineEntity line)
                {
                    obs.Boundary.Add(line.StartPoint);
                    obs.Boundary.Add(line.EndPoint);
                }
                else if (entity is LwPolylineEntity poly)
                {
                    obs.Boundary.AddRange(poly.Vertices);
                }
                else
                {
                    // Diğer nesneler için bounding box köşeleri
                    var box = entity.GetBoundingBox();
                    obs.Boundary.Add(box.Min);
                    obs.Boundary.Add(new Afney.Cad.Geometry.Primitives.Vector3D(box.Max.X, box.Min.Y, 0));
                    obs.Boundary.Add(box.Max);
                    obs.Boundary.Add(new Afney.Cad.Geometry.Primitives.Vector3D(box.Min.X, box.Max.Y, 0));
                }

                obstacles.Add(obs);
            }
        }
        
        Serilog.Log.Information("Mimari Analiz Bitti. Tespit Edilen Engel: {Count}", obstacles.Count);
        return obstacles;
    }

    /*
    METOD ADI: FindEnclosedArea (Kapalı Alan Tespiti)
    AMACI: Kullanıcının tıkladığı bir noktanın etrafını saran duvarları tespit ederek oda sınırlarını (Poligon) oluşturmak.
    GELİŞTİRME: Işın sayısı artırıldı ve "Gap Tolerance" (Boşluk Toleransı) mantığı eklendi.
    */
    /*
       NE: Kapalı Alanı Bul (FindEnclosedArea)
       NEDEN: Kullanıcının tıkladığı noktadan dışarıya binlerce hayali ışın göndererek (Ray Casting), o noktayı saran en yakın engellerden (Duvar) kapalı bir mahal poligonu üretmek için.
    */
    public List<Afney.Cad.Geometry.Primitives.Vector3D> FindEnclosedArea(Afney.Cad.Geometry.Primitives.Vector3D centerPoint)
    {
        var result = new List<Afney.Cad.Geometry.Primitives.Vector3D>();
        
        // 1. Önce Akıllı Tanıma Dene (Layer-based)
        var obstacles = RecognizeObstacles(); 
        
        Serilog.Log.Information("📐 MAHAL ARAMA: Merkez Nokta = ({X}, {Y})", centerPoint.X, centerPoint.Y);
        Serilog.Log.Information("🏗️  MİMARİ TANIM SONUCU: {Count} engel tespit edildi", obstacles.Count);
        
        // ** FALLBACK MEKANİZMASI **
        // Eğer layer bazlı tanıma 0 sonuç verdiyse, TÜM Line/Polyline'ları kullan
        if (obstacles.Count == 0)
        {
            Serilog.Log.Warning("⚠️  Layer bazlı analiz başarısız! FALLBACK: Tüm çizgiler duvar varsayılıyor...");
            
            var allEntities = _database.GetAllEntities();
            foreach (var ent in allEntities)
            {
                if (ent is LineEntity || ent is LwPolylineEntity)
                {
                    var obs = new ArchitecturalObstacle
                    {
                        Type = ObstacleType.Wall,
                        SourceEntityId = ent.Id,
                        OriginalLayer = ent.Layer ?? "0"
                    };

                    if (ent is LineEntity line)
                    {
                        obs.Boundary.Add(line.StartPoint);
                        obs.Boundary.Add(line.EndPoint);
                    }
                    else if (ent is LwPolylineEntity poly)
                    {
                        obs.Boundary.AddRange(poly.Vertices);
                    }

                    obstacles.Add(obs);
                }
            }
            
            Serilog.Log.Information("✅ FALLBACK SONUÇ: {Count} çizgi engel olarak eklendi", obstacles.Count);
        }
        
        if (obstacles.Count == 0) 
        {
            Serilog.Log.Error("❌ HİÇ ÇIZGI BULUNAMADI! DWG boş olabilir.");
            return result;
        }

        // Mimari çizgileri "Line Segment" listesine dönüştür
        var segments = new List<(Afney.Cad.Geometry.Primitives.Vector3D P1, Afney.Cad.Geometry.Primitives.Vector3D P2)>();
        foreach(var obs in obstacles)
        {
            if (obs.Boundary.Count < 2) continue;
            for(int i=0; i<obs.Boundary.Count-1; i++) segments.Add((obs.Boundary[i], obs.Boundary[i+1]));
            if(obs.Boundary.Count > 2) segments.Add((obs.Boundary.Last(), obs.Boundary.First()));
        }

        Serilog.Log.Information("🔷 SEGMENT SAYISI: {Count}", segments.Count);

        // Ray Casting - Yüksek Hassasiyet (360 Derece / 1080 Ray = 0.33 derece hassasiyet)
        int rayCount = 1080; 
        double angleStep = 360.0 / rayCount;
        double maxDistance = 500000; // 500 metre

        Serilog.Log.Information("🌟 RAY CASTING BAŞLIYOR: {RayCount} ışın gönderiliyor...", rayCount);

        Afney.Cad.Geometry.Primitives.Vector3D? lastPoint = null;
        int hitCount = 0;

        for (int i = 0; i < rayCount; i++)
        {
            double angleRad = (i * angleStep) * System.Math.PI / 180.0;
            var direction = new Afney.Cad.Geometry.Primitives.Vector3D(System.Math.Cos(angleRad), System.Math.Sin(angleRad), 0);
            
            double minT = maxDistance;
            Afney.Cad.Geometry.Primitives.Vector3D? closestPoint = null;

            foreach (var seg in segments)
            {
                var intersect = RaySegmentIntersection(centerPoint, direction, seg.P1, seg.P2);
                if (intersect.HasValue)
                {
                    double dist = centerPoint.DistanceTo(intersect.Value);
                    if (dist < minT && dist > 1.0) // 1mm'den yakınsa (kendisi) yoksay
                    {
                        minT = dist;
                        closestPoint = intersect;
                    }
                }
            }

            if (closestPoint.HasValue)
            {
                hitCount++;
                // Gürültü Azaltma: Eğer önceki nokta ile aşırı yakınsa ekleme (10mm tolerans)
                if (lastPoint == null || closestPoint.Value.DistanceTo(lastPoint.Value) > 10.0)
                {
                    result.Add(closestPoint.Value);
                    lastPoint = closestPoint;
                }
            }
        }

        Serilog.Log.Information("✅ RAY CASTING TAMAMLANDI: {HitCount}/{RayCount} kesişim, {ResultCount} benzersiz nokta", 
            hitCount, rayCount, result.Count);

        if (result.Count < 3)
        {
            Serilog.Log.Error("❌ KAPALIALN BULUNAMADI! Yeterli nokta yok: {Count}", result.Count);
        }

        // Poligonu İşle ve Filtrele
        // 1. Çok yakın noktaları birleştir (Noise Reduction)
        // 2. Olası Mahal Tipini Tahmin Et
        return result;
    }

    /*
    METOD ADI: PredictRoomType (Mahal Tipi Tahmini)
    AMACI: Bulunan kapalı alanın (poligon) içinde "SALON", "MUTFAK", "WC" gibi bir yazı var mı diye bakar.
    NEDEN: Mimari projelerde mekan isimleri genellikle yazıyla belirtilir. Bunu okuyarak manuel giriş zahmetini ortadan kaldırırız.
    */
    /*
       NE: Mahal Tipi Tahmin Et (PredictRoomType)
       NEDEN: Mahal sınırları içindeki metin (Text) verilerini tarayarak; o mekanın bir mutfak, banyo veya salon olup olmadığını semantik olarak çözümlemek için.
    */
    public string PredictRoomType(List<Vector3D> boundary)
    {
        if (boundary == null || boundary.Count < 3) return "Bilinmiyor";

        // Poligonun BoundingBox'ını al (Hızlı filtreleme için)
        double minX = boundary.Min(p => p.X);
        double maxX = boundary.Max(p => p.X);
        double minY = boundary.Min(p => p.Y);
        double maxY = boundary.Max(p => p.Y);

        var bbox = new CadBoundingBox(new Vector3D(minX, minY, 0), new Vector3D(maxX, maxY, 0));
        
        // Bu alandaki tüm TEXT nesnelerini bul
        var textEntities = _database.QueryEntities(bbox).OfType<TextEntity>();

        foreach (var txt in textEntities)
        {
            // Yazının tam konumu poligonun içinde mi?
            if (IsPointInPolygon(txt.Position, boundary))
            {
                var content = txt.Text.ToUpper();
                if (content.Contains("SALON")) return "Salon";
                if (content.Contains("MUTFAK") || content.Contains("KITCHEN")) return "Mutfak";
                if (content.Contains("BANYO") || content.Contains("BATH")) return "Banyo";
                if (content.Contains("WC") || content.Contains("TUVALET")) return "WC";
                if (content.Contains("YATAK") || content.Contains("BEDROOM")) return "Yatak Odası";
                if (content.Contains("HOL") || content.Contains("ANTRE") || content.Contains("KORIDOR")) return "Koridor";
                if (content.Contains("BALKON") || content.Contains("TERAS")) return "Balkon";
                if (content.Contains("SAYAC")) return "Sayaç Yeri";
                if (content.Contains("SIĞINAK")) return "Sığınak";
                if (content.Contains("DEPO")) return "Depo";
                if (content.Contains("TESISAT") || content.Contains("SHAFT") || content.Contains("ŞAFT")) return "Şaft";
            }
        }

        return "Oda"; // Varsayılan
    }

    /// <summary>
    /// Mahal sınırları içindeki metinleri analiz ederek detaylı oda özelliklerini (Alan, Malzeme vb.) çıkarır.
    /// </summary>
    public void ParseRoomAttributes(RoomEntity room)
    {
        if (room.Boundary == null || room.Boundary.Vertices.Count < 3) return;

        var boundary = room.Boundary.Vertices;
        
        // Poligonun BoundingBox'ını al
        double minX = boundary.Min(p => p.X);
        double maxX = boundary.Max(p => p.X);
        double minY = boundary.Min(p => p.Y);
        double maxY = boundary.Max(p => p.Y);

        var bbox = new CadBoundingBox(new Vector3D(minX, minY, 0), new Vector3D(maxX, maxY, 0));
        
        // Bu alandaki tüm TEXT/MTEXT nesnelerini bul
        var textEntities = _database.QueryEntities(bbox).OfType<TextEntity>();
        var candidates = new List<TextEntity>();

        foreach (var txt in textEntities)
        {
            if (IsPointInPolygon(txt.Position, boundary))
            {
                candidates.Add(txt);
            }
        }

        if (candidates.Count == 0) return;

        // Aday metinleri analiz et
        // Genellikle Mahal Etiketi, içinde "Alan" veya "m²" geçen veya çok satırlı olan metindir.
        // Biz hepsini birleştirip analiz edelim veya en zengin içeriği seçelim.
        
        foreach (var txt in candidates)
        {
            ApplyRegexAttributes(txt.Text, room);
        }
    }

    private void ApplyRegexAttributes(string text, RoomEntity room)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Satır satır da bakabiliriz
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // 1. Oda Adı Tespiti (Basit Heuristic: İlk satır genellikle addır, eğer malzeme değilse)
        // Eğer henüz bir isim atanmadıysa veya "Mahal" ise
        if (room.RoomName == "Mahal" || room.RoomName == "Oda")
        {
             string potentialName = lines[0].Trim();
             // İsim içinde : yoksa ve malzeme keywordleri yoksa
             if (!potentialName.Contains(":") && !potentialName.Contains("Döş") && !potentialName.Contains("Duvar"))
             {
                 room.RoomName = potentialName;
                 // Type tahminini güncelle
                 // Type tahminini güncelle
                 if (potentialName.ToUpper().Contains("SALON")) room.Type = RoomType.StandardRoom; // LivingRoom -> StandardRoom
                 else if (potentialName.ToUpper().Contains("MUTFAK")) room.Type = RoomType.Kitchen;
                 else if (potentialName.ToUpper().Contains("BANYO")) room.Type = RoomType.Bathroom;
                 else if (potentialName.ToUpper().Contains("WC")) room.Type = RoomType.Toilet; // WC -> Toilet
                 else if (potentialName.ToUpper().Contains("YATAK")) room.Type = RoomType.StandardRoom; // Bedroom -> StandardRoom
                 else if (potentialName.ToUpper().Contains("ANTRE") || potentialName.ToUpper().Contains("HOL")) room.Type = RoomType.Corridor;
             }
        }

        // 2. Regex ile Özellik Ayıklama
        // Döşeme: (Döşeme|Döş.|Zemin|Floor)\s*[:=]\s*(.+)
        var matchFloor = System.Text.RegularExpressions.Regex.Match(text, @"(Döşeme|Döş\.|Zemin|Floor)\s*[:=]\s*(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchFloor.Success) room.FloorMaterial = matchFloor.Groups[2].Value.Trim();

        // Duvar: (Duvar|Wall)\s*[:=]\s*(.+)
        var matchWall = System.Text.RegularExpressions.Regex.Match(text, @"(Duvar|Wall)\s*[:=]\s*(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchWall.Success) room.WallMaterial = matchWall.Groups[2].Value.Trim();

        // Tavan: (Tavan|Ceiling)\s*[:=]\s*(.+)
        var matchCeiling = System.Text.RegularExpressions.Regex.Match(text, @"(Tavan|Ceiling)\s*[:=]\s*(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchCeiling.Success) room.CeilingMaterial = matchCeiling.Groups[2].Value.Trim();

        // Alan: (Alan|Area|A)\s*[:=]?\s*([\d\.,]+)\s*(m²|m2)?
        // Not: Alan zaten geometriden hesaplanıyor ama kontrol için okunabilir. Şimdilik atlıyoruz.
    }

    // Geometri Kesişim Yardımcısı (Standard Vector Math)
    /*
       NE: Işın ve Segment Kesişimi (RaySegmentIntersection)
       NEDEN: Ray Casting algoritması sırasında bir ışının duvar segmentini kesip kesmediğini saptamak için kullanılan vektörel matematik fonksiyonu.
    */
    private Afney.Cad.Geometry.Primitives.Vector3D? RaySegmentIntersection(
        Afney.Cad.Geometry.Primitives.Vector3D rayOrigin, 
        Afney.Cad.Geometry.Primitives.Vector3D rayDir, 
        Afney.Cad.Geometry.Primitives.Vector3D p1, 
        Afney.Cad.Geometry.Primitives.Vector3D p2)
    {
        var v1 = rayOrigin;
        var v2 = rayOrigin + rayDir; // Direction vector itself
        var v3 = p1;
        var v4 = p2;

        double det = (v2.X - v1.X) * (v4.Y - v3.Y) - (v2.Y - v1.Y) * (v4.X - v3.X);
        if (Math.Abs(det) < 1e-9) return null; // Paralel

        double t = ((v3.X - v1.X) * (v4.Y - v3.Y) - (v3.Y - v1.Y) * (v4.X - v3.X)) / det;
        double u = ((v3.X - v1.X) * (v2.Y - v1.Y) - (v3.Y - v1.Y) * (v2.X - v1.X)) / det;

        // t: ray parametresi (0..sonsuz)
        // u: segment parametresi (0..1)
        
        if (t >= 0 && u >= 0 && u <= 1)
        {
            return new Afney.Cad.Geometry.Primitives.Vector3D(
                v1.X + t * (v2.X - v1.X),
                v1.Y + t * (v2.Y - v1.Y),
                0
            );
        }

        return null;
    }

    /*
    NE: Poligon İçinde Nokta Testi (IsPointInPolygon)
    NEDEN: Karmaşık geometrili odalarda (L-Tipi vb.), bir vitrifiyenin gerçekten o oda içinde kalıp kalmadığını saptamak için.
    NASIL: Jordan Curve Theorem (Ray Casting) kullanır. Noktadan sağa doğru hayali bir ışın gönderir; poligon kenarlarıyla kesişim sayısı tek ise nokta içerdedir.
    */
    public bool IsPointInPolygon(Vector3D p, List<Vector3D> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                 (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
