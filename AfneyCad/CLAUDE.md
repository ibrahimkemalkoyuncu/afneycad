# AfneyCAD — Geliştirici Kılavuzu

## Proje Yapısı

```
AfneyCad/
├── src/
│   ├── Afney.Cad.Presentation/    # WPF UI — MainWindow, Dialogs, Views
│   ├── Afney.Cad.Application/     # Servisler (SnapEngine, SelectionManager, MahalExport)
│   ├── Afney.Cad.Commands/        # Çizim & mühendislik komutları (ICadCommand implementasyonları)
│   ├── Afney.Cad.Database/        # CadDatabase, TransactionManager, Persistence
│   ├── Afney.Cad.Domain/          # Entity modelleri, ECS, Blocks
│   ├── Afney.Cad.Geometry/        # Vector3D, Matrix4x4, NURBS, Topology
│   ├── Afney.Cad.Infrastructure/  # DWG/DXF/IFC Import-Export servisleri
│   ├── Afney.Cad.Mechanical/      # MEP kernel, hesaplama motorları, servisler
│   ├── Afney.Cad.Render/          # Skia tabanlı çizim motoru
│   ├── Afney.Cad.SpatialIndex/    # R-Tree uzamsal indeks
│   └── Afney.Cad.Common/          # Paylaşılan yardımcılar
```

## MainWindow Partial Class Haritası

Session #38'de 4745 satırlık MainWindow.xaml.cs 6 dosyaya ayrıldı:

| Dosya | Satır | Sorumluluk |
|-------|-------|------------|
| `MainWindow.xaml.cs` | 365 | Core: fields, ctor, tab/panel/keyboard yönetimi |
| `MainWindow.Commands.cs` | 1031 | Çizim komutları, komut satırı, blok işlemleri |
| `MainWindow.FileOps.cs` | 588 | Dosya aç/kaydet/import/export (DWG/DXF/IFC/PDF/PNG/Excel) |
| `MainWindow.Layers.cs` | 208 | Layer picker, görünürlük, dondurma, sistem katman toggle |
| `MainWindow.Engineering.cs` | 1478 | MEP mühendislik: hesaplama, mahal, BOM, pompa, çakışma |
| `MainWindow.ViewControls.cs` | 135 | Zoom, 2D/3D toggle, OSnap, Ortho, Undo/Redo |

### MainWindow.xaml.cs (Core)
- `MainWindow()` — Constructor, event wiring, AutoSave başlatma
- `CreateNewDocument()` — MDI sekme oluşturma (Database, Kernel, Viewport)
- `Window_KeyDown()` — Global kısayollar (Ctrl+Z/Y/S/C/X/V/L/F, F8)
- `OnTabChanged()` — Sekme değişiminde context güncelleme
- `OnLeftTab_Navigator/Layers()` — Sol panel sekme geçişi
- `OnLayerVisibilityChanged()` — Katman gizle/göster
- `OnEntityModifiedFromRightPanel()` — Sağ panel property değişikliği
- `OnClosed()` — Uygulama kapanışında kaynak temizliği

### MainWindow.Commands.cs
**Temel Çizim:**
- `OnLineCommand`, `OnCircleCommand`, `OnPolylineCommand`, `OnRectangleCommand`
- `OnTrimCommand`, `OnExtendCommand`, `OnMirrorCommand`, `OnExplodeCommand`
- `OnMoveCommand`, `OnCopyCommand`, `OnOffsetCommand`, `OnHatchCommand`

**Boyutlandırma:**
- `OnLinearDimCommand`, `OnAlignedDimCommand`, `OnRadiusDimCommand`, `OnAngularDimCommand`
- `OnContinueDimCommand`, `OnDistCommand`
- `OnDimTextHeightSmall/Medium/Large()`

**Mekanik Çizim:**
- `OnDrawPipeCommand` — Boru çizimi + SyncMechanicalSettings
- `OnPlaceFixtureOnWall` — Duvara vitrifiye yerleştirme
- `OnConnectFixtureCommand`, `OnRiserPipeCommand`, `OnSourcePointCommand`
- `OnRouteDuctCommand`, `OnConnectDuctCommand` — HVAC kanal

**Blok İşlemleri:**
- `OnBlockCommand` — Blok tanımlama (BMakeDialog)
- `OnInsertCommand` — Blok yerleştirme
- `OnWBlockCommand` — Mimari kat hazırlama sihirbazı

**Mimari:**
- `OnArchDetectCommand` — DWG layer'larından duvar/kolon/kapı algılama
- `OnArchBomCommand` — Mimari metraj HTML raporu
- `OnDefineBuilding` — Çok katlı bina tanımlama + 3D montaj

**Komut Satırı:**
- `CommandInput_KeyDown()` — AutoCAD benzeri hızlı komut girişi (L, C, P, TRIM, vb.)
- `ExecuteCommand()` — Komut yönlendirici

**BOM/Metraj:**
- `OnSelectionBomCommand`, `OnHvacBomCommand`, `OnGenerateBOQ_Click`

### MainWindow.FileOps.cs
- `OnNewProject/File/Window()` — Yeni proje/dosya/pencere
- `OnOpenFile()` — DWG/DXF dosya açma
- `LoadDwgInternal()` — DWG import + outlier removal + Z-flattening + paralel işleme
- `OnSave/SaveAs()`, `SaveToFile()` — DWG/DXF kaydetme
- `SaveLayerState/LoadLayerState()` — Gizli katman durumu persist
- `OnExportDwgCommand/DxfCommand()` — Export
- `OnIfcImportCommand/ExportCommand()` — IFC BIM
- `OnExportExcel/Png/PdfExport/HtmlViewer/AxonometricExport()`

### MainWindow.Layers.cs
- `RefreshActiveLayerCombo()` — Katman picker listesini doldur
- `SetActiveLayerUI()` — Aktif katman label/renk güncelle
- `OnLayerPickerBtnClick/NameClick/VisibilityToggle/FreezeToggle/LockToggle()`
- `OnToggleLayerPanel()`, `OnCloseTab_Click()`
- `OnSyncSystemLayers()` — MEP katman senkronizasyonu
- `OnToggleColdWater/HotWater/WasteWater/Fire/Gas/Vent()` — Sistem katman toggle
- `OnShowAllSystems()`, `ToggleSystemLayer()`

### MainWindow.Engineering.cs
**Hesaplama:**
- `OnRecalculateSystem()` — Async hidrolik analiz (TS 1258)
- `OnAutoPipeSizing()` — Otomatik boru çaplandırma
- `OnCalculateFlowCommand()` — Debi hesaplama
- `OnPressureDropCalc()` — Kritik hat basınç kaybı raporu
- `OnPumpSelection()` — Pompa seçimi (Q-H eğrisi)

**Mahal (Oda):**
- `OnSelectRoom()` — Akıllı mahal tanımlama + otomatik vitrifiye
- `OnSmartDetectRoomClick()` — Blok tabanlı oda algılama
- `OnManualMahalDefine()` — Manuel sınır çizimi
- `OnRectMahalDefine()` — Dikdörtgen mahal
- `OnAutoDetectSpacesCommand()` — Tüm odaları otonom bul

**Bağlantı:**
- `OnConnectReceptors()` — Armatürleri ana hatta bağla
- `OnAutoBranchingClick()` — Seçili cihazları boruya bağla
- `OnRiserConnection()` — Yatay-dikey kolon bağlantısı
- `OnDoublePipeRoute()` — Çift hat (sıcak/soğuk) rotalama

**Mimari Analiz:**
- `OnRecognizeArchitecture()` — Katman bazlı mimari tanıma
- `OnClashDetectionClick()` — MEP-mimari çakışma analizi
- `OnPressureMapToggle()`, `OnClashHighlightToggle()` — Görsel overlay

**Raporlama:**
- `OnGenerateBOM()` — Metraj tablosu çizime ekle
- `OnShowBOMReport()` — BOM tablo/metin
- `OnGenerateHydraulicReport()` — HTML hidrolik rapor
- `OnAnalyzeSpecClick()` — Keşif + teknik şartname
- `OnShowIsometricScheme()` — İzometrik şema

**Etiketleme:**
- `OnAutoAnnotate()`, `OnClearAnnotations()` — Otomatik boru etiketi
- `OnCalculationTable()` — Hesap tablosu (DN sync)

**Kütüphane/Katalog:**
- `OnFixtureLibrary/ValveLibrary/ManageCatalog/ArchitecturalLibrary()`
- `OnStandardSelection()` — Standart seçimi (TS/DIN)
- `OnManufacturerCatalog()` — Üretici kataloğu

**Özel Hesaplar:**
- `OnWasteWaterDesign/CalcSheet()`, `OnRainWaterCalc()`, `OnGasCalc()`
- `OnSepticTankDesign()`, `OnFireFightingDesign()`
- `OnHeatingDesign()`, `OnHvacDesign()`, `OnCoolingDesign()`
- `OnHotWaterCirculation()`, `OnPressureZoneDesign()`, `OnPipeCostAnalysis()`
- `OnAutoSizing()` — Otomatik boyutlandırma

**Araçlar:**
- `OnPipe3DView()`, `OnMultiStoryManager()`, `OnWallParallelRoute()`
- `OnBimProperties/SmartBimConvert()` — BIM özellikleri
- `OnFlowAnimationToggle()` — Akış animasyonu
- `OnCloudBackup()` — Bulut yedekleme
- `OnAuditSystem()` — Sistem validasyonu (Flow Lock)

### MainWindow.ViewControls.cs
- `OnZoomExtents()`, `OnToggle2DView/3DView()`
- `OnToggleProjectNavigator/IntelligencePanel()`
- `OnOsnapMasterToggle/FlagToggle()` — OSNAP kontrolleri
- `OnOrthoModeToggle()` — Ortho modu (F8)
- `OnUndo/Redo()`, `UpdateUndoLabels()`

## Build

```bash
dotnet build
```

Target: .NET 10, WPF (Windows only).
