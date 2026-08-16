# Roadmap — Gerçek CSG Boolean Operasyonları (Tam Topolojik B-Rep)

> **Durum:** Planlama → İmplementasyon başladı — 2026-07-19, Session #53
> **Bağlam:** `BRepBuilder.cs` başlığı boolean işlemleri (union/subtract/intersect) açıkça
> "kapsam dışı" olarak dokümante ediyor. `WallBRepService`, kapı/pencere boşluklarını
> BOOLEAN OLMADAN, duvarı parçalara bölerek (segmentasyon) çözüyor — çalışıyor ama genel
> değil (ör. döner açılı bir boşluk, iki farklı katıdan oluşan karmaşık bir kesişim, veya bir
> borunun bir duvarı gerçekten "delmesi" segmentasyonla ifade edilemez).
>
> **KULLANICI KARARI:** Mesh-seviyesi (üçgen soup) ara adım ATLANIYOR — doğrudan
> **tam topolojik B-Rep boolean** (`Solid`/`Face`/`Loop`/`TopologyEdge`/`Vertex` seviyesinde,
> sonuç yine geçerli bir winged-edge `Solid` — Euler doğrulanabilir, IFC'ye temiz geometri
> olarak yazılabilir) hedefleniyor. Mevcut `BRepBuilder`/`WallBRepService`/segmentasyon kodu
> SİLİNMEYECEK — boolean, ek bir yetenek olarak `Afney.Cad.Geometry.Topology` üzerine inşa
> edilecek.

## Neden Zor

Gerçek CSG boolean, iki katı cismin yüzeylerinin birbirini nerede kestiğini bulup, o kesişim
eğrileri boyunca yüzeyleri bölüp, ortaya çıkan parçaları "içeride/dışarıda/sınırda" olarak
sınıflandırıp, sonucu tekrar geçerli bir manifold B-Rep'e birleştirmeyi gerektirir. Endüstri
standardı kernel'ler (OpenCASCADE, CGAL, Parasolid) bunun için on yıllardır geliştirilen, çok
büyük kod tabanları kullanır — sıfırdan "tam genel" bir versiyonu yazmak gerçekçi değil.

## İlerleme Durumu (2026-07-19, Session #53)

**Faz 1-3 TAMAMLANDI ve DOĞRULANDI** (gerçek testlerle, Euler formülü + analitik hacim/alan
karşılaştırması):
- ✅ `PlaneIntersection.cs` + `FaceIntersection.cs` — iki Face'in gerçek kesişim segmentini
  buluyor (kutu-kutu senaryosuyla elle doğrulandı, `FaceIntersectionTests.cs`).
- ✅ `EdgeSplitter.cs` — bir kenarı ikiye bölüyor, HER İKİ komşu Face'in Loop'unu güncelliyor,
  Euler formülünü ve hacmi koruyor (`EdgeSplitterTests.cs`, 3/3).
- ✅ `FaceSplitter.cs` — bir Face'i bir kiriş boyunca iki alt-Face'e ayırıyor, Euler formülünü
  ve hacmi koruyor, alan hesabıyla çapraz doğrulandı (`FaceSplitterTests.cs`, 2/2).
- ✅ `SolidClassifier.cs` — Möller–Trumbore ışın-üçgen kesişimiyle nokta-içi-katı testi,
  iç-içe geçmiş iki kutu senaryosuyla doğrulandı (`SolidClassifierTests.cs`, 4/4).

**Faz 4 TAMAMLANDI (2026-07-20, Session #52) — ama planlanandan FARKLI, daha dar/daha sağlam
bir kapsamla:** Roadmap'in önerdiği ilk test senaryosu (A=[0,2000]³, B=[1000,3000]×[0,2000]×
[0,2000] — B'nin Y/Z aralığı A'nınkiyle BİREBİR aynı) implementasyon sırasında analiz edilince
şu ortaya çıktı: bu senaryoda B'nin A ile GERÇEKTEN (coplanar olmayan şekilde) kesişen TEK yüzü
B'nin X=1000 yüzüdür — B'nin diğer 5 yüzü ya A'nın tamamen dışında (X=3000) ya da A'nın karşılık
gelen yüzleriyle TAM ÇAKIŞIK (coplanar: alt/üst/Y=0/Y=2000). Coplanar yüz çiftleri, Faz 1-3'ün
KENDİ dokümante ettiği "dejenere, kapsam dışı" durumdur (`FaceIntersection` paralel düzlemler
için boş liste döner) — genel iki-katı SUBTRACT'i bu senaryoda dahi doğru yapmak, iki BAĞIMSIZ
Solid'in aynı konumdaki ama FARKLI Vertex nesnelerini birleştiren bir "vertex kaynaşması"
(vertex welding) mekanizması gerektiriyordu — bu, Faz 1-3'ün primitiflerinin ötesinde, kendi
başına ayrı bir mühendislik çabası (gerçek CSG kernel'lerinin kod hacminin büyük kısmı buradan
gelir).

**Bunun yerine teslim edilen, daha temel ve daha genel kullanışlı bir birim:**
- ✅ `PlaneCutter.cs` — `CutWithPlane(Solid, planePoint, planeNormal)`: bir Solid'i TEK bir
  düzlemle keser (yarı-uzay SUBTRACT), pozitif tarafı tutar, negatif tarafı atar, kesim yerine
  yeni bir "kapak" Face ekler. Roadmap'in önerdiği senaryo TAM OLARAK buna indirgeniyor (B, A'nın
  X eksenindeki her noktasını X=1000'in ötesinde kapsıyor → A∖B = A'yı X=1000 düzlemiyle kesip
  X<1000 tarafını tutmakla BİREBİR AYNI sonucu veriyor) — bu yüzden roadmap'in kendi test
  senaryosu, genel iki-katı SUBTRACT olmadan da TAM OLARAK doğrulanabildi.
  `PlaneCutterTests.cs`, 4/4: hem genelleştirilmiş bir "kutuyu ortadan kes" senaryosu, hem
  roadmap'in TAM önerdiği slab-cut senaryosu, ikisi de `BRepBuilder.ExtrudeBox`'ın bağımsız
  ürettiği beklenen sonuçla (hacim + Euler formülü + kapak alanı + kapak normal yönü) çapraz
  doğrulandı; artı iki dejenere-durum testi (düzlem katıyı hiç kesmiyor → açık hata).
- **Bulunan ve düzeltilen 2 gerçek implementasyon hatası** (ilk yazımda, testlerle yakalandı):
  (1) kesişim noktaları hesaplanırken `loop.Edges[i]`'e MUTASYON SIRASINDA (canlı listeden)
  erişiliyordu — bir kenarı bölmek listedeki SONRAKİ index'leri kaydırıyor, bu da ikinci kesişim
  için YANLIŞ kenar nesnesi seçilmesine yol açıyordu (kenar referansları artık splitlerden ÖNCE
  anlık görüntü/snapshot olarak alınıyor); (2) yeni oluşturulan kapak Face'i `solid.Faces`
  listesine hiç EKLENMİYORDU (Euler sayısı V-E+F=1 çıkıyordu, 2 olması gerekirken) — basit ama
  gerçek bir unutma hatasıydı.

**Genel iki-katı SUBTRACT (coplanar yüz birleştirme + vertex kaynaşması dahil) SONRAKİ bir
faza bırakıldı** — artık NET olarak anlaşılmış, dokümante edilmiş bir kapsamla: coplanar bir
A/B yüz çifti tespit edilince (aynı düzlem, aynı yönelim) B'nin kopyası tamamen elenmeli (A'nın
yüzü kazanır), ve A'nın kesim kirişleriyle B'nin ilgili yüzünün köşeleri AYNI Vertex nesnesine
indirgenmeli (konum bazlı eşleştirme + tolerans) — `PlaneCutter.CutWithPlane` bu ileride bu iş
için bir alt-adım (building block) olarak yeniden kullanılabilir (B'nin her yüzü için A'yı o
yüzün düzlemiyle art arda kesmek).

**GÜNCELLEME (2026-07-22, Session #55) — 1. yapı taşı (vertex kaynaşması) TAMAMLANDI:**
Önce bir araştırma ajanı görevlendirildi (gerçek CSG kernel'lerin — OpenCASCADE/CGAL —
coplanar merge + vertex welding'i nasıl ele aldığı araştırıldı). Ajanın NET tavsiyesi: bu
oturumda SADECE vertex kaynaşmasını (izole, önceki hiçbir testi bozmayan bir yapı taşı olarak)
tamamlamak, coplanar yüz birleştirme + genel SUBTRACT montajını (Faz 4'ün asıl hedefi) ayrı bir
oturuma bırakmak — "genel SUBTRACT + UNION/INTERSECT'i bu oturumda bitirmek gerçekçi değil"
değerlendirmesiyle.

- ✅ `Boolean/VertexWelder.cs` (yeni) — `Weld(IEnumerable<Solid>, tolerance)` / `Weld(Solid, tolerance)`:
  birbirine `tolerance` mesafesinden yakın `Vertex` nesnelerini (referans eşitliği — `Vertex`
  bir `class`, `Equals`/`GetHashCode` override edilmemiş) tek bir ortak nesneye indirger, TÜM
  `TopologyEdge.StartVertex`/`EndVertex` referanslarını buna göre yönlendirir (Id eşleştirmesi
  DEĞİL, fiziksel referans mutasyonu — `Solid.GetVertices()`'in `HashSet<Vertex>` kullanımı
  buna dayanıyor).
- **Tolerans netliği (araştırma bulgusu):** Bu, `SpaceDetectionEngine`/`WallChainBuilder`'daki
  kullanıcı-çizim toleransı (`MergeTolerance`, 5mm) ile KARIŞTIRILMAMALI — geometrik "aynı nokta
  mı" kararı, `PlaneCutter.Tolerance` (1e-6) mertebesinde olmalı; çağıran açıkça seçer,
  varsayılan yok (kasıtlı).
- **Kapsam (bilinçli, dar):** Basit O(n²) mesafe karşılaştırması, grup başına SADECE ilk elemana
  karşı (TRANSİTİF DEĞİL) — büyük Solid sayıları için ileride spatial hash gerekebilir.
- **Yeni testler:** `VertexWelderTests.cs` (3 test) — iki bağımsız kutunun paylaştığı TEK köşeyi
  doğru kaynaştırdığını (16→15 tekil vertex, `Assert.Same` ile referans eşitliği), kaynaşacak
  çift yokken hiçbir şeyi değiştirmediğini, hacim/Euler geçerliliğinin korunduğunu doğruluyor.
- **Sıradaki adımlar (araştırma ajanının önerdiği sıra):** (2) coplanar Face tespiti (normal
  paralelliği + düzlem ofseti eşitliği), (3) coplanar 2D polygon boolean (mevcut
  `FaceIntersection.ComputePlaneBasis`'in genişletilmesi), (4) genel SUBTRACT montajı — ÖNCE B
  convex özel durumu (PlaneCutter'ın B'nin yüzleriyle art arda çağrılması + VertexWelder),
  SONRA genel (içbükey B, `SolidClassifier` ile alt-Face sınıflandırma) durum.

**GÜNCELLEME (2026-07-31, Session #55) — 2. ve 3. yapı taşları TAMAMLANDI:**
- ✅ `Boolean/CoplanarFaceDetector.cs` — `AreCoplanar(Face, Face, angleTolerance, offsetTolerance)`:
  normal paralelliği (zıt yönlü dahil) + aynı normal'e göre ölçülen düzlem ofseti eşitliği.
  `CoplanarFaceDetectorTests.cs` (3 test).
- ✅ `Boolean/ConvexPolygonClipper2D.cs` — `Intersect(polyA, polyB, normal)`: dışbükey iki
  coplanar poligonun kesişimi (genelleştirilmiş Sutherland-Hodgman, yarı-düzlem kırpma).
  **Bilinçli dar kapsam:** SADECE dışbükey∩dışbükey (matematiksel olarak her zaman tek/dışbükey
  sonuç — çok parçalı çıktı İMKANSIZ, `List<Vector3D>` dönüşü dürüst). İçbükey girdide
  `InvalidOperationException` (sessiz yanlış sonuç yerine). UNION/DIFFERENCE bilinçli olarak
  kapsam dışı — SUBTRACT'in coplanar-payı kararı sadece INTERSECT'e ihtiyaç duyuyor.
  `ConvexPolygonClipper2DTests.cs` (5 test: özdeş kare, kısmi örtüşme, ayrık, biri diğerini
  kapsıyor, içbükey girdi → throw).

**GÜNCELLEME (2026-07-31, Session #55) — 4. adım (genel SUBTRACT montajı) ARAŞTIRILDI, BİLİNÇLİ
OLARAK ERTELENDİ (rushed implementasyon yerine):** Ana Yasa gereği bir araştırma ajanı
görevlendirildi — "(B convex özel durumu, PlaneCutter'ı art arda çağırmak)" fikrinin gerçekten
doğru olup olmadığı sınandı. **İki kritik bulgu, ikisi de "acele implementasyon yerine dürüst
erteleme" kararını gerektirdi:**

1. **Naif "B'nin her yüz düzlemiyle art arda kes, dış tarafı tut" YANLIŞ:** Dışbükey B için
   `B = ∩ᵢ insideᵢ` (yüzlerin iç yarı-uzaylarının KESİŞİMİ), De Morgan'a göre
   `complement(B) = ∪ᵢ outsideᵢ` — yani bir BİRLEŞİM, kesişim DEĞİL. `PlaneCutter`'ı art arda
   "dış tarafı tut" şeklinde çağırmak `∩ᵢ outsideᵢ`'yi hesaplar (her kesim bir öncekinden DAHA
   FAZLA malzeme atar) — B, A'ya göre küçükse bu genelde BOŞ küme çıkar. Doğru yöntem: önce
   A'yı B'nin düzlemleriyle art arda kesip (bu kez İÇ tarafı tutarak — kesişim-kesişimi yine
   kesişimdir, bu adım DOĞRU) `A∩B`'yi bulmak, SONRA `A−B`'yi ayrı bir yüz-yeniden-sınıflandırma
   adımıyla (A'nın B-dışı kalan parçaları + B'nin A-içi parçalarının normali ters çevrilmiş
   hâli) monte etmek — roadmap'in ORİJİNAL Faz 4 planıyla birebir aynı, `PlaneCutter`'ın kendisi
   bu ikinci adımı sağlamıyor (attığı "dış" parçayı SAKLAMIYOR, tam da montaj için gereken
   parça).
2. **Kernel'in kendisi "boşluklu katı" (solid-with-cavity) temsil EDEMİYOR:** `Solid.cs`
   incelendi — `Faces` düz bir `List<Face>`, `IsValid()` sabit `V-E+F==2` (genus 0) şartı
   koşuyor. Kabuk (shell) grubu / iç boşluk kavramı YOK. B TAMAMEN A'nın içinde kalırsa
   (ör. bir katının ortasında delinmemiş bir boşluk), sonuç prensipte "dış kabuk + iç kabuk"
   gerektirir — bu veri modelinde KATEGORİK OLARAK temsil edilemez (eksik özellik değil,
   yapısal bir sınırlama). Dürüst dar kapsam: SADECE B, A'nın SINIRINA değen/kesen durumlar
   (kanal boşluğu bir duvarı deliyor, cihaz hacmi bir sınır duvarıyla kırpılıyor) — B tam
   gömülüyse (cavity) AÇIK HATA.

**Karar:** Bu, "PlaneCutter + VertexWelder'ı birleştirmek" değil, GERÇEKTEN YENİ bir algoritma
(yüz parça takibi + yeniden sınıflandırma + montaj, birkaç yüz satır, çok sayıda incelikli
hata potansiyeli) — aceleyle bu oturumda sıkıştırmak yerine, ayrı bir ODAKLANMIŞ oturuma
bırakıldı (VertexWelder/PlaneCutter'ın kendisinin de daha önce aynı şekilde daraltıldığı gibi).
Somut, hazır algoritma (bir sonraki oturum doğrudan uygulayabilir):
1. A'yı B'nin her yüz düzlemiyle art arda kes, HER SEFERİNDE İÇ tarafı tut (`PlaneCutter`'ın
   "pozitif tarafı tut" mantığının aynısı, sadece normal işareti B'nin içine bakacak şekilde
   seçilir) → sonuç `A∩B` (`C` diyelim).
2. Adım 1'i, HER orijinal A-yüzü için "B-dışında kalan parça"yı ATMAK yerine SAKLAYACAK şekilde
   yeniden yapılandır (`PlaneCutter`'ın mevcut "discarded" mantığı tam tersine çevrilmeli).
3. `C`'nin kesim sırasında oluşan kapak yüzlerinin (B'nin sınırının A içine yansıması) normalini
   ters çevir — bunlar A'ya açılan oyuğun/çentiğin duvarları olur.
4. Korunan A-parçaları + ters-çevrilmiş-C-kapak-yüzleri tek bir yeni `Solid`'de birleştirilir,
   `VertexWelder` ile köşeler kaynaştırılır, `IsValid()` + hacim kontrolüyle (`A.Hacim -
   Kesişim.Hacim == Sonuç.Hacim`) doğrulanır.
5. Ön koşul kontrolü: B, A içinde TAM GÖMÜLÜ değilse çalışsın (ör. B'nin en az bir köşesi/yüzü
   A'nın dışında) — aksi halde açık `NotSupportedException` ("cavity/boşluklu katı kapsam
   dışı — çok-kabuklu Solid desteği gerekir").
- **Sıradaki adım (net):** yukarıdaki 5 adımı `Boolean/SolidSubtractor.cs` (veya benzeri) olarak
  uygula, `PlaneCutter`'a dokunmadan (paralel, ayrı bir "iç tut + parça sakla" varyantı yazarak
  veya `PlaneCutter`'a opsiyonel bir `keepBothSides`/callback parametresi ekleyerek — hangisi
  daha az riskli olduğuna implementasyon zamanı karar verilmeli), box-minus-box slab senaryosu
  + sınır-çentiği senaryosuyla test edilmeli.

**GÜNCELLEME (2026-08-02) — İmplementasyona başlamadan ÖNCE, gerçek kaynak koda (PlaneCutter/
FaceSplitter/TopologyEdge, satır satır) karşı doğrulama yapıldı ve önceki araştırma ajanının
planının da kaçırdığı 2 YENİ yapısal sorun bulundu. Ana Yasa gereği (aceleyle yanlış kod yerine
dürüst tespit) implementasyon YİNE ertelendi, ama bu kez somut bir çözüm yolu ve net bir kapsam
sınırı ile:**

1. **Chord-edge "öksüzleşmesi" (çözüldü, tasarım hazır):** `FaceSplitter.SplitAtChord` chord
   kenarını HEM `faceA.LeftFace`/`RightFace` HEM `faceB`'ye doğru şekilde atıyor — ama
   `PlaneCutter.BuildCapFace`, kesim sonunda bu ikisinden BİRİNİ (atılan tarafı) körlemesine
   `capFace` ile EZİYOR (`if (forward) edge.LeftFace = capFace; else edge.RightFace = capFace;`).
   Atılan yarı (`faceB`) hafızada kalmaya devam ediyor ve kendi Loop'u hâlâ bu chord kenarını
   referans alıyor, ama artık chord kenarının kendisi `faceB`'yi TANIMIYOR (`LeftFace`/
   `RightFace` alanları `faceA`+`capFace`'e işaret ediyor). Yani `faceB`'yi doğrudan yeni bir
   `Solid`'e eklemek, `IsValid()`'i YANLIŞLIKLA geçebilen ama gerçekte tutarsız (kenar-komşuluğu
   kırık) bir B-Rep üretir — sessiz yanlış sonuç riski.
   **Çözüm (tasarlandı, kodlanmadı):** `CutWithPlaneKeepDiscarded` varyantı, `BuildCapFace`
   atılan tarafı ele geçirmeden HEMEN ÖNCE, chord kenarının bir KOPYASINI (`dup`, aynı
   Start/EndVertex) oluşturup atılan Face'in Loop'undaki referansı buna yönlendirmeli; sonra
   TÜM bu `dup` kenarlarından, orijinal kapağın (`-n` normalli) TAM AYNASI olan ikinci bir
   "mirror cap" (`+n` normalli) inşa edilmeli — `BuildCapFace`'in mevcut `forward` mantığı
   AYNI kenar sırası/yönü üzerinde çalıştığından, mirror cap için "ters slot"a (Left↔Right)
   yazması gerekiyor (küçük bir `assignToOppositeSide` parametresiyle çözülebilir).

2. **İç-yüz (internal face) çakışması (ÇÖZÜLMEDİ, kapsam sınırlaması gerekiyor):** Roadmap'in
   "A'yı B'nin HER yüz düzlemiyle art arda kes" adımı matematiksel olarak (`∪Dᵢ = A\B`, ayrık
   parçalama özdeşliği) DOĞRU — ama bu SADECE bölgelerin (region) birleşimi için doğru, SINIR
   YÜZEYLERİNİN doğrudan üst üste toplanabileceği anlamına GELMİYOR. `Dᵢ` (adım i'de atılan
   parça) ile `Dⱼ` (j>i, sonraki bir adımda atılan parça) GEOMETRİK OLARAK KOMŞU olabilir —
   ikisi de sonuçta `A\B`'nin İÇİNDE kalacağından, aralarındaki ortak sınır (adım i'nin "mirror
   cap"ının bir kısmı) `A\B`'nin GERÇEK dış sınırı DEĞİL, İÇ (internal) bir yüzey olur ve B-Rep
   sonucuna DAHİL EDİLMEMELİ. Naif "her `Dᵢ`'nin tam mirror cap'ini ekle" yaklaşımı, B'nin
   birden fazla yüzü A'nın sınırını farklı yerlerden kesiyorsa (ör. B'nin bir köşesi A'nın bir
   köşesinden çıkıyorsa), sonuçta FAZLADAN/İÇ-SAPLANMIŞ yüzeyler üretip `IsValid()`'i (Euler)
   BOZAR ya da (daha kötüsü) Euler sayısı tesadüfen tutsa bile geometrik olarak yanlış (çift
   duvarlı/boşluklu) bir katı üretir. Bunun doğru çözümü (gerçek CSG kernel'lerinin yaptığı),
   her mirror cap parçasının HANGİ bölgeyle (C=A∩B mi, yoksa BAŞKA bir Dⱼ mi) komşu olduğunu
   sınıflandırıp sadece C'ye komşu kısmı sınıra dahil etmek — bu, Faz 3'ün (`SolidClassifier`)
   tam kapsamlı kullanımını gerektiriyor, roadmap'in basitleştirilmiş "B-convex özel durumu"
   kısayolunun ÖTESİNDE bir iş.
   **Ek bulunan dejenere durum:** B'nin bazı yüzlerinin düzlemi A'nın GEÇERLİ sınırını hiç
   kesmeyebilir (ör. B, A'dan çok daha büyükse B'nin çoğu yüzü A'nın tamamen dışında kalır) —
   bu durumda `chordEdges` boş kalır ve `BuildCapFace` HEMEN `NotSupportedException` fırlatır
   (roadmap'in "her B-yüzüyle art arda kes" döngüsü bu durumu hiç ele almıyor); önce
   "bu B-yüzünün düzlemi A'nın MEVCUT sınırını gerçekten kesiyor mu" ön-kontrolü şart
   (kesmiyorsa o yüzü sessizce atla).

**Karar (ikinci kez, aynı gerekçeyle):** Genel çok-yüzlü SUBTRACT implementasyonu YİNE
ertelendi — 2 numaralı sorun (iç-yüz sınıflandırması), roadmap'in öngördüğünden daha büyük bir
iş (Faz 3 `SolidClassifier`'ın tam entegrasyonu). **Daraltılmış, GERÇEKTEN teslim edilebilir
alternatif:** B'nin A'nın sınırıyla SADECE TEK BİR düzlemde (yani B'nin SADECE BİR yüzü A'yı
gerçekten kesiyor, diğerleri A'nın tamamen dışında veya tamamen A'nın B-kesişiminin içinde)
kesiştiği özel durum — ki bu zaten Faz 4'ün ilk teslimatı olan `PlaneCutter.CutWithPlane` ile
BİREBİR aynı sonucu veriyor (yukarıdaki "İlerleme Durumu" bölümünde açıklandığı gibi). Yani
pratikte en yaygın MEP senaryosu (bir kanal/boru tek bir düz duvar yüzünü deliyor) ZATEN
`PlaneCutter` ile çözülebiliyor — asıl eksik olan, B'nin A'yı BİRDEN FAZLA yüzünden (ör. bir
köşeden) kestiği GERÇEKTEN genel durum, ve bu hâlâ ayrı, odaklanmış bir oturum gerektiriyor.

**GÜNCELLEME (2026-08-02, devam) — 1. adım (chord-edge düzeltmesi) VE dar-kapsamlı
`SolidSubtractor` UYGULANDI:**

1. ✅ `Boolean/PlaneCutter.cs` — `CutWithPlaneKeepDiscarded(Solid, planePoint, planeNormal,
   discardedSolidName)` (yeni, ADDITIVE — `CutWithPlane` DOKUNULMADI, davranışı BİREBİR aynı
   kalıyor, testle doğrulandı). Yukarıda tasarlanan çözüm birebir uygulandı: her chord'un atılan
   tarafa bakan yarısı, `BuildCapFace` tarafından ele geçirilmeden HEMEN ÖNCE aynı Start/End
   Vertex'e sahip bir `dup` kopyasına devredilir (`ReplaceEdgeInFace` ile atılan Face'in Loop'u
   `dup`'a yönlendirilir); tüm `dup` kenarlarından `BuildCapFaceOnFreeSide` ile ikinci bir
   "mirror cap" (ters normal) inşa edilir. `BuildCapFaceOnFreeSide`, orijinal `BuildCapFace`'in
   "forward" yön kuralı yerine her kenarın HÂLÂ BOŞ olan Left/Right slotuna bakar — bu, ayrı bir
   `assignToOppositeSide` parametresine gerek KALMADAN doğru slotu otomatik seçiyor (tasarımda
   öngörülenden daha basit çıktı).
   - Yeni testler: `PlaneCutterKeepDiscardedTests.cs` (3 test) — (a) atılan yarının bağımsız bir
     `Solid` olarak Euler-geçerli olduğunu VE her kenarın Left/Right Face'inin kendi Loop'unda
     GERÇEKTEN o kenarı referans aldığını (öksüzleşme yok) doğruluyor, (b) mirror cap'in ZIT
     normal + AYNI alan taşıdığını, (c) kept tarafın `CutWithPlane`'in doğrudan çağrılmasıyla
     BİREBİR aynı sonucu (hacim, Euler, kapak alanı/normali) verdiğini kanıtlıyor.
   - **Bulunan gerçek test hatası (yakalandı, düzeltildi):** İlk yazımda mirror cap testi
     `Math.Abs(Normal.X) > 0.9` filtresiyle YANLIŞLIKLA 2 eşleşme buluyordu — çünkü kübün
     tamamen atılan X=-1000 yan yüzü de (normal (-1,0,0)) mirror cap ile (normal (+1,0,0))
     AYNI alana (4.000.000, 2000×2000) sahip. Filtre `Normal.X > 0.9` (mutlak değer DEĞİL) olarak
     düzeltildi — implementasyonda hata YOKTU, test ayırt edici gücü yetersizdi.

2. ✅ `Boolean/SolidSubtractor.cs` (yeni) — `Subtract(Solid a, Solid b)`: roadmap'in "tek-düzlem"
   özel durumunu (B'nin A'nın sınırını SADECE TEK BİR yüz düzleminde GERÇEKTEN/transversal
   kestiği durum) otomatik TESPİT edip `PlaneCutter.CutWithPlane`'e devrediyor — B'nin o yüzünün
   KENDİ outward Normal'i doğrudan `planeNormal` olarak kullanılabiliyor (roadmap'in elle
   `-Vector3D.XAxis` seçtiği kuralın genelleştirilmiş/otomatik hâli — bkz. dosya başındaki NEDEN
   NORMAL DOĞRUDAN KULLANILABİLİR notu).
   - Dejenere atlama: `PlaneIntersectsSolidBoundary` yardımcı fonksiyonu, `PlaneCutter.
     CutWithPlane`'in KENDİ per-face "mixed" (hasPos && hasNeg) sınıflandırma kuralıyla BİREBİR
     aynı testi kullanıyor — B'nin bir yüzü A'nın sınırını hiç kesmiyorsa (tamamen dışında veya
     coplanar) sessizce ADAY LİSTESİNE eklenmiyor (throw YOK).
   - Aday sayısı 0 → `NotSupportedException` ("B tamamen dışında veya gömülü/cavity — kapsam
     dışı"). Aday sayısı >1 → `NotSupportedException` ("çok-yüzlü genel SUBTRACT kapsam dışı,
     SolidClassifier entegrasyonu gerekir").
   - Yeni testler: `SolidSubtractorTests.cs` (3 test) — (a) box-minus-box slab senaryosunun
     `SolidSubtractor.Subtract` ile doğrudan `PlaneCutter.CutWithPlane` çağrısıyla BİREBİR aynı
     sonucu (hacim, Euler, kapak alanı/normali) verdiği, (b) B'nin A'yı BİRDEN FAZLA yüzden
     kestiği köşe-çentiği senaryosunda (`B=[1500,3000]×[1500,3000]×[0,2000]`, A'nın X VE Y
     sınırlarını AYRI AYRI transversal kesiyor) `NotSupportedException` fırlatıldığı, (c) B
     tamamen A'nın dışındayken de aynı hatanın fırlatıldığı doğrulandı.

**Test sonucu:** `dotnet test` — 345/345 BAŞARILI (önceki 339 + bu oturumun 6 yeni testi), 0
başarısız, hiçbir mevcut test (özellikle `PlaneCutterTests`, `VertexWelderTests`) BOZULMADI.

**Kapsam dışı kalan (BİLİNÇLİ, DEĞİŞMEDİ):** Genel çok-yüzlü SUBTRACT (roadmap'in 2 numaralı
"iç-yüz/internal-face çakışması" sorunu — bkz. yukarıdaki 2026-08-02 girişi) HÂLÂ
uygulanmadı — bu, `SolidClassifier`'ın (Faz 3) tam entegrasyonunu (her mirror-cap parçasının
hangi bölgeyle komşu olduğunu sınıflandırma) gerektiriyor, roadmap'in kendi değerlendirmesiyle
(iki ayrı araştırma turu sonrası) tek oturumda güvenle teslim edilemeyecek kadar büyük bir iş.
`SolidSubtractor.Subtract`, bu genel durumla karşılaşınca AÇIK `NotSupportedException` fırlatır
(sessiz yanlış geometri ÜRETMEZ) — sıradaki odaklanmış oturum, roadmap'in "Somut, hazır
algoritma" bölümündeki (satır ~150-163) 5 adımı doğrudan uygulayabilir; `CutWithPlaneKeepDiscarded`
artık o adımların (özellikle 2 ve 3 numaralı, "discarded parçaları saklama" ve "mirror cap normal
ters çevirme") temel yapı taşı olarak HAZIR.

**GÜNCELLEME (2026-08-04) — `GeneralSolidSubtractor` + `FaceRegionClassifier` UYGULANDI, AMPİRİK
OLARAK 2 AYRI YAPISAL ENGEL BULUNDU (ikisi de mevcut kutu-tabanlı test senaryolarında GERÇEKTEN
tetiklendi, teorik değil):**

Bir araştırma ajanı (Ana Yasa gereği) yukarıdaki 5 adımlı algoritmayı kaynak kodla çapraz
doğrulayıp `FaceRegionClassifier.IsFaceAdjacentToRegion` (mirror cap'in centroid'ini KENDİ
outward normali boyunca epsilon-kaydırıp `SolidClassifier.IsPointInside` ile komşu bölgeye
bitişikliğini test eden yeni, izole bir yapı taşı) + `GeneralSolidSubtractor.Subtract` (adayları
ard arda `CutWithPlaneKeepDiscarded` ile B'nin İÇİNE doğru keserek `a`'yı A∩B'ye daraltan, her
adımda atılan Dᵢ parçasının mirror cap'ini `FaceRegionClassifier` ile filtreleyip sonucu
`VertexWelder`+`IsValid()` ile monte eden montaj) önerdi ve "bu oturumda güvenle uygulanabilir"
dedi. Uygulandı, testlerle sınandı — **iki farklı gerçek kutu-kutu senaryosunun İKİSİ de
`IsValid()` başarısızlığına çarptı, farklı kök nedenlerle:**

1. **Köşe-çentiği** (B, A'nın bir köşesini örtüyor, sonuç TEK PARÇA olurdu): ardışık kesimlerin
   mirror cap'leri KISMEN örtüşüyor — ilk kesimin mirror cap'i (o anki `a`'nın TAM kesitini
   kaplıyor, henüz sonraki düzlemlerle daraltılmamış) hem gerçek A∩B'ye HEM DE ikinci kesimde
   atılacak Dⱼ'ye bitişik alanlar İÇERİYOR. `FaceRegionClassifier` Face'i BÜTÜN olarak tek bir
   bölgeye atıyor (ikili karar) — bu yüzden Face'in KENDİSİNİN (`ConvexPolygonClipper2D` ile
   diğer aday düzlemlerin yarı-uzaylarına göre kırpılması) bölünmesi gerekiyor, ayrı bir
   mühendislik adımı.
2. **"Through-slot"** (B, A'yı X ekseninde ortadan bir dilim gibi kesiyor, diğer 2 eksende A'yı
   TAM kapsıyor — mirror cap'ler bu kez GERÇEKTEN örtüşmüyor): ama sonuç Solid'i İKİ AYRI,
   BAĞLANTISIZ parçadan (X'in iki ucundaki kalan bloklar) oluşuyor. `Solid.IsValid()`'in Euler
   testi (`V-E+F==2`) TEK bağlantılı bileşen (genus 0, tek kabuk) varsayıyor — iki bağımsız kutu
   birleştirilince `eulerChar=4` çıkıyor, KATEGORİK olarak geçersiz sayılıyor. Bu, `SolidSubtractor`'ın
   zaten dokümante ettiği "cavity kapsam dışı" sınırlamasıyla AYNI kök neden (çok-kabuklu Solid
   desteği yok) — sadece farklı bir tetikleyici senaryo.

**Sonuç:** Bu codebase'in basit kutu-tabanlı `Solid` modelinde, çok-düzlem SUBTRACT'in denenen
İKİ doğal aday senaryosunun (köşe-çentiği, through-slot) HİÇBİRİ şu anki yapı taşlarıyla
gerçekleşmiyor — güvenli, gerçekleşen bir "happy path" YOK. `GeneralSolidSubtractor` yine de
DEĞERLİ: (a) tek-düzlem durumunu `SolidSubtractor` ile birebir aynı sonuçla çözüyor (delegasyon),
(b) çok-düzlem durumunda SESSİZ yanlış geometri ÜRETMİYOR — `IsValid()` güvenlik ağı her iki
bilinen başarısızlık modunu da AÇIK `InvalidOperationException` ile yakalıyor. `FaceRegionClassifier`
kendi başına doğru ve test edilmiş bir yapı taşı (komşu-Solid bitişiklik testi) — sadece
Face-bölme olmadan tek başına yeterli değil. **Kod tabanına EKLENDİ** (additive, mevcut 345 testi
bozmadı, +7 yeni test: 4 `GeneralSolidSubtractorTests`, 3 `FaceRegionClassifierTests`).

**Sıradaki oturum için net, ampirik olarak doğrulanmış iki ayrı yol (kullanıcı hangisinin
öncelikli olduğuna karar vermeli):**
- **(A) Çok-kabuklu `Solid` desteği** (through-slot + cavity'yi AYNI ANDA çözer, `Solid.IsValid()`'in
  Euler testini "bağlantılı bileşen başına 2" olacak şekilde genelleştirmek gerekir — muhtemelen
  daha temel/geniş etkili).
- **(B) `ConvexPolygonClipper2D` ile mirror-cap Face bölme** (SADECE köşe-çentiğini çözer — MEP
  senaryolarında muhtemelen daha sık karşılaşılan durum, ör. bir kanalın bir duvar köşesini kesmesi).

**GÜNCELLEME (2026-08-04, aynı gün devam) — (A) UYGULANDI (aşağıda), (B) DENENDİ, DAHA BÜYÜK
BİR ENGEL BULUNUP ERTELENDİ:**

**(A) tamamlandı:** `Solid.IsValid()` artık bağlantılı-bileşen (kabuk) başına Euler doğrulaması
yapıyor (`V-E+F==2` her kabuk için ayrı ayrı, TOPLAMDA değil) — through-slot senaryosu artık
GEÇERLİ ve doğru hacimle çalışıyor. **Uygulama sırasında GERÇEK bir regresyon yakalandı ve
düzeltildi:** komşuluk grafiği ilk yazımda `TopologyEdge.LeftFace`/`RightFace` alanlarına
bakarak kuruluyordu — ama `FaceSplitter` bir Face'i böldüğünde KOMŞU Face'in bu alanları HER
ZAMAN yeni (bölünmüş) Face'e güncellemiyor (stale referans). Bu, `Faces` listesinde artık
OLMAYAN "hayalet" Face'lerin bileşene dahil edilip yanlış V/E/F sayımına yol açmasına neden
oldu (`PlaneCutterTests`/`SolidSubtractorTests` kırıldı). **Düzeltme:** komşuluk artık SADECE
`Faces` listesindeki (authoritative) Face'lerin kendi `Loop.Edges`'inde PAYLAŞTIKLARI kenarlara
bakılarak kuruluyor, stale alanlara değil.

**(B) denendi — SANILANDAN DAHA BÜYÜK bir yapısal engel bulundu:** Matematiksel olarak doğru
yaklaşım netti: D₀'ın mirror cap'i, SONRAKİ aday düzlemlerin (D₀'dan sonra kesilen) "içeri"
yarı-uzaylarına göre `ConvexPolygonClipper2D` benzeri bir yarı-düzlem kırpmayla kırpılırsa,
kalan parça TAM OLARAK A∩B'nin o düzlemdeki gerçek sınır yüzeyine eşit çıkıyor (elle
doğrulandı, köşe-çentiği örneğiyle) — `FaceRegionClassifier`'a bile gerek kalmıyor. AMA
kırpma, mirror cap'in sınırında YENİ bir kesim kenarı yaratıyor ve winged-edge modeli
(`TopologyEdge.LeftFace`+`RightFace`, İKİSİ DE dolu olmalı — `Solid.IsValid()`'in manifold
kuralı) bu yeni kenarın "diğer tarafında" bir Face gerektiriyor. Bu diğer taraf, KOMŞU D_j
parçasının KENDİ topolojisinde EŞLEŞEN bir ikiz kenar olmalı — yani salt 2D poligon kırpma
YETMİYOR, İKİ AYRI D_i/D_j parçası arasında `PlaneCutter.CutWithPlaneKeepDiscarded`'daki `dup`
kenar tekniğinin bir GENELLEMESİNİ (parçalar-arası kenar dikişi/cross-piece edge stitching)
gerektiriyor — bu, chord-edge fix'ten (tek Solid içinde, tek kesim) DAHA BÜYÜK, daha riskli bir
mühendislik işi (kaç D_i-D_j çiftinin nerede kesiştiğini bulup her biri için doğru ikiz kenarları
kurmak). **Karar (üçüncü kez, aynı gerekçeyle — sessions #34/#35'in "aceleyle yanlış kod yerine
dürüst tespit" kararıyla tutarlı):** Bu, ayrı, odaklanmış bir oturum gerektiriyor. Kod
değişikliği YAPILMADI (araştırma + matematiksel doğrulama, mevcut 356 testin hiçbiri etkilenmedi).

**GÜNCELLEME (2026-08-06) — "Yol B" (mirror-cap Face bölme + cross-piece edge stitching)
TAMAMLANDI — algoritma DEĞİŞTİRİLEREK (ardışık kesim yerine klasik subdivide→classify→
reconstruct) çözüldü, KOD YAZILDI, testlerle doğrulandı:**

Bir araştırma ajanı (Ana Yasa gereği), önce klasik B-Rep boolean literatürünü (Requicha &
Voelcker, "Boolean Operations in Solid Modeling: Boundary Evaluation and Merging Algorithms",
Proc. IEEE 1985 — subdivision/classification/reconstruction üç aşaması; ayrıca Naylor/
Amanatides/Thibault'nin BSP-tree merging yaklaşımı) araştırdı, sonra kaynak kodu (`PlaneCutter`,
`GeneralSolidSubtractor`, `FaceRegionClassifier`, `Solid`, `TopologyEdge`, `FaceSplitter`,
`SolidClassifier`) satır satır inceledi. **Kritik bulgu:** önceki oturumun "Yol B" analizi
DOĞRUYDU ama eksikti — sadece mirror-cap'lerin KISMEN örtüşmesi değil, ARDIŞIK kesim
yaklaşımının kendisi (`PlaneCutter.CutWithPlaneKeepDiscarded`'ı B'nin düzlemleriyle sırayla
çağırmak) yapısal olarak kusurluydu: bir SONRAKİ adımın kestiği "A yüzü", önceki bir adımın
ürettiği ARA-kapak yüzü olabiliyordu (ör. iki adımlı köşe-çentiğinde ikinci kesim, ilk kesimin
mirror cap'inin bir parçasını "A yüzü" gibi işleyip KENDİ İÇİNDE de sahte/iç parça üretebiliyordu)
— bu, roadmap'in daha önce hiç belgelemediği İKİNCİ bir gizli hata kaynağıydı.

**Çözüm (ardışık kesim TAMAMEN TERK EDİLDİ, yerine):** `GeneralSolidSubtractor.Subtract`'in
çok-düzlem yolu, klasik subdivide→classify→reconstruct ile yeniden yazıldı:
1. A'nın HER orijinal Face'i TÜM aday düzlemlere göre TEK SEFERDE (eşzamanlı) alt-parçalara
   ayrılıyor (`SplitFaceAgainstPlanes`) — bir alt-parça bir düzlemin TAMAMEN dışında bulunur
   bulunmaz KESİN "kept" sayılıyor (De Morgan OR, kısa devre — daha fazla düzlem kontrol
   edilmiyor); TÜM düzlemlerden "insideB" olarak hayatta kalan bir parça KESİN "discarded".
   Bu, her alt-parçanın SADECE A'nın orijinal 1 yüzünden türemesini garanti ediyor (önceki
   ara-kapak sorunu YAPISAL OLARAK ortadan kalkıyor — hiçbir ara-kapak "A yüzü" gibi tekrar
   işlenmiyor).
2. Her aday düzlemin TAM (kırpılmamış) kesit poligonu, A'nın MUTASYONA UĞRAMAMIŞ orijinal
   Face'lerinin sınır poligonlarından BAĞIMSIZ bir geçişte toplanıyor (`FindPlaneChordOnPolygon`)
   — sınıflandırma geçişinin kısa-devresine bağlı KALMADAN (ilk yazımda bu ayrım yapılmamıştı,
   köşe-çentiği testinde "kesim kirişleri kapanmıyor" hatasına yol açtı — düzeltildi).
3. Bu tam kesit poligonu, DİĞER TÜM aday düzlemlerin "içeri" (insideB) yarı-uzaylarına göre
   3D yarı-uzay kırpılıyor (`ClipPolygonByHalfSpace`, coplanar izdüşüme gerek kalmadan doğrudan
   3D'de Sutherland-Hodgman) — kırpılmış poligondan TAMAMEN YENİ (fresh) Vertex/TopologyEdge'lerden
   oluşan bağımsız bir kapak Face'i inşa ediliyor, her kenarın SADECE TEK tarafı dolduruluyor.
4. **YENİ genel yapı taşı — `Boolean/OpenEdgeStitcher.cs`:** `VertexWelder.Weld` sonrası, TÜM
   "açık" (tek tarafı dolu) kenarları (StartVertex,EndVertex) çiftine göre gruplayıp eşleşen
   ikizleri birleştiriyor — roadmap'in aylardır aradığı "cross-piece edge stitching"in GENEL,
   parça-bağımsız çözümü (hangi D_i/D_j çiftinin nerede kesiştiğini AYRICA izlemeye gerek
   KALMADAN). Kaynak kod incelemesiyle doğrulandı: bu codebase'te `TopologyEdge.LeftFace`/
   `RightFace` alanları SADECE `Solid.IsValid()`'in manifold-null kontrolünde ve Face-bağlantı
   BFS'inde kullanılıyor — gerçek geometrik traversal HER ZAMAN her Face'in kendi
   `Loop.GetOrderedVertices()`'i ile bağımsız kuruluyor (Next/Prev işaretçileri zaten tam
   bakımlı değil, `EdgeSplitter`'ın kendi notunda da kabul edildiği gibi) — bu yüzden dikiş
   "yön uyumu" kontrolüne gerek KALMADAN, sadece boş slotu doldurarak yapılabiliyor (başta
   düşünülenden ÇOK daha basit çıktı).

**Bulunan ve düzeltilen 2 gerçek implementasyon hatası (testlerle yakalandı):**
1. Kapak normali TERS işaretliydi (`planes[i].Normal` — B'nin KENDİ dışa-dönük normali —
   doğrudan kullanılmıştı; ama kapak A∖B'nin sınırı olduğundan `-normal` olmalıydı — through-slot
   senaryosunun hacim testi `4e9` yerine `6.67e9` çıkararak bunu yakaladı).
2. Temizlik geçişi (kırpma sonrası artık `a.Faces`'te olmayan Face'lere işaret eden kenar
   referanslarını serbest bırakma) SADECE `FaceSplitter`'ın ürettiği YENİ kiriş kenarlarını
   tarıyordu — `EdgeSplitter`'ın bir kiriş kenarını SONRAKİ bir düzlemle TEKRAR böldüğünde
   ürettiği alt-parça kenarları (`edgeA.LeftFace=edge.LeftFace` ile ESKİ referansı miras alan)
   bu taramada YOKTU. Köşe-çentiği testinde 2 kenar dikilmeden açık kalıyordu (`IsValid()`
   `False` dönüyordu, hacim doğru olmasına rağmen). **Düzeltme:** temizlik artık `a.GetEdges()`
   üzerinden (yani `a.Faces`'ten ULAŞILABİLEN HER kenardan) geçiyor, sadece izlenen kirişlerden
   değil.

**Test sonucu:** `dotnet test` — **358/358 BAŞARILI** (önceki 356 + net +2), 0 başarısız,
HİÇBİR mevcut test bozulmadı. Köşe-çentiği senaryosu artık `NotSupportedException`/
`InvalidOperationException` fırlatmıyor — `IsValid()`=`true` VE hacim analitik olarak doğru
(`A_hacim − kesişim_hacmi = sonuç_hacmi`, `precision:3`). Ek olarak DAHA GENEL bir senaryo da
eklenip doğrulandı: B, A'nın GERÇEK bir 3D köşesini (X, Y VE Z eksenlerinin ÜÇÜNÜ birden)
örtüyor (3 aday düzlem, her kapağın DİĞER İKİ düzlemle çift-kırpılması gerekiyor) — bu da
`IsValid()`=`true` ve doğru hacimle çalışıyor (`Subtract_TrueCornerNotch_ThreePlanes_...`).

**Değiştirilen/eklenen dosyalar:** `Boolean/GeneralSolidSubtractor.cs` (çok-düzlem yolu TAMAMEN
yeniden yazıldı — tek-düzlem `SolidSubtractor`'a delegasyon yolu DOKUNULMADI, davranışı birebir
aynı), `Boolean/OpenEdgeStitcher.cs` (yeni, genel/tekrar-kullanılabilir yapı taşı — sadece
SUBTRACT değil, ileride UNION/INTERSECT montajında da kullanılabilir). `PlaneCutter.cs`,
`SolidSubtractor.cs`, `VertexWelder.cs`, `FaceRegionClassifier.cs` DOKUNULMADI (additive —
`FaceRegionClassifier` artık `GeneralSolidSubtractor` tarafından kullanılmıyor ama kendi
testleriyle birlikte codebase'te kalıyor, başka bir bağlamda faydalı olabilir).

**Kapsam dışı (bilinçli, hâlâ geçerli):** B içbükeyse veya A'nın bir Face'i bir aday düzlemi
2'den fazla kenarında kesiyorsa (dışbükey olmayan kesişim) `NotSupportedException`; B tamamen
A'nın dışında/gömülü (cavity) ise `NotSupportedException`; 3+ parçanın TAM AYNI kenarda
buluştuğu (T-birleşim) dejenere durumda `OpenEdgeStitcher` açık `InvalidOperationException`.

**GÜNCELLEME (2026-08-07) — Faz 5 (UNION/INTERSECT) ele alındı: INTERSECT TAMAMLANDI (kod
yazıldı, testlerle doğrulandı), UNION için YENİ bir yapısal engel bulunup BİLİNÇLİ OLARAK
ERTELENDİ (kod değişikliği yapılmadan, sadece araştırma/analiz):**

Roadmap'in Faz 5 notu ("Aynı Faz 1-3 altyapısı üzerine, farklı birleştirme kuralı") test edildi.
Önce kaynak kod (`GeneralSolidSubtractor.cs`'in 2026-08-06 subdivide→classify→reconstruct
yeniden yazımı, `SolidSubtractor`, `PlaneCutter`, `VertexWelder`, `OpenEdgeStitcher`,
`SolidClassifier`) satır satır incelendi, sonra bir web araştırmasıyla (Requicha & Voelcker
1985'in kendisi) genel B-Rep boundary-merging'in gerçekten "boundary evaluation AND merging"
olarak ikiye ayrıldığı (yani sınır BİRLEŞTİRME'nin kesişim/kırpmadan AYRI, kendi başına bir
algoritma sınıfı olduğu) doğrulandı — bu, aşağıdaki elle türetilen bulguyla tutarlı.

**INTERSECT(A,B) — GÜVENLE tamamlandı, `GeneralSolidSubtractor`'a DOKUNMADAN:** Matematiksel
inceleme, `GeneralSolidSubtractor.SubtractMultiPlane`'in subdivide adımının (`SplitFaceAgainstPlanes`)
aslında SAF bir "A'yı B'nin (dışbükey) yarı-uzaylarına göre kırp" (3D Sutherland-Hodgman)
operasyonu olduğunu gösterdi — SUBTRACT bu kırpmanın "outsideB" dalını tutar (+ kapak, normali
B'nin normalinin TERSİ), INTERSECT ise AYNI kırpmanın "insideB" dalını (SUBTRACT'in şu ana kadar
ATTIĞI `discardedFragments`) tutmalı (+ AYNI kapak, ama normali B'NİN KENDİ normaliyle, TERS
ÇEVRİLMEDEN). Köşe-çentiği ve through-slot senaryoları ELLE (köşe koordinatları takip edilerek)
doğrulandı: her iki senaryoda da bu formül eksiksiz, boşluksuz bir katı üretiyor — çünkü
INTERSECT, TEK bir Solid'in (A'nın) B'ye göre dışbükey-kırpılmasından ibaret, B'nin kendi
Face'lerini AYRICA bölüp iki bağımsız decomposition'ı dikişlemeye gerek YOK.
- ✅ `Boolean/GeneralSolidIntersector.cs` (yeni) — `Intersect(Solid a, Solid b)`. Tek-düzlem
  durumu `PlaneCutter.CutWithPlane(a, point, -normal)`'e devrediyor (B'nin içi, B'nin outward
  normalinin TERSİ yönünde). Çok-düzlem durumu, `GeneralSolidSubtractor`'ın ŞİMDİ `internal`
  yapılan (davranışı DEĞİŞMEYEN, saf erişilebilirlik değişikliği) yardımcı metodlarını
  (`SplitFaceAgainstPlanes`, `FindPlaneChordOnPolygon`, `ChainVertexPairsIntoLoop`,
  `ClipPolygonByHalfSpace`, `BuildFreshOpenCapFace`, `PlaneIntersectsSolidBoundary`) YENİDEN
  KULLANIYOR — 250 satırlık test edilmiş bir algoritmayı kopyalamak yerine.
  `GeneralSolidSubtractor.Subtract`'in KENDİSİ (public API, davranış) DOKUNULMADI.
- Yeni testler: `GeneralSolidIntersectorTests.cs` (7 test) — tek-düzlem, B tamamen dışarıda
  (throw), köşe-çentiği (hacim + nokta-içi/dışı), gerçek 3D köşe (3 düzlem), through-slot
  (hacim + nokta-içi/dışı). Hacimler, `GeneralSolidSubtractorTests`'in "kesişim_hacmi"
  terimleriyle ÇAPRAZ tutarlı (aynı A,B girdileri, aynı beklenen sayılar).
- **Test sonucu: `dotnet test` — 365/365 BAŞARILI** (önceki 358 + 7 yeni), 0 başarısız, HİÇBİR
  mevcut test bozulmadı.

**UNION(A,B) — YENİ bir yapısal engel bulundu, KOD YAZILMADAN ertelendi:** UNION, INTERSECT'in
aksine SUBTRACT'in altyapısı üzerine GÜVENLE oturmuyor. Sebep (elle, köşe-çentiği senaryosuyla
somut olarak doğrulandı — A=[0,2000]³, B=[1500,3000]²×[0,2000]):
- UNION(A,B)'nin sınırı = (A'nın B-DIŞI parçaları) ∪ (B'nin A-DIŞI parçaları) — bu SUBTRACT/
  INTERSECT'in aksine İKİ BAĞIMSIZ decomposition gerektiriyor: A, B'nin düzlemleriyle VE AYRICA
  B, A'nın düzlemleriyle (`SplitFaceAgainstPlanes`'in simetriği, B üzerinde de çağrılması).
- Bu iki decomposition'ın "kesilmiş" açık kenar döngüleri GENEL OLARAK AYNI 3D eğri
  ÜZERİNDE DEĞİL. Somut örnek (köşe-çentiği): A'nın açık kenar döngüsü B'nin düzlemlerinde
  (X=1500/Y=1500, A'nın İÇİNDEN geçen bir kesim) oluşuyor — köşe kutusunun [1500,2000]²
  KENDİSİNİN İKİ yüzünü (X=1500 ve Y=1500 yüzlerini) çevreliyor. B'nin açık kenar döngüsü ise
  A'nın düzlemlerinde (X=2000/Y=2000, B'nin İÇİNDEN geçen bir kesim) oluşuyor — AYNI köşe
  kutusunun DİĞER İKİ yüzünü (X=2000 ve Y=2000) çevreliyor. Bu iki 6-köşeli döngü sadece 2
  köşede (köşe kutusunun (2000,1500,·) ve (1500,2000,·) dikey kenarlarında) kesişiyor —
  `OpenEdgeStitcher` bu 2 kenarı dikebilir, ama GERİYE kalan 4 kenar (A'nın döngüsünden 2,
  B'nin döngüsünden 2) HİÇBİR eşleşen ikiz bulamadan açık kalıyor — çünkü gerçek boşluk,
  köşe kutusunun geriye kalan İKİ yüzünü (X=1500-X=2000 arası "köprü" yüzeyleri) örten YENİ bir
  yüzeyle doldurulmalı, bu iki bağımsız decomposition'ın kenarlarını basitçe eşleştirerek DEĞİL.
- Bu "köprü yüzü" (bridging face) inşası, `OpenEdgeStitcher`'ın çözdüğü sorundan (TEK bir
  Solid'in kendi içindeki tutarlı kırpma sınırı) YAPISAL OLARAK FARKLI bir problem sınıfı —
  görevin kendisinin de önceden işaret ettiği üçüncü terim ("ikisinin sınırının dışa bakan
  kesişimi") tam olarak bu eksik parça. Bu üçüncü terimin genel inşası (hangi köprü
  yüzeylerinin nerede gerektiği, kaç tane olacağı, nasıl yönlendirileceği) roadmap'in şu ana
  kadar çözdüğü hiçbir yapı taşıyla (VertexWelder, OpenEdgeStitcher, ConvexPolygonClipper2D,
  FaceRegionClassifier) DOĞRUDAN karşılanmıyor — ayrı, odaklanmış bir mühendislik oturumu
  gerektiriyor.

**Karar (Ana Yasa gereği, "aceleyle yanlış kod yerine dürüst tespit" — sessions #34/#35/#55'in
kararlarıyla tutarlı):** UNION için KOD YAZILMADI. `GeneralSolidSubtractor.cs`/
`GeneralSolidIntersector.cs`/mevcut 365 testin HİÇBİRİ bu araştırmadan etkilenmedi.
**Sıradaki oturum için net başlangıç noktası:** `SplitFaceAgainstPlanes`'i B üzerinde de
(A'nın candidate planes'ine göre) çağırıp `outsideFragments`'i (hem A hem B için) birleştirmek
kolay kısım; asıl iş, köşe kutusunun kalan yüzlerini kapatan köprü yüzey(ler)inin genel
inşası — muhtemelen `SolidClassifier`/`FaceRegionClassifier`'ın tam entegrasyonunu (hangi açık
kenarın hangi köprü yüzeyine ait olduğunu sınıflandırmak için) gerektirecek.

**GÜNCELLEME (2026-08-07, devam) — UNION için önerilen "mirror-cap kısayolu" hipotezi ELLE
ÇÜRÜTÜLDÜ, ayrıca BAĞIMSIZ bir ikinci yapısal engel daha bulundu (KOD YAZILMADI, sadece
araştırma/analiz):**

Bir sonraki oturuma önerilen kısayol hipotezi şuydu: "`SUBTRACT(A,B)`'nin ürettiği mirror cap
(A∩B sınırının A tarafından türetilmiş kopyası) ile `SUBTRACT(B,A)`'nın ürettiği mirror cap
(AYNI sınırın B tarafından türetilmiş kopyası) aslında AYNI yüzeyin iki kopyası olmalı — ikisi
de `VertexWelder` ile kaynaştırılıp İKİSİ DE elenebilir, böylece köprü yüzü hiç gerekmez."
Bu hipotez, `GeneralSolidSubtractorTests.cs`'teki köşe-çentiği senaryosunun (A=[0,2000]³,
B=[1500,3000]×[1500,3000]×[0,2000]) GERÇEK köşe koordinatlarıyla elle sınandı:

- **`SUBTRACT(A,B)`'nin kapakları:** aday düzlemler B'nin X=1500 ve Y=1500 yüzleri. Kapaklar
  TAM OLARAK bu düzlemlerde oluşuyor — X=1500 kapağı `Y∈[1500,2000], Z∈[0,2000]` dikdörtgeni,
  Y=1500 kapağı `X∈[1500,2000], Z∈[0,2000]` dikdörtgeni (her biri A'nın TAM kesitinin diğer
  aday düzlemin insideB yarı-uzayına göre kırpılmasıyla, `ClipPolygonByHalfSpace`).
- **`SUBTRACT(B,A)`'nın kapakları:** aday düzlemler (simetrik olarak) A'nın X=2000 ve Y=2000
  yüzleri. Kapaklar X=2000 (`Y∈[1500,2000], Z∈[0,2000]`) ve Y=2000 (`X∈[1500,2000], Z∈[0,2000]`)
  konumunda oluşuyor.
- **Sonuç: hipotez YANLIŞ.** `SUBTRACT(A,B)`'nin kapakları X=1500/Y=1500'de, `SUBTRACT(B,A)`'nın
  kapakları X=2000/Y=2000'de — **500 birim ARALARINDA**, aynı yüzeyin iki kopyası DEĞİL, dört
  ayrı, birbirine PARALEL/UZAK dikdörtgen. `VertexWelder`'ın kaynaştıracağı ortak bir köşe/kenar
  bile yok (en yakın ortak nokta sadece köşe kutusunun `(2000,1500,·)` ve `(1500,2000,·)`
  köşegen kenarları — roadmap'in 2026-08-07 (ilk) girdisinin zaten belgelediği "sadece 2 köşede
  kesişen iki 6-köşeli döngü" bulgusuyla BİREBİR tutarlı). Görev tanımının önerdiği kısayol,
  önceki araştırmanın bulgusunu DOĞRULUYOR, ÇÜRÜTMÜYOR — köprü yüzü ihtiyacı GERÇEK.

**İKİNCİ, önceki turlarda hiç belgelenmemiş bir engel bulundu — üst/alt kapaklar için de sorun
var:** Köşe-çentiği testinde A ve B AYNI Z aralığını kullanıyor (`ExtrudeBox(..., ZAxis, ...,
2000)` her ikisinde de) — yani A'nın üst/alt yüzleri (Z=2000/Z=0) ile B'nin üst/alt yüzleri
TAM ÇAKIŞIK (coplanar) düzlemlerde. `PlaneIntersectsSolidBoundary` bu yüzden Z=2000/Z=0'ı hiçbir
zaman aday düzlem olarak SEÇMİYOR (tüm A köşeleri ya `dist=0` ya `dist<0`, `hasPos` hiç `true`
olmuyor) — bu ZATEN çok-düzlem SUBTRACT/INTERSECT'in doğru çalışmasının bir koşulu. Ama UNION
için bu, farklı bir sorun yaratıyor: UNION'ın üst yüzü, A'nın kare izdüşümü İLE B'nin kare
izdüşümünün TAM birleşimi (8 köşeli "merdiven" oktogon: `(0,0),(2000,0),(2000,1500),(3000,1500),
(3000,3000),(1500,3000),(1500,2000),(0,2000)` — elle türetildi, A'nın [0,2000]² karesi ile B'nin
[1500,3000]² karesinin geometrik birleşimi) OLMALI. Ama `SUBTRACT(A,B)`'nin ürettiği A'nın
üst-yüz parçası SADECE A'nın kendi L-şekli (A'nın karesi EKSİ A∩B köşesi — [1500,2000]²'lik köşe
"delinmiş"), `SUBTRACT(B,A)`'nın ürettiği B'nin üst-yüz parçası da SADECE B'nin kendi L-şekli
(B'nin karesi EKSİ AYNI köşe). **Bu iki L-şeklinin 2D birleşimi ((A_L)∪(B_L)) = (A∪B) EKSİ köşe
— yani ORTADA GERÇEKTE OLMAMASI GEREKEN bir DELİK/ÇENTİK bırakır** (köşe bölgesi A∪B'nin
GERÇEKTEN İÇİNDE/dolu olması gerekirken, iki L-şekli ayrı ayrı oradan kaçınıyor). Doğru üst yüz
(tam oktogon) SADECE A_top ∪ B_top'un GERÇEK bir 2D poligon BİRLEŞİMİ (union, kesişim değil) ile
elde edilebilir — ama bu iki poligon dışbükey OLSA BİLE birleşimleri genel olarak dışbükey
DEĞİLDİR (bu örnekte oktogon açıkça içbükey köşeler içeriyor). `ConvexPolygonClipper2D` kendi
başlığında BUNU açıkça kapsam dışı bırakıyor ("UNION/DIFFERENCE bilinçli olarak kapsam dışı —
SUBTRACT'in coplanar-payı kararı sadece INTERSECT'e ihtiyaç duyuyor") — kod tabanında HİÇBİR
poligon-BİRLEŞİM (union) primitifi yok, sadece kesişim (`ConvexPolygonClipper2D.Intersect`,
`ClipPolygonByHalfSpace`, ikisi de yarı-uzay/dışbükey KIRPMA, birleşim değil). **Not:** bu ikinci
engel köşe-çentiği senaryosuna ÖZGÜ bir koşula (A/B'nin Z aralıklarının TESADÜFEN aynı olması)
bağlı görünüyor — 3-düzlemli "gerçek köşe" senaryosunda (`Subtract_TrueCornerNotch_ThreePlanes`,
B'nin Z aralığı A'dan FARKLI) her üç eksen de aday düzlem olduğundan bu ÖZEL coplanarlık sorunu
oluşmuyor, ama o senaryoda da birinci engel (köprü yüzü ihtiyacı, üç düzlemde) AYNEN geçerli —
yani ikinci engel EK bir komplikasyon, birincinin YERİNE geçmiyor.

**Kısa web araştırması (Ana Yasa gereği) — gerçek kernel'ler bunu nasıl çözüyor:** OpenCASCADE
`BOPAlgo_Builder`/`BOPAlgo_BOP`, boolean operasyondan ÖNCE ayrı bir **"Section" aşaması**
(`BOPAlgo_Section`/genel "General Fuse" algoritması) çalıştırır: TÜM girdi katılarının
yüzey-yüzey kesişim EĞRİLERİNİ (p-curve'ler dahil) TEK SEFERDE, PAYLAŞILAN bir veri yapısında
hesaplar — yani A'nın kestiği kenar ile B'nin kestiği kenar A VE B ARASINDA PAYLAŞILAN AYNI
kenar nesnesidir (iki BAĞIMSIZ, sonradan dikilen kopya DEĞİL). SUBTRACT/UNION/INTERSECT hepsi bu
TEK PAYLAŞILAN decomposition üzerinden, sadece hangi parçaların/hangi yönde tutulacağına dair bir
sınıflandırma FARKI ile üretilir (`BOPAlgo_BOP`, `BOPAlgo_Builder`'ı miras alır, "aynı General
Fuse altyapısı üzerine farklı birleştirme kuralı" — roadmap'in Faz 5 notunun umduğu ŞEY tam
olarak bu, ama gerçek kernel'de bu, PAYLAŞILAN kesişim hesaplamasıyla baştan doğru inşa ediliyor,
bizim kodumuzdaki gibi SONRADAN iki bağımsız sonucu dikmeye ÇALIŞMIYOR). CGAL Nef polyhedra ise
tamamen FARKLI bir stratejiyle bu sorunu YAPISAL OLARAK ortadan kaldırıyor: yarı-uzayların
kesişim/tümleyen kombinasyonlarına dayalı bir temsil kullanıyor (boolean operasyonlara göre
KAPALI/closed by construction) — "iki ayrı sınır decomposition'ını dikme" adımı hiç YOK.

**Bu bulgunun kod tabanı için anlamı:** `GeneralSolidSubtractor`/`GeneralSolidIntersector`'ın
şu anki mimarisi (A'yı B'nin düzlemlerine göre BAĞIMSIZ kes / B'yi A'nın düzlemlerine göre
BAĞIMSIZ kes, sonra sonuçları dikmeye çalış) UNION için YAPISAL OLARAK YANLIŞ temel — doğru çözüm
bir "dikiş numarası" (stitching trick) DEĞİL, A VE B'nin kesişim kenarlarını/eğrilerini EN
BAŞTAN PAYLAŞILAN/ORTAK bir hesaplama olarak üretip HER İKİ solid'i de bu ORTAK kesişim kümesine
göre bölmek (ki bu, `SplitFaceAgainstPlanes`'in ŞU ANKİ tek-yönlü "A'yı B'nin düzlemlerine göre
böl" tasarımının temelden değiştirilmesini gerektirir) — bu, `VertexWelder`/`OpenEdgeStitcher`/
chord-edge fix gibi önceki oturumların çözdüğü sorunlardan (TEK bir solid'in kendi İÇİNDEKİ
tutarlı decomposition'ı) NİTELİKSEL OLARAK daha büyük bir mimari değişiklik — muhtemelen
`GeneralSolidSubtractor`/`GeneralSolidIntersector`'ın kendisinin YENİDEN yazılmasını gerektirir
(additive bir ek DEĞİL). Ayrıca ikinci engel (coplanar üst/alt yüzler için gerçek 2D poligon
BİRLEŞİMİ) `ConvexPolygonClipper2D`'nin bilinçli kapsam dışı bıraktığı bir primitifi (non-convex
polygon union) gerektiriyor — bu da AYRI bir mühendislik çabası (Vatti/Martinez-Rueda sweep-line
veya en azından "iki dışbükey poligonun genel birleşimi" için özel bir algoritma).

**Karar (Ana Yasa gereği, dördüncü kez aynı gerekçeyle):** UNION için KOD YAZILMADI — görev
tanımının önerdiği kısayol hipotezi elle çürütüldü (yanlış çıktı), bulunan engel önceki
oturumun belgelediğinden DAHA BÜYÜK (iki ayrı, birbirinden bağımsız yapısal sorun: köprü yüzü +
non-convex poligon birleşimi). `GeneralSolidSubtractor.cs`/`GeneralSolidIntersector.cs`/mevcut
365 testin HİÇBİRİNE dokunulmadı, dokunulmadığı için hiçbiri etkilenmedi (kod değişikliği yok).
**Sıradaki oturum için net başlangıç noktası (güncellenmiş, iki ayrı alt-problem olarak):**
(1) A/B arasında PAYLAŞILAN bir kesişim-kenarı/eğrisi temsili tasarlamak (OpenCASCADE'in
"Section" aşamasının bu koda uyarlanmış, küçültülmüş bir versiyonu — muhtemelen
`SplitFaceAgainstPlanes`'in iki-yönlü/simetrik bir varyantı, A'nın kestiği her kirişin B'nin
kestiği KARŞILIK GELEN kirişle AYNI Vertex nesnesini paylaşmasını garanti eden bir mekanizma);
(2) coplanar üst/alt (veya genel olarak iki solid'in aynı düzlemde çakışan yüzleri) için gerçek
bir dışbükey-poligon BİRLEŞİMİ primitifi (`ConvexPolygonClipper2D.Intersect`'in yanına, AYNI
dosyada veya yeni bir dosyada, additive). Her iki alt-problem de kendi başına, ayrı, odaklanmış
birer oturum gerektirebilir.

**GÜNCELLEME (2026-08-14) — "Section-first" mimarisi somutlaştırıldı (`FaceIntersection` +
segment-zincirleme), YENİ bir yapısal gereksinim (polyline-chord `FaceSplitter`) net olarak
tespit edildi — KOD YAZILMADAN ertelendi (beşinci kez, Ana Yasa gereği):**

Bu oturum, önceki (2026-08-07) girdinin bıraktığı "PAYLAŞILAN bir kesişim-eğrisi temsili baştan
kurulmalı" fikrini SOMUTLAŞTIRMAYA çalıştı. Tüm ilgili kaynak dosyalar (bu dosyanın önceki
güncellemelerinde adı geçen HEPSİ + `FaceIntersection.cs`/`PlaneIntersection.cs`) yeniden okundu,
sonra köşe-çentiği VE 3-düzlemli "gerçek köşe" (`Subtract_TrueCornerNotch_ThreePlanes` testinin
AYNI girdisi: A=[0,2000]³, B=[1500,3000]³) senaryolarıyla ELLE koordinat takibi yapıldı.

**Bulunan fırsat:** `FaceIntersection.Intersect(faceA, faceB)` (Faz 1'den beri kod tabanında var,
şimdiye kadar SADECE Faz 2'nin planlı ama hiç tamamlanmamış genel yüz-bölme akışı için
düşünülmüştü) iki Face'in kesişimini PLANE-vs-TÜM-SOLID (`SplitFaceAgainstPlanes`'in kullandığı
yaklaşım) değil, GERÇEK POLİGON SINIRLARINA kırpılmış segment olarak veriyor — bu segmentler A
tarafında da B tarafında da AYNI 3D noktalarda oluşuyor (iki düzlemin kesişimi, düzlemler A/B'nin
kendi yüzeyleri) — yani madde 41'in çürüttüğü "SUBTRACT(A,B) kapağı X=1500'de, SUBTRACT(B,A)
kapağı X=2000'de, 500 birim arada" sorunu bu yaklaşımda YAPISAL OLARAK oluşmuyor (segment aynı
İKİ düzlemin kesişimi olduğu için tanım gereği tek bir konumda).

**Elle doğrulanan YENİ engel (3-düzlem senaryosu, A=[0,2000]³, B=[1500,3000]³):**
- A'nın X=2000 yüzü ∩ B'nin Y=1500 yüzü → segment (2000,1500,z), z∈[1500,2000] (B'nin Z sınırı
  kısıtlıyor — A'nın Z sınırı [0,2000] daha geniş).
- A'nın X=2000 yüzü ∩ B'nin Z=1500 yüzü → segment (2000,y,1500), y∈[1500,2000].
- Bu iki segment TAM (2000,1500,1500) noktasında birleşiyor. Zincirlenince
  (2000,1500,2000) → (2000,1500,1500) → (2000,2000,1500) polyline'ı oluşuyor — bu polyline'ın
  İKİ UCU (Z=2000 ve Y=2000) A'nın KENDİ Face sınırında, ama ORTA noktası ((2000,1500,1500))
  A'nın Face'inin İÇİNDE (Y=1500, Z=1500 ikisi de A'nın X=2000 yüzünün [0,2000]×[0,2000]
  aralığının kesin İÇİNDE, sınırında değil).
- **Sonuç:** gerçek kesişim "kirişi" genel olarak TEK düz segment DEĞİL, birden fazla yüz-çifti
  segmentinin zincirlenmesiyle oluşan bir POLYLINE, ve bu polyline'ın ARA noktaları Face'in
  KENDİ sınırında OLMUYOR. Mevcut `FaceSplitter.SplitAtChord`, `v1`/`v2`'nin İKİSİNİN de
  `face.GetOuterLoop().GetOrderedVertices()` içinde (yani Face'in kendi sınırında) bulunmasını
  ZORUNLU kılıyor (`FindIndex` ile arıyor, bulamazsa `ArgumentException`) — bu polyline'ı
  OLDUĞU GİBİ tek bir `SplitAtChord` çağrısıyla kullanmak mümkün DEĞİL.

**Somut, net gereksinim (bir sonraki oturum için, önceki 4 girdinin hiçbirinde bu netlikte
yoktu):**
1. `FaceSplitter`'a (veya yanına, additive) polyline-chord desteği: `SplitAtPolylineChord(Solid,
   Face, List<Vertex> orderedChordVerts)` — ilk/son eleman Face'in kendi sınırında olmalı (aksi
   halde açık hata — dejenere/kapsam dışı), ARA elemanlar Face'in İÇİNDE yeni `Vertex`'ler olarak
   eklenip ardışık `TopologyEdge`'lerle birbirine bağlanmalı, iki alt-Face'in Loop'ları bu
   YENİ kenar zincirinin İKİ yönünü paylaşmalı (`SplitAtChord`'un tek-kenar deseninin doğal
   genellemesi).
2. Bunun üzerine, HER A-Face'i (B'nin TÜM Face'leriyle `FaceIntersection.Intersect` çağrılıp
   sonuçlar aynı Face üzerinde uç-noktalarına göre zincirlenerek) VE simetrik olarak HER B-Face'i
   bu polyline'larla bölen, `SolidClassifier.IsPointInside` ile fragman sınıflandırması yapan
   YENİ bir segment-tabanlı subdivide algoritması (`SplitFaceAgainstPlanes`'in plane-tabanlı
   değil, segment-tabanlı kardeşi).
3. Beklenen kazanım: adım 1-2 doğru çalışırsa, A'nın "B-dışı" fragmanları + B'nin "A-dışı"
   fragmanları `VertexWelder` + `OpenEdgeStitcher` ile DOĞRUDAN dikilebilir OLABİLİR (ayrı bir
   kapak/köprü-yüzü inşasına GEREK KALMADAN) — çünkü kesim zaten PAYLAŞILAN noktalarda yapıldı.
   Bu, henüz test edilmemiş bir HİPOTEZ (adım 1-2 olmadan doğrulanamaz), ama madde 40/41'in
   "ayrı bir köprü-yüzü inşası gerekiyor" karamsarlığından daha iyimser bir olası sonuç.

**Coplanar üst/alt yüzler için düzeltilmiş (daha dar) değerlendirme:** `FaceIntersection`,
paralel/coplanar düzlem çiftlerinde boş liste döner — köşe-çentiği senaryosunun ÇAKIŞIK üst/alt
yüzleri (yukarıdaki 1-2 numaralı adımlarla) hiç segment üretmeyecek, bu yüzden doğru birleşimleri
(8 köşeli oktogon) HÂLÂ ayrı bir 2D poligon-BİRLEŞİM primitifi gerektiriyor — madde 41'in
tespiti hâlâ geçerli. AMA kapsam düşünüldüğünden DAHA DAR: girdi her zaman İKİ DIŞBÜKEY poligon
(A/B'nin tüm yüzleri dışbükey) — iki dışbükey kümenin birleşimi (içbükey olsa bile) HER ZAMAN
TEK bir kapalı döngüdür (basit-bağlantılı, delikli/çok-parçalı olamaz), sınırı SADECE "A'nın
B-dışı kalan kenar parçaları + B'nin A-dışı kalan kenar parçaları"ndan oluşur — yani TAM
Vatti/Martinez-Rueda genel sweep-line'ı DEĞİL, çok daha dar bir "Weiler-Atherton, 2-dışbükey-
girdi özel durumu" (`ConvexPolygonClipper2D.Union`, additive, tahmini 100-150 satır) yeterli
olurdu.

**Karar (Ana Yasa gereği, beşinci kez aynı gerekçeyle):** UNION için KOD YAZILMADI. Bu oturumda
bulunan mimari (polyline-chord `FaceSplitter` + segment-tabanlı subdivide + dar-kapsamlı
convex-convex 2D union), önceki oturumlardan DAHA SOMUT ve daha net bir başlangıç noktası, ama
EN AZ 3 ayrı yeni yapı taşının sıfırdan yazılıp kapsamlı test edilmesini gerektiriyor —
tek oturumda "10/10 ilk seferde" kalite bariyerini güvenle geçecek kadar küçük değil.
`GeneralSolidSubtractor.cs`/`GeneralSolidIntersector.cs`/mevcut 474 testin HİÇBİRİNE
dokunulmadı (sadece kaynak inceleme + kâğıt üzerinde koordinat doğrulaması, kod değişikliği yok).

## Kademeli Plan — Tam Topolojik B-Rep Boolean

**Yeni modül:** `Afney.Cad.Geometry.Topology.Boolean` (harici kütüphane yok — projenin mevcut
"kendi yaz" felsefesiyle tutarlı, `BRepBuilder`/`PolygonTriangulator` gibi pure C#).

### Faz 1 — Düzlem-düzlem ve yüz-yüz kesişim
- `PlaneIntersection.cs`: iki düzlemin (Face.Normal + bir nokta) kesişim DOĞRUSUNU hesapla.
- `FaceIntersection.cs`: bu doğruyu her iki Face'in poligon sınırına kırp (clip) — sonuç,
  her Face üzerinde 0, 1 veya birden fazla kesişim SEGMENTİ.
- **İlk test senaryosu:** iki kutunun (BRepBuilder.ExtrudeBox) kesişen iki yüzü — segment
  elle hesaplanabilir, testle doğrulanır.

### Faz 2 — Kesişim boyunca yüz bölme (face re-topology)
- Bir Face'in sınır Loop'una, kesişim segmentinin uç noktaları YENİ Vertex olarak eklenir
  (mevcut Vertex/TopologyEdge nesneleri paylaşılarak — bu oturumdaki `BRepBuilder`'ın
  "aynı nesneyi referansla" deseniyle tutarlı).
  Face, kesişim segmenti boyunca İKİ yeni Face'e bölünür (her biri geçerli, kapalı bir Loop).
- Bu adım en riskli/hataya açık kısım — dejenere durumlar (segment bir Face köşesinden
  geçiyor, segment bir kenarla çakışık) ayrı ele alınmalı.

### Faz 3 — İç/dış sınıflandırma (point-in-solid)
- Her yeni alt-Face için, merkez noktasından diğer Solid'e bir ışın gönder, kaç TopologyEdge/
  Face kestiğini say (tek sayı = içeride). `Afney.Cad.Geometry.Algorithms`'a
  `RayCastSolidIntersection` eklenir (mevcut `GeomUtils.RayCast`'in 3D/Solid genellemesi).

### Faz 4 — Sonuç Solid montajı (SUBTRACT önce)
- SUBTRACT(A,B) = A'nın B-dışı alt-Face'leri + B'nin A-içi alt-Face'leri (normalleri ters
  çevrilmiş) — hepsi TEK bir yeni `Solid` nesnesinde toplanır, winged-edge Left/Right/Next/
  Prev bağlantıları yeniden kurulur (bkz. `BRepBuilder.AttachFace` deseni).
- **Doğrulama kriteri:** `Solid.IsValid()` (Euler) + analitik hacim (A_hacim − kesişim_hacmi
  = sonuç_hacmi, basit kutu-kutu senaryosunda elle hesaplanabilir).

### Faz 5 — UNION, INTERSECT
Aynı Faz 1-3 altyapısı üzerine, farklı birleştirme kuralı.

### Bilinen Riskler (dürüstçe, baştan)
- Dejenere durumlar (teğet temas, tam çakışma, coplanar yüz üst üste binmesi, vertex-vertex
  çakışması) — genel CSG kernel'lerinin çoğu kod hacmini bu durumlar oluşturur. İlk
  implementasyon SADECE "temiz" durumları (transversal kesişim, dejenere yok) hedefleyecek;
  dejenere durumlar tespit edilip AÇIK HATA fırlatılacak (sessiz yanlış sonuç yerine).
- Performans: O(n_faces_A × n_faces_B) naif kesişim testi — büyük Solid'lerde
  `Afney.Cad.SpatialIndex` R-Tree ile broad-phase filtreleme gerekebilir (ileri faz).

## Doğrulama Kriteri
- Bilinen basit durumlar için (ör. bir kutudan başka bir kutuyu çıkarma) sonucun hacminin
  analitik olarak doğru olduğu test edilmeli (A_hacim - kesişim_hacmi = sonuç_hacmi).
- Dejenere durumlar (teğet temas, tam çakışma, kesişmeyen mesh'ler) için ayrı testler.
- Performans: büyük mesh'lerde (ör. tam bir kat) makul sürede tamamlanmalı — R-Tree ile
  broad-phase filtreleme olmadan Faz 1 bile pratik olmayabilir, bu yüzden ilk implementasyonda
  broad-phase dahil edilmeli.

## Güncelleme — 2026-08-14/15 (Session #64-66) — UNION için "Section-First" Yapı Taşları

Madde 40-41'in bıraktığı yer: UNION'ın "A'yı B'ye bağımsız kes / B'yi A'ya bağımsız kes, sonra dik"
mimarisinin temelden yanlış olduğu, paylaşılan bir kesişim-eğrisi temsili (OpenCASCADE'in
"Section-first" deseni) gerektiği biliniyordu ama hiç somutlaştırılmamıştı.

**Yapı taşı 1 — `FaceSplitter.SplitAtPolylineChord` (TAMAMLANDI, commit `f2fc124`):** Mevcut
`SplitAtChord` (tek düz chord, iki ucu da Face'in kendi sınırında) genelleştirildi — çok-segmentli
polyline chord desteği eklendi, ARA noktalar Face'in İÇİNDE olabiliyor. 2-noktalı polyline
`SplitAtChord` ile birebir aynı sonucu üretiyor (regresyon riski yok). 3-düzlemli "gerçek köşe"
senaryosunda elle hesaplanmış alan/Euler/hacim doğrulamalarıyla test edildi.

**Yapı taşı 2 — `SegmentBasedSubdivider` (TAMAMLANDI, commit `2724880`):** `FaceIntersection.
Intersect`'i (plane değil, GERÇEK poligon-sınırlı kesişim) kullanarak A'nın her Face'ini B'nin
GERÇEK Face'leriyle kesiştirip polyline'a zincirleyip bölen, `SolidClassifier` ile sınıflandıran
yapı taşı. 3-düzlemli gerçek köşe senaryosu İLK DENEMEDE uçtan uca çalıştı.

**YENİ keşif (canlı testte bulundu, önceki hiçbir oturumda belgelenmemişti):** `FaceIntersection.
Intersect`, coplanar (aynı düzlem) Face çiftlerinde HER ZAMAN boş segment listesi döndürür
varsayımı YANLIŞ. Gerçek davranış: coplanar TAM ÇAKIŞIK çiftlerde boş dönüyor, ama coplanar
KISMEN ÇAKIŞAN çiftlerde (sınırları birbirini kesen, ör. köşe-çentiği senaryosunun üst/alt
yüzleri) TUTARSIZ — bazı yüz-çifti kombinasyonlarında sınır-kesişim segmenti üretiyor, bazılarında
üretmiyor (yön/kenar sırasına bağlı görünüyor, kök nedeni incelenmedi — `FaceIntersection`'ın
kendisinde, bu yapı taşının kapsamı dışında). Bu, "segments.Count==0 → güvenle bölünmeden
sınıflandır" varsayımını kırıyordu — düzeltme olarak `SegmentBasedSubdivider.
HasAmbiguousCoplanarOverlap` eklendi: `CoplanarFaceDetector.AreCoplanar` + 3D AABB izdüşüm
örtüşme testiyle bu belirsiz durumu tespit edip sessizce yanlış sınıflandırmak YERİNE açık
`NotSupportedException` fırlatıyor.

**Sıradaki adımlar (net, somut):**
1. **Convex-convex 2D union primitifi** — coplanar kısmen-örtüşen Face çiftlerini (yukarıdaki
   `NotSupportedException` durumu) gerçekten çözmek için gerekiyor. Girdi HER ZAMAN iki dışbükey
   poligon (mevcut `ConvexPolygonClipper2D`'nin kendi varsayımıyla tutarlı) — bu yüzden genel
   Vatti/Martinez-Rueda YERİNE dar-kapsamlı bir Weiler-Atherton-benzeri "sadece 2 dışbükey girdi"
   union'ı yeterli (tahminen 100-150 satır, `ConvexPolygonClipper2D.Union` olarak eklenebilir).
2. **`FaceIntersection.Intersect`'in coplanar-kısmi-örtüşme tutarsızlığının kök nedeni** — ayrı,
   izole bir araştırma konusu (bu tutarsızlık düzeltilirse `HasAmbiguousCoplanarOverlap` koruması
   daha az durumda devreye girer, ama koruma yine de kalmalı — savunma katmanı).
3. **`GeneralSolidUnion` assembly'si** — `SubdivideAndClassifyOutside(a,b)` (A'nın B-dışı
   fragmanları) + simetrik `SubdivideAndClassifyOutside(b,a)` (B'nin A-dışı fragmanları) sonucunu
   `VertexWelder`/`OpenEdgeStitcher` ile dikip TEK bir Solid'e montajlamak — coplanar olmayan
   (temiz, transversal kesişimli) durumlar için ŞİMDİ mevcut yapı taşlarıyla denenebilir hâlde;
   coplanar durumlar madde 1'deki primitif tamamlanana kadar `NotSupportedException` ile
   korunacak.

## Güncelleme — 2026-08-15 (Session #67) — `ConvexPolygonClipper2D.Union` (3. Yapı Taşı, TAMAMLANDI)

**Yazıldı ve doğrulandı:** `ConvexPolygonClipper2D.Union(polyA, polyB, normal)` — girdi/ön-koşul
`Intersect` ile AYNI (dışbükey, basit, tek döngü). Kenar-tabanlı yaklaşım: her poligonun her
kenarı diğer poligonun TÜM kenarlarıyla kesiştirilip alt-segmentlere bölünür, diğerinin
KESİNLİKLE dışında kalan alt-segmentler tutulur, tutulanlar uç-nokta eşleşmesiyle TEK kapalı
döngüye zincirlenir. Tam-kapsama/özdeşlik için ayrı bir kısa-yol var (genel algoritmanın "iki
taraf da dışlanır → boş sonuç" tuzağından kaçınmak için). Ayrık girdi `InvalidOperationException`
fırlatır (kapsam dışı — birleşimleri tek basit poligon değil).

**Somut test senaryosu (roadmap'in kendi köşe-çentiği örneği) elle doğrulandı ve kilitlendi:**
A=[0,2000]², B=[1500,3000]² → 8 köşeli oktogon, alan 6.000.000 (=4.000.000+2.250.000-250.000).
6 yeni test (`ConvexPolygonClipper2DTests`) — tam kapsama, özdeşlik, kısmi örtüşme, ayrık girdi
(hata), içbükey girdi (hata), basit-döngü/tekrarsız-köşe doğrulaması. `dotnet build`: 0 hata.
`dotnet test`: 498/498 geçti (önceki 491 + bu oturumun 7 yeni testi — regresyon yok).

**`SegmentBasedSubdivider` entegrasyonu — YAPILMADI (dürüst gerekçeyle, Ana Yasa):** Görev
tanımı `HasAmbiguousCoplanarOverlap` durumunda `NotSupportedException` yerine `Union` çağrısıyla
gerçek sınıflandırma yapılmasını öneriyordu ama analiz şu KOORDİNASYON sorununu ortaya çıkardı:

- `SubdivideAndClassifyOutside(a,b)` HER ZAMAN `a` tarafının "B-dışı" fragmanlarını üretir.
  Coplanar kısmen-örtüşen bir A-Face/B-Face çifti bulunduğunda, doğru UNION sınırı bu ikisinin
  `Union`'udur — ama bu union-face'i SADECE BİR TARAF üretmeli (iki taraf da üretirse nihai
  Solid'de ÇİFT/üst-üste-binen yüz oluşur; hiçbiri üretmezse boşluk/delik oluşur).
- Bu fonksiyon (`SubdivideAndClassifyOutside`) TEK YÖNLÜ çalışır (A→B) ve `b` parametresini asla
  mutasyona uğratmaz/tüketmez — simetrik çağrı `SubdivideAndClassifyOutside(b,a)` TAMAMEN AYRI,
  bağımsız bir çağrıdır, aralarında PAYLAŞILAN bir durum (state) yok. Fonksiyonun kendisi "ben mi
  üretmeliyim yoksa diğer taraf mı üretecek" sorusunu YEREL bilgiyle cevaplayamaz.
- Bir tie-break (ör. `Face.Id` karşılaştırması — hangi Face'in Guid'i küçükse o üretsin) İLK
  bakışta çözüm gibi görünüyor, ama doğruluğu HENÜZ TASARLANMAMIŞ `GeneralSolidUnion`
  assembly'sinin `a`/`b`'yi nasıl kopyaladığına/orkestre ettiğine bağlı: eğer çağıran taraf
  `SubdivideAndClassifyOutside(aWorkCopy, b)` ve `SubdivideAndClassifyOutside(bWorkCopy, a)`
  şeklinde BAĞIMSIZ ÇALIŞMA KOPYALARI kullanırsa (fonksiyonun kendi dokümantasyonunun ZORUNLU
  kıldığı desen — `a` yerinde mutasyona uğruyor), kopyalanan Face'lerin Guid'leri ORİJİNAL
  Face'lerinkiyle EŞLEŞMEZ, tie-break tutarsızlaşır (bir çağrı "A kazandı" derken diğeri "A
  kaybetti" diyebilir — sessizce ÇİFT ya da EKSİK yüz üretir).
- Bu, kod satırı sayısıyla ilgili bir risk değil (birleştirme/entegrasyon kendisi ~10-20 satır
  olurdu) — DOĞRULUK GARANTİSİ `GeneralSolidUnion`'ın (madde 3, kapsam dışı) kendi Face-kimlik/
  kopyalama sözleşmesine bağlı, o henüz YOK. Bu yüzden `SegmentBasedSubdivider.
  HasAmbiguousCoplanarOverlap` konumundaki `NotSupportedException` KORUNDU (sessizce çift/eksik
  yüz üretmek yerine dürüst hata) — `Union` birincil primitif olarak HAZIR ve TEST EDİLDİ,
  entegrasyonu `GeneralSolidUnion` assembly'si Face-kimlik/kopyalama sözleşmesini netleştirdiğinde
  yapılmalı (aynı oturumda, o assembly'nin kendi tasarımının bir parçası olarak — ayrı bir "sonra
  entegre et" adımı DEĞİL, çünkü doğru tasarım assembly'nin kendisinin nasıl orkestre ettiğine
  bağlı).

## Güncelleme — 2026-08-15 (Session #68) — `GeneralSolidUnion` (4. ve SON yapı taşı, coplanar-olmayan durumlar için TAMAMLANDI)

**Yazıldı ve doğrulandı:** `Boolean/GeneralSolidUnion.cs` — `Union(Solid a, Solid b, string
resultName = "A_union_B")`. Görev tanımının taslağı BİREBİR uygulandı: A ve B'nin bağımsız derin
kopyaları (`CloneSolid`, bu dosyada YENİ — kod tabanında daha önce genel amaçlı bir Solid
deep-copy yardımcı metodu YOKTU, grep ile doğrulandı) çıkarılıp `SegmentBasedSubdivider.
SubdivideAndClassifyOutside(aWork, bRef)` ve simetrik `SubdivideAndClassifyOutside(bWork, aRef)`
çağrılır (TOPLAM 4 klon — hem çalışma kopyası hem savunmacı salt-okunur referans kopyası her iki
taraf için ayrı ayrı), sonuçlar (`aOutside ∪ bOutside`) tek bir `Solid`'e eklenir.

**Kaynak kod incelemesiyle BULUNAN, görev tanımında AÇIKÇA yazılmayan kritik bir ek adım:**
`FaceSplitter.SplitAtPolylineChord` bölünen bir Face'in İKİ yarısını da (`faceA`+`faceB`)
`solid.Faces`'e ekliyor, ama `SubdivideAndClassifyOutside` bunlardan sadece dışarıda kalanı
`outsideFragments`'e dahil ediyor — kept fragmanın paylaşılan kesim kirişi kenarı, artık sonuçta
OLMAYAN "hayalet" bir Face'e (discarded yarı) işaret eden DOLU bir `LeftFace`/`RightFace` alanı
taşımaya devam ediyordu; bu, `OpenEdgeStitcher`'ın "açık kenar" filtresini (`LeftFace==null XOR
RightFace==null`) atlatıp kenarın karşı taraftan gelen eşleşen ikiziyle DİKİLMEDEN kalmasına yol
açıyordu. Çözüm: `ClearDanglingFaceReferences` — `result.Faces`'e eklenen fragmanların TÜM
kenarları taranır, `result.Faces` içinde OLMAYAN bir Face'e işaret eden `LeftFace`/`RightFace`
`null`'a çekilir (`GeneralSolidSubtractor`/`GeneralSolidIntersector`'ın AYNI temizlik desenine,
farklı bir kaynak — İKİ bağımsız Solid'in birleşimi — üzerinde uygulanmış hâli). Bu adımdan
SONRA `VertexWelder.Weld` + `OpenEdgeStitcher.Stitch` çağrılıyor.

**HİPOTEZ DOĞRULANDI (roadmap'in 2026-08-14 girdisinin "henüz test edilmemiş" dediği varsayım):**
"Section-first" mimarisi (A'nın kesim kirişinin B'nin kesim kirişiyle TANIM GEREĞİ aynı 3D
konumda olması) sayesinde köprü/mirror-cap yüzü inşasına HİÇ gerek kalmadan, sadece yukarıdaki
temizlik + `VertexWelder`/`OpenEdgeStitcher` ile doğru, geçerli bir Solid üretiliyor — roadmap'in
2026-08-07 girdilerinin belgelediği "köprü yüzü zorunlu" engeli, PLANE-tabanlı eski mimaride
(`SplitFaceAgainstPlanes`) GERÇEKTEN aşılamazdı ama segment-tabanlı (`SegmentBasedSubdivider`)
mimaride hiç ORTAYA ÇIKMIYOR.

**Test senaryosu (görev tanımının "gerçek köşe" senaryosu) — DOĞRULANDI, AMA görev tanımının
KENDİ elle-hesabında bulunan bir aritmetik hata YAKALANDI VE DÜZELTİLDİ:** A=[0,2000]³,
B=[1500,3000]³ → kesişim [1500,2000]³ = **500³ = 125.000.000** mm³ (görev tanımı "500³" diyip
ama hesaba 421.875.000 [=750³] koymuştu — kendi içinde tutarsızdı). Doğru UNION hacmi =
8.000.000.000 + 3.375.000.000 − 125.000.000 = **11.250.000.000** mm³ — `dotnet test` bunu
üretti, elle A/B koordinatlarından türetilen doğru formülle KİLİTLENDİ (görev tanımının yanlış
10.953.125.000 değeri testte KULLANILMADI).

**Yeni testler:** `GeneralSolidUnionTests.cs` (5 test) — (a) 3-düzlemli gerçek köşe senaryosunun
hacmi + `IsValid()`, (b) orijinal A/B'nin mutasyona UĞRAMADIĞI (hacim/Face-sayısı/`IsValid()`
öncesi-sonrası), (c) nokta-içi/dışı sınıflandırması (A'nın kendi içi, B'nin kendi içi, A∩B içi,
her ikisinin de dışı), (d) B tamamen A'nın dışında → 2 bağımsız kabuklu GEÇERLİ Solid (Session
#64'ün çok-kabuklu `IsValid()` desteğiyle), (e) coplanar kısmen-örtüşen köşe-çentiği senaryosunda
`SegmentBasedSubdivider`'ın `NotSupportedException`'ının YAKALANMADAN/YUTULMADAN yukarı fırladığı.

**Test sonucu: `dotnet build` — 0 hata. `dotnet test` — 503/503 BAŞARILI** (önceki 498 + bu
oturumun 5 yeni testi), 0 başarısız, HİÇBİR mevcut test bozulmadı.

**Kapsam dışı (bilinçli, DEĞİŞMEDİ):** Coplanar kısmen-örtüşen Face çiftleri — `SegmentBasedSubdivider.
HasAmbiguousCoplanarOverlap`'ın `NotSupportedException`'ı `GeneralSolidUnion` içinde HİÇ
yakalanmıyor/yutulmuyor, olduğu gibi çağırana yükseliyor (görev tanımının açık isteği). Bu
istisnanın gerçek çözümü (`ConvexPolygonClipper2D.Union`'ın, madde/Session #67'nin belgelediği
Face-kimlik/kopyalama KOORDİNASYON sorununu çözecek şekilde entegrasyonu) HÂLÂ ayrı bir oturum
gerektiriyor — bu oturum sadece coplanar-OLMAYAN "temiz" durumları teslim etti (görevin kendi
kapsam sınırıyla TUTARLI).

**Roadmap durumu:** Faz 5'in dört yapı taşı (`FaceSplitter.SplitAtPolylineChord`,
`SegmentBasedSubdivider`, `ConvexPolygonClipper2D.Union`, `GeneralSolidUnion`) artık kod
tabanında MEVCUT — UNION artık coplanar-olmayan girdiler için genel, test edilmiş bir operasyon.
Kalan tek açık iş, coplanar durumun `GeneralSolidUnion`'a entegrasyonu (Session #67'nin
belgelediği koordinasyon tasarımı).

## Güncelleme — 2026-08-15 (Session #69) — Coplanar Durum ÇÖZÜLDÜ: UNION Artık Genel Olarak Çalışıyor

Session #68'in bıraktığı tek açık iş — coplanar kısmen-örtüşen Face çiftlerinin `GeneralSolidUnion`'a
entegrasyonu — koordinasyon sorunu şu şekilde çözülerek tamamlandı:

**Tasarım — ön-geçiş (pre-pass), `SegmentBasedSubdivider`'ın İÇİNDE değil `GeneralSolidUnion`'ın
kendisinde:** `GeneralSolidUnion.Union(a,b)`, `SubdivideAndClassifyOutside`'ı çağırmadan ÖNCE,
A ve B'nin TÜM Face'lerini AYNI ANDA gören `MergeCoplanarOverlappingFacesInto` adlı bir ön-geçiş
çalıştırıyor: coplanar+izdüşüm-örtüşen (AYNI YÖNLÜ, `na·nb>0`) her (aFace,bFace) çifti
`ConvexPolygonClipper2D.Union` ile TEK bir birleşik Face'e indirgenip, bu ikisi segment-toplama
rolündeki TÜM 4 çalışma klonundan (`aWork`/`aRefForB`/`bWork`/`bRefForA`) SİLİNİYOR — böylece
`SegmentBasedSubdivider.HasAmbiguousCoplanarOverlap` bu çiftler için bir daha HİÇ tetiklenmiyor
("hangi taraf üretir" sorusu, tek bir merkezi yerde her iki tarafı da görerek çözüldüğü için
yapısal olarak ortadan kalkıyor).

**Canlı testte bulunan YENİ (ikinci) bug — sınıflandırma/segment-toplama rol ayrımı gerekiyordu:**
Coplanar Face'ler segment-toplama klonlarından silinince, `SolidClassifier.IsPointInside`'ın
ışın-üçgen sayımı o Face'in konumundan geçen ışınları hiç saymıyor, bu da GERÇEKTEN B'nin içinde
kalan noktaların (özellikle A'nın kesişim bölgesindeki fragmanlarının) yanlışlıkla "dışarıda"
sınıflandırılmasına yol açıyordu. Çözüm: `SubdivideAndClassifyOutside`'a opsiyonel
`classificationSolid` parametresi eklendi (varsayılan `null` → eski davranış, TÜM mevcut
çağıranlar etkilenmez) — segment-toplama TAM olmayan (coplanar Face'leri silinmiş) bir klonla
yapılabilirken, sınıflandırma HER ZAMAN ayrı, TAM/eksiksiz bir kabuk klonuna (`bClassifyForA`/
`aClassifyForB` — ön-geçişten muaf tutulan 2 EK klon) göre yapılıyor.

**Doğrulama (köşe-çentiği senaryosu, A=[0,2000]³, B=[1500,3000]²×[0,2000]):** `IsValid()`,
hacim (12.000.000.000 mm³ = 8×10⁹ + 4,5×10⁹ − 0,5×10⁹ kesişim), üst yüzün alanı (6.000.000 —
`ConvexPolygonClipper2DTests`'in AYNI kare boyutlarıyla doğruladığı oktogon alanıyla TUTARLI) ve
4 farklı nokta sınıfı (A-yalnız/B-yalnız/A∩B/tamamen-dışarı) ile kilitlendi.

**Kapsam (bilinçli, dar):** SADECE aynı yönlü (`na·nb>0`) coplanar çiftler birleştiriliyor — zıt
yönlü coplanar çakışma (iç boşluk/cavity duvarı gibi) farklı bir CSG durumu, kapsam dışı (o durumda
normal `HasAmbiguousCoplanarOverlap` koruması devreye girer). Her aFace/bFace en fazla BİR kez
eşleştiriliyor — aynı A-Face'in birden fazla B-Face ile coplanar-örtüştüğü daha karmaşık durumlar
(roadmap'in şu ana kadar hiç karşılaşmadığı bir senaryo) da kapsam dışı.

**Roadmap durumu — TAMAMLANDI:** Session #40'ta başlayan CSG UNION hedefi (6+ oturum, 4 yapı taşı,
2 gerçek bug keşfi/düzeltmesi) artık kod tabanında hem temiz hem coplanar durumlar için çalışıyor
ve test edilmiş. Kalan bilinçli kapsam dışı: zıt yönlü coplanar çakışma (iç boşluk), aynı A-Face'in
birden fazla B-Face ile coplanar örtüşmesi, `FaceIntersection.Intersect`'in kendi coplanar-tutarsızlık
kök nedeni (savunma katmanı hâlâ yerinde, ayrı bir araştırma konusu olarak kalıyor).
