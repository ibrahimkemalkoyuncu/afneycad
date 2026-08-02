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
