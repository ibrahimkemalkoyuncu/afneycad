# 08 — Teknik Borç (Denetimle Doğrulanmış, Açık Kalanlar)

Durum: 2026-09-24. Her madde denetimde koddan doğrulandı; "UNVERIFIED" işaretliler doğrulanmadı.

## A. Kalıcı ürün riskleri
| ID | Bulgu | Öncelik |
|----|-------|---------|
| TB-01 | ~~Grip sürükleme geri alınamıyor~~ — **KAPATILDI (AUD-19)**: `GripEditOperation` + `TransactionManager.RecordApplied`; ESC eski konuma döner; bırakınca `UpdateEntity` (indeks + topoloji). UI'da elle denenmedi — DOĞRULANMADI. | ✔ |
| TB-12 | `CadDatabase.UpdateEntity` → `QuadTree.Remove` güncel (taşınmış) bbox ile arıyor; eski düğümlerde artık girişler kalıyor (işlevsel etkisi yok, bellek şişmesi). | P3 |
| TB-13 | ~~`SaveToFile` kaydetmeden önce hedef dosyayı yedeklemiyordu~~ — **KAPATILDI (AUD-20)**: gerçek üretimde tekrarlanan olay (1.3MB proje 8KB'a düştü — kurtarma akışı + Kaydet kombinasyonu). `FileBackup.BackupBeforeOverwrite` eklendi (AutoCAD `.bak` kuralı). | ✔ |
| TB-14 | Kurtarma akışı (`CheckCrashRecovery`) modal `MessageBox` ile çalışıyor; sert kapatma (force-kill/görev yöneticisi) sonrası kalıntı `.session.lock` her başlatmada bu diyaloğu tetikliyor — kullanıcı yanlışlıkla eski/boş bir otomatik kaydı etkin belgeye yükleyebilir. TB-13 ile risk azaldı (Kaydet artık yedekliyor) ama diyalog UX'i hâlâ belirsiz ("düzgün kapanmadı" güven aralığı olmadan). | P2 |
| TB-02 | DWG/DXF export kapsamı dar: `AcadSharpDocumentBuilder` yalnızca Line/Circle/Arc/Text/LwPolyline/Pipe dönüştürüyor (artık kullanıcıya uyarı veriyor ama veri yine de dışlanıyor). DXF yazıcı Spline/Hatch'i yaklaşık (çizgi) yazıyor. | P1 |
| TB-03 | DWG/IFC importları desteklenmeyen tipleri (3DSOLID, Mesh, MLeader, Table…) uyarısız atıyor; düz `TEXT`'te `\U+` çözülmüyor. | P1 |
| TB-04 | `LoadDwgEntities` büyük DWG'de UI thread'inde `Parallel.ForEach` çalıştırıyor (donma riski). | P1 |
| TB-05 | `CadViewport`/`CadDocumentContext.Dispose` DB olay abonelikleri kopmuyor (sızıntı/asılı callback). | P2 |
| TB-06 | `SolidClassifier` her sorguda solid'i yeniden tessellate ediyor; `GeneralSolidUnion` 6 tam kopya — büyük modellerde ölçeklenmez. | P2 |
| TB-07 | ~20–25 dialogda servis çağrısı `try/catch`'siz (Pump*, Fan, AHU, EnergyPerformance, HeatPump, FloorHeating, AutoRoute…). ~75/94 dialog geçersiz girdiyi sessizce varsayılana çeviriyor. | P2 |
| TB-08 | `ClashReportDialog` MainWindow özel alanlarına reflection ile erişiyor (yeniden adlandırmada sessizce bozulur). | P2 |
| TB-09 | ~~Kalan komutların undo doğrulaması~~ — **KAPATILDI**: 31 MechanicalCommands + 35 BasicCommands dosyası tarandı. Commands projesinde `TransactionManager`'ı atlayan doğrudan `AddEntity/RemoveEntity/UpdateEntity` yok. RouteDuctCommand/RiserPipeCommand'da branşman/split mantığı YOK (RoutePipeCommand'daki hatanın kapsamı dışında). Room/Mahal komutları (CreateRoom/DetectRoom/ManualMahal/EdgeCaptureMahal/RectMahal/SelectRoom) callback ile MainWindow'a devrediyor, tamamı doğru şekilde `TransactionManager.Submit` çağırıyor. Fillet/Chamfer/Solid Boolean taban sınıfları üzerinden doğru submit ediyor. Tek bulgu: `MainWindow.Commands.Console.ExecuteCommand`'ın "MAHAL" case'i boş callback `(room)=>{}` kullanıyor ama bu switch tamamen ulaşılamaz (grep ile doğrulandı — `ExecuteCommand` sadece `"OFFSET"` literaliyle tek yerden çağrılıyor); canlı kullanıcıyı etkilemiyor, dokunulmadı. | ✔ |
| TB-15 | **Presentation'da ~45 doğrudan `AddEntity/RemoveEntity`** (undo'yu atlar) — **KISMEN KAPATILDI**: `ValveLibraryDialog` (commit `7497a3e`), `XrefManagerDialog`, `WallParallelRouteDialog`, `DrawingUpdateDialog`, `LayoutSheetDialog` (Insert/Explode/ExplodeAll/RemoveRef/CaptureAll — en riskli olan; `ExplodeAll`'ın kendi "Bu işlem geri alınamaz" uyarısı artık doğru değildi, metin güncellendi) düzeltildi — hepsi `TransactionManager`/`CompositeOperation` alıp kullanıyor. Kalan: `MainWindow.Engineering.Reports` (4), `FixtureLibraryDialog`, `MainWindow.FileOps` ve ~13 "Çizime Ekle" rapor diyalogu (özet metni ekler — düşük etki, silme yok). | P2 |
| TB-10 | `PipingPathfinderService`/`PipingRoutingService`, `ArcEntity.Transform` (yarıçap ölçeklenmiyor), `HatchEntity` grip düzenleme, `TableEntity` hücre metni çizilmiyor, `SchemaLayoutEngine` yerleşim üretmiyor. | P2 |
| TB-11 | Standart atıfları (NFPA13, TS 825, EN 12056, ASHRAE…) yalnızca isimle; tablo değerleri standart metniyle doğrulanmadı. TS 825 iki farklı yılla anılıyor (:2023/:2024). | P2 |

## B. Gölge çoğaltma (aynı problemin birden çok bağımsız çözümü)
Yönlendirme (3), oda algılama (3), riser diyagramı (4), teknik şartname (3), atık/yağmur suyu boyutlandırma (3), BOM (3), soğutma yükü (2), ısı kaybı (2), fixture-unit tabloları (2), `WCDiameterRule.cs` (2 aynı isim), sistem-katman isimlendirme (2 farklı şema). Geçmişte bu desenden kaynaklanan gerçek hatalar zaten bulunup düzeltilmişti (NFPA13/EN12845 enum çakışması, 3× Colebrook-White, septik formülü, atık su K-faktörü, maliyet anahtar uyuşmazlığı). Konsolide edilmeyenler aynı hata sınıfının adayı.
Ek: iki farklı friction-factor hâlâ var (HotWaterCirculation, FloorHeating Blasius, DuctSizing ampirik).

## C. Ölü / yetim kod (silinmedi)
`CadEngine.cs`, `AdvancedSnapService.cs`, `FileFormats/DxfReader.cs`, `DwgEngine/*` (stub), `AdvancedDxfWriterService`, `AdvancedIfcService`, `ExcelMultiSheetService`, `PathfindingService`, `ComponentSystem` (ECS), `Afney.Cad.Common` (boş proje). Not: `AdvancedDxfWriterService` belgelenmiş "daha doğru" yazıcı ama çağrılmıyor; canlı olan `DxfWriterService`.

## D. Dokümantasyon
`CLAUDE.md` satır sayıları %6–56 bayat (metot envanteri doğru); `SpatialIndex` R-Tree değil QuadTree (yalnız 2D, Z yok sayılır); `Afney.Cad.Common` boş.
