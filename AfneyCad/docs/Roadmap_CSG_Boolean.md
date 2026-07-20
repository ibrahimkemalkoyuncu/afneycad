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
