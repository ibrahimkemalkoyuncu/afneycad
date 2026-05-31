# AfneyCAD Geliştirme ve Eksiklik Analizi (Gap Analysis)

> **Son güncelleme:** 2026-05-31 — Session #30 sonrası durum  
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
| Hesap Tablosu Spreadsheet Entegrasyonu | ⚠️ **Kısmi** | Tesisat ekipmanları bağımsız dialog'larda; merkezi tabloya bağlama eksik |

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

## 4. Uzun Vadeli Yol Haritası (Kalan İstekler)

| Özellik | Öncelik | Açıklama |
|---|---|---|
| Hesap Tablosu Ekipman Entegrasyonu | Orta | WaterTank/WaterMeter/ExpansionTank → merkezi spreadsheet'e veri akışı |
| Bulut Senkronizasyonu | Düşük | Proje dosyası cloud backup (Azure/GDrive) |
| Real-time Çakışma Vurgusu | Orta | ClashDetection → viewport'ta kırmızı overlay |
| Boru Ağı Animasyonu | Düşük | Akış yönü ve hız animasyonu |
| Mobil Görüntüleyici | Düşük | Web/mobil read-only görüntüleme |

---

*Tüm Eksiklikler.md §1–3 maddeleri Session #30 itibarıyla tamamlanmıştır.*
