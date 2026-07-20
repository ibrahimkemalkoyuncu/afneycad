# AfneyCAD vs 4M FineSANI — Denetim Geçmişi (Kod Doğrulamalı)

> Bu dosya, `karsilastirma.md` ve `Eksiklikler.md`'nin aksine **kod okunmadan yapılan öz-değerlendirme değildir**.
> Her girdi, gerçek kaynak koduna bakan (dosya:satır referanslı) bağımsız doğrulama turlarının sonucudur.
> Amaç: bir sonraki denetimle doğrudan karşılaştırılabilecek, güvenilir bir zaman serisi tutmak.

---

## Session #52 — 2026-07-19 — Baseline (ilk kod-doğrulamalı tam denetim)

**Yöntem:** 4 paralel araştırma ajanı, 12 kategori, ~65 alt-özellik, her biri dosya:satır referanslı.
**Rapor:** [AfneyCAD vs 4M FineSANI — Kod Doğrulamalı Denetim](https://claude.ai/code/artifact/6af61033-c18f-493c-9bc3-8822c7584e7c) (artifact)

### Genel Ortalama: **6.8 / 10**

| # | Kategori | Puan | Bir sonraki denetimde bak |
|---|----------|------|---------------------------|
| 1 | Çizim Araçları | 8.0 | Hatch/Block/Snap/Selection bu turda derinlemesine incelenmedi |
| 2 | Boyutlandırma | 6.0 | IFC export (3/10, hiç yok), DXF export gerçek DIMENSION entity değil (6/10) |
| 3 | 3D Görselleştirme / B-Rep | 5.8 | Ana render (Skia) hâlâ 2D; B-Rep sadece Pipe3DViewWindow'da izole; Axonometric gerçek 3D değil |
| 4 | Hidrolik Hesap + Mühendislik | 7.8 | Mimari çakışma tespiti sadece BBox (7/10) |
| 5 | HVAC | 7.0 | Cooling/Heating/Psychrometric/Acoustic/Energy örnekleme bazlı doğrulandı, tek tek değil |
| 6 | BIM / IFC | 6.2 | B-Rep kernel'e bağlı değil (3/10 — kod kendi yorumunda itiraf ediyor), composite curve/fitting import yok |
| 7 | Mimari Tanıma / Mahal | 6.7 | Sıfır test kapsamı (planar-graph, ray-casting hiç test edilmemiş) |
| 8 | Dosya I/O | 7.2 | Xref reload-tracking/nested/clip boundary yok (6/10) |
| 9 | Raporlama / BOM | 6.4 | "Real-Time Cost" ismi yanıltıcı — sabit fiyat tablosu (5/10) |
| 10 | Çok Katlı Bina | 6.2 | Sıfır test kapsamı; riser routing/collision yok, kaba toleranslar (500mm grid) |
| 11 | UX / Otomasyon | 7.0 | Layer ISO 13567 iddiası sadece yorumda (5/10), gerçek standart yapısı yok |
| 12 | Özel Hesaplar | 6.8 | FireFighting basınç kaybı kaba yaklaşım, gerçek Hazen-Williams değil (6/10) |

---

## Session #53 — 2026-07-19 — 3D/B-Rep kategorisi hedefli iyileştirme

**Yöntem:** Session #52'de en düşük puanlı kategoriden başlanarak (3D/B-Rep, 5.8), somut kod
değişiklikleri yapıldı, ardından **bağımsız bir doğrulama ajanı** (geliştirmeyi yapan ajandan
farklı, şüpheci) gerçek kaynak koduna ve teste bakarak yeniden puanladı — kendi kendine
puanlama YAPILMADI.

### Yapılan değişiklikler
| Değişiklik | Dosya | Etki |
|---|---|---|
| Kapı/pencere boşluklu duvar segmentasyonu (boolean'sız, ExtrudeBox parçalarıyla) | `WallBRepService.cs` | Hacim korunumu testlerle kanıtlandı |
| Kanal (Duct) B-Rep desteği — yeni (dikdörtgen + dairesel N-gon) | `DuctBRepService.cs` (yeni) | Önceden 3D'de hiç temsil edilmiyordu |
| Axonometrik export artık gerçek B-Rep duvar wireframe'i çiziyor | `AxonometricExportService.cs` | Önceden sadece boru noktası projeksiyonuydu |
| Pipe3DViewWindow artık kanalları da render ediyor | `Pipe3DViewWindow.xaml.cs` | Kanal B-Rep'i WPF Viewport3D'ye bağlandı |
| 10 yeni test (WallBRepService 6, DuctBRepService 4) | `tests/.../Mechanical/` | Hepsi geçti, tam suite 185/187 (regresyon yok) |

### Bağımsız doğrulama sonucu — Kategori 3: **5.8 → 6.7 / 10**

| Alt-özellik | Önceki | Yeni | Not |
|---|---|---|---|
| B-Rep kernel | 7 | 7 | Değişmedi — boolean/NURBS/fillet hâlâ kapsam dışı (kod kendi dokümante ediyor) |
| BRepKernelTests | 8 | 7 | Değişmedi |
| WallBRepService | 6 | 8 | Gerçek kapı/pencere segmentasyonu, hacim-korunumlu, test edildi |
| DuctBRepService | — | 8 | Yeni alt-kategori — dikdörtgen+dairesel, test edildi |
| Pipe3DViewWindow | 7 | 7 | Kanal+duvar eklendi ama hâlâ izole WPF penceresi |
| AxonometricExportService | 4 | 7 | Artık gerçek B-Rep duvar wireframe'i — sadece nokta projeksiyonu değil |
| Ana render motoru (Skia) | 3 | 3 | **Değişmedi** — hâlâ tamamen 2D, B-Rep'e hiç bağlı değil |

**Dürüstlük notu (ajanın kendi ifadesi):** Kapı/pencere segmentasyonu gerçek CSG DEĞİL —
duvarı boşluk etrafında parçalara bölme hilesi (kod bunu itiraf ediyor, abartı yok). Ana Skia
render motorunun hâlâ B-Rep'e bağlı olmaması bu kategorinin en büyük açığı olmaya devam ediyor.

### Ek iyileştirme (aynı oturum) — Kategori 2: Boyutlandırma: **6.0 → 7.5 / 10**

| Değişiklik | Dosya | Etki |
|---|---|---|
| Gerçek DXF DIMENSION entity (BLOCKS bölümü + anonim `*Dn` blokları, doğru group code'lar) — Linear/Aligned | `DxfWriterService.cs` | AutoCAD'de artık gerçek düzenlenebilir DIMENSION nesnesi (önceden LINE+TEXT patlatmaydı) |
| IFC dimension export (yeni) — `IfcAnnotation` + `IFCGEOMETRICCURVESET`, geometri `DimensionEntity.ExplodeToBasicEntities()`'ten (DRY) | `IfcExportService.cs` | Önceden hiç aktarılmıyordu (3/10) |
| 5 yeni test (3 DXF + 2 IFC) | `tests/.../Infrastructure/` | Geçti ama bağımsız doğrulama ajanı "yüzeysel" (sadece string-contains, group code sırası/sayısal doğruluk doğrulanmadı) olarak işaretledi |

| Alt-özellik | Önceki | Yeni |
|---|---|---|
| DIM komutları | 7 | 7 (dokunulmadı) |
| Stil UI bağlantısı | 8 | 8 (dokunulmadı) |
| DXF export | 6 | 8 |
| IFC export | 3 | 7 |

**Dürüstlük notu:** Radius/Angular DXF'te hâlâ patlatılmış (bilinçli kapsam sınırı — group code hatası riski). IFC'de metin gerçek `IfcTextLiteral` değil, `IfcAnnotation.Name`'e yazılıyor. Testler regresyona karşı korur ama derin doğruluk kanıtı değil.

### Ek iyileştirme (aynı oturum) — Kategori 6: BIM/IFC: **6.2 → 7.0 / 10**

| Değişiklik | Dosya | Etki |
|---|---|---|
| Duvarlar artık IFC'ye export ediliyor (yeni — önceden hiç yoktu) | `IfcExportService.cs` (`ExportWall`) | Gerçek gap kapatıldı |
| B-Rep → IFC bağlantısı (yeni) — `WallBRepService` (kapı/pencere segmentasyonu dahil) + `BRepTessellator` → IFC4 tessellation (`IFCCARTESIANPOINTLIST3D`+`IFCPOLYGONALFACESET`) | `IfcExportService.cs` | Duvar 3D görünümü/axonometrik/IFC artık AYNI B-Rep Solid'i kullanıyor — tek doğruluk kaynağı |
| Çoklu B-Rep segmenti (kapı/pencereli duvar) hâlâ TEK `IfcWall` ürünü | `ExportWall` | Testle kanıtlandı (segment sayısı ≥2, ürün sayısı=1) |
| 3 yeni test | `tests/.../Infrastructure/IfcWallExportTests.cs` | Geçti, ikisi yüzeysel değil (segment/ürün sayımı) |

| Alt-özellik | Önceki | Yeni |
|---|---|---|
| STEP/P21 parser | 7 | 7 (dokunulmadı) |
| Extrusion + rotasyon | 8 | 8 (dokunulmadı) |
| Kavisli duvar ekseni | 6 | 6 (dokunulmadı) |
| MEP import | 7 | 7 (dokunulmadı) |
| B-Rep bağlantısı | 3 | 7 |

**Dürüstlük notu:** Kapı/pencere elemanlarının kendisi (IfcDoor/IfcWindow) hâlâ export edilmiyor — sadece boşluklu duvar gövdesi doğru geometriyle çıkıyor. MEP fitting'ler (boru/kanal/dirsek/T-parça) hâlâ eski `IFCEXTRUDEDAREASOLID` yöntemiyle, B-Rep kernel'e bağlı DEĞİL — sadece duvarlar B-Rep'e geçti, borular/kanallar geçmedi (bir sonraki potansiyel adım).

### Ek iyileştirme (aynı oturum) — Kategori 10: Çok Katlı Bina: **6.2 → 6.75 / 10**

| Değişiklik | Dosya | Etki |
|---|---|---|
| 9 yeni test — MultiStoryBuildingService (kat sıralama, riser segment ardışıklığı) + PressureZoneService (ρ·g·h formül doğrulaması, PRV bölge sayısı) | `tests/.../Mechanical/MultiStoryBuildingServiceTests.cs`, `PressureZoneServiceTests.cs` (yeni) | Önceden bu kategoride SIFIR test vardı |

| Alt-özellik | Önceki | Yeni |
|---|---|---|
| Kat tanımı | 7 | 7.5 |
| Riser oluşturma | 6 | 6 (dokunulmadı) |
| RiserDiagramService | 7 | 7 (dokunulmadı) |
| FloorCopyService | 6 | 6 (dokunulmadı — MEP bağlantı korunumu eksikliği hâlâ duruyor) |
| AutoConnectInterFloorRisers | 7 | 7 (dokunulmadı) |
| PressureZoneService formülü | 8 | 8.5 |
| 3D montaj | 6 | 6 (dokunulmadı) |
| Test kapsamı | 2 | 6 |

**Dürüstlük notu (bağımsız ajanın ifadesi):** Bu artış gerçek ama sınırlı — test eklemek FloorCopyService'in MEP bağlantı korunumu eksikliği gibi gerçek fonksiyonel açıkları KAPATMADI. Kategori "test var, çalışıyor" seviyesinden "iyi test edilmiş" seviyesine geçmedi — sadece 2 servis (8 servisten) için 0'dan somut kanıta geçti.

### Ek iyileştirme (aynı oturum) — Kategori 9: Raporlama: **6.4 → 7.0 / 10**

| Değişiklik | Dosya | Etki |
|---|---|---|
| GERÇEK HATA düzeltmesi: `CalculateProjectCost` malzeme fiyatını `pipe.SystemType` (DomesticColdWater/WasteWater/...) ile arıyordu — `_priceTable` GERÇEK malzeme adlarıyla (PPRC_PN20/PVC_SN4/...) doluydu. İki küme HİÇ kesişmiyordu (bağımsız ajan grep ile doğruladı) → her boru sessizce varsayılan 50 TRY/m'ye düşüyordu | `RealTimeCostService.cs` | Artık `pipe.PipeMaterialType` kullanılıyor, fiyat tablosu `nameof(PipeMaterial.X)` ile gerçek enum üyeleriyle eşleşiyor |
| 3 yeni test (farklı malzeme = farklı fiyat, DN faktörlü tam sayısal doğrulama) | `tests/.../Mechanical/RealTimeCostServiceTests.cs` | Eski koddayken bu testler MATEMATİKSEL OLARAK başarısız olurdu — gerçek regresyon testi |

| Alt-özellik | Önceki | Yeni |
|---|---|---|
| Compliance Report | 8 | 8 (dokunulmadı) |
| Mimari/Selection/HVAC BOM | 7 | 7 (dokunulmadı) |
| Technical Spec | 6 | 6 (dokunulmadı) |
| Real-Time Cost Analysis | 5 | 7 |
| PDF/HTML çıktı | 7 | 7 (dokunulmadı) |

**Dürüstlük notu:** Sınıf adı hâlâ "RealTimeCost" — gerçek piyasa/API entegrasyonu yok (bilinçli, kod içi yorumda itiraf edilmiş). Fitting sayısı hâlâ kaba tahmin (`PipeCount × 1.5`), fixture fiyat eşleştirmesi hâlâ kırılgan string `.Contains()`.

### Ek iyileştirme (aynı oturum) — Kategori 7: Mimari Tanıma / Mahal Yönetimi: **6.7 → 7.0 / 10**

| Değişiklik | Dosya | Etki |
|---|---|---|
| 5 yeni test — SpaceDetectionEngine (tek oda, çok-odalı ayrım, katman filtresi, oda adı tespiti, alan/çevre formülleri) | `tests/.../Mechanical/SpaceDetectionEngineTests.cs` (yeni) | Önceden bu servis SIFIR test kapsıyordu |

| Alt-özellik | Önceki | Yeni |
|---|---|---|
| Otonom oda tespiti | 8 | 9 |
| Tıkla-ile-oda-bul | 7 | 7 (dokunulmadı) |
| Oda tipi tahmini | 7 | 7 (dokunulmadı) |
| Katman bazlı tanıma | 6 | 6 (dokunulmadı) |
| Mimari BOM/oda kütüphanesi | 6 | 6 (dokunulmadı) |

**Dürüstlük notu (bağımsız ajanın ifadesi):** +0.3 kategori artışı TAMAMEN tek bir alt-özelliğin (otonom tespit motoru) test edilmesinden geliyor; 5 alt-özellikten 4'ü (ray-casting tıklama, oda tipi/metin çıkarımı, katman heuristiği, oda/BOM kütüphanesi) hâlâ tamamen test edilmiyor. Ayrıca tek-oda testi "şanslı" bir kenar durumu doğruluyor (tek kapalı döngü → iki eşit alanlı yüzey üretir, strict `>` karşılaştırması sadece birini eler) — bu algoritmanın doğruluğunu kanıtlıyor ama genel dayanıklılığını (gerçek DWG gürültüsü, yay/kavisli duvar, T-kavşak) değil.

### Ek iyileştirme (aynı oturum) — Kategori 3 (devam): 3D/B-Rep: **6.7 → 6.8 / 10**

| Değişiklik | Dosya | Etki |
|---|---|---|
| Konkav (L-şekilli) poligon ekstrüzyon+tessellasyon testi (yeni) | `tests/.../Geometry/BRepKernelTests.cs` | `BRepBuilder.ExtrudePolygon`'ın sadece kutu değil, genel konkav profil de doğru işlediği kanıtlandı |

**Dürüstlük notu (bağımsız ajanın ifadesi):** Bu KÜÇÜK bir iyileştirme — abartılmamalı. Kernel zaten teorik olarak konkav destekliyordu (ear-clipping algoritması genel, konvekslikten bağımsız); bu sadece doğrulandı. Gerçek bir domain kullanım noktası (ör. `RoomBRepService` — SpaceDetectionEngine'in artık tespit edebildiği L-şekilli mahalleri B-Rep'e çeviren bir servis) HÂLÂ YOK (grep ile doğrulandı, `src/`'de hiç yok). "B-Rep kernel" puanı bu yüzden değişmedi (7→7), sadece "BRepKernelTests" hafifçe arttı (7→7.5).

### Ek iyileştirme (aynı oturum) — Kategori 10 (devam): Çok Katlı Bina: **6.75 → 7.0 / 10**

| Değişiklik | Dosya | Etki |
|---|---|---|
| GERÇEK HATA düzeltmesi: `CopyFloorWithConnections`, doğrudan üst üste istiflenmiş katlarda (en yaygın senaryo, dz==source.Height) sıfır uzunluklu dejenere `PipeEntity` üretiyordu (koşul kat-arası mesafeyi kontrol ediyordu, bağlantı borusunun kendi uzunluğunu değil) | `MultiStoryEnhancementService.cs` | Artık gerçek boşluk uzunluğu (`|dz - source.Height|`) kontrol ediliyor |
| 5 yeni test (dejenere boru regresyon testi + boşluklu kat + bağlantı korunumu + riser eşleştirme + farklı sistem tipi ayrımı) | `tests/.../Mechanical/MultiStoryEnhancementServiceTests.cs` (yeni) | Regresyon testi eski kodda KESİNLİKLE fail ederdi — doğrulandı |

| Alt-özellik | Önceki | Yeni |
|---|---|---|
| Kat tanımı | 7.5 | 7.5 (dokunulmadı) |
| Riser oluşturma | 6 | 6 (dokunulmadı) |
| RiserDiagramService | 7 | 7 (dokunulmadı) |
| FloorCopyService (kat kopyalama, geniş anlamda) | 6 | 7 |
| AutoConnectInterFloorRisers | 7 | 7.5 |
| PressureZoneService formülü | 8.5 | 8.5 (dokunulmadı) |
| 3D montaj | 6 | 6 (dokunulmadı) |
| Test kapsamı | 6 | 6.5 |

**Dürüstlük notu (bağımsız ajanın ifadesi):** Çok katlı bina alanında ~13 servis var, bunlardan sadece 3'ünün (~%23) ayrı test dosyası var. Ayrı `FloorCopyService.cs` dosyası (CopyFloorWithConnections'dan farklı, daha zayıf bir implementasyon) hâlâ hiç test edilmiyor.

### Ek iyileştirme (aynı oturum) — Kategori 12: Özel Hesaplar: **6.8 → 7.2 / 10**

| Değişiklik | Dosya | Etki |
|---|---|---|
| GERÇEK HATA düzeltmesi: sprinkler basınç kaybı `0.02 × CeilingHeightM × 10` idi — debi/çap/uzunluktan tamamen bağımsız, "NFPA 13" iddiasına rağmen gerçek Hazen-Williams değildi | `FireFightingService.cs` | Artık gerçek metrik Hazen-Williams: `Δp = 6.05×10⁵×Q^1.85/(C^1.85×d^4.87)`, üsler literatürle doğrulandı |
| 3 yeni test (formül karşılaştırması, tehlike sınıfı→debi→basınç kaybı ilişkisi, uzunlukla doğrusal orantı) | `tests/.../Mechanical/FireFightingServiceTests.cs` (yeni) | Eski kodda `HigherHazardClass` testi KESİNLİKLE fail ederdi (debiden bağımsızdı) |

| Alt-özellik | Önceki | Yeni |
|---|---|---|
| GasCalcSheetService | 8 | 8 (dokunulmadı) |
| HeatingSystemService | 7 | 7 (dokunulmadı) |
| WasteWaterDesignService | 7 | 7 (dokunulmadı) |
| SepticTankService | 6 | 6 (dokunulmadı) |
| FireFightingService | 6 | 8 |

**Dürüstlük notu:** Hâlâ gerçek boru ağı topolojisine bağlı değil (MainPipeLengthM sabit varsayılan/kullanıcı girdisi), yerel kayıplar (dirsek/T/vana) hesaba katılmıyor.

### Ek tur — Kullanıcı isteğiyle: gerçek endüstri standardı denetimi (web araştırma ajanı)

Kullanıcı özel olarak istedi: "Uygulamamız endüstri standardını yakalamış mı? Standartları araştıran bir ajan
değerlendirsin, eksikleri başka bir ajan tamamlasın." Bu, önceki turların aksine kodun KENDİ İÇİNDE tutarlı
olup olmadığını değil, kodun kullandığı SAYISAL DEĞER/FORMÜLLERİN gerçek yayınlanmış standartlarla (NFPA 13,
TS EN 12056-2, DIN 1988-300 vb.) eşleşip eşleşmediğini WebSearch ile araştırdı.

**Bulunan gerçek standart hataları:**
1. **`FireFightingService.cs` — OrdinaryHazard_2 tasarım alanı hatası:** `designArea_m2 = 216` idi — EN 12845'e göre bu OH3'ün (OH1=72, OH2=144, OH3=216 serisi) değeri, OH2'ye kopyala-yapıştırılmış. Düzeltildi → 144 m². Güvenlik açısından tehlikeli değildi (fazla-güvenli tarafta), ama standart uyumsuzdu.
2. **`WasteWaterDesignService.cs` — K-katsayı etiketleme hatası:** TS EN 12056-2 Tablo 3'ün 4 kategorisi (K=0.5 konut / K=0.7 sık kullanım-hastane-okul-otel / K=1.0 yoğun-umumi / K=1.2 özel dikkat) ile kod eşleşmiyordu — UI'da "System I: Hastane" etiketliydi ama K=1.0 veriyordu (doğrusu K=0.7), standardın en üst katmanı K=1.2 hiç kullanılmıyordu (System_IV yanlışlıkla K=1.0). Düzeltildi: enum etiketleri + UI ComboBox metinleri + System_IV→K=1.2.
3. **İki ayrı serviste (`FireFightingService.cs` vs `NFPA13SprinklerService.cs`) aynı isimli ama farklı standarda dayalı `HazardClass` enum'ları** (biri EN 12845, diğeri NFPA 13) — kasıtsız isim çakışması, kafa karıştırıcı. Kod silinmedi/birleştirilmedi (risk), her iki dosyaya çapraz-referans uyarı yorumu eklendi.

**Doğrulanamayan alanlar (dürüstçe belirtildi):** TS 825 2024 güncel U-değer tabloları ve DIN 1988-300/TS EN 806-3 basınç limitlerinin birincil standart metnine erişilemedi — sadece ikincil kaynaklarla dolaylı doğrulandı.

**Hazen-Williams C=120 (çelik), Colebrook-White pürüzlülük değerleri, NFPA13SprinklerService'in kendi tablosu:** standart uyumlu bulundu.

### Ek tur — Kullanıcı isteğiyle: test edilmeyen servislere gerçek test yazımı (ikinci ajan)

Aynı zamanda ikinci bir ajan, kod tabanında hâlâ SIFIR test kapsayan ama gerçek mühendislik mantığı
içeren servisleri taradı ve 5 servise 43 yeni test yazdı: `HeatingSystemService` (8), `PumpSelectionService`
(9), `PressureDropService` (7), `GutterSizingService` (8), `ClashDetectionService` (11). Üçüncü bir bağımsız
ajan bunları şüpheci şekilde doğruladı — hepsi gerçek formül/mantık doğrulaması (yüzeysel "hata fırlatmıyor"
değil). Not: `ClashDetectionService` testleri MEVCUT BBox-tabanlı mimari çakışma mantığını test ediyor,
kodu İYİLEŞTİRMEDİ — puanı bu yüzden sabit kaldı (7/10), sadece test kapsamı eklendi.

### Güncellenmiş alt-özellik puanları

**Hidrolik Hesap + Mühendislik: 7.8 → 7.9/10**
| Alt-özellik | Önceki | Yeni |
|---|---|---|
| HardyCrossSolver | 9 | 9 |
| DomainGuardService | 8 | 8 |
| Colebrook-White | 8 | 8 |
| PressureDropService (Darcy-Weisbach+K) | 8 | 8 (artık 7 gerçek testle doğrulandı) |
| Kritik yol | 7 | 7 |
| ClashDetectionService | 7 | 7 (sadece test eklendi, BBox kısıtı duruyor) |
| PumpSelectionService (yeni) | — | 8 |

**Özel Hesaplar: 7.2 → 7.7/10**
| Alt-özellik | Önceki | Yeni |
|---|---|---|
| GasCalcSheetService | 8 | 8 |
| HeatingSystemService | 7 | 8 (8 test ile formül zinciri doğrulandı) |
| WasteWaterDesignService | 7 | 8 (K-katsayı standart hatası düzeltildi) |
| SepticTankService | 6 | 6 |
| FireFightingService | 8 | 8 (OH2 alan hatası da düzeltildi) |
| GutterSizingService/RainWater (yeni) | — | 8 |

### Genel ortalama: 6.8 → **7.3 / 10** (7 kategori düzeltildi + 2 kategoride ek iyileştirme: Hidrolik 7.8→7.9, Özel Hesaplar 7.2→7.7)

### Ek tur — Kullanıcı isteğiyle: son 2 bilinen test hatası bulunup çözüldü

Bu oturum boyunca "bilinen, alakasız" diye işaretlenen `PortEngineeringTests`'teki 2 hata
kullanıcı isteğiyle araştırıldı — ikisi de GERÇEK hatalardı:

1. **`ReactiveCalculation_AfterDiameterChange_ShouldAutoRecalculate`** — Test riser borusunun
   ucunu hiçbir şeye bağlamıyordu. `DomainGuardService.CheckOpenEnds` (önceki bir oturumda
   sertleştirilmiş, `_database`'den bağımsız çalışan bir kapı) bunu "açık uç" hatası olarak
   yakalayıp `RecalculateProject`'i bilinçli olarak engelliyordu — bu GERÇEK ve DOĞRU bir
   mühendislik davranışı (açık uçlu bir hatta hesaplama anlamsızdır), test bu sertleştirmeden
   sonra güncellenmemişti. Düzeltme: riser'ın ucuna tek portlu bir `MechanicalLoadNode` (sink)
   eklenip gerçek, uçtan uca kapalı bir şebeke kuruldu.
2. **`CreateWashbasin_ShouldHaveCorrectLUAndSize`** — `SanitaryFixtureEntity.CreateWashbasin`
   LU=1.5 kullanıyordu; TS EN 806-2 Tablo 1'e göre standart lavabo LU=0.5'tir (kod tabanının
   8+ başka yerinde de 0.5 kullanılıyor). 1.5 değeri "Cerrahi Lavabo" (DIN 1988-300 §5.3) için
   ayrılmış özel bir armatüre ait, yanlışlıkla kopyalanmış. Aynı hata `PipeWizardService.cs`'de
   3 yerde daha tekrarlanmıştı (`GenerateStandardBathroom`, `GenerateMasterBathroom`) — hepsi
   düzeltildi.

**Sonuç: tam test suite artık 286/286 — bu oturumda ilk kez sıfır bilinen hata.**

### Metodoloji notu
`karsilastirma.md` (610/630, "%97 — FineSANI'yi geçti") ve `Eksiklikler.md` (kategori başı 10/10) bu denetimde
**referans olarak kullanılmadı** (sadece FineSANI'nin özellik listesi için taranmıştı) — kod okunmadan yazılmış
öz-değerlendirmeler oldukları ve gerçek kodla örtüşmediği doğrulandı. Bu dosyadaki puanlar, bundan sonraki
oturumlarda **doğruluk referansı** olarak kullanılmalı; yeni bir puanlama yapılınca buraya yeni bir `## Session #N`
girdisi eklenmeli (üstteki tablo formatıyla), böylece ilerleme gerçek kod üzerinden zaman içinde izlenebilir.

---
