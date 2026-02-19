using System;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Engine;

/*
   NE: Boru Bağlantı Motoru (PipeConnectionEngine)
   NEDEN: İki boru parçasının veya bir boru ile bir fittingin birbirine fiziksel olarak bağlanabilir olup olmadığını kontrol etmek için.

   NASIL (Mühendislik Detayı):
   - Uç Nokta Uyumluluğu: Boru uç noktalarının (Start/End) birbirine olan mesafesini tolerans dahilinde kontrol eder.
   - Bağlantı Onayı: Eğer noktalar birbirine çok yakınsa (Örn: < 1mm), topoloji grafı üzerinden 'Connect' işlemini onaylar.
   - İleride: Çap kontrolü ve malzeme uyumluluğu (Örn: PVC ile Çelik doğrudan bağlanamaz) denetimleri buraya eklenecektir.
*/
public class PipeConnectionEngine
{
    /*
    NE: Bağlanabilirlik Kontrolü
    NEDEN: İki borunun uç uca gelip gelmediğini ve çap uyumluluğunu kontrol etmek için.
    
    MÜHENDİSLİK KURALLARI (Mebrure Hanım):
    1. Mesafe Toleransı: Uç noktalar 1mm içinde olmalı
    2. Çap Toleransı: Çap farkı 5mm'den fazla olamaz (yoksa redüksiyon gerekir)
    3. Malzeme Uyumluluğu: İleride eklenecek (PVC-Çelik bağlanamaz)
    */
    public bool CanConnect(PipeEntity a, PipeEntity b)
    {
        if (a == null || b == null) return false;
        
        const double distanceThreshold = 1.0; // 1mm mesafe toleransı
        const double diameterTolerance = 5.0; // 5mm çap toleransı
        
        // 1. ÇAP KONTROLÜ (Mühendislik Kuralı)
        double diameterDiff = Math.Abs(a.InnerDiameter - b.InnerDiameter);
        if (diameterDiff > diameterTolerance)
        {
            // Farklı çaplı borular doğrudan bağlanamaz - Redüksiyon gerekir
            return false;
        }
        
        // 2. MESAFE KONTROLÜ
        // Tüm uç nokta kombinasyonlarını kontrol et (S-S, S-E, E-S, E-E)
        return IsNear(a.StartPoint, b.StartPoint, distanceThreshold) ||
               IsNear(a.StartPoint, b.EndPoint, distanceThreshold) ||
               IsNear(a.EndPoint, b.StartPoint, distanceThreshold) ||
               IsNear(a.EndPoint, b.EndPoint, distanceThreshold);
    }

    // NE: Mesafe Denetimi
    // NASIL: İki 3D nokta arasındaki Öklid mesafesini hesaplar.
    private bool IsNear(Vector3D p1, Vector3D p2, double threshold)
    {
        return p1.DistanceTo(p2) < threshold;
    }
}

