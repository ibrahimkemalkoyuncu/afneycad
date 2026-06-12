# AfneyCAD Geliştirme ve Eksiklik Analizi (Gap Analysis)

> **Son güncelleme:** 2026-06-13 — Session #34 sonrası durum  
> Bu belge, AfneyCAD'in mevcut yetenekleri ile endüstri standardı olan FINE MEP (AutoBUILD & ADAPT/FCALC) yazılımları arasındaki farkları özetlemektedir.

---

## 1. AutoBUILD (Mimari BIM Modelleme)

| Özellik | Durum | Not |
|---|---|---|
| IFC Import (Revit/ArchiCAD) | ✅ **Var** | `IfcImportService` — IFC 2x3+4 (Session #20) |
| IFC Export | ✅ **Var** | `IfcExportService` |
| Parametrik BIM Nesneleri | ✅ **Var** | `ArchitecturalObstacle` + `BimMaterialLayer` — U-değeri (ISO 6946), yangın (TS EN 13501-1), ses yalıtımı · `BimPropertiesDialog` (Session #30) |
| Akıllı DWG→BIM Dönüşüm | ✅ **Var** | `SmartBimConverterService` + `SmartBimConverterDialog` — LineEntity → ArchitecturalObstacle (Session #30) |
| Geniş Mimari Kütüphane | ✅ **Var** | `ArchitecturalLibraryService` — 20+ nesne: kolon/döşeme/çatı/mobilya/ekipman · `ArchitecturalLibraryDialog` (Session #30) |

---

## 2. ADAPT/FCALC (Hidrolik Hesaplama Motoru)

| Özellik | Durum | Not |
|---|---|---|
| Bağımsız Hesap Modu (CAD'siz) | ✅ **Var** | Manuel Giriş sekmesi + JSON kaydet/yükle + Excel export (Session #30) |
| Çoklu Standart (ASPE/BS/ASHRAE) | ✅ **Var** | `PipeSizer` + `StandardSelectionService` — 4 norm (Session #21) |
| Pompa/Hidrofor Kapasite | ✅ **Var** | `WaterTankService`, `DepoHidroforDialog` |
| Genleşme Tankı | ✅ **Var** | `ThermalExpansionService` (TS EN 13831) |
| Su Sayacı Seçimi | ✅ **Var** | `WaterMeterService` (TS EN 14154) |
| Geri Akış Önleyici | ✅ **Var** | `BackflowPreventerService` (TS EN 1717) |
| Geri Besleme Döngüsü | ✅ **Var** | `DrawingSyncService` — PipeDN_Changed → Ø etiketi anında güncelleme (Session #29) |
| Doğalgaz Hesap Föyü | ✅ **Var** | CalculationTableWindow ⛽ sekmesi + HTML export (Session #30) |
| Hesap Tablosu Spreadsheet Entegrasyonu | ✅ **Var** | `CalculationTableWindow` Ekipmanlar sekmesi — WaterTank+WaterMeter+ExpansionTank+BackflowPreventer → Excel (Session #31) |

---

## 3. Mühendislik ve Kullanılabilirlik

| Özellik | Durum | Not |
|---|---|---|
| Kolon Şeması | ✅ **Var** | `RiserDiagramExportDialog` — gerçek 3D model (Session #28) |
| PDF Antetli Rapor | ✅ **Var** | `PdfExportService` + `TitleBlockInfo` — firma/mühendis/imza (Session #29) |
| Topoloji Analizi | ✅ **Var** | `NetworkTopologyAnalysisService` — DFS/BFS/Dijkstra (Session #28) |
| Basınç Düşümü Haritası | ✅ **Var** | `PressureMapService` — yeşil→sarı→kırmızı toggle (Session #29) |
| Çizim ↔ Hesap Senkronu | ✅ **Var** | `DrawingSyncService` (Session #29) |
| 3D MEP-MEP Çakışma | ✅ **Var** | `ClashDetectionService` — pipe-pipe 3D mesafe + valve BBox (Session #30) |
| MDI Çoklu Proje | ✅ **Var** | `DocumentTabs` + `CreateNewDocument` + + sekme butonu + sayaç (Session #30) |
| Vana Kütüphanesi | ✅ **Var** | `ValveLibraryDialog` — boru üstüne snap+split (Session #28) |

---

## 4. Session #31 — Fine MEP Karşılaştırma Tamamlananlar

| Özellik | Durum | Not |
|---|---|---|
| Isıtma Yük Hesabı (TS 825) | ✅ **Var** | `HeatingSystemService` — 18 şehir, 24 radyatör katalogu, 60/40°C düzeltme · `HeatingDesignDialog` |
| HVAC Kanal Boyutlandırma (TS EN 13779) | ✅ **Var** | `DuctSizingService` — eşit sürtünme, 18 zone tipi · `HvacDesignDialog` |
| Yağmur Oluğu Boyutlandırma (TS EN 12056-3) | ✅ **Var** | `GutterSizingService` — 16 şehir i değeri, Manning yarım daire, DN50-DN160 |
| Genleşme Kompansatörü (TS EN 13480) | ✅ **Var** | `ExpansionLoopService` — 8 malzeme alfa, U/Z/L-dirsek kol boyu |
| Boru Yaşlanma Modeli (AWWA M11) | ✅ **Var** | `MechanicalProjectSettings.EffectiveRoughness` → `PressureDropService` |
| Gürültü Analizi (TS EN 14366 / DIN 4109) | ✅ **Var** | `PipeNoiseService` — Lw modeli, 3 DIN sınıfı, per-segment uyarı |

---

## 5. Uzun Vadeli Yol Haritası (Kalan İstekler)

| Özellik | Öncelik | Açıklama |
|---|---|---|
| Real-time Çakışma Vurgusu | ✅ **Tamamlandı** | `ClashHighlightService` — Critical=kırmızı, Warning=turuncu; 🔴 Çakışma Vurgusu toggle butonu (Session #32) |
| Bulut Senkronizasyonu | Orta | Proje dosyası cloud backup (Azure/GDrive) |
| Boru Ağı Animasyonu | ✅ **Tamamlandı** | `PipeFlowAnimationService` — DispatcherTimer 30fps, hareketli nokta animasyonu; ▶ Akış Animasyonu toggle butonu (Session #33) |
| Mobil Görüntüleyici | ✅ **Tamamlandı** | `HtmlViewerExportService` — inline SVG, sistem renkleri, pan+zoom JS, mobil viewport; 🌐 Mobil HTML butonu (Session #33) |
| Bulut Senkronizasyonu | ✅ **Tamamlandı** | `CloudBackupService` — zaman damgalı .afney.bak, otomatik yedek, maks 20 yedek; ☁️ Yedekle butonu + CloudBackupDialog (Session #33) |

---

---

## 6. Session #34 — FINE SANİ Karşılaştırması Sonrası (Yeni Tamamlananlar)

Session #34'te yapılan kapsamlı FINE SANİ karşılaştırması (uzman puanı: **5.7/10 → hedef 8.0/10**) sonucu belirlenen kritik eksikler tamamlandı.

| Özellik | Durum | Not |
|---|---|---|
| Soğutma Yük Hesabı (ASHRAE / TS EN 12831-3) | ✅ **Tamamlandı** | `CoolingLoadService` — iletim, güneş kazancı, iç yükler, gizil yük; 18 şehir yaz verisi; Chiller/VRF seçimi · `CoolingDesignDialog` (Session #34) |
| Üretici Ekipman Kataloğu | ✅ **Tamamlandı** | `ManufacturerCatalogService` — Grundfos/Wilo pompa Q/H eğrileri; Valsir/Wavin/Geberit boru sınıfları; Honeywell/Danfoss vanaları + Kv/ΔP · `ManufacturerCatalogDialog` (Session #34) |
| 3D Axonometrik İzometrik Şema | ✅ **Tamamlandı** | `AxonometricExportService` — kabinetik axonometri; kat kesit çizgileri; DN etiket; HTML/SVG · `AxonometricExportDialog` (Session #34) |

*Tüm kritik FINE SANİ eksikleri Session #34 itibarıyla tamamlanmıştır. Tahmini revize puan: 7.5–8.0 / 10.*
