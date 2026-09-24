# 08 — Teknik Borç (Denetimle Doğrulanmış, Açık Kalanlar)

Durum: 2026-09-24. Her madde denetimde koddan doğrulandı; "UNVERIFIED" işaretliler doğrulanmadı.

## A. Kalıcı ürün riskleri
| ID | Bulgu | Öncelik |
|----|-------|---------|
| TB-01 | **Grip sürükleme geri alınamıyor**: `CadViewport.Input.cs` nesneyi canlı değiştiriyor, `TransactionManager`'a işlemiyor (ESC de eski konumu geri getirmiyor). | P1 |
| TB-02 | DWG/DXF export kapsamı dar: `AcadSharpDocumentBuilder` yalnızca Line/Circle/Arc/Text/LwPolyline/Pipe dönüştürüyor (artık kullanıcıya uyarı veriyor ama veri yine de dışlanıyor). DXF yazıcı Spline/Hatch'i yaklaşık (çizgi) yazıyor. | P1 |
| TB-03 | DWG/IFC importları desteklenmeyen tipleri (3DSOLID, Mesh, MLeader, Table…) uyarısız atıyor; düz `TEXT`'te `\U+` çözülmüyor. | P1 |
| TB-04 | `LoadDwgEntities` büyük DWG'de UI thread'inde `Parallel.ForEach` çalıştırıyor (donma riski). | P1 |
| TB-05 | `CadViewport`/`CadDocumentContext.Dispose` DB olay abonelikleri kopmuyor (sızıntı/asılı callback). | P2 |
| TB-06 | `SolidClassifier` her sorguda solid'i yeniden tessellate ediyor; `GeneralSolidUnion` 6 tam kopya — büyük modellerde ölçeklenmez. | P2 |
| TB-07 | ~20–25 dialogda servis çağrısı `try/catch`'siz (Pump*, Fan, AHU, EnergyPerformance, HeatPump, FloorHeating, AutoRoute…). ~75/94 dialog geçersiz girdiyi sessizce varsayılana çeviriyor. | P2 |
| TB-08 | `ClashReportDialog` MainWindow özel alanlarına reflection ile erişiyor (yeniden adlandırmada sessizce bozulur). | P2 |
| TB-09 | Kalan MechanicalCommands (28/30) ve ~15 BasicCommands dosyası denetlenmedi — **UNVERIFIED**; RouteDuct/RiserPipe'ta RoutePipe'taki gibi undo sorunu olabilir. | P1 (doğrulama) |
| TB-10 | `PipingPathfinderService`/`PipingRoutingService`, `ArcEntity.Transform` (yarıçap ölçeklenmiyor), `HatchEntity` grip düzenleme, `TableEntity` hücre metni çizilmiyor, `SchemaLayoutEngine` yerleşim üretmiyor. | P2 |
| TB-11 | Standart atıfları (NFPA13, TS 825, EN 12056, ASHRAE…) yalnızca isimle; tablo değerleri standart metniyle doğrulanmadı. TS 825 iki farklı yılla anılıyor (:2023/:2024). | P2 |

## B. Gölge çoğaltma (aynı problemin birden çok bağımsız çözümü)
Yönlendirme (3), oda algılama (3), riser diyagramı (4), teknik şartname (3), atık/yağmur suyu boyutlandırma (3), BOM (3), soğutma yükü (2), ısı kaybı (2), fixture-unit tabloları (2), `WCDiameterRule.cs` (2 aynı isim), sistem-katman isimlendirme (2 farklı şema). Geçmişte bu desenden kaynaklanan gerçek hatalar zaten bulunup düzeltilmişti (NFPA13/EN12845 enum çakışması, 3× Colebrook-White, septik formülü, atık su K-faktörü, maliyet anahtar uyuşmazlığı). Konsolide edilmeyenler aynı hata sınıfının adayı.
Ek: iki farklı friction-factor hâlâ var (HotWaterCirculation, FloorHeating Blasius, DuctSizing ampirik).

## C. Ölü / yetim kod (silinmedi)
`CadEngine.cs`, `AdvancedSnapService.cs`, `FileFormats/DxfReader.cs`, `DwgEngine/*` (stub), `AdvancedDxfWriterService`, `AdvancedIfcService`, `ExcelMultiSheetService`, `PathfindingService`, `ComponentSystem` (ECS), `Afney.Cad.Common` (boş proje). Not: `AdvancedDxfWriterService` belgelenmiş "daha doğru" yazıcı ama çağrılmıyor; canlı olan `DxfWriterService`.

## D. Dokümantasyon
`CLAUDE.md` satır sayıları %6–56 bayat (metot envanteri doğru); `SpatialIndex` R-Tree değil QuadTree (yalnız 2D, Z yok sayılır); `Afney.Cad.Common` boş.
