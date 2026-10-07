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
