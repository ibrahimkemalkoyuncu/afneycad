# AfneyCAD — Metod İndeksi

> **Otomatik oluşturuldu** | Güncelleme: 2026-07-06
> Projenin tüm katmanlarındaki public/private metodların Türkçe açıklamalı indeksidir.
> Toplam belgelenen metod: 300+

---

## İçindekiler

1. [Afney.Cad.Presentation](#afneycadpresentation) — UI, MainWindow partial sınıfları
2. [Afney.Cad.Commands](#afneycadcommands) — Çizim ve mühendislik komutları
3. [Afney.Cad.Application](#afneycadapplication) — Uygulama servisleri (Snap, Selection, Export)
4. [Afney.Cad.Database](#afneycaddatabase) — Veritabanı, TransactionManager, Operations
5. [Afney.Cad.Domain](#afneycaddomain) — Entity modelleri, CadLayer, abstractions
6. [Afney.Cad.Mechanical](#afneycadmechanical) — MEP kernel, topoloji, servisler
7. [Afney.Cad.Geometry](#afneycadgeometry) — Vector3D, BoundingBox, GeomUtils
8. [Afney.Cad.Render](#afneycadrender) — SkiaRenderContext, IRenderContext
9. [Afney.Cad.Infrastructure](#afneycadinfrastructure) — DWG/DXF/IFC import-export

---
Artık tüm anahtar dosyaları okudum. Kapsamlı belgeleme raporunu sunuyorum.

---

## AfneyCad Proje Kaynak Kod Dokümantasyonu

---

## Afney.Cad.Presentation

### MainWindow.xaml.cs — `MainWindow : Window` (partial)

Namespace: `Afney.Cad.Presentation`

| Metod / Üye | İmza | Açıklama |
|---|---|---|
| `CreateNewDocument` | `CadDocumentContext CreateNewDocument(string title, string? filePath)` | Yeni bir sekme oluşturur: CadDatabase, TransactionManager, SnapEngine, SelectionManager ve MechanicalKernel başlatır; sekmeye ekler. |
| `CreateTabContent` | `TabItem CreateTabContent(Control viewport)` | Verilen viewport kontrolü için sekme içeriği (başlık çubuğu + kapat butonu) oluşturur. |
| `MainWindow_Closing` | `void MainWindow_Closing(object sender, CancelEventArgs e)` | Pencere kapanırken kullanıcıya onay sorar, AutoSave servisini durdurur. |
| `ApplyUserSettings` | `void ApplyUserSettings()` | Kaydedilmiş kullanıcı ayarlarını (tema, ızgara aralığı, ölçü birimi vb.) arayüze yansıtır. |
| `OnEntityModifiedFromRightPanel` | `void OnEntityModifiedFromRightPanel(CadEntity entity)` | Sağ panel özellik düzenleyicisinden yapılan değişiklikleri veritabanına ve topoloji grafına işler. |
| `Window_PreviewTextInput` | `void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)` | Klavyeden girilen karakterleri komut satırına yönlendirir (AutoCAD tarzı). |
| `OnLayerVisibilityChanged` | `void OnLayerVisibilityChanged(string layerName, bool visible)` | Belirtilen katmanı gizler/gösterir ve viewport'u yeniler. |
| `Window_KeyDown` | `void Window_KeyDown(object sender, KeyEventArgs e)` | F3=OSNAP, F10=Polar, ESC=İptal, DELETE=Sil, CTRL+Z/Y=Geri Al/Yinele gibi kısayolları işler. |
| `OnTabChanged` | `void OnTabChanged(object sender, SelectionChangedEventArgs e)` | Aktif sekme değiştiğinde `_activeContext`'i günceller, katman listesini ve araç çubuklarını yeniden yükler. |
| `OnLeftTab_Navigator` | `void OnLeftTab_Navigator(object sender, RoutedEventArgs e)` | Sol panelde Proje Gezgini sekmesini etkinleştirir. |
| `OnLeftTab_Layers` | `void OnLeftTab_Layers(object sender, RoutedEventArgs e)` | Sol panelde Katmanlar sekmesini etkinleştirir. |
| `ToggleLeftPanel` | `void ToggleLeftPanel()` | Sol paneli açar/kapatır. |
| `OnClosed` | `void OnClosed(object sender, EventArgs e)` | Uygulama tamamen kapandığında temizlik (dispose) işlemlerini yapar. |

---

### MainWindow.Commands.cs — `MainWindow` (partial)

| Metod | İmza | Açıklama |
|---|---|---|
| `OnLineCommand` | `void OnLineCommand(object s, RoutedEventArgs e)` | `LineCommand` komutunu başlatır. |
| `OnCircleCommand` | `void OnCircleCommand(object s, RoutedEventArgs e)` | `CircleCommand` komutunu başlatır. |
| `OnTrimCommand` | `void OnTrimCommand(object s, RoutedEventArgs e)` | `TrimCommand` (Buda) komutunu başlatır; mevcut zoom değerini tolerans olarak aktarır. |
| `OnExtendCommand` | `void OnExtendCommand(object s, RoutedEventArgs e)` | `ExtendCommand` komutunu başlatır. |
| `OnMirrorCommand` | `void OnMirrorCommand(object s, RoutedEventArgs e)` | Seçili nesnelerle `MirrorCommand` komutunu başlatır. |
| `OnExplodeCommand` | `void OnExplodeCommand(object s, RoutedEventArgs e)` | `ExplodeCommand` komutunu başlatır; blokları parçalar. |
| `OnMoveCommand` | `void OnMoveCommand(object s, RoutedEventArgs e)` | Seçili nesnelerle `MoveCommand` komutunu başlatır. |
| `OnCopyCommand` | `void OnCopyCommand(object s, RoutedEventArgs e)` | `CopyCommand` komutunu başlatır. |
| `OnLinearDimCommand` | `void OnLinearDimCommand(object s, RoutedEventArgs e)` | Yatay/düşey ölçü çizgisi komutunu başlatır. |
| `OnAlignedDimCommand` | `void OnAlignedDimCommand(object s, RoutedEventArgs e)` | Paralel (hizalı) ölçü komutunu başlatır. |
| `OnRadiusDimCommand` | `void OnRadiusDimCommand(object s, RoutedEventArgs e)` | Yarıçap ölçü komutunu başlatır. |
| `OnAngularDimCommand` | `void OnAngularDimCommand(object s, RoutedEventArgs e)` | Açı ölçü komutunu başlatır. |
| `OnContinueDimCommand` | `void OnContinueDimCommand(object s, RoutedEventArgs e)` | Zincir ölçü (devam) komutunu başlatır. |
| `OnMTextCommand` | `void OnMTextCommand(object s, RoutedEventArgs e)` | Çok satırlı metin ekleme komutunu başlatır. |
| `OnPolylineCommand` | `void OnPolylineCommand(object s, RoutedEventArgs e)` | `PolylineCommand` komutunu başlatır. |
| `OnRectangleCommand` | `void OnRectangleCommand(object s, RoutedEventArgs e)` | `RectangleCommand` komutunu başlatır. |
| `OnHatchCommand` | `void OnHatchCommand(object s, RoutedEventArgs e)` | Tarama (Hatch) komutunu başlatır. |
| `OnDrawPipeCommand` | `void OnDrawPipeCommand(object s, RoutedEventArgs e)` | `RoutePipeCommand` komutunu başlatır; sistem tipini araç çubuğundan okur. |
| `OnRouteDuctCommand` | `void OnRouteDuctCommand(object s, RoutedEventArgs e)` | `RouteDuctCommand` komutunu başlatır. |
| `OnConnectDuctCommand` | `void OnConnectDuctCommand(object s, RoutedEventArgs e)` | `ConnectDuctCommand` komutunu başlatır. |
| `OnConnectFixtureCommand` | `void OnConnectFixtureCommand(object s, RoutedEventArgs e)` | `ConnectFixtureCommand` komutunu başlatır. |
| `OnRiserPipeCommand` | `void OnRiserPipeCommand(object s, RoutedEventArgs e)` | Kolon borusu çizme komutunu başlatır. |
| `OnSourcePointCommand` | `void OnSourcePointCommand(object s, RoutedEventArgs e)` | Kaynak nokta (su girişi) komutunu başlatır. |
| `SyncMechanicalSettings` | `void SyncMechanicalSettings(RoutePipeCommand? cmd)` | Araç çubuğundaki çap, sistem tipi ve eğim ayarlarını komut nesnesine senkronize eder. |
| `GetActiveSystemType` | `MechanicalSystemType GetActiveSystemType()` | Araç çubuğundaki sistem seçicisinden aktif sistem tipini döndürür. |
| `OnWBlockCommand` | `void OnWBlockCommand(object s, RoutedEventArgs e)` | Seçili nesneleri harici blok (WBLOCK) olarak kaydeder. |
| `OnBlockCommand` | `void OnBlockCommand(object s, RoutedEventArgs e)` | Blok tanımlama (`BlockCommand`) komutunu başlatır. |
| `OnInsertCommand` | `void OnInsertCommand(object s, RoutedEventArgs e)` | Blok ekleme (`InsertCommand`) komutunu başlatır. |
| `OnDefineMahalCommand` | `void OnDefineMahalCommand(object s, RoutedEventArgs e)` | Mahal (oda) tanımlama komutunu başlatır. |
| `OnInspectMahalCommand` | `void OnInspectMahalCommand(object s, RoutedEventArgs e)` | `MahalInspectCommand` komutunu başlatır. |
| `OnRiserGenerateCommand` | `void OnRiserGenerateCommand(object s, RoutedEventArgs e)` | Kolon şeması oluşturma komutunu başlatır. |
| `OnSmartLabelCommand` | `void OnSmartLabelCommand(object s, RoutedEventArgs e)` | `SmartLabelCommand` komutunu başlatır. |
| `OnLegendCommand` | `void OnLegendCommand(object s, RoutedEventArgs e)` | Lejant oluşturma komutunu başlatır. |
| `OnGenerateBOQ_Click` | `void OnGenerateBOQ_Click(object s, RoutedEventArgs e)` | Malzeme metrajı (BOQ) raporu oluşturur. |
| `OnIfcExportCommand` | `void OnIfcExportCommand(object s, RoutedEventArgs e)` | IFC formatında ihracat başlatır. |
| `OnArchBomCommand` | `void OnArchBomCommand(object s, RoutedEventArgs e)` | Mimari eleman metraj raporunu üretir. |
| `OnHvacBomCommand` | `void OnHvacBomCommand(object s, RoutedEventArgs e)` | HVAC eleman metraj raporunu üretir. |
| `OnSelectionBomCommand` | `void OnSelectionBomCommand(object s, RoutedEventArgs e)` | Yalnızca seçili nesnelerin metrajını üretir. |
| `OnDefineBuilding` | `void OnDefineBuilding(object s, RoutedEventArgs e)` | Çok katlı bina tanımı diyaloğunu açar, kat aktivasyonu sağlar. |
| `CommandInput_KeyDown` | `void CommandInput_KeyDown(object s, KeyEventArgs e)` | Komut satırından yazılan 40+ komutu (L, C, TR, MI vb.) çözümler ve ilgili komut metodunu çağırır. |
| `ExecuteCommand` | `void ExecuteCommand(string commandName)` | Komut adıyla programatik komut gönderimi yapar. |

---

### MainWindow.FileOps.cs — `MainWindow` (partial)

| Metod | İmza | Açıklama |
|---|---|---|
| `OnNewProject` | `void OnNewProject(object s, RoutedEventArgs e)` | Tüm sekmeleri kapatır, yeni boş proje başlatır. |
| `OnNewFile` | `void OnNewFile(object s, RoutedEventArgs e)` | Mevcut belgeye yeni boş çizim sekmesi ekler. |
| `OnNewWindow` | `void OnNewWindow(object s, RoutedEventArgs e)` | Yeni bir ana pencere örneği açar. |
| `OnOpenFile` | `void OnOpenFile(object s, RoutedEventArgs e)` | Dosya açma diyaloğu gösterir; DWG/DXF seçilen dosyayı `LoadDwgInternal`'a iletir. |
| `LoadDwgInternal` | `async Task LoadDwgInternal(string filePath)` | Arka plan Task içinde DWG/DXF dosyasını okur; `DwgImportService` veya `DxfImportService` kullanır. |
| `LoadDwgEntities` | `void LoadDwgEntities(string path, List<CadEntity> entities, Stopwatch sw)` | İçe aktarılan entity listesini işler: >500 km aykırı değerleri filtreler, sıfır uzunluklu çizgileri eler, katmanları çıkarır, mahal analizini başlatır. |
| `OnSave` | `void OnSave(object s, RoutedEventArgs e)` | Mevcut dosya yoluna kaydeder; yol yoksa `OnSaveAs`'i çağırır. |
| `OnSaveAs` | `void OnSaveAs(object s, RoutedEventArgs e)` | Farklı kaydet diyaloğunu açar, `SaveToFile`'a iletir. |
| `SaveToFile` | `void SaveToFile(string filePath)` | DXF (`DxfWriterService`) veya DWG (`DwgExportService`) olarak seçilen yola yazar. |
| `SaveLayerState` | `void SaveLayerState(string filePath)` | Katman görünürlük durumlarını `.layerstate` sidecar dosyasına JSON olarak kaydeder. |
| `LoadLayerState` | `void LoadLayerState(string filePath)` | `.layerstate` dosyasından katman durumlarını okuyup uygular. |
| `OnExportDwgCommand` | `void OnExportDwgCommand(object s, RoutedEventArgs e)` | DWG formatında ihracat. |
| `OnExportDxfCommand` | `void OnExportDxfCommand(object s, RoutedEventArgs e)` | DXF R12 formatında ihracat. |
| `OnExportExcel` | `void OnExportExcel(object s, RoutedEventArgs e)` | `ExcelExportService` ile .xlsx ihracat. |
| `OnPdfExport` | `void OnPdfExport(object s, RoutedEventArgs e)` | PDF olarak çizimi dışa aktarır. |
| `OnRiserDiagramExport` | `void OnRiserDiagramExport(object s, RoutedEventArgs e)` | Kolon şemasını PNG/DXF/HTML olarak dışa aktarır. |
| `OnExportPng` | `void OnExportPng(object s, RoutedEventArgs e)` | Viewport'u PNG resim dosyası olarak kaydeder. |
| `OnExportHtmlViewer` | `void OnExportHtmlViewer(object s, RoutedEventArgs e)` | Tarayıcıda açılabilir interaktif HTML görüntüleyici oluşturur. |
| `OnAxonometricExport` | `void OnAxonometricExport(object s, RoutedEventArgs e)` | İzometrik aksimetrik görünümü dışa aktarır. |
| `OnIfcImportCommand` | `void OnIfcImportCommand(object s, RoutedEventArgs e)` | IFC dosyası içe aktarma diyaloğunu açar. |
| `OnDwgImportDialog` | `void OnDwgImportDialog(object s, RoutedEventArgs e)` | Başka bir DWG'yi mevcut çizime aktarma diyaloğunu açar. |
| `OnExportMahalData` | `void OnExportMahalData(object s, RoutedEventArgs e)` | Oda/mahal verilerini JSON'a aktarmak için `MahalExportService` kullanır. |
| `OnManufacturerCatalog` | `void OnManufacturerCatalog(object s, RoutedEventArgs e)` | Üretici kataloğu ekranını açar. |

---

### MainWindow.Layers.cs — `MainWindow` (partial)

| Metod | İmza | Açıklama |
|---|---|---|
| `RefreshActiveLayerCombo` | `void RefreshActiveLayerCombo(CadDatabase db)` | Katman açılır listesini yeniden oluşturur; "0" katmanını en başa koyar. |
| `SetActiveLayerUI` | `void SetActiveLayerUI(string name, string colorBrush)` | Araç çubuğundaki aktif katman adını ve renk kutusunu günceller. |
| `UpdateToolbarLayerIndicator` | `void UpdateToolbarLayerIndicator(string layerName)` | Katmanın uint renk değerini hex'e çevirerek araç çubuğuna yazar. |
| `OnLayerPickerBtnClick` | `void OnLayerPickerBtnClick(object s, RoutedEventArgs e)` | Katman seçici açılır penceresini açar. |
| `OnLayerNameClick` | `void OnLayerNameClick(object s, RoutedEventArgs e)` | Tıklanan katmanı aktif katman olarak ayarlar. |
| `OnLayerVisibilityToggle_Click` | `void OnLayerVisibilityToggle_Click(object s, RoutedEventArgs e)` | Katmanın görünürlüğünü açar/kapatır. |
| `OnLayerFreezeToggle_Click` | `void OnLayerFreezeToggle_Click(object s, RoutedEventArgs e)` | Katmanı dondurur/çözer. |
| `OnLayerLockToggle_Click` | `void OnLayerLockToggle_Click(object s, RoutedEventArgs e)` | Katmanı kilitler/kilidini açar. |
| `OnToggleLayerPanel` | `void OnToggleLayerPanel(object s, RoutedEventArgs e)` | Sol katman panelini açar/kapatır. |
| `OnCloseTab_Click` | `void OnCloseTab_Click(object s, RoutedEventArgs e)` | Üzerindeki sekmeyi kapatır; belge bağlamını temizler. |
| `OnSyncSystemLayers` | `void OnSyncSystemLayers(object s, RoutedEventArgs e)` | MEP sistem katmanlarını veri tabanındaki standart isimlere göre oluşturur/günceller. |
| `OnToggleColdWater` | `void OnToggleColdWater(object s, RoutedEventArgs e)` | Soğuk su katmanını görünür/gizli yapar. |
| `OnToggleHotWater` | `void OnToggleHotWater(object s, RoutedEventArgs e)` | Sıcak su katmanını görünür/gizli yapar. |
| `OnToggleWasteWater` | `void OnToggleWasteWater(object s, RoutedEventArgs e)` | Pis su katmanını görünür/gizli yapar. |
| `OnToggleFire` | `void OnToggleFire(object s, RoutedEventArgs e)` | Yangın sistemi katmanını görünür/gizli yapar. |
| `OnToggleGas` | `void OnToggleGas(object s, RoutedEventArgs e)` | Gaz sistemi katmanını görünür/gizli yapar. |
| `OnToggleVent` | `void OnToggleVent(object s, RoutedEventArgs e)` | Havalandırma katmanını görünür/gizli yapar. |
| `OnShowAllSystems` | `void OnShowAllSystems(object s, RoutedEventArgs e)` | Tüm MEP sistem katmanlarını görünür yapar. |
| `ToggleSystemLayer` | `void ToggleSystemLayer(string layerName, Button btn)` | Belirtilen katman adını bulur, görünürlüğünü tersine çevirir, buton rengini günceller. |

---

### MainWindow.Engineering.cs — `MainWindow` (partial)

| Metod | İmza | Açıklama |
|---|---|---|
| `OnRecalculateSystem` | `async void OnRecalculateSystem(object s, RoutedEventArgs e)` | Tüm MEP sistemini `MechanicalKernel.RecalculateProject` ile asenkron olarak analiz eder. |
| `OnPressureDropCalc` | `void OnPressureDropCalc(object s, RoutedEventArgs e)` | Kritik hat basınç düşümü hesabını tetikler. |
| `OnAutoPipeSizing` | `void OnAutoPipeSizing(object s, RoutedEventArgs e)` | TS 1258'e göre tüm borulara otomatik çap ataması yapar. |
| `OnGenerateLegend` | `void OnGenerateLegend(object s, RoutedEventArgs e)` | Çizim üzerinde MEP lejant tablosu oluşturur. |
| `OnCalculateFlowCommand` | `void OnCalculateFlowCommand(object s, RoutedEventArgs e)` | Toplam debi ve yükleme birimi hesabı yapar. |
| `OnLevelManager` | `void OnLevelManager(object s, RoutedEventArgs e)` | Kat yöneticisi diyaloğunu açar. |
| `OnBuildingProperties` | `void OnBuildingProperties(object s, RoutedEventArgs e)` | Bina özellikleri (kat sayısı, yükseklikler) diyaloğunu açar. |
| `OnAutoDetectSpacesCommand` | `void OnAutoDetectSpacesCommand(object s, RoutedEventArgs e)` | `AutoDetectSpacesCommand` ile çizimdeki odaları otomatik tanımlar. |
| `OnSelectRoom` | `void OnSelectRoom(object s, RoutedEventArgs e)` | Oda seçim iş akışını başlatır; AutoLayout ile vitrifiye yerleşimi önerir. |
| `OnManualMahalDefine` | `void OnManualMahalDefine(object s, RoutedEventArgs e)` | `ManualMahalCommand` komutunu başlatır; kullanıcı köşeleri manuel çizer. |
| `OnRectMahalDefine` | `void OnRectMahalDefine(object s, RoutedEventArgs e)` | `RectMahalCommand` komutuyla iki köşe tıklamasıyla dikdörtgen mahal tanımlar. |
| `OnSmartDetectRoomClick` | `void OnSmartDetectRoomClick(object s, RoutedEventArgs e)` | Duvar geometrisini analiz ederek odaları otomatik tespit eder. |
| `OnConnectReceptors` | `void OnConnectReceptors(object s, RoutedEventArgs e)` | `ConnectFixtureCommand` komutunu başlatır. |
| `OnAutoBranchingClick` | `void OnAutoBranchingClick(object s, RoutedEventArgs e)` | `AutoBranchingService` ile vitriflyeleri en yakın boruya otomatik bağlar. |
| `OnRiserAutoPosition` | `void OnRiserAutoPosition(object s, RoutedEventArgs e)` | Kolon borularını kat planlarına göre otomatik konumlandırır. |
| `OnRiserConnection` | `void OnRiserConnection(object s, RoutedEventArgs e)` | Kolonları yatay dallara bağlar. |
| `OnRecognizeArchitecture` | `void OnRecognizeArchitecture(object s, RoutedEventArgs e)` | `ArchitecturalRecognitionService` ile DWG katmanlarını BIM engellerine dönüştürür. |
| `OnGenerateBOM` | `void OnGenerateBOM(object s, RoutedEventArgs e)` | Tüm sistem için BOM (Bill of Materials) raporu oluşturur. |
| `OnClashDetectionClick` | `void OnClashDetectionClick(object s, RoutedEventArgs e)` | `ClashDetectionService` ile MEP çakışmalarını tespit eder. |
| `OnGenerateHydraulicReport` | `void OnGenerateHydraulicReport(object s, RoutedEventArgs e)` | Hidrolik hesap föyünü (HTML/Excel) oluşturur. |
| `OnPumpSelection` | `void OnPumpSelection(object s, RoutedEventArgs e)` | Pompa seçim diyaloğunu açar. |
| `OnBimProperties` | `void OnBimProperties(object s, RoutedEventArgs e)` | Seçili nesnenin BIM özelliklerini gösterir. |
| `OnSmartBimConvert` | `void OnSmartBimConvert(object s, RoutedEventArgs e)` | Seçili CAD geometrisini BIM nesnesine dönüştürür. |
| `OnArchitecturalLibrary` | `void OnArchitecturalLibrary(object s, RoutedEventArgs e)` | Mimari eleman kütüphanesi penceresini açar. |
| `OnAnalyzeSpecClick` | `void OnAnalyzeSpecClick(object s, RoutedEventArgs e)` | Özellik analizi diyaloğunu açar. |
| `OnShowIsometricScheme` | `async void OnShowIsometricScheme(object s, RoutedEventArgs e)` | Kolon şemasını asenkron üretir; HTML/DXF/PNG formatlarında gösterir. |
| `GenerateIsometricHtml` | `string GenerateIsometricHtml(IEnumerable<PipeEntity>, IEnumerable<SanitaryFixtureEntity>, int floor)` | SVG+HTML kolon şeması oluşturur; kat bazlı özet tablolar içerir. |
| `ResolveFloorLevels` | `static Dictionary<Guid, int> ResolveFloorLevels(IEnumerable<CadEntity>)` | 3 strateji ile kat Z değerlerini çözer: gerçek Z, katman adı veya sanal indeks. |
| `ParseFloorFromLayer` | `static int? ParseFloorFromLayer(string? layerName)` | "KAT_2", "GROUND" gibi katman isimlerinden kat numarasını çıkarır. |
| `NearestPipeZ` | `static double NearestPipeZ(SanitaryFixtureEntity, IEnumerable<PipeEntity>)` | Vitrifiyenin Z koordinatını en yakın borudan atar. |
| `BuildRiserPrimitives` | `List<RiserPrimitive> BuildRiserPrimitives(IEnumerable<PipeEntity>, IEnumerable<SanitaryFixtureEntity>)` | HTML/DXF/PNG çıktısı için kolon şeması geometri primitiflerini oluşturur. |

---

### MainWindow.ViewControls.cs — `MainWindow` (partial)

| Metod | İmza | Açıklama |
|---|---|---|
| `OnZoomExtents` | `void OnZoomExtents(object s, RoutedEventArgs e)` | Viewport'u tüm nesneleri gösterecek şekilde otomatik zum yapar. |
| `OnToggleProjectNavigator` | `void OnToggleProjectNavigator(object s, RoutedEventArgs e)` | Proje Gezgini panelini açar/kapatır. |
| `OnToggleIntelligencePanel` | `void OnToggleIntelligencePanel(object s, RoutedEventArgs e)` | Sağ (Zeka/Özellikler) paneli açar/kapatır. |
| `OnToggle2DView` | `void OnToggle2DView(object s, RoutedEventArgs e)` | Viewport'u 2D plan görünümüne geçirir; View2DBtn'i cyan, View3DBtn'i gri yapar. |
| `OnToggle3DView` | `void OnToggle3DView(object s, RoutedEventArgs e)` | Viewport'u 3D izometrik görünüme geçirir; View3DBtn'i turuncu yapar. |
| `OnOsnapModeToggle` | `void OnOsnapModeToggle(object s, RoutedEventArgs e)` | OSNAP'ı açar/kapatır (F3 kısayolu). |
| `OnPolarModeToggle` | `void OnPolarModeToggle(object s, RoutedEventArgs e)` | Polar izleme modunu açar/kapatır (F10). |
| `OnOsnapMasterToggle` | `void OnOsnapMasterToggle(object s, RoutedEventArgs e)` | Tüm OSNAP modlarını tek butonla açar/kapatır; renk geri bildirimi verir. |
| `OnOsnapFlagToggle` | `void OnOsnapFlagToggle(object s, RoutedEventArgs e)` | Endpoint/Midpoint/Center/Perpendicular bayraklarını ayrı ayrı açar/kapatır. |
| `OnOrthoModeToggle` | `void OnOrthoModeToggle(object s, RoutedEventArgs e)` | Ortho modunu (yalnızca yatay/dikey çizim) etkinleştirir/devre dışı bırakır. |
| `OnUndo` | `void OnUndo(object s, RoutedEventArgs e)` | `CommandHistory.Undo()` çağırır, viewport'u yeniler, durum çubuğuna geri alınan işlemi yazar. |
| `OnRedo` | `void OnRedo(object s, RoutedEventArgs e)` | `CommandHistory.Redo()` çağırır, viewport'u yeniler. |
| `UpdateUndoLabels` | `void UpdateUndoLabels()` | Geri Al/Yinele buton etiketlerini günceller (stub). |

---

## Afney.Cad.Commands

### Abstractions/ICadCommand.cs — `ICadCommand`

| Üye | İmza | Açıklama |
|---|---|---|
| `CommandName` | `string CommandName { get; }` | Komutun adı (Örn: "LINE"). |
| `ActivePoint` | `Vector3D? ActivePoint { get; }` | Komutun şu anki referans noktası (rubber band için). |
| `Start` | `void Start()` | Komutu başlatır, kullanıcıya ilk adım mesajını verir. |
| `OnPointerPressed` | `void OnPointerPressed(Vector3D point)` | Kullanıcı tıklamasını işler. |
| `OnPointerMoved` | `void OnPointerMoved(Vector3D point)` | Mouse hareketini işler; ghost güncellemesi için. |
| `OnKeyDown` | `void OnKeyDown(InputKey key)` | Klavye girişini işler (ENTER, ESC vb.). |
| `Draw` | `void Draw(IRenderContext context)` | Komut tamamlanmadan önce geçici (ghost) çizim yapar. |
| `Cancel` | `void Cancel()` | Komutu iptal eder, geçici verileri temizler. |
| `OnFeedback` | `event Action<string> OnFeedback` | Kullanıcıya mesaj gönderme olayı. |
| `OnCompleted` | `event Action OnCompleted` | Komut tamamlandığında tetiklenir. |

---

### BasicCommands/LineCommand.cs — `LineCommand : ICadCommand`

| Metod | İmza | Açıklama |
|---|---|---|
| `LineCommand` | `LineCommand(CadDatabase, TransactionManager)` | Veritabanı ve transaction yöneticisiyle çizgi komutu başlatır. |
| `Start` | `void Start()` | "LINE: İlk noktayı belirtin." mesajı gönderir. |
| `OnPointerPressed` | `void OnPointerPressed(Vector3D)` | İlk tıklamada başlangıç noktasını saklar; ikinci tıklamada `AddEntityOperation` ile kalıcı çizgi ekler ve nokta zincirini sürdürür. |
| `OnPointerMoved` | `void OnPointerMoved(Vector3D)` | Ghost çizgisinin bitiş noktasını mouse pozisyonuna taşır (lastik bant). |
| `OnKeyDown` | `void OnKeyDown(InputKey)` | ENTER/SPACE ile komutu tamamlar. |
| `Draw` | `void Draw(IRenderContext)` | Ghost çizgiyi ekranda gösterir. |
| `Cancel` | `void Cancel()` | Ghost ve başlangıç noktasını temizler. |

---

### BasicCommands/CircleCommand.cs — `CircleCommand : ICadCommand`

| Metod | İmza | Açıklama |
|---|---|---|
| `CircleCommand` | `CircleCommand(CadDatabase, TransactionManager)` | Merkez-yarıçap yöntemiyle çember komutu. |
| `Start` | `void Start()` | "CIRCLE: Merkez noktasını belirtin." mesajı gönderir. |
| `OnPointerPressed` | `void OnPointerPressed(Vector3D)` | İlk tıklamada merkezi saklar; ikinci tıklamada uzaklığı yarıçap olarak hesaplar, `AddEntityOperation` ile ekler. |
| `OnPointerMoved` | `void OnPointerMoved(Vector3D)` | Ghost çemberin yarıçapını anlık günceller. |
| `OnKeyDown` | `void OnKeyDown(InputKey)` | Stub (kullanılmaz). |
| `Draw` | `void Draw(IRenderContext)` | Ghost çemberi çizer. |
| `Cancel` | `void Cancel()` | Ghost ve merkez noktasını temizler. |
| `CalculateDistance` | `double CalculateDistance(Vector3D, Vector3D)` | İki nokta arasındaki 2D Öklid mesafesini hesaplar. |

---

### BasicCommands/MoveCommand.cs — `MoveCommand : ICadCommand`

| Metod | İmza | Açıklama |
|---|---|---|
| `MoveCommand` | `MoveCommand(CadDatabase, TransactionManager, IEnumerable<CadEntity>)` | Seçili nesnelerle taşıma komutu başlatır. |
| `Start` | `void Start()` | Seçim yoksa komutu iptal eder; varsa "Baz noktasını belirtin." mesajı gönderir. |
| `OnPointerPressed` | `void OnPointerPressed(Vector3D)` | İlk tıklamada baz noktasını saklar; ikinci tıklamada delta vektörü hesaplayarak `CompositeOperation` içinde tüm nesnelere `MoveEntityOperation` uygular. |
| `OnPointerMoved` | `void OnPointerMoved(Vector3D)` | Mouse pozisyonunu kaydeder (rubber band için). |
| `Draw` | `void Draw(IRenderContext)` | Baz noktası-mouse arası kesikli çizgi çizer. |
| `Cancel` | `void Cancel()` | Baz noktasını temizler. |

---

### BasicCommands/TrimCommand.cs — `TrimCommand : ICadCommand`

| Metod | İmza | Açıklama |
|---|---|---|
| `TrimCommand` | `TrimCommand(CadDatabase, TransactionManager, double currentZoom)` | Zoom'a bağlı hit toleransı ile buda komutu. |
| `Start` | `void Start()` | "TRIM (Buda): Budanacak kısmı seçin." mesajı gönderir. |
| `OnPointerPressed` | `void OnPointerPressed(Vector3D)` | Tıklanan noktaya en yakın çizgi/boruyu bulur; kesişimleri `DoSegmentsIntersect` ile saptayıp T parametresi aralıklarına göre hedef nesneyi böler ve iki parça oluşturur. |
| `GetTParameter` | `double GetTParameter(Vector3D A, Vector3D B, Vector3D P)` | P noktasının AB doğrusu üzerindeki projeksiyon parametresini (0-1) hesaplar. |
| `CloneWithNewPoints` | `CadEntity CloneWithNewPoints(CadEntity, Vector3D, Vector3D)` | Kaynak entity'yi klonlayıp uç noktalarını değiştirir. |
| `Cancel` | `void Cancel()` | `OnCompleted` tetikler. |

---

### BasicCommands/MirrorCommand.cs — `MirrorCommand : ICadCommand`

| Metod | İmza | Açıklama |
|---|---|---|
| `MirrorCommand` | `MirrorCommand(CadDatabase, TransactionManager, IEnumerable<CadEntity>)` | Seçili nesnelerle ayna komutu. |
| `Start` | `void Start()` | Seçim yoksa iptal eder; varsa eksen ilk noktasını ister. |
| `OnPointerPressed` | `void OnPointerPressed(Vector3D)` | İlk tıklamada eksen başını saklar; ikinci tıklamada `ApplyMirror` çağırır. |
| `OnPointerMoved` | `void OnPointerMoved(Vector3D)` | Ghost nesneleri günceller. |
| `UpdateGhosts` | `void UpdateGhosts(Vector3D secondPoint)` | `Matrix4x4.Reflection` ile seçili nesnelerin önizleme kopyalarını üretir. |
| `ApplyMirror` | `void ApplyMirror(Vector3D secondPoint)` | Yansıma matrisi hesaplar, klonları dönüştürür, `CompositeOperation` ile ekler. |
| `Draw` | `void Draw(IRenderContext)` | Eksen çizgisini (kesikli) ve ghost nesneleri çizer. |
| `Cancel` | `void Cancel()` | Ghost nesneleri temizler. |

---

### Engine/CommandManager.cs — `CommandManager`

| Metod | İmza | Açıklama |
|---|---|---|
| `CommandManager` | `CommandManager()` | Yeni bir komut yöneticisi oluşturur. |
| `StartCommand` | `void StartCommand(ICadCommand command)` | Önceki komutu iptal edip yeni komutu aktif eder, yaşam döngüsünü başlatır. |
| `ProcessPointerPressed` | `void ProcessPointerPressed(Vector3D location)` | Tıklama koordinatını aktif komuta iletir. |
| `ProcessPointerMoved` | `void ProcessPointerMoved(Vector3D location)` | Mouse konumunu aktif komuta iletir. |
| `ProcessKeyDown` | `void ProcessKeyDown(InputKey key)` | ESC ile iptal, diğer tuşları aktif komuta iletir. |
| `CancelCommand` | `void CancelCommand()` | Aktif komutu iptal eder, olay aboneliklerini keser. |
| `DrawGhost` | `void DrawGhost(IRenderContext ctx)` | Aktif komutun ghost çizimini render eder. |
| `IsCommandActive` | `bool IsCommandActive { get; }` | Aktif komut olup olmadığını döndürür. |
| `CommandFeedback` | `event Action<string> CommandFeedback` | Komutlardan gelen mesajları yayınlar. |

---

### History/CommandHistory.cs — `CommandHistory`

| Metod | İmza | Açıklama |
|---|---|---|
| `CommandHistory` | `CommandHistory(TransactionManager)` | TransactionManager'ı sarar; StateChanged olayını bağlar. |
| `Undo` | `void Undo()` | `TransactionManager.Undo()` çağırır. |
| `Redo` | `void Redo()` | `TransactionManager.Redo()` çağırır. |
| `GetUndoText` | `string GetUndoText()` | Geri alınacak işlemin adıyla buton etiketi döndürür. |
| `GetRedoText` | `string GetRedoText()` | Yinelenecek işlemin adıyla buton etiketi döndürür. |
| `CanUndo` | `bool CanUndo { get; }` | Geri alınabilir işlem var mı? |
| `CanRedo` | `bool CanRedo { get; }` | Yinelenebilir işlem var mı? |
| `TransactionManager` | `TransactionManager TransactionManager { get; }` | İç TransactionManager'a erişim (peek için). |

---

### MechanicalCommands/RoutePipeCommand.cs — `RoutePipeCommand : ICadCommand`

| Metod | İmza | Açıklama |
|---|---|---|
| `RoutePipeCommand` | `RoutePipeCommand(CadDatabase, MechanicalKernel)` | Veritabanı ve kernel ile boru yönlendirme komutu başlatır; `PipeRoutingEngine` ile `FittingSelector`'ı bağlar. |
| `SetSettings` | `void SetSettings(double diameter, MechanicalSystemType, string material, double slope)` | Çizilecek borunun çap, sistem tipi, malzeme ve eğim değerlerini ayarlar. |
| `Start` | `void Start()` | "ROUTEPIPE: Başlangıç noktasını belirtin." mesajı gönderir. |
| `OnPointerPressed` | `void OnPointerPressed(Vector3D)` | Her tıklamada boru segmenti oluşturur; gerekirse dirsek/redüksiyon ekler. |
| `OnPointerMoved` | `void OnPointerMoved(Vector3D)` | Ghost boruyu günceller. |
| `Draw` | `void Draw(IRenderContext)` | Ghost boru ve fitingleri çizer. |
| `Cancel` | `void Cancel()` | Mevcut rotayı bitirir. |

---

## Afney.Cad.Application / Services

### Services/SnapEngine.cs — `SnapEngine`

| Metod / Özellik | İmza | Açıklama |
|---|---|---|
| `EnableEndpoint` | `bool EnableEndpoint { get; set; }` | Endpoint snap bayrağı. |
| `EnableMidpoint` | `bool EnableMidpoint { get; set; }` | Midpoint snap bayrağı. |
| `EnableCenter` | `bool EnableCenter { get; set; }` | Center snap bayrağı. |
| `EnablePerpendicular` | `bool EnablePerpendicular { get; set; }` | Perpendicular snap bayrağı. |
| `IsOsnapEnabled` | `bool IsOsnapEnabled { get; set; }` | Tüm OSNAP'ı etkinleştirir/devre dışı bırakır. |
| `FindSnapPoint` | `SnapPoint? FindSnapPoint(Vector3D cursor, double zoom, Vector3D? lastPoint)` | Zoom'a bağlı aperture (maks. 5000 birim) içindeki en yakın snap noktasını bulur; statik ve dik snap dahil. |
| `CalculatePerpendicularSnap` | `SnapPoint? CalculatePerpendicularSnap(CadEntity, Vector3D, Vector3D lastPt)` | Verilen entiy'ye dik snap noktasını hesaplar. |
| `GetPerpendicularPoint` | `Vector3D GetPerpendicularPoint(Vector3D p, Vector3D s, Vector3D e)` | Noktanın segment üzerindeki dik izdüşümünü dot product ile bulur. |

---

### Services/SelectionManager.cs — `SelectionManager`

| Metod | İmza | Açıklama |
|---|---|---|
| `SelectionManager` | `SelectionManager(CadDatabase)` | Veritabanı EntityRemoved olayına abone olur. |
| `IsSelected` | `bool IsSelected(Guid)` | Belirtilen ID'nin seçili olup olmadığını O(1) döndürür. |
| `AddToSelection` | `void AddToSelection(CadEntity)` | Nesneyi seçim kümesine ekler, `IsSelected` bayrağını ayarlar. |
| `ToggleEntity` | `void ToggleEntity(Guid)` | Seçiliyse kaldırır, değilse ekler. |
| `ClearSelection` | `void ClearSelection()` | Tüm seçimi temizler. |
| `GetSelectedEntities` | `IEnumerable<CadEntity> GetSelectedEntities()` | Seçili nesnelerin cache'lenmiş listesini döndürür. |
| `SelectByCrossing` | `void SelectByCrossing(CadBoundingBox)` | Yeşil crossing seçimi: kutuyla kesişen tüm nesneleri seçer. |
| `SelectByWindow` | `void SelectByWindow(CadBoundingBox)` | Mavi pencere seçimi: kutu içinde tamamen kalan nesneleri seçer. |
| `CopyToClipboard` | `void CopyToClipboard(Vector3D basePoint)` | Seçili nesneleri base point ile panoya kopyalar. |
| `PasteFromClipboard` | `void PasteFromClipboard(Vector3D targetPoint)` | Pano içeriğini hedef noktaya yapıştırır; delta farkı hesaplar. |
| `DeleteSelected` | `void DeleteSelected()` | Seçili tüm nesneleri transaction ile siler. |
| `DrawSelection` | `void DrawSelection(IRenderContext, HashSet<string>? hiddenLayers)` | Seçili nesneleri vurgu (highlight) modunda çizer. |
| `DrawGrips` | `void DrawGrips(SKCanvas, Func<Vector3D, SKPoint>)` | Seçili nesnelerin grip (mavi tutma noktası) kontrolcülerini çizer. |

---

### Services/MahalExportService.cs — `MahalExportService`

| Metod | İmza | Açıklama |
|---|---|---|
| `MahalExportService` | `MahalExportService(CadDatabase)` | Veritabanıyla başlatılır. |
| `ExportMahalDataToJson` | `void ExportMahalDataToJson(string outputFilePath)` | Çizimdeki metin varlıklarından oda bilgilerini çıkarır ve JSON olarak kaydeder. |
| `ProcessTexts` | `List<MahalData> ProcessTexts(List<TextEntity>)` | Regex ile m² kalıplarını bulur, anahtar kelime sözlüğüyle oda tipini belirler. |
| `CreateMahalData` | `MahalData CreateMahalData(string name, double area, TextEntity, Dictionary)` | Mahal verisi kaydı oluşturur. |
| `AssignKatAndDaire` | `void AssignKatAndDaire(List<MahalData>, List<TextEntity>)` | Yakınlık analizi ile kat ve daire bilgilerini oda listesine atar. |

---

## Afney.Cad.Database

### Core/CadDatabase.cs — `CadDatabase`

| Metod / Özellik | İmza | Açıklama |
|---|---|---|
| `CadDatabase` | `CadDatabase()` | "0" katmanını oluşturur; ±10^12 aralıklı QuadTree başlatır. |
| `ActiveLayerName` | `string ActiveLayerName { get; set; }` | Aktif çizim katmanı adı. |
| `Clear` | `void Clear()` | Tüm varlık, katman ve uzamsal indeksi sıfırlar. |
| `AddEntity` | `void AddEntity(CadEntity)` | Varlığı ekler, QuadTree'ye indeksler, `EntityAdded` olayını tetikler. |
| `RemoveEntity` | `void RemoveEntity(Guid)` | Varlığı ve uzamsal indeks kaydını kaldırır, `EntityRemoved` olayını tetikler. |
| `UpdateEntity` | `void UpdateEntity(CadEntity)` | Uzamsal indeksi günceller, `EntityUpdated` olayını tetikler. |
| `QueryEntities` | `IEnumerable<CadEntity> QueryEntities(CadBoundingBox)` | QuadTree uzamsal sorgusunu çalıştırır. |
| `SelectByBox` | `IEnumerable<CadEntity> SelectByBox(CadBoundingBox, bool isCrossing)` | Window (tamamen içinde) veya Crossing (kesiştiren) seçimi yapar. |
| `GetAllEntities` | `IEnumerable<CadEntity> GetAllEntities()` | Tüm varlıkları döndürür. |
| `GetEntity` | `CadEntity? GetEntity(Guid)` | ID ile tek varlık getirir. |
| `AddLayer` | `void AddLayer(CadLayer)` | Yeni katman ekler. |
| `GetLayer` | `CadLayer? GetLayer(string name)` | Katman adına göre katman döndürür. |
| `GetLayers` | `IEnumerable<CadLayer> GetLayers()` | Tüm katmanları döndürür. |
| `ClearSelection` | `void ClearSelection()` | Tüm varlıkların `IsSelected` bayrağını temizler. |
| `Select` | `void Select(CadEntity)` | Varlığı seçili işaretler. |
| `Deselect` | `void Deselect(CadEntity)` | Varlığın seçimini kaldırır. |
| `GetSelectedEntities` | `IEnumerable<CadEntity> GetSelectedEntities()` | Seçili varlıkları döndürür. |
| `AddBlock` | `void AddBlock(CadBlockRecord)` | Blok tanımı ekler. |
| `GetBlock` | `CadBlockRecord? GetBlock(string name)` | Ada göre blok tanımı getirir. |
| `GetBlocks` | `IEnumerable<CadBlockRecord> GetBlocks()` | Tüm blok tanımlarını döndürür. |
| `EntityAdded` | `event Action<CadEntity> EntityAdded` | Varlık eklenmesinde tetiklenir. |
| `EntityRemoved` | `event Action<CadEntity> EntityRemoved` | Varlık silinmesinde tetiklenir. |
| `EntityUpdated` | `event Action<CadEntity> EntityUpdated` | Varlık güncellendiğinde tetiklenir. |

---

### Transactions/TransactionManager.cs — `TransactionManager`

| Metod / Özellik | İmza | Açıklama |
|---|---|---|
| `CanUndo` | `bool CanUndo { get; }` | Geri alınabilir işlem var mı? |
| `CanRedo` | `bool CanRedo { get; }` | Yinelenebilir işlem var mı? |
| `PeekUndoName` | `string? PeekUndoName()` | Geri alınacak işlemin adını stack'ten taşımadan okur. |
| `PeekRedoName` | `string? PeekRedoName()` | Yinelenecek işlemin adını okur. |
| `Submit` | `void Submit(IOperation operation)` | İşlemi çalıştırır, Redo'yu temizler, Undo stack'e ekler. |
| `Undo` | `void Undo()` | Son işlemi tersine çevirir; Redo stack'e taşır. |
| `Redo` | `void Redo()` | Geri alınmış işlemi yeniden uygular; Undo stack'e taşır. |
| `StateChanged` | `event Action StateChanged` | Undo/Redo durumu değiştiğinde UI güncellemek için tetiklenir. |

---

### Transactions/Operations — İşlem Sınıfları

| Sınıf | Metod | Açıklama |
|---|---|---|
| `AddEntityOperation` | `Do()` | Varlığı veritabanına ekler. |
| `AddEntityOperation` | `Undo()` | Eklenen varlığı kaldırır. |
| `RemoveEntityOperation` | `Do()` | Varlığı veritabanından kaldırır. |
| `RemoveEntityOperation` | `Undo()` | Silinen varlığı geri ekler. |
| `MoveEntityOperation` | `Do()` | Varlığı delta vektörü kadar taşır; topoloji günceller. |
| `MoveEntityOperation` | `Undo()` | Ters yön vektörü ile taşıma işlemini geri alır. |
| `CompositeOperation` | `Add(IOperation)` | Birden fazla operasyonu toplu işlemek üzere birleştirir. |
| `CompositeOperation` | `Do()` | Tüm alt operasyonları sırayla çalıştırır. |
| `CompositeOperation` | `Undo()` | Tüm alt operasyonları ters sırayla geri alır. |

---

## Afney.Cad.Domain

### Abstractions/CadEntity.cs — `abstract CadEntity`

| Üye | İmza | Açıklama |
|---|---|---|
| `Id` | `Guid Id { get; }` | Benzersiz varlık kimliği. |
| `Layer` | `string? Layer { get; set; }` | Ait olduğu katman adı. |
| `Color` | `uint Color { get; set; }` | ARGB renk değeri. |
| `IsSelected` | `bool IsSelected { get; set; }` | Seçim durumu. |
| `Selectable` | `bool Selectable { get; set; }` | Seçilebilir mi? |
| `IsFromBlock` | `bool IsFromBlock { get; }` | Blok referansından türetildi mi? |
| `TransformMatrix` | `Matrix4x4 TransformMatrix { get; set; }` | Blok transformasyon matrisi. |
| `Linetype` | `string? Linetype { get; set; }` | Çizgi tipi adı (Continuous, Dashed vb.). |
| `LineWeight` | `double LineWeight { get; set; }` | Çizgi ağırlığı (mm). |
| `Draw` | `abstract void Draw(IRenderContext)` | Render motoruna kendini çizdirir. |
| `CalculateBoundingBox` | `protected abstract CadBoundingBox CalculateBoundingBox()` | Sınırlayıcı kutu hesaplar. |
| `Move` | `abstract void Move(Vector3D delta)` | Varlığı delta kadar taşır. |
| `Transform` | `abstract void Transform(Matrix4x4)` | Matris dönüşümü uygular. |
| `Clone` | `abstract CadEntity Clone()` | Varlığın bağımsız kopyasını oluşturur. |
| `GetSnapPoints` | `abstract IEnumerable<SnapPoint> GetSnapPoints()` | OSNAP yakalanabilir noktalarını döndürür. |
| `DistanceTo` | `virtual double DistanceTo(Vector3D)` | Noktaya dik mesafeyi hesaplar; varsayılan bounding box merkezi. |
| `GetGripPoints` | `virtual IEnumerable<Vector3D> GetGripPoints()` | Grip (tutma noktası) koordinatlarını döndürür. |
| `MoveGripPointAt` | `virtual void MoveGripPointAt(int index, Vector3D)` | Belirtilen grip noktasını yeni konuma taşır. |
| `GetBoundingBox` | `CadBoundingBox GetBoundingBox()` | Cache'li sınırlayıcı kutu döndürür. |
| `GetRenderWeight` | `float GetRenderWeight()` | Ekran kalınlığını zoom bağımsız hesaplar. |
| `InvalidateCache` | `void InvalidateCache()` | Bounding box cache'ini geçersiz kılar. |
| `CopyBaseProperties` | `void CopyBaseProperties(CadEntity target)` | Layer, Color, Linetype gibi temel özellikleri hedefe kopyalar. |

---

### Entities/Basic/LineEntity.cs — `LineEntity : CadEntity`

| Metod | İmza | Açıklama |
|---|---|---|
| `LineEntity` | `LineEntity(Vector3D start, Vector3D end)` | Başlangıç ve bitiş noktasıyla çizgi oluşturur. |
| `GetLength` | `double GetLength()` | Çizgi uzunluğunu hesaplar. |
| `DistanceTo` | `override double DistanceTo(Vector3D)` | Noktanın çizgiye dik uzaklığını T parametresi yöntemiyle hesaplar. |
| `Draw` | `override void Draw(IRenderContext)` | Hairline kalınlıkta çizgiyi çizer. |
| `CalculateBoundingBox` | `protected override CadBoundingBox CalculateBoundingBox()` | Min/Max uç noktalarından sınırlayıcı kutu oluşturur. |
| `Move` | `override void Move(Vector3D delta)` | Her iki uç noktayı da delta kadar kaydırır. |
| `GetSnapPoints` | `override IEnumerable<SnapPoint>` | StartPoint (Endpoint), EndPoint (Endpoint), orta nokta (Midpoint) döndürür. |
| `Transform` | `override void Transform(Matrix4x4)` | Uç noktaları matrisle dönüştürür. |
| `Clone` | `override CadEntity Clone()` | Yeni bir `LineEntity` kopyası döndürür. |
| `GetGripPoints` | `override IEnumerable<Vector3D>` | Start, End ve orta noktayı grip olarak döndürür. |
| `MoveGripPointAt` | `override void MoveGripPointAt(int, Vector3D)` | index=0→Start, 1→End, 2→tüm nesneyi taşır. |

---

### Entities/Basic/CircleEntity.cs — `CircleEntity : CadEntity`

| Metod | İmza | Açıklama |
|---|---|---|
| `CircleEntity` | `CircleEntity(Vector3D center, double radius)` | Merkez ve yarıçapla çember oluşturur. |
| `Draw` | `override void Draw(IRenderContext)` | `DrawCircle` ile çember çizer. |
| `CalculateBoundingBox` | `protected override` | Merkez ± yarıçap sınırlayıcı kutu. |
| `Move` | `override void Move(Vector3D delta)` | Merkezi delta kadar taşır. |
| `GetSnapPoints` | `override IEnumerable<SnapPoint>` | Center, 4 quadrant noktası. |
| `Transform` | `override void Transform(Matrix4x4)` | Merkezi matrisle dönüştürür. |
| `Clone` | `override CadEntity Clone()` | Çemberin kopyasını döndürür. |
| `GetGripPoints` | `override IEnumerable<Vector3D>` | Merkez ve 4 quadrant. |

---

### Entities/Basic/ArcEntity.cs — `ArcEntity : CadEntity`

| Metod | İmza | Açıklama |
|---|---|---|
| `ArcEntity` | `ArcEntity(Vector3D center, double radius, double startAngle, double endAngle)` | Merkez, yarıçap, başlangıç/bitiş açısı (radyan) ile yay oluşturur. |
| `Draw` | `override void Draw(IRenderContext)` | 32 segmentle tessellate ederek segment-by-segment çizer. |
| `CalculateBoundingBox` | `protected override` | Basitleştirilmiş Merkez ± yarıçap kutu. |
| `Move` | `override void Move(Vector3D delta)` | Merkezi taşır. |
| `Transform` | `override void Transform(Matrix4x4)` | Merkezi matrisle dönüştürür. |
| `Clone` | `override CadEntity Clone()` | Yayın kopyasını döndürür. |
| `GetSnapPoints` | `override IEnumerable<SnapPoint>` | Center, StartPoint (Endpoint), EndPoint (Endpoint). |

---

### Entities/Basic/LwPolylineEntity.cs — `LwPolylineEntity : CadEntity`

| Metod | İmza | Açıklama |
|---|---|---|
| `LwPolylineEntity` | `LwPolylineEntity(IEnumerable<Vector3D>, bool isClosed)` | Köşe noktaları ve kapalılık bayrağıyla çokluçizgi oluşturur. |
| `Draw` | `override void Draw(IRenderContext)` | Tüm segmentleri hairline kalınlıkta çizer; kapalıysa son-ilk kenarı da ekler. |
| `CalculateBoundingBox` | `protected override` | Tüm vertex'lerden Min/Max tarayarak AABB hesaplar. |
| `Move` | `override void Move(Vector3D delta)` | Tüm vertex'leri delta kadar kaydırır. |
| `GetSnapPoints` | `override IEnumerable<SnapPoint>` | Her vertex'i Endpoint olarak döndürür. |
| `Transform` | `override void Transform(Matrix4x4)` | Tüm vertex'lere matris uygular. |
| `Clone` | `override CadEntity Clone()` | Derin kopya (vertex listesi dahil). |
| `GetGripPoints` | `override IEnumerable<Vector3D>` | Tüm vertex'leri grip olarak döndürür. |

---

### Entities/Annotation/DimensionEntity.cs — `DimensionEntity : CadEntity`

| Metod | İmza | Açıklama |
|---|---|---|
| `DimensionEntity` | `DimensionEntity(Vector3D p1, Vector3D p2, Vector3D dimLinePoint, DimensionType)` | Ölçü tipi ve noktalarla ölçü nesnesi oluşturur. |
| `Draw` | `override void Draw(IRenderContext)` | `DimType`'a göre `DrawLinear`, `DrawAligned`, `DrawRadius` veya `DrawAngular` çağırır. |
| `GetMeasurement` | `double GetMeasurement()` | Ölçüm değerini (mm veya derece) hesaplar. |
| `GetAngleDegrees` | `double GetAngleDegrees()` | İki vektör arasındaki açıyı Atan2 ile hesaplar. |
| `GetDxfText` | `string GetDxfText()` | DXF çıktısı için ölçü metin dizisini döndürür. |
| `GetText` | `string GetText()` | Ölçüyü birimi olan metin olarak formatlar (mm/m/derece). |

---

### Tables/CadLayer.cs — `CadLayer : INotifyPropertyChanged`

| Özellik / Metod | İmza | Açıklama |
|---|---|---|
| `CadLayer` | `CadLayer(string name)` | Belirtilen adla katman oluşturur. |
| `Name` | `string Name { get; set; }` | Katman adı. |
| `Color` | `uint Color { get; set; }` | ARGB renk; değişince `ColorBrush`'ı da bildirir. |
| `ColorBrush` | `string ColorBrush { get; }` | WPF'e için hex renk dizesi (#RRGGBB). |
| `IsVisible` | `bool IsVisible { get; set; }` | Katman görünürlüğü; değişince `BulbIcon` bildirilir. |
| `BulbIcon` | `string BulbIcon { get; }` | Görünürlük için ampul ikonu (💡/🌑). |
| `IsFrozen` | `bool IsFrozen { get; set; }` | Dondurma durumu; `FreezeIcon` bildirir. |
| `FreezeIcon` | `string FreezeIcon { get; }` | Dondurma için kar/güneş ikonu. |
| `IsLocked` | `bool IsLocked { get; set; }` | Kilit durumu; `LockIcon` bildirir. |
| `LockIcon` | `string LockIcon { get; }` | Kilit ikonu. |
| `LineWeight` | `double LineWeight { get; set; }` | Çizgi ağırlığı (mm). |
| `Description` | `string Description { get; set; }` | Katman açıklaması. |

---

## Afney.Cad.Mechanical

### MechanicalKernel.cs — `MechanicalKernel`

| Özellik / Metod | İmza | Açıklama |
|---|---|---|
| `TopologyGraph` | `MechanicalTopologyGraph TopologyGraph { get; }` | MEP topoloji grafı. |
| `ConnectionEngine` | `PipeConnectionEngine ConnectionEngine { get; }` | Boru bağlanabilirlik motoru. |
| `ConstraintSolver` | `ConstraintSolver ConstraintSolver { get; }` | Geometrik kısıt çözücü. |
| `ValidationGate` | `ValidationGate ValidationGate { get; }` | Mühendislik doğrulama kapısı. |
| `FlowCalculation` | `FlowCalculationService FlowCalculation { get; }` | Debi hesap servisi. |
| `PressureDrop` | `PressureDropService PressureDrop { get; }` | Basınç düşümü servisi. |
| `Metadata` | `MechanicalMetadataService Metadata { get; }` | Tesisat meta verisi servisi. |
| `SystemConfigs` | `Dictionary<MechanicalSystemType, SystemConfig> SystemConfigs { get; }` | Sistem başına konfigürasyon. |
| `LevelManager` | `LevelManager LevelManager { get; }` | Kat yöneticisi. |
| `ProjectSettings` | `ProjectSettings ProjectSettings { get; }` | Proje ayarları. |
| `Rules` | `List<IMechanicalRule> Rules { get; }` | Mühendislik kuralları. |
| `PipeStandards` | `PipeStandardLibrary PipeStandards { get; }` | Boru standartları kütüphanesi. |
| `FittingSelector` | `FittingSelector FittingSelector { get; }` | Akıllı fitting seçici. |
| `ArchitecturalObstacles` | `List<ArchitecturalObstacle> ArchitecturalObstacles { get; }` | Mimari engelller listesi. |
| `ProjectModel` | `ProjectModel ProjectModel { get; }` | Proje BIM modeli. |
| `Pathfinder` | `PipePathfinder Pathfinder { get; }` | Boru rota bulucu. |
| `IsoSync` | `IsometricSyncService IsoSync { get; }` | Kolon şeması senkronizasyon servisi. |
| `OnRequestAddEntity` | `event Action<CadEntity> OnRequestAddEntity` | Kernel'dan entity ekleme isteği olayı. |
| `OnRequestDeleteEntity` | `event Action<CadEntity> OnRequestDeleteEntity` | Kernel'dan entity silme isteği olayı. |
| `SetDatabase` | `void SetDatabase(CadDatabase)` | Veritabanını bağlar; IsoSync ve ValidationGate'i başlatır. |
| `RegisterDefaultRules` | `void RegisterDefaultRules()` | TS 1258 mühendislik kurallarını yükler. |
| `GetRiserSchemas` | `IEnumerable<RiserSchema> GetRiserSchemas(IEnumerable<MechanicalEntity>)` | Kolon şema özetlerini üretir. |
| `ValidateEntity` | `bool ValidateEntity(MechanicalEntity, out string message)` | Mühendislik kurallarını çalıştırır; geçersizse mesaj döndürür. |
| `OnEntityAddedToDatabase` | `void OnEntityAddedToDatabase(CadEntity)` | Topoloji ekler, portları otomatik bağlar, hidrolik güncelleme tetikler. |
| `OnEntityRemovedFromDatabase` | `void OnEntityRemovedFromDatabase(CadEntity)` | Topolojiden kaldırır, bağlı entity'leri günceller. |
| `OnEntityUpdatedInDatabase` | `void OnEntityUpdatedInDatabase(CadEntity)` | Topoloji portlarını günceller, etiketleri senkronize eder, duvar-cihaz ilişkisini yönetir. |
| `RecalculateProject` | `void RecalculateProject(IEnumerable<CadEntity>)` | Tam TS 1258 hidrolik analiz zincirini çalıştırır. |
| `ResolveAllClashes` | `void ResolveAllClashes(IEnumerable<CadEntity>)` | MEP-vs-MEP çakışmalarını otomatik çözer. |
| `AutoConnectPorts` | `void AutoConnectPorts(MechanicalEntity)` | 2mm eşiğiyle port-to-port ve port-to-body otomatik bağlantı; dirsek/redüksiyon/T ekler. |
| `SplitPipeAndConnect` | `void SplitPipeAndConnect(PipeEntity, MechanicalPort, Vector3D)` | Boruyu T-kavşakta böler, TeeEntity oluşturur. |
| `SyncConnectedPipes` | `void SyncConnectedPipes(SanitaryFixtureEntity)` | Cihaz taşındığında bağlı boruları esnetitr. |
| `TriggerHydraulicUpdate` | `void TriggerHydraulicUpdate(bool force)` | Re-entry korumasıyla hidrolik güncelleme tetikler. |
| `SyncPipeLabels` | `void SyncPipeLabels(PipeEntity)` | PipeLabelEntity konumlarını boru üzerine senkronize eder. |

---

### Entities/MechanicalEntity.cs — `abstract MechanicalEntity : CadEntity`

| Özellik / Metod | İmza | Açıklama |
|---|---|---|
| `MetadataChanged` | `event Action<MechanicalEntity>` | Özellik değiştiğinde reaktif hesaplama için tetiklenir. |
| `SuppressMetadataEvents` | `bool SuppressMetadataEvents { get; set; }` | Otomatik boyutlandırma sırasında olayları bastırır. |
| `SystemType` | `MechanicalSystemType SystemType { get; set; }` | Sistem tipi (Soğuk su, Pis su vb.); değişince `MetadataChanged` tetikler. |
| `InnerDiameter` | `double InnerDiameter { get; set; }` | İç çap (mm); değişince `MetadataChanged` tetikler. |
| `PipeMaterialType` | `PipeMaterial PipeMaterialType { get; set; }` | Malzeme; değişince `MetadataChanged` tetikler. |
| `EntityType` | `MechanicalEntityType EntityType { get; set; }` | Mekanik eleman tipi. |
| `IsCalculationUpToDate` | `bool IsCalculationUpToDate { get; set; }` | Hesap geçerlilik bayrağı (Dirty flag). |
| `IsSizeLocked` | `bool IsSizeLocked { get; set; }` | Çap kilidi; otomatik boyutlandırma bu çapa dokunmaz. |
| `InsulationThickness` | `double InsulationThickness { get; set; }` | Dış izolasyon kalınlığı (mm); BIM ve clash için kullanılır. |
| `GetPorts` | `abstract List<MechanicalPort> GetPorts()` | Bağlantı portlarını döndürür. |
| `OnMetadataChanged` | `protected void OnMetadataChanged()` | `IsCalculationUpToDate = false` yaparak olayı tetikler; bastırma modunda es geçer. |

---

### Entities/PipeEntity.cs — `PipeEntity : MechanicalEntity`

| Özellik / Metod | İmza | Açıklama |
|---|---|---|
| `PipeEntity` | `PipeEntity(Vector3D start, Vector3D end, double diameter)` | Başlangıç, bitiş ve iç çap ile boru oluşturur. |
| `StartPoint` / `EndPoint` | `Vector3D` | Boru uç koordinatları. |
| `FlowRate` | `double FlowRate { get; set; }` | Akış debisi (m³/h). |
| `Pressure` | `double Pressure { get; set; }` | İşletme basıncı (bar). |
| `Temperature` | `double Temperature { get; set; }` | Sıcaklık (°C). |
| `Slope` | `double Slope { get; set; }` | Eğim (%); atık su için cazibe akışı. |
| `LoadUnits` | `double LoadUnits { get; set; }` | Yükleme birimi (TS 1258 Fixture Unit). |
| `IsCarryingWCLoad` | `bool IsCarryingWCLoad { get; set; }` | Klozet yükü taşıyor mu? (Min DN100 kontrolü için). |
| `FlowDirection` | `int FlowDirection { get; set; }` | Akış yönü (0=belirsiz, 1=Start→End, -1=End→Start). |
| `Velocity` | `double Velocity { get; set; }` | Akış hızı (m/s). |
| `PressureDrop` | `double PressureDrop { get; set; }` | Basınç kaybı (mSS). |
| `HasHydraulicViolation` | `bool HasHydraulicViolation { get; set; }` | Validasyon hatası var mı? |
| `ApplySystemColor` | `void ApplySystemColor()` | TS 1258/TS EN 12056 standartlarına göre sistem rengi atar (mavi=soğuk, kırmızı=sıcak vb.). |
| `GetLength` | `double GetLength()` | Boru uzunluğunu hesaplar. |
| `DistanceTo` | `override double DistanceTo(Vector3D)` | Noktanın boruya dik mesafesini T parametresi yöntemiyle hesaplar. |
| `GetVelocity` | `double GetVelocity()` | V=Q/A formülüyle akış hızını hesaplar. |
| `Draw` | `override void Draw(IRenderContext)` | Kalın gövde çizer; etiket (Ø, eğim), akış oku ekler. Hesap geçersizse sarı, ihlal varsa kırmızı. |
| `DrawFlowArrow` | `void DrawFlowArrow(IRenderContext, Vector3D, Vector3D, uint)` | Boru üzerine üçgen akış yönü oku çizer. |
| `GetPorts` | `override List<MechanicalPort> GetPorts()` | Start ve End portları (akış yönü vektörleriyle) döndürür. |
| `Clone` | `override CadEntity Clone()` | Borunun kopyasını döndürür. |
| `Move` | `override void Move(Vector3D delta)` | Her iki uç noktayı da taşır. |
| `Transform` | `override void Transform(Matrix4x4)` | Uç noktaları matrisle dönüştürür. |
| `GetGripPoints` | `override IEnumerable<Vector3D>` | Start, End ve orta nokta. |
| `MoveGripPointAt` | `override void MoveGripPointAt(int, Vector3D)` | Grip noktasına göre Start/End/tüm boru taşır. |

---

### Entities/SanitaryFixtureEntity.cs — `SanitaryFixtureEntity : MechanicalEntity`

| Metod / Özellik | İmza | Açıklama |
|---|---|---|
| `SanitaryFixtureEntity` | `SanitaryFixtureEntity(Vector3D position, string fixtureType, double fu)` | Tip ve yükleme birimiyle vitrifiye oluşturur; `InitializeDefaults` çağırır. |
| `InitializeDefaults` | `void InitializeDefaults(string type)` | Tipe göre (Lavabo, WC, Duş vb.) boyut ve port ofsetlerini ayarlar. |
| `GetPorts` | `override List<MechanicalPort> GetPorts()` | Rotasyona göre dönüştürülmüş soğuk su, sıcak su ve pis su portlarını döndürür; TS 1258 çap kurallarını uygular. |
| `Draw` | `override void Draw(IRenderContext)` | TS/DIN standart MEP sembolleri: Lavabo oval, WC rezervuar+oturak, Duş ızgara, Küvet iç profil vb. |
| `CalculateBoundingBox` | `protected override` | Rotasyonlu dikdörtgenin 4 köşesini transforme ederek AABB hesaplar. |
| `DrawPortSymbols` | `void DrawPortSymbols(IRenderContext)` | Port-only modda: her bağlantı noktasına artı (+) sembolü ve kısa etiket çizer. |
| `Clone` | `override CadEntity Clone()` | Vitrifiyenin kopyasını döndürür. |
| `CreateWashbasin` | `static SanitaryFixtureEntity CreateWashbasin(Vector3D)` | Standart yarım ayak lavabo fabrika metodu. |
| `CreateWC` | `static SanitaryFixtureEntity CreateWC(Vector3D)` | Rezervuarlı klozet fabrika metodu (DN100). |
| `CreateShower` | `static SanitaryFixtureEntity CreateShower(Vector3D)` | Duş teknesi fabrika metodu. |
| `CreateBathtub` | `static SanitaryFixtureEntity CreateBathtub(Vector3D)` | Banyo küveti fabrika metodu. |
| `CreateSink` | `static SanitaryFixtureEntity CreateSink(Vector3D)` | Mutfak eviyesi fabrika metodu. |
| `CreateFloorDrain` | `static SanitaryFixtureEntity CreateFloorDrain(Vector3D)` | Döşeme süzgeci fabrika metodu. |
| `CreateUrinal` | `static SanitaryFixtureEntity CreateUrinal(Vector3D)` | Pisuvar fabrika metodu. |
| `CreateWashingMachine` | `static SanitaryFixtureEntity CreateWashingMachine(Vector3D)` | Çamaşır makinesi fabrika metodu. |
| `CreateDishwasher` | `static SanitaryFixtureEntity CreateDishwasher(Vector3D)` | Bulaşık makinesi fabrika metodu. |
| `CreateWaterHeater` | `static SanitaryFixtureEntity CreateWaterHeater(Vector3D)` | Elektrikli şofben fabrika metodu. |
| `CreateDoubleSink` | `static SanitaryFixtureEntity CreateDoubleSink(Vector3D)` | Çift gözlü eviye fabrika metodu. |
| `GetSnapPoints` | `override IEnumerable<SnapPoint>` | Merkez (Center) ve tüm portlar (Connection) döndürür. |

---

### Engine/MechanicalTopologyGraph.cs — `MechanicalTopologyGraph` + `GraphNode`

| Metod | İmza | Açıklama |
|---|---|---|
| `Clear` | `void Clear()` | Tüm düğümleri ve odaları temizler. |
| `AddEntity` | `void AddEntity(MechanicalEntity)` | Yeni düğüm ekler; `ConcurrentDictionary` ile thread-safe. |
| `RemoveEntity` | `void RemoveEntity(Guid)` | Düğümü kaldırır; `DisconnectAll` çağırır. |
| `AddRoom` | `void AddRoom(RoomEntity)` / `void AddRoom(MahalEntity)` | Oda/mahal listesine ve genel düğümlere ekler. |
| `Connect` | `void Connect(MechanicalPort p1, MechanicalPort p2)` | İki port arasında çift yönlü bağ kurar. |
| `Disconnect` | `void Disconnect(MechanicalPort)` | Portun bağlantısını keser; karşı tarafı da temizler. |
| `GetNeighbors` | `IEnumerable<GraphNode> GetNeighbors(Guid)` | Düğümün bağlı komşularını döndürür. |
| `GetNode` | `GraphNode? GetNode(Guid)` | ID ile O(1) düğüm erişimi. |
| `GetNodeByPort` | `GraphNode? GetNodeByPort(MechanicalPort)` | Porta sahip entity'nin düğümünü döndürür. |
| `GetVerticalNeighbors` | `IEnumerable<GraphNode> GetVerticalNeighbors(Guid)` | Dikey (kolon) boruya bağlı komşuları döndürür. |
| `IsVertical` | `bool IsVertical(PipeEntity)` | Borunun Z ekseni boyunca (|Z| > 0.9) dikey olup olmadığını kontrol eder. |
| `GraphNode.UpdatePorts` | `void UpdatePorts(MechanicalEntity)` | Düğümün portlarını yeniden yükler; mevcut bağlantıları korur. |
| `GraphNode.DisconnectAll` | `void DisconnectAll()` | Tüm portların `IsConnected` bayrağını temizler. |
| `GraphNode.GetNeighbors` | `IEnumerable<GraphNode> GetNeighbors(MechanicalTopologyGraph)` | Bağlı portların karşı düğümlerini döndürür; snapshot (ToArray) ile thread-safe. |

---

### Engine/PipeConnectionEngine.cs — `PipeConnectionEngine`

| Metod | İmza | Açıklama |
|---|---|---|
| `CanConnect` | `bool CanConnect(PipeEntity a, PipeEntity b)` | 1mm mesafe toleransı ve 5mm çap toleransıyla iki borunun bağlanabilir olup olmadığını kontrol eder. |
| `IsNear` | `bool IsNear(Vector3D, Vector3D, double threshold)` | Öklid mesafesi eşik altında mı? |

---

## Afney.Cad.Geometry

### Primitives/Vector3D.cs — `readonly record struct Vector3D`

| Metod / Özellik | İmza | Açıklama |
|---|---|---|
| `X`, `Y`, `Z` | `double X, Y, Z` | Koordinat bileşenleri. |
| `Length` | `double Length()` | Vektör uzunluğu (Öklid normu). |
| `LengthSquared` | `double LengthSquared()` | Uzunluğun karesi; kök almadan karşılaştırma için. |
| `DistanceTo` | `double DistanceTo(Vector3D other)` | İki nokta arasındaki mesafe. |
| `Dot` | `double Dot(Vector3D other)` | Nokta çarpım (iç çarpım). |
| `Cross` | `Vector3D Cross(Vector3D other)` | Vektörel çarpım. |
| `Normalize` | `Vector3D Normalize()` | Birim vektör; sıfır uzunluk korumalı. |
| `operator+` | `Vector3D operator+(Vector3D a, Vector3D b)` | Vektör toplama. |
| `operator-` | `Vector3D operator-(Vector3D a, Vector3D b)` | Vektör çıkarma. |
| `operator*` | `Vector3D operator*(Vector3D v, double s)` | Skaler çarpım. |
| `operator/` | `Vector3D operator/(Vector3D v, double s)` | Skaler bölme. |
| `operator-` | `Vector3D operator-(Vector3D v)` | Negatif vektör. |
| `Min` | `static Vector3D Min(Vector3D, Vector3D)` | Bileşen bazlı minimum. |
| `Max` | `static Vector3D Max(Vector3D, Vector3D)` | Bileşen bazlı maksimum. |
| `Zero`, `XAxis`, `YAxis`, `ZAxis` | `static readonly Vector3D` | Sık kullanılan sabit vektörler. |

---

### Primitives/CadBoundingBox.cs — `struct CadBoundingBox`

| Metod / Özellik | İmza | Açıklama |
|---|---|---|
| `Min`, `Max` | `Vector3D Min, Max` | Kutunun minimum ve maksimum köşesi. |
| `Width`, `Height`, `Depth` | `double` | Boyutlar. |
| `Center` | `Vector3D Center` | Kutu merkezi. |
| `Contains(Vector3D)` | `bool Contains(Vector3D)` | Nokta kutu içinde mi? |
| `Intersects` | `bool Intersects(CadBoundingBox)` | AABB örtüşme kontrolü. |
| `Contains(CadBoundingBox)` | `bool Contains(CadBoundingBox)` | Tam kapsama kontrolü. |
| `Expand` | `CadBoundingBox Expand(double margin)` | Her yönde genişletilmiş kutu döndürür. |
| `GetCorners` | `Vector3D[] GetCorners()` | 8 köşe noktası döndürür. |
| `Empty` | `static CadBoundingBox Empty` | Boş (geçersiz) sınırlayıcı kutu sabiti. |

---

### Algorithms/GeomUtils.cs — `static class GeomUtils`

| Metod | İmza | Açıklama |
|---|---|---|
| `RayCast` | `bool RayCast(Vector3D origin, Vector3D dir, IEnumerable<(Vector3D,Vector3D)> segments, double maxRange)` | Işın-segment kesişim testi; ilk kesişim noktasında durur. |
| `ArePointsConnected` | `bool ArePointsConnected(Vector3D p1, Vector3D p2, double tolerance)` | Tolerans içinde mesafe kontrolü. |
| `FindNearbySegments` | `List<(Vector3D,Vector3D)> FindNearbySegments(Vector3D search, segments, currentSeg, double tol)` | Uç noktası yakın bitişik segmentleri bulur. |
| `CalculateClockwiseAngle` | `double CalculateClockwiseAngle(Vector3D current, Vector3D next)` | İki vektör arasındaki 0-360° saat yönü açıyı hesaplar. |
| `GetIntersectionLineLine` | `bool GetIntersectionLineLine(p1,p2,p3,p4, out Vector3D)` | Cramer kuralıyla iki çizginin kesişim noktasını bulur. |
| `DoSegmentsIntersect` | `bool DoSegmentsIntersect(a,b,c,d, out Vector3D)` | İki doğru parçasının gerçekten kesişip kesişmediğini test eder. |
| `PointToSegmentDistance` | `double PointToSegmentDistance(Vector3D p, Vector3D a, Vector3D b)` | Noktanın segmente dik mesafesi. |
| `IsPointInPolygon` | `bool IsPointInPolygon(Vector3D, IEnumerable<Vector3D>)` | Işın döküm (ray casting) yöntemiyle nokta-içinde-poligon testi. |

---

## Afney.Cad.Render

### Engines/SkiaRenderContext.cs — `SkiaRenderContext : IRenderContext`

| Metod / Özellik | İmza | Açıklama |
|---|---|---|
| `SkiaRenderContext` | `SkiaRenderContext(SKCanvas canvas, double pixelSize)` | SkiaSharp canvas ve piksel boyutuyla render bağlamı oluşturur. |
| `PixelSize` | `double PixelSize { get; }` | 1 piksel = kaç dünya birimi (zoom faktörünün tersi). |
| `IsIsometric` | `bool IsIsometric { get; set; }` | İzometrik modda render bayrağı. |
| `IsHighlightMode` | `bool IsHighlightMode { get; set; }` | Seçim glow (parlak sarı) modunu etkinleştirir. |
| `GetPaint` | `SKPaint GetPaint(uint color, double thickness, bool isDashed, string linetype)` | Renk, kalınlık ve linetype'a göre paint önbelleğinden döndürür; Hairline teknolojisi uygular. |
| `DrawLine` | `void DrawLine(Vector3D from, Vector3D to, uint color, double thickness, string? linetype, bool isDashed)` | Çizgi çizer; Dashed/Dotted/Dashdot tipleri simüle eder. |
| `DrawCircle` | `void DrawCircle(Vector3D center, double radius, uint color, double thickness)` | Çember çizer. |
| `DrawArc` | `void DrawArc(Vector3D center, double radius, double start, double end, uint color, double thickness)` | Yay çizer. |
| `DrawText` | `void DrawText(string text, Vector3D position, double angleDeg, double height, uint color)` | Döndürülmüş metin yazar; subpixel rendering ile. |
| `DrawSolidLine` | `void DrawSolidLine(Vector3D from, Vector3D to, uint color, double innerDia, double outerDia)` | Kalın boru gövdesi çizer (iç+dış çap). |
| `DrawRectangle` | `void DrawRectangle(Vector3D p1, Vector3D p2, uint color, double thickness)` | Dikdörtgen çizer. |

---

## Afney.Cad.Infrastructure

### Import/DxfImportService.cs — `DxfImportService`

| Metod | İmza | Açıklama |
|---|---|---|
| `ImportDxf` | `List<CadEntity> ImportDxf(string filePath, string targetLayer)` | DXF dosyasını ACadSharp ile açar; paralel işlemle entity'lere dönüştürür; INSUNITS ölçekleme uygular. |
| `ConvertEntityFull` | `CadEntity? ConvertEntityFull(Entity, Matrix4x4, layerColors, depth, visitedBlocks)` | Insert (blok), Dimension, Hatch, Line, Arc→LwPolyline, Circle, LwPolyline, MText/Text, Ellipse→LwPolyline, Spline, Point tiplerini özyinelemeli dönüştürür. |
| `MapArc` | `LwPolylineEntity MapArc(Arc)` | Arc'ı 16 segmentli polyline'a dönüştürür. |
| `MapEllipse` | `LwPolylineEntity MapEllipse(Ellipse)` | Ellipse'i 48 segmentli polyline'a dönüştürür. |
| `MapSpline` | `SplineEntity MapSpline(Spline)` | Spline'ı `SplineEntity`'ye dönüştürür. |

---

### Import/DwgImportService.cs — `DwgImportService`

| Metod | İmza | Açıklama |
|---|---|---|
| `DwgImportService` | `DwgImportService()` | ACI palette'i statik önbelleğe yükler; reflection önbelleğini başlatır. |
| `ImportDwg` | `List<CadEntity> ImportDwg(string filePath)` | ACadSharp `DwgReader` ile DWG dosyasını okur; katman renk/linetype/frozen/locked verilerini çıkarır; entity'leri dönüştürür. |
| `InitializeAciPalette` | `static void InitializeAciPalette()` | 256 renk ACI→RGB tablosunu başlatır. |
| `MapColor` | `uint MapColor(ACadSharp.Color)` | ACI rengini ARGB uint'e dönüştürür. |
| `GetCachedProperty` | `static PropertyInfo? GetCachedProperty(Type, string)` | Reflection önbelleğiyle property erişimi; Hatch gecikmesini 14s→0.1s indirir. |

---

### Export/DxfWriterService.cs — `DxfWriterService`

| Metod | İmza | Açıklama |
|---|---|---|
| `DxfWriterService` | `DxfWriterService(CadDatabase)` | Veritabanıyla başlatılır. |
| `WriteToFile` | `void WriteToFile(string filePath)` | Tüm veritabanını DXF R12 ASCII formatında yazar. |
| `WriteEntitiesToFile` | `void WriteEntitiesToFile(string filePath, IEnumerable<CadEntity>)` | Belirtilen entity listesini DXF R12 olarak yazar. |
| `WriteHeader` | `static void WriteHeader(StringBuilder)` | DXF HEADER section ($ACADVER=AC1009, mm birimleri). |
| `WriteTables` | `void WriteTables(StringBuilder)` | LTYPE ve LAYER tablolarını yazar. |
| `WriteEntities` | `static void WriteEntities(StringBuilder, List<CadEntity>)` | LINE/TEXT/CIRCLE/ARC/DIMENSION/POLYLINE/SPLINE/HATCH/PIPE tiplerini yazar. |
| `WriteLine` / `WriteCircle` / `WriteArc` | `static void Write*(StringBuilder, entity)` | Her entity tipi için DXF group code'larını yazar. |
| `WriteDimension` | `static void WriteDimension(StringBuilder, DimensionEntity)` | Tüm ölçü tiplerini (Linear/Aligned/Radius/Angular) LINE+TEXT olarak DXF R12 temsili yazar. |
| `WritePolyline` | `static void WritePolyline(StringBuilder, LwPolylineEntity)` | Vertex'leri LINE segmentlerine dönüştürür. |
| `WriteSpline` | `static void WriteSpline(StringBuilder, SplineEntity)` | Kontrol noktalarını LINE segmentlerine dönüştürür. |
| `WriteHatch` | `static void WriteHatch(StringBuilder, HatchEntity)` | Sınır vertex'lerini LINE döngüsü olarak yazar. |
| `WritePipe` | `static void WritePipe(StringBuilder, PipeEntity)` | Boru merkezini ince çizgi (LINE) olarak yazar (R12 LwPolyline genişliği desteklemiyor). |
| `ArgbToAci` | `static int ArgbToAci(uint argb)` | ARGB rengini 256 girişli ACI tablosunda Öklid uzaklığıyla en yakın AutoCAD renk indeksine çevirir. |
| `Group` / `GroupXYZ` | `static void Group*(StringBuilder, ...)` | DXF group code + değer çiftleri yazar. |

---

### Export/DwgExportService.cs — `DwgExportService`

| Metod | İmza | Açıklama |
|---|---|---|
| `DwgExportService` | `DwgExportService(CadDatabase)` | Veritabanıyla başlatılır. |
| `WriteToFile` | `void WriteToFile(string filePath)` | `AcadSharpDocumentBuilder.Build` ile ACadSharp dokümanı oluşturur; `DwgWriter` ile R2004+ DWG olarak kaydeder. |

---

### Export/ExcelExportService.cs — `ExcelExportService`

| Metod | İmza | Açıklama |
|---|---|---|
| `ExcelExportService` | `ExcelExportService(CadDatabase)` | Veritabanıyla başlatılır. |
| `WriteToFile` | `void WriteToFile(string filePath, CalcSheetResult? waste, CalcSheetResult? rain, string projectName, string engineer)` | ClosedXML ile .xlsx yazar; Özet + Metraj + Pis Su + Yağmur Suyu sayfaları oluşturur. |
| `AddSummarySheet` | `void AddSummarySheet(XLWorkbook, string, string)` | Proje bilgileri, sistem istatistikleri ve özet tablo. |
| `AddBomSheet` | `void AddBomSheet(XLWorkbook)` | Grup bazlı boru/armatür metraj sayfası. |
| `AddWasteWaterSheet` | `void AddWasteWaterSheet(XLWorkbook, CalcSheetResult)` | Pis su hesap föyü sayfası. |
| `AddRainWaterSheet` | `void AddRainWaterSheet(XLWorkbook, CalcSheetResult)` | Yağmur suyu hesap föyü sayfası. |
| `StyleLabelColumn` / `StyleSectionHeader` | yardımcı metodlar | Hücre ve satır stillerini uygular (başlık arka planı, font vb.). |

---

Bu rapor, AfneyCad projesinin tüm 9 ana katmanını kapsamaktadır. Toplam belgelenen metod sayısı 300'ü aşmaktadır. Her tabloda metodun tam imzası ve işlevinin Türkçe açıklaması yer almaktadır.