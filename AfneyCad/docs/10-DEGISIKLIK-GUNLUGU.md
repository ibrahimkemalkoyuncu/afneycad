# 10 — Değişiklik Günlüğü (Kod Denetimi Oturumu)

Kaynak: 7 paralel salt-okunur tarama (625 kaynak dosya, ~95.000 satır) + sonraki düzeltmeler.
Her satır: Özellik ID — Problem — Değişiklik — Test — Risk. Tüm derlemeler 0 hata; tam test paketi **785/785**.

## P0 — Veri kaybı / kritik (commit `b12af87`, 2026-09-23)

| ID | Problem | Değişiklik (dosya) | Test | Risk |
|----|---------|--------------------|------|------|
| AUD-01 | DXF export `Encoding.ASCII` her Türkçe karakteri `?` yapıyordu | `DxfWriterService.Group()` → `\U+XXXX` kaçışı | `DxfWriter_TurkishText_…` | Düşük (dosya ASCII kalır) |
| AUD-02 | Kilitli/dondurulmuş katman seçilip grip ile düzenlenebiliyordu | `SelectionManager.IsSelectable()` (4 giriş noktası) | `SelectionManager_LockedLayer_…` | Düşük |
| AUD-03 | `IsModified` hiç true olmuyordu; sekme/uygulama kapanışında uyarı yoktu | `MainWindow.xaml.cs`, `MainWindow.Layers.cs` (Kaydet/Kaydetme/İptal) | Manuel (UI) — **DOĞRULANMADI** | Orta (UI akışı) |
| AUD-04 | AutoSave yalnızca ilk sekmeye bağlıydı | `RebindAutoSave()` + `OnTabChanged` | Manuel (UI) — **DOĞRULANMADI** | Düşük |
| AUD-05 | RotateCommand undo matrisi bayat `_currentAngle` kullanıyordu | `RotateCommand.cs` | Kod incelemesi | Düşük |
| AUD-06 | Boru branşmanı eski boruyu undo'yu atlayarak siliyordu | `RoutePipeCommand` + `OnDrawPipeCommand` (event → `RemoveEntityOperation`) | Manuel — **DOĞRULANMADI** | Orta |
| AUD-07 | `CompositeOperation.Do()` kısmi başarısızlıkta rollback yapmıyordu | `CompositeOperation.cs` (atomik) | `CompositeOperation_WhenSubOperationThrows_…` | Düşük |
| AUD-08 | DWG export desteklenmeyen entity'leri sessizce atıyordu | `AcadSharpDocumentBuilder` / `DwgExportService` / `OnExportDwgCommand` (uyarı) | `AcadSharpDocumentBuilderTests` (mevcut) | Düşük |

## P1 (commitler `3eca220`, `86bd1ef`, `0e8a0b5`, `e7002ea`)

| ID | Problem | Değişiklik | Test | Risk |
|----|---------|------------|------|------|
| AUD-09 | Polyline'da diklik snap'i hep ilk segmente bakıyordu | `SnapEngine.CalculatePerpendicularSnap` | `SnapEngine_Perpendicular_…` | Düşük |
| AUD-10 | Trim/Extend'de LwPolyline sınır olamıyordu | `TrimCommand`, `ExtendCommand` (+ortak yardımcılar) | `Trim_Line_CanBeTrimmedAgainstPolylineBoundary` | Düşük |
| AUD-11 | RoutePipeCommand standart adını "TS EN 12056" sabitliyordu → PPRC/Steel için hep `null`; Steel verisi yoktu | `StandardsLibrary` (+DIN 2440 Steel, `GetStandardForMaterial`), `RoutePipeCommand` | `StandardsLibrary_*` | Düşük |
| AUD-12 | Export servisleri doğrudan hedefe yazıyordu (çökmede dosya bozulur) | `IO/AtomicFile` + DXF/DWG/IFC/Excel/Word | `AtomicFile_WhenWriteFails_…` + harness 7/7 | Düşük |
| AUD-13 | Lisans: demo key Release'te geçerli, 16-bit checksum, `GenerateKey` public | `#if DEBUG`, checksum 32-bit, `internal` + `tools/Afney.Cad.LicenseTool` | Harness 5/5 (DEBUG/RELEASE) | **Yüksek: eski anahtarlar geçersiz** (kullanıcı onayıyla) |

## P2 (commit `1baeaa2`, 2026-09-24)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-14 | ESC grip sürüklemeyi sıfırlamıyordu | `CadViewport.Input.cs` | Manuel — **DOĞRULANMADI** |
| AUD-15 | Blok `Transform` Rotation/Scale'i uygulamıyordu (ROTATE/SCALE blokta işe yaramıyordu) | `BlockReferenceEntity.Transform` | `BlockReference_Transform_*` (+ters dönüşüm) |
| AUD-16 | Sıfır uzunluklu Line/Polyline segmenti/Ölçü oluşturulabiliyordu | `LineCommand`, `PolylineCommand`, `LinearDimCommand` | `LineCommand_SecondClickAtSamePoint_…` |
| AUD-17 | `ThermalExpansionService` payda ≤ 0'da sessiz yanlış sonuç | fail-loud `InvalidOperationException` | `ThermalExpansion_ValveBelowPrecharge_Throws` |
| AUD-18 | OffsetCommand kopya rengini katman adına göre tahmin ediyordu | Kaynak rengi `_sourceColors` ile korunuyor | Kod incelemesi |

## Bilinçli olarak dokunulmayanlar
- `CadEngine.cs`, `AdvancedSnapService.cs` — doğrulanmış ölü kod; kullanıcı silmeyi reddetti.
- Bkz. `08-TEKNIK-BORC.md`.

## P2 devamı (2026-09-24)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-19 | Grip düzenleme geri alınamıyordu; sürükleme `UpdateEntity` çağırmadığından uzamsal indeks/topoloji güncellenmiyordu; ESC nesneyi yarım stretch'te bırakıyordu | `GripEditOperation` (yeni), `TransactionManager.RecordApplied` (yeni), `CadViewport.Input.cs` (durum yakalama, bırakınca tek birleşik işlem, ESC'de geri dönüş; bağlı dirsek/komşu boru dahil) | `GripEdit_RecordApplied_…`, `GripEdit_UpdateEntity_…` (787/787). Arayüzde elle denenmedi — **DOĞRULANMADI** |

| AUD-20 | `ValveLibraryDialog` vana yerleştirirken mevcut boruyu bölüp siliyordu (2 ekleme + 1 silme + vana) doğrudan veritabanına — geri alınamıyordu | Dialog `TransactionManager` alıyor; tüm adımlar tek `CompositeOperation` ("Vana yerleştir"); çağıran `OnValveLibrary` güncellendi | Derleme + 787/787; arayüzde elle denenmedi — **DOĞRULANMADI** |

## P0 devamı — gerçek üretim veri kaybı olayı (2026-09-30)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-20 | `SaveToFile` kaydetmeden önce hedef dosyayı hiç yedeklemiyordu. Gerçekte tekrarlanan olay: sert kapatma (force-kill) sonrası kalıntı `.session.lock` → `CheckCrashRecovery` modal diyaloğu → eski/neredeyse-boş bir otomatik kayıt etkin belgeye yüklendi → Kaydet'e basıldı → kullanıcının 1.3MB'lık gerçek mimari projesi (`ornek_proje.dwg`) 8KB'a düştü. `.bak` dosyasından (AutoCAD'in kendi yedeği, dokunulmamış) tam kurtarılabilirlik salt-okunur test edilerek doğrulandı (42.163 entity, 43 katman). | Yeni `Afney.Cad.Infrastructure.IO.FileBackup.BackupBeforeOverwrite` — kaydetmeden önce mevcut dosyayı AutoCAD'in `.bak` kuralıyla yedekler; `MainWindow.FileOps.SaveToFile` bunu çağırıyor | `FileBackup_*` (3 test, 790/790) |

Not: Gerçek kullanıcı dosyası (`ornek_proje.dwg`) bu oturumda onaylı kurtarma bekliyor — bkz. TB-14.

## TB-09 doğrulama tamamlandı (2026-09-30)

31 MechanicalCommands + 35 BasicCommands dosyası tarandı — RoutePipeCommand dışında
TransactionManager'ı atlayan başka bir undo-bypass bulunmadı. RouteDuct/RiserPipe'ta
branşman/split mantığı hiç yok (risk kapsamı dışı). Room/Mahal komutlarının tümü
callback zinciriyle doğru şekilde Submit ediyor. Tek bulgu (`ExecuteCommand`'ın ulaşılamaz
"MAHAL" dalındaki boş callback) canlı kod yolunda değil, dokunulmadı. Kod değişikliği yok,
sadece doğrulama — bkz. TB-09.

## TB-15 devamı (2026-09-30)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-21 | `XrefManagerDialog` (Attach/Detach), `WallParallelRouteDialog` (otomatik rota), `DrawingUpdateDialog` (etiket sil+ekle), `LayoutSheetDialog` (blok yerleştir/patlat/tümünü patlat/referans kaldır/tüm katları yakala — en riskli olan, `ExplodeAll` kendi UI metninde "geri alınamaz" diye uyarıyordu ve gerçekten öyleydi) doğrudan `_database.AddEntity/RemoveEntity` çağırıyordu. Hepsine opsiyonel `TransactionManager` parametresi eklendi; mutasyonlar `AddEntityOperation`/`RemoveEntityOperation`/`CompositeOperation` ile geri alınabilir hale getirildi. `WasteWaterCalcSheetDialog` da `DrawingUpdateDialog`'a geçirmek için parametre aldı. | Tam çözüm derlemesi (0 hata) + 790/790 mevcut test (regresyon yok). Dialog UI mantığı WPF context gerektirdiği için ayrı unit test eklenmedi — mevcut codebase konvansiyonuyla tutarlı. |

## TB-15 büyük ölçüde kapatıldı (2026-10-07)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-22 | `MainWindow.Engineering.Reports.OnAutoAnnotate/OnClearAnnotations` doğrudan `_database.AddEntity/RemoveEntity` kullanıyordu; `AutoAnnotationService.ClearAnnotations()` silmeyi kendisi yapıyordu (TransactionManager'a erişimi yok). `FixtureLibraryDialog` (vitrifiye ekleme) de doğrudan ekliyordu. | `AutoAnnotationService`'e geri alınabilir `FindAnnotations()` eklendi (`ClearAnnotations()` geriye dönük uyumlu kaldı). MainWindow artık `CompositeOperation` ile ekliyor/siliyor. `FixtureLibraryDialog` opsiyonel `TransactionManager` alıyor. | `AutoAnnotationService_FindAnnotations_*`, `AutoAnnotationService_ClearAnnotations_*` (792/792) |

Not: `ExportRiserDxf`'teki 3 `db.AddEntity` incelendi — geçici/tek-kullanımlık bir `CadDatabase`'e (canlı belge değil, sadece DXF/DWG export için) yazıyor; undo kapsamı dışı, yanlış pozitifti.

## TB-02 kısmen kapatıldı (2026-10-08)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-23 | `AcadSharpDocumentBuilder.ToAcad()` yalnızca 6 entity tipini (Line/Circle/Arc/Text/LwPolyline/Pipe) destekliyordu; Spline/Hatch/Solid/Duct DWG export'ta tamamen kayboluyordu. | `ToAcad` artık liste döndürüyor (tek→çoklu dönüşüm desteği için). Spline → ACadSharp'ın gerçek `Spline` sınıfı (DXF yazıcının aksine TAM NURBS, yaklaşık değil). Hatch → kapalı sınır polyline (dolgu kapsam dışı, dokümante). Solid → `BRepTessellator` ile üçgenlenip her üçgen bir `Face3D` (DxfWriterService.WriteSolid ile aynı teknik). Duct → Pipe gibi genişlikli polyline. | `Build_SplineHatchSolidDuct_AreNoLongerSkipped`, `Build_Spline_PreservesControlPointCount` (794/794) |

Kalan kapsam dışı: Dimension, Elbow/Tee/Wall/Room gibi MEP fitting'leri — bkz. `08-TEKNIK-BORC.md` TB-02.

## TB-03 kısmen kapatıldı — DWG/DXF (2026-10-08)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-24 | `DwgImportService`/`DxfImportService`'in ana switch'leri eşleşmeyen tipleri (Mesh/Solid3D/MLeader/Table/Leader/Viewport/Ray/XLine/Dimension[sadece DXF] vb.) hiçbir uyarı olmadan atlıyordu. Düz TEXT entity'lerinde (`\U+XXXX`, eski DWG'lerde Türkçe karakter kaydı) hiç kaçış çözümü yapılmıyordu — sadece MTEXT'te vardı; DXF'te MText dahil HİÇ yoktu. | Her iki servise `LastUnsupportedEntityCounts` (ConcurrentDictionary) eklendi; `CadImporter` bunu aktarıyor; `MainWindow.FileOps.LoadDwgEntities` kullanıcıya "Kısmi İçe Aktarım" uyarısı gösteriyor. `\U+` çözümü `DwgImportService.DecodeUnicodeEscapes` (internal static) olarak ayrıştırıldı — MTEXT'in format-komutu temizliğinden bağımsız; `MapText`'e ve DXF'in hem MText hem TextEntity dallarına uygulandı. | `ImportDwg_PlainTextEntity_DecodesUnicodeEscapes`, `ImportDwg_UnsupportedEntityType_IsCountedNotSilentlyDropped` (796/796) |

## TB-04 kapatıldı — büyük DWG importunda UI donması (2026-10-09)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-25 | `LoadDwgInternal` sadece `CadImporter.Import`'u arka plana (`Task.Run`) alıyordu; `await` sonrası çağrılan `LoadDwgEntities`'in kendi ağır işi — katman renk çıkarma (LINQ GroupBy), outlier temizleme (`AsParallel`/`Parallel.ForEach`), kısa-çizgi filtreleme (bir `Parallel.ForEach` daha), binlerce `_database.AddEntity` çağrısı ve `Viewport.ZoomExtents()` — UI thread'inde senkron çalışıyordu. Büyük DWG'lerde (örn. 42.000+ entity) arayüz saniyelerce "Yanıt Vermiyor" durumuna giriyordu. Önkoşul bulgusu: `CadViewport.InvalidateViewport()` (`_database.EntityAdded/Removed` olayına bağlı, `CadCanvas.InvalidateVisual()` çağırıyor) dispatcher-korumasızdı — bu nedenle `_database.AddEntity`'yi naif şekilde arka plana taşımak cross-thread `InvalidOperationException` fırlatırdı. Tüm `EntityAdded/Removed/Updated` abonelikleri (`CadEngine`, `SpatialQueryService`, `IsoSyncService`, `MechanicalKernel`, `SelectionManager`, `MainWindow.xaml.cs` IsModified, `CadViewport`) tek tek tarandı — `CadViewport` dışında hiçbiri WPF kontrolüne dokunmuyordu. | `CadViewport.InvalidateViewport()` artık `CadCanvas.CheckAccess()` kontrolü yapıyor, UI thread değilse `Dispatcher.BeginInvoke` ile atlıyor. `LoadDwgInternal`'daki `Task.Run` artık import'u VE `LoadDwgEntities`'in tamamını kapsıyor (`LoadDwgEntities`'in iç mantığına dokunulmadı — zaten `Dispatcher.Invoke` kullanan layer paneli yenileme bloğu aynı kaldı). `LoadDwgEntities`'in tek doğrudan WPF dokunuşu (`StatusText.Text` ataması) `Dispatcher.Invoke`'a alındı. | Davranışsal/UI-thread fix'i — build 0 hata, 796/796 mevcut test (regresyon yok); büyük dosyada arayüz donmasının gözle doğrulanması kullanıcıya bırakıldı — **DOĞRULANMADI** |

## TB-05 kapatıldı — DB olay abonelik sızıntısı (2026-10-09)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-26 | `CadViewport.Initialize()` isimsiz lambda ile `_database.EntityAdded/EntityRemoved`'a abone oluyordu; `Dispose()` bu lambdayı elinde tutmadığı için aboneliği koparamıyordu. Risk: `CadViewport` native SKPaint kaynaklarını Dispose ettikten sonra, Database nesnesi hâlâ (başka bir referans zincirinden) erişilebilir kalırsa, abonelik üzerinden `InvalidateViewport()` çağrılıp dispose edilmiş kaynaklara dokunabilirdi. Canlı kod yolunda diğer aday aboneler incelendi: `MainWindow.xaml.cs`'deki ctx-düzeyi abonelikler Database/MechanicalKernel/ctx'in aynı `CadDocumentContext` tarafından birlikte sahiplenilip birlikte Dispose edilmesi nedeniyle gerçek bir sızıntı değil (GC erişilemez-ada toplar); `CadEngine`/`AdvancedSelectionService`'in statik singleton aboneliği ise hiçbir yerde örneklenmeyen ölü koddur (Group C). | `_database.EntityAdded += (e) => InvalidateViewport()` → adlandırılmış `OnDatabaseEntityChanged` metoduna geçirildi; `CadViewport.Dispose()` artık `-= OnDatabaseEntityChanged` ile koparıyor. | Davranışsal (olay aboneliği) — build 0 hata, 796/796 mevcut test (regresyon yok); native kaynak dispose sırasını birim testiyle doğrulamak pratik değil |

Kalan: IFC importu (`IfcImportService`) bu turda denetlenmedi.

## TB-07 kısmen kapatıldı — dialog try/catch sertleştirmesi (2026-10-09)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-28 | 94 dialogdan 13'ü hiç `catch` içermiyordu — servis çağrısı (parse edilen kullanıcı girdisiyle hesaplama, dosya I/O) bir istisna fırlatırsa WPF Dispatcher'a kadar yükselip UYGULAMAYI ÇÖKERTİYORDU (sessiz veri kaybı değil ama tüm oturumun kaybı). | 10 dialogun hesaplama/export metodu try/catch + Türkçe "Hata" MessageBox'ı ile sarıldı: `AHUDesignDialog.Calculate_Click`, `ArchitecturalLibraryDialog.Insert_Click`, `EnergyPerformanceDialog.Calculate_Click`/`ExportHtml_Click`, `FanSelectionDialog.Search`, `FloorHeatingDialog.Calculate_Click`/`ExportHtml_Click`, `HeatPumpDialog.Calculate_Click`, `LayerStateManagerDialog.SaveCurrent_Click`/`Apply_Click`, `PumpGroupDialog.ExportHtml_Click`, `PumpSelectionDialog.RunSelection`/`CheckCavitation_Click`, `SprinklerDesignDialog.Calculate_Click`. Hesaplama MANTIĞI hiçbir yerde değişmedi — sadece hata artık sessizce çökmek yerine görünür bir MessageBox'a dönüşüyor. Kalan 3 dialog (`NewProjectWizardDialog`, `PipeWizardDialog`, `StandardSelectionDialog`) BİLİNÇLİ atlandı — sadece statik şablon/enum okuması, parse/hesap riski yok. | Build 0 hata, 796/796 mevcut test (regresyon yok). Davranış değişikliği SADECE hata yolunda (önceden: çökme: şimdi: MessageBox) — normal/başarılı yol hiç değişmedi |

Kalan (bilinçli, kullanıcı onayıyla ertelendi): ~75/94 dialogun geçersiz girdiyi sessizce varsayılana çevirmesi — her dialogun kendi UX kararını (hangi alan zorunlu, hangi uyarı mesajı) gerektiren çok daha büyük, ayrı bir iş.

## TB-10 kısmen kapatıldı — Arc/Circle Transform yarıçap ölçeklenmemesi (2026-10-10)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-31 | `ArcEntity.Transform(Matrix4x4)` SADECE `Center`'ı dönüştürüyordu, `Radius`'a hiç dokunmuyordu (kod içinde "Radius transform (Uniform scaling varsayımı)" yorumu bırakılmıştı — yapılmamış bir TODO). Bir SCALE/MIRROR işlemi (ölçekli matris) yayı yanlış boyutta bırakıyordu. Aynı taramada `CircleEntity.Transform`'da da BİREBİR aynı hata bulundu (ayrı, bağımsız keşif). | `BlockReferenceEntity.Transform` (AUD-15) ile AYNI teknik: matrisin birim-X vektörüne etkisinden (`matrix.Transform(X) - matrix.Transform(0)`'ın uzunluğu) tekdüze ölçek faktörü çıkarılıp `Radius`'a çarpılıyor. Rotasyon/öteleme-ONLY matrislerde scaleFactor=1 — eski davranış bu durumda birebir korunuyor (regresyon yok). | `ArcEntity_Transform_WithScaleMatrix_ScalesRadius`, `ArcEntity_Transform_WithRotationOnly_LeavesRadiusUnchanged`, `CircleEntity_Transform_WithScaleMatrix_ScalesRadius` (799/799) |

## TB-10 devam — TableEntity hücre metni çizilmiyordu (2026-10-10)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-32 | `TableEntity.Draw()`'ın veri-yerleştirme döngüsü hücre metnini okuyup boş olup olmadığını kontrol ediyordu ama dolu olduğunda bile hiçbir çizim yapmıyordu — döngü gövdesi sadece bir yorum satırıydı ("Gerçek uygulamada TextEntity.Draw mantığı buraya entegre edilir" — hiç yapılmamış bir TODO). Tablo ızgarası (çerçeve+satır/sütun çizgileri) doğru çiziliyordu ama BOM/metraj tablolarının İÇERİĞİ hiç görünmüyordu — boş kutular gibi render oluyordu. | `TextEntity.Draw`'daki ile AYNI `IRenderContext.DrawText(text, cellPos, 0, RowHeight * 0.5, Color, centerAlign: false)` çağrısı eklendi — sol kenardan küçük boşlukla, satır yüksekliğinin yarısı font boyutuyla. | `TableEntity_Draw_RendersCellText` — `FakeRenderContext` ile hücre metninin gerçekten `DrawText`'e ulaştığı doğrulandı (800/800) |

## TB-10 devam — HatchEntity grip ile düzenlenemiyordu (2026-10-10)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-33 | `HatchEntity.GetGripPoints()` sınır köşelerini döndürüyordu (grip kutucukları ekranda görünüyordu) ama `MoveGripPointAt` hiç override edilmemişti — taban sınıfın varsayılanı (`InvalidateCache()` dışında hiçbir şey yapmaz) devredeydi. Kullanıcı bir Hatch köşesini sürükleyince grip görsel olarak hareket ediyor GİBİ görünse bile sınır poligonu hiç değişmiyordu. | `LineEntity`/`ArcEntity`'deki aynı basit indeks-eşleştirme deseniyle `MoveGripPointAt(index, newPosition)` eklendi — `BoundaryVertices[index]` doğrudan güncelleniyor. | `HatchEntity_MoveGripPointAt_UpdatesBoundaryVertex` (801/801) |

## TB-10 kapatıldı — kalan 2 alt-madde ölü/yetim kod çıktı (2026-10-10)

Kalan 2 alt-maddeyi kapatmadan önce koda bakıldı: `SchemaLayoutEngine.ComputeLayout` hiçbir yerden çağrılmıyor (`grep -rn "new SchemaLayoutEngine"` boş sonuç) — `FixtureSchema` modelinde 2D konum alanı bile yok, tamamen yetim/tamamlanmamış bir özellik, canlı render yoluna hiç bağlı değil. `PipingRoutingService` de hiçbir yerden çağrılmıyor (`grep -rn "PipingRoutingService"` sadece kendi dosyasında eşleşiyor) — `MechanicalKernel.Pathfinder` SADECE `PipingPathfinderService`'i kullanıyor; "kod tekrarı" denilen şey aslında bir canlı implementasyon + bir ölü kopyaydı. Kullanıcının önceki "ölü kod silinmesin" tercihi gereği ikisine de dokunulmadı; TB-10 bu doğrulamayla KAPATILDI (kod değişikliği gerektiren 3 alt-madde zaten AUD-31/32/33 ile düzeltilmişti).

## TB-08 kapatıldı — ClashReportDialog reflection kaldırıldı (2026-10-10)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-30 | `ClashReportDialog.ApproveClash_Click`/`ClashesDataGrid_SelectionChanged`, `Application.Current.MainWindow as MainWindow` üzerinden `GetField("_mechanicalKernel"/"_selectionManager"/"_database", BindingFlags.NonPublic)` ile MainWindow/CadViewport'un private alanlarına erişiyordu — bu alanlardan biri yeniden adlandırılırsa/taşınırsa derleme hatası vermeden SESSİZCE bozulurdu (reflection `null` döner, kod çökmeden eksik çalışır). | `ClashReportDialog` constructor'ı artık opsiyonel `MechanicalKernel kernel`, `CadDatabase database`, `SelectionManager selectionManager` parametreleri alıyor; 3 reflection bloğu kaldırılıp doğrudan bu alanlara erişime çevrildi. `MainWindow.OnClashDetectionClick` (`MainWindow.Engineering.Architecture.cs`) çağrısı `_mechanicalKernel`/`_database`/`_activeContext?.SelectionManager`'ı geçiriyor. | Build 0 hata, 796/796 mevcut test (regresyon yok). Davranış değişmedi — sadece reflection yerine tip-güvenli parametre geçişi |

## TB-07 devam — en kritik dialoglarda sessiz-varsayılan görünürlüğü (2026-10-10)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-29 | Can/mal güvenliği etkileyen hesap diyaloglarında (`FireFightingDialog`, `GasCalcDialog`, `RainWaterCalcDialog`, `SprinklerDesignDialog`) boş/geçersiz sayısal alanlar SESSİZCE bir varsayılana düşüyordu — kullanıcı kendi girdisinin kullanılmadığını fark etmeden yangın/gaz/yağmur suyu hesabının "kendi değerleriyle" yapıldığını sanabiliyordu. | Yeni `Dialogs/NumericInputHelper.cs` (`GetDoubleOrWarn`/`GetIntOrWarn`) — AYNI varsayılana-düşme davranışını korur (hesap SONUCU hiç değişmedi), sadece hangi alanların varsayılana düştüğünü bir listede toplar. 4 dialogun en kritik hesap metotları (`FireFightingDialog.SprinklerCalculate_Click`/`HydrantCalculate_Click`, `GasCalcDialog.Calculate_Click`, `RainWaterCalcDialog.Calculate_Click`, `SprinklerDesignDialog.Calculate_Click`) bunu kullanacak şekilde güncellendi; varsayılana düşen alan varsa hesaptan ÖNCE tek bir uyarı MessageBox'ı gösteriliyor. `GasCalcDialog.Calculate_Click`/`BuildOptions` ayrıca try/catch almadığı için o da eklendi. `SprinklerDesignDialog`'da ESKİ `>0` kontrol mantığı (sadece boş/parse-hatası değil, "0 veya negatif" de varsayılana düşürüyordu) BİREBİR korundu — sadece görünürlük eklendi, eşik mantığı değişmedi. `SepticTankDialog`/`WasteWaterDesignDialog` incelendi, zaten `Parse`/açık doğrulama kullandıkları (sessiz varsayılan yok) için dokunulmadı. | Build 0 hata, 796/796 mevcut test (regresyon yok). Davranış değişikliği SADECE görünürlükte — varsayılan değerler ve hesap sonuçları birebir aynı |

## TB-06 kısmen kapatıldı — CSG Boolean tessellation önbellekleme (2026-10-09)

| ID | Problem | Değişiklik | Test |
|----|---------|------------|------|
| AUD-27 | `SolidClassifier.IsPointInside(Solid, ...)` her çağrıda `BRepTessellator.Tessellate(solid)`'i yeniden çalıştırıyordu. `SegmentBasedSubdivider.SubdivideAndClassifyOutside`'ın hot-loop'u (`ClassifyAndCollect`, her A-Face/fragman için bir kez) AYNI `classifyAgainst` Solid'ine karşı tekrar tekrar bu yeniden-tessellate'i tetikliyordu — Solid o döngü boyunca mutasyona uğramadığından (sadece `a.Faces` değişir) bu tamamen gereksiz tekrar işti; büyük/çok-yüzlü Solid'lerde O(fragman×yüzey) maliyete yol açıyordu. `GeneralSolidUnion`'ın 6 tam `CloneSolid` çağrısı da aynı madde altında incelendi: dosya başı yorumları her klonun ayrı bir canlı-testte-bulunan doğruluk hatasını (ambiguous-coplanar/teğet-kesişim/ışın-sayım yanlış sınıflandırması) önlediğini ayrıntılı belgeliyor — BİLİNÇLİ bırakıldı, dokunulmadı. | `SolidClassifier`'a önceden-tessellate edilmiş veri alan `IsPointInside(vertices, faces, point, ...)` overload'ı eklendi (eski `IsPointInside(Solid, ...)` artık buna delege ediyor, davranış birebir aynı). `SegmentBasedSubdivider.SubdivideAndClassifyOutside` `classifyAgainst`'i döngüden ÖNCE bir kez tessellate edip sonucu `ClassifyAndCollect`'e geçiriyor. | Build 0 hata, 796/796 mevcut test (regresyon yok). Saf performans fix'i — sonuç/davranış değişmedi, yeni birim testi gerektirmedi |
