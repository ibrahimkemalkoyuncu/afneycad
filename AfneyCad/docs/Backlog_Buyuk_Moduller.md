# Büyük Modül Backlog'u (gelecek_yapilacaklar.txt kaynaklı)

> Bu modüller mevcut FINE MEP parity sürümü stabilize olduktan sonra ele
> alınacak şekilde planlanmıştı (`gelecek_yapilacaklar.txt`). Her biri tek
> oturumda bitirilemeyecek kapsamda — burada bir sonraki adım + tahmini
> kapsam not edildi, böylece hangi session'da hangi alt-parçadan başlanacağı
> netleşir.

## 1. HVAC Kanal Modülü
- **Kapsam:** DuctEntity dikdörtgen/yuvarlak/oval geometri, otomatik dirsek/
  redüksiyon; ASHRAE/CIBSE eş-sürtünme Duct Sizer; radyatör/Fan-Coil çizimi;
  menfez/difüzör/VAV/AHU kütüphanesi.
- **Mevcut temel:** `DuctSizingService` (TS EN 13779) ve `DuctEntity` zaten var
  (bkz. Eksiklikler.md Session #31) — asıl eksik: dirsek/redüksiyon otomatik
  atama ve ASHRAE eş-sürtünme yöntemi varyantı.
- **İlk adım:** Mevcut `DuctSizingService`'i incele, eş-sürtünme (equal-friction)
  algoritmasının zaten olup olmadığını doğrula; eksikse ekle.

## 2. Gelişmiş 3D BIM Görüntüleyici
- **Kapsam:** Walk & Fly kamera (first-person), Section Box, malzeme bazlı
  gölgelendirme.
- **Mevcut temel:** `OrbitCamera` + `ViewCube` var (Eksiklikler.md #13).
  First-person kontrol ve section box yok.
- **İlk adım:** Kamera modelini incele (`OrbitCamera`), WASD+mouse-look modu
  ekle; Section Box için mevcut render pipeline'ında clip-plane desteği olup
  olmadığını araştır (SkiaSharp 2D render olduğu için gerçek 3D section box
  büyük mimari değişiklik gerektirebilir — önce fizibilite incelemesi şart).

## 3. Elektrik Tesisat Modülü
- **Kapsam:** Kablo tavası/borusu 3D çizim + çakışma kontrolü, pano/devre/
  aydınlatma/priz yerleşimi, gerilim düşümü + akım taşıma + aydınlatma analizi.
- **Mevcut temel:** Yok — sıfırdan yeni domain (yeni Entity tipleri, yeni
  Mechanical alt-servisleri, yeni ribbon sekmesi).
- **İlk adım:** `Afney.Cad.Domain`'de `CableTrayEntity`/`ElectricalPanelEntity`
  taslağı + mevcut `PipeEntity`/`DuctEntity` desenini örnek alarak entity
  şeması tasarımı (ayrı planlama oturumu gerektirir).

## 4. Parametrik Ekipman Tasarımcısı (Family Editor)
- **Kapsam:** Kullanıcının kendi ekipmanını (kombi/kazan/pompa) 3D hacim +
  su bağlantı noktalarıyla tanımlaması (Revit .rfa benzeri).
- **Mevcut temel:** `ArchitecturalLibraryService`, `ManufacturerCatalogService`
  statik kataloglar sağlıyor; parametrik/kullanıcı tanımlı obje editörü yok.
- **İlk adım:** Basit bir "custom equipment" JSON şeması (BBox + port noktaları)
  tasarla; editör UI'ı sonraki adım.

## 5. AI Destekli Auto-Routing İyileştirmesi
- **Kapsam:** Katlar arası şaft boşluklarını bulup zemin-bodrum arası en uygun
  güzergahı otomatik çizme.
- **Mevcut temel:** `AutoRouteService` (A*, 2D/tek kat) zaten var
  (Eksiklikler.md Session #37, "AfneyCAD'in Ötesinde").
  Eksik olan: çok katlı şaft tespiti + kat-arası bağlantı.
- **İlk adım:** `AutoRouteService`'in şu an tek kat / Z-sabit mi çalıştığını
  doğrula; `FloorSnapshotService.DetectFloors` ile şaft (boşluk) tespiti için
  ayrı bir `ShaftDetectionService` fizibilitesi.

---
**Öncelik önerisi:** #1 (HVAC) ve #5 (Auto-Routing) mevcut servislerin
üzerine inşa edildiği için en düşük riskli/en hızlı kazanım; #3 ve #4 sıfırdan
yeni domain gerektirdiği için ayrı bir mimari planlama oturumu (Plan modu)
gerektirir.
