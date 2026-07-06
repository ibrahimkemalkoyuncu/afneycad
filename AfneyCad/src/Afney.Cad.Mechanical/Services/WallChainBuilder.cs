using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Duvar Zinciri Oluşturucu (WallChainBuilder)
   NEDEN: Kullanıcının tek tek tıklayarak seçtiği N adet duvar segmentini
          sıralı bir zincire (chain) dönüştürüp kapalı polygon üretmek için.

   NASIL (Mühendislik Detayı — Greedy Endpoint Chaining):
   1. Her segmentin iki endpoint'ini çıkar (P1, P2).
   2. "En yakın endpoint" heuristiği ile segmentleri sıraya diz.
   3. Zincirin son noktası ile ilk noktası gapTolerance içindeyse
      son noktayı ilk noktaya snap'le → kapalı polygon.
   4. Kapanamıyorsa: null döndür, hangi köşenin açık olduğunu bildir.

   NEDEN BU YAKLAŞIM:
   - AutoCAD'nin BOUNDARY komutu da benzer greedy chaining kullanır.
   - Kullanıcı duvarları sırasız seçse bile (1-3-2-4) algoritma doğru sırayı bulur.
   - Segment endpoint'leri mm hassasiyetinde eşleşmeyebilir; gap tolerance bunu telafi eder.
*/
public class WallChainBuilder
{
    // ─── Sabitler ──────────────────────────────────────────────────────────────

    /// Varsayılan gap toleransı (mm): Kapı (~900mm) ve pencere (~2400mm) boşluklarını otomatik köprüler.
    public double GapTolerance { get; set; } = 2500.0;

    /// Kapı/pencere açıklık maksimum genişliği (mm): Bu mesafeye kadar otomatik kapanır.
    private const double MaxOpeningBridge = 3000.0;

    // ─── Public API ────────────────────────────────────────────────────────────

    /*
       NE: Segment Listesinden Kapalı Polygon Üret (Build)
       DÖNDÜRÜR: Köşe noktaları listesi (en az 3 köşe) veya null (kapalı polygon üretilemedi).
       PARAMETRE logMessages: Kullanıcıya gösterilecek durum mesajları buraya eklenir.
    */
    public List<Vector3D>? Build(List<(Vector3D P1, Vector3D P2)> segments, out string statusMessage)
    {
        statusMessage = string.Empty;

        if (segments.Count == 0)
        {
            statusMessage = "Seçili duvar veya nokta yok.";
            return null;
        }

        // 1. Segmentleri zincire diz
        var chain = GreedyChain(segments, out double maxGap);
        if (chain == null || chain.Count < 3)
        {
            statusMessage = $"Duvarlar zincirlenemedi. Maksimum boşluk: {maxGap:F0}mm. Seçimi kontrol edin.";
            return null;
        }

        // 2. Closure kontrolü: ilk ve son nokta birleşiyor mu?
        double closureGap = chain[chain.Count - 1].DistanceTo(chain[0]);
        Serilog.Log.Information("[WallChain] Zincir köşe sayısı: {Count}, Closure gap: {Gap:F1}mm (max açıklık: {Tol:F1}mm)",
            chain.Count, closureGap, MaxOpeningBridge);

        if (closureGap <= MaxOpeningBridge)
        {
            // Kapı/pencere boşluğu dahil otomatik kapat — son nokta ilk noktaya snap'lenir.
            // Bu "açıklık çizgisi" zaten doğru alan hesabını sağlar (FINE MEP standardı).
            chain[chain.Count - 1] = chain[0];
            chain.RemoveAt(chain.Count - 1);
            string openingNote = closureGap > 10.0
                ? $" (açıklık: {closureGap:F0}mm otomatik köprülendi)"
                : string.Empty;
            statusMessage = $"Mahal oluşturuldu: {chain.Count} köşe.{openingNote}";
            return chain;
        }
        else
        {
            statusMessage = $"Duvar zinciri kapanamadı. Son boşluk: {closureGap:F0}mm — " +
                            $"Eksik duvar olabilir, lütfen kontrol edin.";
            Serilog.Log.Warning("[WallChain] Closure başarısız: {Gap:F1}mm > {Tol:F1}mm", closureGap, MaxOpeningBridge);
            return null;
        }
    }

    // ─── Özel: Greedy Endpoint Chaining ───────────────────────────────────────

    /*
       NE: Greed Zinciri Oluştur (GreedyChain)
       NEDEN: Segmentleri sırasız seçilen listeden doğru sıraya koymak için.

       ALGORİTMA:
       1. İlk segment olarak "en uzak iki endpoint'e sahip olan"ı al (outer boundary başlangıcı).
       2. Kalan segmentlerden her seferinde mevcut zincirin son noktasına en yakın endpoint'i bul.
       3. O segmenti doğru yönde (gerekirse ters çevirerek) zincire ekle.
       4. Tüm segmentler eklendiğinde zincirin noktalarını döndür.
    */
    private List<Vector3D>? GreedyChain(List<(Vector3D P1, Vector3D P2)> segments, out double maxGap)
    {
        maxGap = 0;
        var remaining = segments.ToList();
        var chain = new List<Vector3D>();

        // İlk segmenti seç: zinciri başlatan segment (herhangi biri)
        var first = remaining[0];
        chain.Add(first.P1);
        chain.Add(first.P2);
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            Vector3D tail = chain[chain.Count - 1];

            // Kalan segmentlerden tail'e en yakın endpoint'i bul
            int bestIdx = -1;
            bool bestFlipped = false;
            double bestDist = double.MaxValue;

            for (int i = 0; i < remaining.Count; i++)
            {
                double d1 = tail.DistanceTo(remaining[i].P1);
                double d2 = tail.DistanceTo(remaining[i].P2);

                if (d1 < bestDist) { bestDist = d1; bestIdx = i; bestFlipped = false; }
                if (d2 < bestDist) { bestDist = d2; bestIdx = i; bestFlipped = true; }
            }

            if (bestIdx < 0)
            {
                maxGap = bestDist;
                return null; // Bağlantı yok
            }

            if (bestDist > maxGap) maxGap = bestDist;

            // Segmenti doğru yönde zincire ekle
            var seg = remaining[bestIdx];
            remaining.RemoveAt(bestIdx);

            // Nokta segment (P1 == P2): gap noktası → sadece tek nokta ekle
            bool isPointSegment = seg.P1.DistanceTo(seg.P2) < 1.0;
            if (isPointSegment)
            {
                // Gap noktası: zincire tek nokta olarak ekle (tail'den farklıysa)
                if (seg.P1.DistanceTo(tail) > 1.0)
                    chain.Add(seg.P1);
            }
            else if (!bestFlipped)
            {
                // P1 → P2 yönünde
                if (seg.P1.DistanceTo(tail) > 1.0) chain.Add(seg.P1);
                chain.Add(seg.P2);
            }
            else
            {
                // P2 → P1 yönünde (ters)
                if (seg.P2.DistanceTo(tail) > 1.0) chain.Add(seg.P2);
                chain.Add(seg.P1);
            }
        }

        return chain;
    }

    // ─── Yardımcılar ───────────────────────────────────────────────────────────

    /*
       NE: Entity'den Segment Çıkar (ExtractSegments)
       NEDEN: LineEntity veya LwPolylineEntity'den (P1, P2) çiftleri üretmek için.
              ManualMahalCommand bu metodu kullanır.
    */
    public static List<(Vector3D P1, Vector3D P2)> ExtractSegments(
        System.Collections.Generic.IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> entities)
    {
        var result = new List<(Vector3D P1, Vector3D P2)>();
        foreach (var ent in entities)
        {
            if (ent is LineEntity line && line.StartPoint.DistanceTo(line.EndPoint) > 1.0)
            {
                result.Add((line.StartPoint, line.EndPoint));
            }
            else if (ent is LwPolylineEntity poly && poly.Vertices.Count >= 2)
            {
                for (int i = 0; i < poly.Vertices.Count - 1; i++)
                    result.Add((poly.Vertices[i], poly.Vertices[i + 1]));
                if (poly.IsClosed && poly.Vertices.Count > 2)
                    result.Add((poly.Vertices[poly.Vertices.Count - 1], poly.Vertices[0]));
            }
        }
        return result;
    }
}
