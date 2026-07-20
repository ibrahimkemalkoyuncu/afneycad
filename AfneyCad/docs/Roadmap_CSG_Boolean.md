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

**Faz 4 (SUBTRACT montajı) BAŞLANMADI — bilinçli durak noktası:** Genel durumda TEK bir Face,
diğer Solid'in BİRDEN FAZLA Face'iyle kesişip birden fazla segment alabilir (ör. bir kutunun
köşesini kesen başka bir kutu — 3 yüz her biri İKİ segmentle kesilir, L-şekilli bir sınır
oluşur). Mevcut `FaceSplitter.SplitAtChord` sadece TEK bir kiriş (2 sınır noktası → 2 alt-yüz)
destekliyor — çoklu-segment (N sınır noktası → N alt-yüz, bazıları delik/hole içerebilir) genel
bir yüz-yeniden-üçgenleme algoritması gerektiriyor. Bu, Faz 1-3'ten daha büyük bir ek yatırım.
**Önerilen ilk hedef senaryo (basit ama gerçek):** İki eksene tam hizalı, kesişim düzlemi
solid'lerin y/z kesitini TAM kapsayan kutular (ör. A=[0,2000]³, B=[1000,3000]×[0,2000]×[0,2000])
— bu durumda her etkilenen yüz sadece TEK bir kirişle kesiliyor (mevcut primitiflerle
çözülebilir), sonuç basit bir kutu olduğu için (BRepBuilder.ExtrudeBox'ın kendi çıktısıyla
çapraz doğrulanabilir) test etmek kolay.

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
