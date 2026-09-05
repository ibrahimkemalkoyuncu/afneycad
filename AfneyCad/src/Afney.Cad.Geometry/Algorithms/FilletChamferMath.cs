using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Algorithms;

/*
   NE: Fillet/Chamfer Matematiği (FilletChamferMath)
   NEDEN: FILLET (kavisli birleştirme, R yarıçaplı yay ile iki çizgiyi teğet birleştirme) ve
          CHAMFER (pah kırma, iki çizgiyi belirli mesafelerde kesip düz bir çizgiyle birleştirme)
          komutları öncesinde kod tabanında HİÇ yoktu. Bu sınıf, AutoCAD'in bu iki en temel
          düzenleme komutunun saf geometrisini (nesne/veritabanı bağımlılığı olmadan, test
          edilebilir şekilde) sağlar.

   KAPSAM: Sadece iki AYRI doğru (Line-Line) çifti. Polyline'ın ardışık segmentleri arası
           fillet/chamfer KAPSAM DIŞI (TrimCommand/TrimPolyline'daki "kendi kendini kesme
           kapsam dışı" notuyla tutarlı bir sınırlama — bu oturumda sadece Line-Line desteklenir).

   NASIL (Ortak matematik):
   1. İki doğrunun SONSUZ uzantılarının kesişim noktası P bulunur (GeomUtils.GetIntersectionLineLine).
      Paralelse (kesişim yoksa) işlem başarısız döner — sessizce yanlış sonuç ÜRETİLMEZ.
   2. Her doğru için, kullanıcının o doğru üzerinde tıkladığı noktaya (pick) EN YAKIN uç nokta
      "korunacak uç" (keepEnd) olarak seçilir — diğer uç tamamen atılır ve yerine teğet/pah
      noktası konur. Bu, gerçek AutoCAD FILLET/CHAMFER davranışıyla tutarlıdır: kullanıcı hangi
      tarafı tıklarsa o taraf korunur.
   3. P'den keepEnd'e doğru birim yön vektörü (dir) hesaplanır.
   4. FILLET: İki yön vektörü arasındaki açı (alpha) bulunur. Teğet uzunluğu T = R / tan(alpha/2).
      Teğet noktaları P + dir*T. Yayın merkezi, açıortay yönünde P'den d = R / sin(alpha/2)
      uzaklıktadır. Yayın küçük (minor, sweep < π) tarafı köşeyi yuvarlayan gerçek yaydır.
      CHAMFER: Pah noktaları P + dirA*dist1, P + dirB*dist2 — aralarına düz çizgi çekilir.
   5. Doğrular neredeyse çakışık/kolineer ise (alpha ≈ 0 veya π) FILLET/CHAMFER geometrik olarak
      tanımsızdır (teğet uzunluğu sonsuza gider) — bu da açıkça başarısız olarak raporlanır.
*/
public static class FilletChamferMath
{
    private const double Epsilon = 1e-9;
    private const double AngleEpsilon = 1e-6;

    public readonly record struct FilletResult(
        Vector3D TrimmedAStart, Vector3D TrimmedAEnd,
        Vector3D TrimmedBStart, Vector3D TrimmedBEnd,
        Vector3D ArcCenter, double ArcRadius, double ArcStartAngle, double ArcEndAngle);

    public readonly record struct ChamferResult(
        Vector3D TrimmedAStart, Vector3D TrimmedAEnd,
        Vector3D TrimmedBStart, Vector3D TrimmedBEnd,
        Vector3D ChamferStart, Vector3D ChamferEnd);

    /*
       NE: Ortak Ön Hazırlık (Kesişim + Korunan Uç + Yön Vektörü)
       NEDEN: Fillet ve Chamfer'ın ikisi de aynı "P noktası + iki yön vektörü" ön hesabına
              ihtiyaç duyar; kopyalamamak için tek yerde toplandı.
    */
    private static bool TryPrepare(
        Vector3D aStart, Vector3D aEnd, Vector3D bStart, Vector3D bEnd,
        Vector3D pickA, Vector3D pickB,
        out Vector3D p, out Vector3D keepA, out Vector3D keepB,
        out Vector3D dirA, out Vector3D dirB, out double distKeepA, out double distKeepB,
        out string? error)
    {
        keepA = default; keepB = default; dirA = default; dirB = default;
        distKeepA = 0; distKeepB = 0;

        if (!GeomUtils.GetIntersectionLineLine(aStart, aEnd, bStart, bEnd, out p))
        {
            error = "Doğrular paralel — kesişim noktası yok, FILLET/CHAMFER uygulanamaz.";
            return false;
        }

        keepA = pickA.DistanceTo(aStart) <= pickA.DistanceTo(aEnd) ? aStart : aEnd;
        keepB = pickB.DistanceTo(bStart) <= pickB.DistanceTo(bEnd) ? bStart : bEnd;

        distKeepA = p.DistanceTo(keepA);
        distKeepB = p.DistanceTo(keepB);
        if (distKeepA < Epsilon || distKeepB < Epsilon)
        {
            error = "Seçilen nokta kesişim noktasıyla çakışıyor — FILLET/CHAMFER uygulanamaz.";
            return false;
        }

        dirA = (keepA - p) / distKeepA;
        dirB = (keepB - p) / distKeepB;
        error = null;
        return true;
    }

    /// <summary>İki doğru arasına R yarıçaplı, her ikisine teğet bir yay (fillet) hesaplar.</summary>
    public static bool TryComputeFillet(
        Vector3D aStart, Vector3D aEnd, Vector3D bStart, Vector3D bEnd,
        double radius, Vector3D pickA, Vector3D pickB,
        out FilletResult result, out string? error)
    {
        result = default;

        if (radius <= 0)
        {
            error = "FILLET yarıçapı pozitif olmalı.";
            return false;
        }

        if (!TryPrepare(aStart, aEnd, bStart, bEnd, pickA, pickB, out var p, out var keepA, out var keepB, out var dirA, out var dirB, out var distKeepA, out var distKeepB, out error))
            return false;

        double dot = System.Math.Clamp(dirA.Dot(dirB), -1.0, 1.0);
        double alpha = System.Math.Acos(dot); // (0, π)

        if (alpha < AngleEpsilon || alpha > System.Math.PI - AngleEpsilon)
        {
            error = "Doğrular neredeyse kolineer (açı ≈ 0° veya 180°) — FILLET tanımsız.";
            return false;
        }

        double tangentLength = radius / System.Math.Tan(alpha / 2.0);

        // NE/NEDEN — GERÇEK HATA (Session #75 denetiminde bulundu): Teğet uzunluğu
        // hiç korunan-uca-olan-gerçek-mesafeyle (distKeepA/distKeepB) karşılaştırılmıyordu.
        // Kısa bir çizgi ucunda büyük bir R ile FILLET çalıştırılırsa teğet noktası
        // orijinal segmentin ÖTESİNE (keepA'nın arkasına) düşüyor, sessizce ters yönde
        // uzayan yanlış bir geometri üretiyordu — kullanıcıya hiçbir uyarı verilmeden.
        if (tangentLength > distKeepA - Epsilon || tangentLength > distKeepB - Epsilon)
        {
            error = "FILLET yarıçapı bu doğrular için çok büyük — teğet noktası çizgi dışına taşıyor.";
            return false;
        }

        var tangentA = p + dirA * tangentLength;
        var tangentB = p + dirB * tangentLength;

        var bisector = (dirA + dirB);
        double bisectorLen = bisector.Length();
        if (bisectorLen < Epsilon)
        {
            error = "Açıortay yönü hesaplanamadı (doğrular ters yönlü) — FILLET tanımsız.";
            return false;
        }
        bisector = bisector / bisectorLen;

        double centerDist = radius / System.Math.Sin(alpha / 2.0);
        var center = p + bisector * centerDist;

        double angleA = GeomUtils.AngleOf(center, tangentA);
        double angleB = GeomUtils.AngleOf(center, tangentB);

        double sweepAtoB = angleB - angleA;
        if (sweepAtoB < 0) sweepAtoB += 2 * System.Math.PI;

        double startAngle, endAngle;
        if (sweepAtoB <= System.Math.PI)
        {
            startAngle = angleA;
            endAngle = angleB;
        }
        else
        {
            startAngle = angleB;
            endAngle = angleA;
        }

        result = new FilletResult(tangentA, keepA, tangentB, keepB, center, radius, startAngle, endAngle);
        error = null;
        return true;
    }

    /// <summary>İki doğruyu, kesişim noktasından dist1/dist2 mesafede kesip düz bir pah çizgisiyle birleştirir.</summary>
    public static bool TryComputeChamfer(
        Vector3D aStart, Vector3D aEnd, Vector3D bStart, Vector3D bEnd,
        double dist1, double dist2, Vector3D pickA, Vector3D pickB,
        out ChamferResult result, out string? error)
    {
        result = default;

        if (dist1 <= 0 || dist2 <= 0)
        {
            error = "CHAMFER mesafeleri pozitif olmalı.";
            return false;
        }

        if (!TryPrepare(aStart, aEnd, bStart, bEnd, pickA, pickB, out var p, out var keepA, out var keepB, out var dirA, out var dirB, out var distKeepA, out var distKeepB, out error))
            return false;

        // NE/NEDEN — GERÇEK HATA (Session #75 denetiminde bulundu, FILLET ile aynı sınıf
        // hata): dist1/dist2 hiç korunan-uca-olan-gerçek-mesafeyle karşılaştırılmıyordu.
        if (dist1 > distKeepA - Epsilon || dist2 > distKeepB - Epsilon)
        {
            error = "CHAMFER mesafesi bu doğrular için çok büyük — pah noktası çizgi dışına taşıyor.";
            return false;
        }

        var chamferA = p + dirA * dist1;
        var chamferB = p + dirB * dist2;

        result = new ChamferResult(chamferA, keepA, chamferB, keepB, chamferA, chamferB);
        error = null;
        return true;
    }
}
