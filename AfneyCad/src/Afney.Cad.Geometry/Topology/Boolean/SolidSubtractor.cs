using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Geometry.Topology.Boolean;

/*
   NE: Katı SUBTRACT (SolidSubtractor) — CSG Boolean, DAR KAPSAMLI genel SUBTRACT sarmalayıcısı.
   NEDEN: `docs/Roadmap_CSG_Boolean.md` (2026-08-02 güncellemesi, ikinci araştırma turu) —
          genel çok-yüzlü SUBTRACT (B, A'nın sınırını BİRDEN FAZLA yüzden kesiyor) için
          "iç-yüz (internal face) çakışması" sorunu ÇÖZÜLMEDİ (tam `SolidClassifier`
          entegrasyonu gerektiriyor, kapsam dışı bırakıldı — bkz. roadmap satır ~193-214).
          AMA roadmap'in kendi tespiti: B'nin A'nın sınırıyla SADECE TEK BİR düzlemde kesiştiği
          özel durum (en yaygın MEP senaryosu — bir kanal/boru tek bir düz duvar yüzünü deliyor)
          ZATEN `PlaneCutter.CutWithPlane` ile TAM OLARAK çözülüyor (roadmap satır ~219-225).

   YÖNTEM: B'nin her yüzünün düzlemi, A'nın MEVCUT sınırını GERÇEKTEN (transversal — bir Face'in
          köşeleri hem pozitif hem negatif tarafta) kesiyor mu diye kontrol edilir:
          - Hiçbiri kesmiyorsa (B, A'nın tamamen dışında VEYA tamamen A'nın B∩A'sının içinde
            gömülü) → açık hata (roadmap'in "cavity kapsam dışı" kararıyla tutarlı).
          - TAM BİR yüz kesiyorsa → tek-düzlem durumu, `PlaneCutter.CutWithPlane` DOĞRUDAN
            çağrılır (o yüzün KENDİ dışa dönük Normal'i ile — bu, "atılacak tarafın B'nin içine
            baktığı" yönü otomatik doğru verir, ayrıca bkz. alttaki NEDEN NORMAL DOĞRUDAN
            KULLANILABİLİR notu).
          - BİRDEN FAZLA yüz kesiyorsa → genel çok-yüzlü durum, kapsam dışı: açık
            `NotSupportedException` (sessiz yanlış geometri yerine).

   NEDEN B'NİN YÜZ NORMALİ DOĞRUDAN KULLANILABİLİR: `PlaneCutter.CutWithPlane`, verilen
          `planeNormal` yönündeki (pozitif) tarafı TUTAR. B'nin bir yüzünün outward Normal'i,
          B'nin İÇİNDEN dışına, yani A'nın B-dışı kalan bölgesine doğru bakar (bkz.
          `BRepBuilder.ExtrudeBox`'ın ürettiği outward-normal kuralı) — bu yüzden B'nin o
          yüzünün Normal'i AYNEN `planeNormal` olarak geçilirse, "pozitif taraf" tam olarak
          A∖B'nin kalması gereken tarafı olur. `PlaneCutterTests.
          CutWithPlane_RoadmapScenario_SlabCutReducesToSinglePlaneCut` testinin
          `-Vector3D.XAxis` kullanması da BİREBİR bu kuralın elle uygulanmış hâlidir (B'nin
          X=1000 yüzünün outward normali -X'tir).

   KAPSAM DIŞI (bilinçli, roadmap'in ikinci araştırma turunun kararıyla): çok-yüzlü genel
          SUBTRACT (iç-yüz sınıflandırması, `SolidClassifier` entegrasyonu gerektirir) ve
          B'nin A içinde tamamen gömülü olduğu (cavity/boşluklu katı) durum — ikisi de AÇIK
          `NotSupportedException` ile korunuyor, sessiz yanlış geometri ÜRETİLMİYOR.
*/
public static class SolidSubtractor
{
    /*
       NE: `a` Solid'inden `b` Solid'ini çıkarır — SADECE b'nin a'nın sınırını TEK BİR
           düzlemde kestiği özel durumda çalışır.
       NOT: `a` YERİNDE (in place) değiştirilir — `PlaneCutter.CutWithPlane` ile AYNI desen.
            `b` DOKUNULMAZ (sadece yüz düzlemleri/normalleri okunur).
       ÇIKTI: Yeni oluşturulan kapak Face'i (`PlaneCutter.CutWithPlane` ile birebir aynı).
    */
    public static Face Subtract(Solid a, Solid b)
    {
        var candidatePlanes = GeneralSolidSubtractor.CollectCandidatePlanes(a, b);

        if (candidatePlanes.Count == 0)
            throw new NotSupportedException(
                "SolidSubtractor: B, A'nın sınırını hiçbir yüz düzleminde GERÇEKTEN (transversal) kesmiyor — " +
                "B tamamen A'nın dışında ya da tamamen A içinde gömülü (cavity/boşluklu katı, çok-kabuklu Solid " +
                "desteği gerekir) olabilir; her iki durum da kapsam dışı, bkz. Roadmap_CSG_Boolean.md.");

        if (candidatePlanes.Count > 1)
            throw new NotSupportedException(
                $"SolidSubtractor: B, A'nın sınırını {candidatePlanes.Count} FARKLI düzlemde kesiyor — " +
                "çok-yüzlü genel SUBTRACT kapsam dışı (iç-yüz/internal-face sınıflandırması için tam " +
                "SolidClassifier entegrasyonu gerekir). Bkz. Roadmap_CSG_Boolean.md, 2026-08-02 güncellemesi.");

        var (point, normal) = candidatePlanes[0];
        return PlaneCutter.CutWithPlane(a, point, normal);
    }
}
