using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Application.Services;

/*
   NE: Yakalama Motoru (Snap Engine)
   NEDEN: Kullanıcının boru uçlarına, vana merkezlerine veya hat ortalarına hassas şekilde kenetlenmesini sağlamak için.

   MÜHENDİSLİK DETAYI:
   - AutoCAD'deki OSNAP (Object Snap) mantığıyla çalışır.
   - Ekran zoom seviyesine göre değişken bir yakalama alanı (Aperture) hesaplar.
   - En yakın Snap noktasını (Endpoint, Center, Midpoint vb.) bularak çizimi doğrusal hale getirir.
   - Sıhhi tesisat hatlarının sızdırmazlık (bağlantı) bütünlüğü için kritiktir.
*/
public class SnapEngine
{
    private readonly CadDatabase _database;
    private const double ApertureSize = 15.0; // Pixel cinsinden yakalama alanı

    // OSNAP (Object Snap) Aç/Kapa Bayrakları
    public bool EnableEndpoint { get; set; } = true;
    public bool EnableMidpoint { get; set; } = true;
    public bool EnableCenter { get; set; } = true;
    public bool EnablePerpendicular { get; set; } = true;
    // Tüm Snap motorunu komple kapatmak için Ana Şalter
    public bool IsOsnapEnabled { get; set; } = true;

    public SnapEngine(CadDatabase database)
    {
        _database = database;
    }

    /*
    METOD ADI: FindSnapPoint
    AMACI: Kullanıcının mouse imlecine en yakın hassas yakalama noktasını bulmak.
    NEDEN: Milimetrik hassasiyet gerektiren CAD çizimlerinde elle tıklama yeterli değildir.
    NASIL: 
    - Veritabanındaki tüm nesnelerin statik snap noktalarını (uç, orta, merkez) kontrol eder.
    - Eğer 'lastPoint' verilmişse (çizim devam ediyorsa), hatlar üzerinde diklik (Perpendicular) noktalarını hesaplar.
    */
    /*
       NE: Snap Noktası Bul (FindSnapPoint)
       NEDEN: Mouse imlecinin yakınındaki en uygun hassas yakalama noktasını (Uç, Orta, Merkez veya Dik) uzamsal sorgu ile saptamak için.
    */
    public SnapPoint? FindSnapPoint(Vector3D cursorPosition, double currentZoom, Vector3D? lastPoint = null)
    {
        // Zoom seviyesine göre dünya koordinatlarında arama yarıçapı
        double searchRadius = ApertureSize / currentZoom;
        
        // --- PERFORMANS KORUMASI ---
        // Çok geniş alanda snap aramak (zoom out iken) performansı düşürür.
        const double MaxSearchRadius = 5000.0; // 5 metre sınırı (Dünya birimi)
        if (searchRadius > MaxSearchRadius) searchRadius = MaxSearchRadius;
        
        // KONUMSAL SORGULAMA: Tüm çizim yerine sadece aperture içindeki nesneleri tara
        var searchBox = new CadBoundingBox(
            new Vector3D(cursorPosition.X - searchRadius, cursorPosition.Y - searchRadius, -1000),
            new Vector3D(cursorPosition.X + searchRadius, cursorPosition.Y + searchRadius, 1000)
        );

        SnapPoint? bestSnap = null;
        double minDistance = double.MaxValue;

        // Osnap tamamen kapalıysa hiç arama yapma
        if (!IsOsnapEnabled) return null;

        // 0. AUTO-ALIGN ORIGIN SNAP (Kalıcı Kenetlenme - Genellikle Center/Insertion sayılır)
        // Kullanıcı 0,0 noktasına yaklaştığında herzaman merkeze (Origin) oturtmak için
        double originDist = cursorPosition.DistanceTo(Vector3D.Zero);
        if (EnableCenter && originDist <= searchRadius)
        {
            minDistance = originDist;
            bestSnap = new SnapPoint(Vector3D.Zero, SnapPointType.Insertion); // Origin bir Blok Yerleştirme noktası gibi davranır
        }

        foreach (var entity in _database.QueryEntities(searchBox))
        {
            // 1. STATİK NOKTALARI KONTROL ET (Endpoint, Midpoint, Center vb.)
            foreach (var snap in entity.GetSnapPoints())
            {
                // UI'dan gelen filtrelere (Endpoint, Midpoint vb.) göre yakalama noktasını ele
                bool isSnapAllowed = false;
                switch (snap.Type)
                {
                    case SnapPointType.Endpoint: isSnapAllowed = EnableEndpoint; break;
                    case SnapPointType.Midpoint: isSnapAllowed = EnableMidpoint; break;
                    case SnapPointType.Center: case SnapPointType.Insertion: isSnapAllowed = EnableCenter; break;
                    default: isSnapAllowed = true; break; // Diğer (veya Node) snapler açık kabul ediliyor
                }

                if (!isSnapAllowed) continue;

                double distance = cursorPosition.DistanceTo(snap.Position);
                if (distance <= searchRadius && distance < minDistance)
                {
                    minDistance = distance;
                    bestSnap = snap;
                }
            }

            // 2. DİNAMİK NOKTALAR (PERPENDICULAR) - Sadece çizim devam ediyorsa ve Perpendicular açıksa
            if (EnablePerpendicular && lastPoint.HasValue)
            {
                SnapPoint? perpSnap = CalculatePerpendicularSnap(entity, cursorPosition, lastPoint.Value);
                if (perpSnap.HasValue)
                {
                    double dist = cursorPosition.DistanceTo(perpSnap.Value.Position);
                    if (dist <= searchRadius && dist < minDistance)
                    {
                        minDistance = dist;
                        bestSnap = perpSnap;
                    }
                }
            }
        }

        return bestSnap;
    }

    /*
       NE: Diklik Snap'i Hesapla (CalculatePerpendicularSnap)
       NEDEN: Devam eden bir çizim hattından bir nesneye (boru veya çizgi) inilen dikmenin temas noktasını geometrik olarak saptamak için.
    */
    private SnapPoint? CalculatePerpendicularSnap(CadEntity entity, Vector3D cursor, Vector3D lastPoint)
    {
        var snaps = entity.GetSnapPoints().ToList();
        var endPoints = snaps.Where(s => s.Type == SnapPointType.Endpoint).ToList();

        if (endPoints.Count >= 2)
        {
            var p1 = endPoints[0].Position;
            var p2 = endPoints[1].Position;
            Vector3D perp = GetPerpendicularPoint(lastPoint, p1, p2);
            return new SnapPoint(perp, SnapPointType.Perpendicular);
        }

        return null;
    }

    /*
       NE: Dik Noktayı Getir (GetPerpendicularPoint)
       NEDEN: Bir noktanın bir doğru parçası üzerindeki en yakın izdüşümünü vektörel çarpım ile bularak diklik noktasını saptamak için.
    */
    private Vector3D GetPerpendicularPoint(Vector3D p, Vector3D s, Vector3D e)
    {
        Vector3D v = e - s;
        Vector3D w = p - s;
        double c1 = w.Dot(v);
        double c2 = v.Dot(v);

        if (c2 <= 0) return s;

        double b = c1 / c2;
        // İzdüşüm noktası hattın dışındaysa s veya e'ye kısıtlayalım (AutoCAD mantığı)
        if (b < 0) return s;
        if (b > 1) return e;

        return s + (v * b);
    }
}
