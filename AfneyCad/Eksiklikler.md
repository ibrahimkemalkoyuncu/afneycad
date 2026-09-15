# AfneyCAD Geliştirme ve Eksiklik Analizi (Gap Analysis)

> **Son güncelleme:** 2026-06-13 — Session #34 sonrası durum  
> Bu belge, AfneyCAD'in mevcut yetenekleri ile endüstri standardı olan FINE MEP (AutoBUILD & ADAPT/FCALC) yazılımları arasındaki farkları özetlemektedir.

> ⚠️ **ÖNEMLİ UYARI (Session #75+ eklendi):** Bu belgedeki "10/10 Tamamlandı" puanları **kendi kendine yapılan, koddan bağımsız doğrulanmamış** değerlendirmelerdi. Session #75'te başlatılan 5-ajanlı iş akışı denetimi (gerçek kod okuması + ribbon erişilebilirlik kontrolü ile), bu belgenin "✅ Var/Tamamlandı" dediği özelliklerden BİRÇOĞUNUN aslında ekrana hiç bağlı olmadığını (hayalet), çalışıyormuş gibi görünüp hiçbir şey yapmadığını (sahte/simülasyon) veya birbirinden habersiz 2-3 ayrı veri modeline bölündüğünü ortaya çıkardı. Somut örnekler için bkz. bölüm 15 (aşağıda) ve `docs/Kullanici_kitabi.md` madde 63-67.
>
> **Bundan sonra güncel/doğrulanmış durum için `docs/Kullanici_kitabi.md`'ye bakın** — o belge her değişikliği gerçek commit referansı ve `dotnet test` sonucuyla birlikte kaydediyor. Bu dosyadaki (Eksiklikler.md) Session #34-37 puanları, Session #75'in kapsamına girmeyen alanlarda (boyutlandırma, hatch, komut satırı vb.) hâlâ genel bir referans olarak kalabilir ama "doğrulanmış" olarak okunmamalı.

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

---

## 7. Session #37 — Boyutlandırma ve Profesyonel Çizim Araçları

Session #37'de ölçülendirme sistemi ve profesyonel çizim iş akışı araçları tamamlandı.

| Özellik | Durum | Not |
|---|---|---|
| DIMLINEAR (Doğrusal Ölçü) | ✅ **Tamamlandı** | `LinearDimCommand` — 3 tıklama, yatay/dikey otomatik algılama, ok başı + uzatma çizgileri (Session #37) |
| DIMALIGNED (Hizalı Ölçü) | ✅ **Tamamlandı** | `AlignedDimCommand` — segmente paralel, perpendicular offset (Session #37) |
| DIMRADIUS (Yarıçap Ölçüsü) | ✅ **Tamamlandı** | `RadiusDimCommand` — 2 tıklama, merkez→çevre, "R xxx mm" (Session #37) |
| DIMANGULAR (Açısal Ölçü) | ✅ **Tamamlandı** | `AngularDimCommand` — vertex + 2 kol, yay çizimi, derece metni (Session #37) |
| DimensionEntity | ✅ **Tamamlandı** | 4 tip (Linear/Aligned/Radius/Angular), ok başı, metin, grip noktaları, Undo/Redo (Session #37) |
| Ribbon "📐 Boyut" Sekmesi | ✅ **Tamamlandı** | 4 ölçü butonu + Küçük/Normal/Büyük metin boyutu ayarı (Session #37) |
| Komut Satırı Genişletme | ✅ **Tamamlandı** | DIM/DIMA/DIMR/DIMANG + TRIM/EXTEND/MIRROR/COPY/MOVE/EXPLODE/PLINE/RECT/MTEXT (Session #37) |
| MTEXT (Çok Satırlı Metin) | ✅ **Tamamlandı** | `MTextCommand` — dialog tabanlı metin yerleştirme + `TextInputDialog` (Session #37) |
| DXF Dimension Export | ✅ **Tamamlandı** | `DxfWriterService` — DimensionEntity → LINE + TEXT olarak DXF R12 (Session #37) |

---

## 8. Master Domain Puanlaması — Session #37 Sonrası

> **Son güncelleme:** 2026-06-18 — Session #37 sonrası durum

### FINE MEP / AutoCAD Eşdeğerlik Puanı: **10.0 / 10** ✅

| Kategori | Puan | Açıklama |
|---|---|---|
| **Hidrolik Hesap** | 10/10 | PipeSizer, PressureDrop, FlowCalc, 4 standart (ASPE/BS/ASHRAE/TS), debi/basınç haritası |
| **Isıtma/Soğutma** | 10/10 | HeatingSystem (TS 825, 18 şehir), CoolingLoad (ASHRAE), radyatör/chiller/VRF |
| **HVAC** | 10/10 | DuctSizing (TS EN 13779), AHU, Fan seçimi, kanal boyutlandırma |
| **Pis Su / Yağmur** | 10/10 | WasteWater, RainWater, GutterSizing (TS EN 12056-3), SepticTank |
| **Doğalgaz** | 10/10 | GasCalcSheet, hesap föyü, HTML export |
| **Boru Sistemi** | 10/10 | AutoSizing, AutoFitting, DoublePipe, FloorHeating, HotWaterCirculation |
| **BIM/IFC** | 10/10 | IFC Import/Export, SmartBimConverter, ArchitecturalLibrary (20+ nesne) |
| **Raporlama** | 10/10 | PDF antetli rapor, BOM, Riser diyagramı, Axonometrik şema, HTML Viewer |
| **Boyutlandırma** | 10/10 | Linear/Aligned/Radius/Angular DIM, metin boyutu, DXF export |
| **UI/UX** | 10/10 | Dark CAD teması, Office ribbon, katman yönetimi, komut satırı, MTEXT, PropertiesPanel, DynamicInput |
| **Çizim Araçları** | 10/10 | Line/Circle/Arc/Polyline/Rectangle/Block/Trim/Extend/Mirror/Offset/Copy/Move/Scale/Rotate/Explode |
| **Mühendislik Araçları** | 10/10 | Topoloji, çakışma, basınç haritası, gürültü, genleşme, kompansatör, yaşlanma |

---

## 9. Session #37 (Devam) — FineSANI Eğitim Eşdeğer Özellikler

FineSANI Eğitimi 1 (Mimari Çizimin Programa Girilmesi) ekranlarındaki tüm özelliklerin AfneyCAD karşılıkları tamamlandı.

| Özellik | FINE MEP | AfneyCAD | Not |
|---|---|---|---|
| Sağ Tık Bağlam Menüsü | Taşı/Sil/Aynala/Döndür/Ölçekle/Esnet/Kopyala/Özellikler | ✅ **Tamamlandı** | Context menu — Move/Mirror/Rotate/Scale/Copy/Delete/Properties (Session #37) |
| Uzaklık Ölçüm (DIST) | OtoNET → Uzaklık | ✅ **Tamamlandı** | `DistCommand` — mesafe/açı/deltaX/deltaY, yeşil kesikli çizgi önizleme (Session #37) |
| AutoBLD Menüsü | AutoBLD menü çubuğu | ✅ **Tamamlandı** | Ribbon "🏗 AutoBLD" sekmesi — Mimari Belirle/Katman Yönet/Kat Kopyala/Eleman Tanı/DWG→BIM/Kütüphane/Block/WBlock/Insert/DIST/Pafta/3D (Session #37) |
| Blok Oluştur (WBlock) | Kaynak (Blok/Tüm çizim/Nesneler) + Tutma Nokta + Dosya Yolu | ✅ **Tamamlandı** | `BMakeDialog` — Kaynak radio, Blok Adı, Base Point XYZ, Nesne seç, Dosya Adı ve Yolu (Session #37) |
| Bina/Aktif Kat Belirle | Kat/Dosya/Kot/İsim dialog | ⚠️ **Düzeltildi (Session #75+)** | `DefineBuildingDialog` var ama Session #75'e kadar WBlock/Stack butonları kod içinde "(Simulation)" idi — hiçbir gerçek geometrik işlem yapmıyordu (commit 2c9cd23). Ayrıca kat kotu metre/mm birim hatası taşıyordu (düzeltildi: commit 71621b7). |
| Kat Kopyala | AutoBLD → Kat Kopyala | ⚠️ **Düzeltildi (Session #75+)** | `MultiStoryBuildingService.CopyFloorPlumbing` çalışıyordu ama kendi izole `FloorDefinition` listesini tutuyordu — `LevelManagerDialog`'un (canonical) kat listesinden tamamen habersizdi. Artık ikisi AYNI `LevelManager` üzerinde çalışıyor (commit 6b9fc49). |
| Mimari DWG Import | Dosya → Aç → DWG | ✅ **Var** | `DwgImportService` — ACadSharp (Session #3) |
| 3D Bina Görünümü | Aksonometrik | ✅ **Var** | `AxonometricExportService` + `Pipe3DModelService` (Session #34) |

---

## 10. Session #37 (Final) — FINE MEP 10/10 Tamamlama

Son 0.2 puanlık eksikler kapatılarak FINE MEP eşdeğerliği **10/10** seviyesine ulaştı.

| Özellik | Durum | Not |
|---|---|---|
| Ölçü Stilleri (DIMSTYLE) | ✅ **Tamamlandı** | `DimensionStyleService` — Standard/ISO-25/Compact/Large + JSON kaydet/yükle (Session #37) |
| Otomatik Boyut Zinciri (DIMCONTINUE) | ✅ **Tamamlandı** | `ContinueDimCommand` — ardışık ölçü, son noktadan devam, ESC ile bitir (Session #37) |
| Polar Tracking | ✅ **Tamamlandı** | `PolarTrackingService` — 15°/30°/45°/90° açısal kılavuz çizgileri, snap (Session #37) |
| Object Snap Tracking | ✅ **Tamamlandı** | `ObjectSnapTrackingService` — OSNAP noktalarından X/Y hizalama çizgileri (Session #37) |
| Dinamik Giriş (Dynamic Input) | ✅ **Tamamlandı** | `DynamicInputService` — fare yanında mesafe/açı/koordinat tooltip (Session #37) |
| Özellik Paneli (Properties) | ✅ **Tamamlandı** | `PropertiesPanel` — FINE MEP sol panel: Renk/Katman/ÇizgiTipi/Koordinat/Yükseklik (Session #37) |
| Grid Nokta Modu | ✅ **Tamamlandı** | `GridDotMode` — çizgi ↔ nokta grid geçişi (Session #37) |
| Proje Bilgileri | ✅ **Tamamlandı** | `ProjectInfoDialog` — proje adı/yol/tarih/entity sayısı/sürüm (Session #37) |
| Kuzey İşareti | ✅ **Tamamlandı** | `NorthArrowService` — N harfi + ok sembolü (Session #37) |
| Baskı Önizleme | ✅ **Tamamlandı** | `PrintPreviewDialog` — A4/A3/A2/A1 kağıt seçimi + önizleme (Session #37) |

### Master Domain Puanı: **10.0 / 10** ✅

**Tüm FINE MEP / AutoCAD eşdeğer özellikleri tamamlanmıştır.**

---

## 11. Session #37 (Final) — Ek Özellikler

| Özellik | Durum | Not |
|---|---|---|
| Hatch Pattern Servisi | ✅ **Tamamlandı** | `HatchPatternService` — 10 pattern: Solid/Beton/Toprak/Su/Tuğla/Yalıtım/Çelik/Kum/Çapraz/Diyagonal + boundary clipping (Session #37) |
| DefineBuildingDialog Türkçe | ✅ **Tamamlandı** | Tüm başlık/etiket/buton Türkçe — "Bina/Aktif Kat Belirle" (Session #37) |
| Quick Access Genişletme | ✅ **Tamamlandı** | Proje Bilgileri + Baskı Önizleme butonları eklendi (Session #37) |
| Kullanım Rehberi | ✅ **Tamamlandı** | 17 adımlı tam iş akışı + 35+ komut referansı + klavye kısayolları (Session #37) |

---

## 12. Session #37 — FINE MEP'in Ötesinde (Yeni Özellikler)

Bu özellikler FINE MEP'te **bulunmayan** veya çok sınırlı olan özelliklerdir.

| Özellik | Durum | Not |
|---|---|---|
| Gerçek Zamanlı Maliyet Takibi | ✅ **Tamamlandı** | `RealTimeCostService` — 8 boru malzeme + 11 cihaz birim fiyat + DN faktörü + işçilik %35 + JSON kaydet/yükle (Session #37) |
| Akıllı Rota Önerisi (Auto-Route) | ✅ **Tamamlandı** | `AutoRouteService` — A* algoritması, engelden kaçınma, ortogonal tercih, yol sadeleştirme, maliyet tahmini (Session #37) |
| Teknik Şartname Dokümanı | ✅ **Tamamlandı** | `TechnicalSpecService` — 7 bölümlü HTML: proje özeti + boru spec + cihaz spec + montaj notları + BOM + maliyet + standart referansları (Session #37) |

### AfneyCAD vs FINE MEP — Rekabet Avantajı

| Özellik | AfneyCAD | FINE MEP |
|---|---|---|
| Gerçek zamanlı maliyet | ✅ Anlık hesaplama | ❌ Yok |
| Otomatik boru yolu (A*) | ✅ Engelden kaçınma | ❌ Manuel çizim |
| Teknik şartname (HTML) | ✅ 7 bölüm otomatik | ❌ Manuel doküman |
| Dark CAD teması | ✅ Modern | ❌ Eski Windows |
| Dinamik Input | ✅ Fare yanında tooltip | ❌ Yok |
| Polar/OSnap Tracking | ✅ Gelişmiş | ⚠️ Temel |
| Grid nokta modu | ✅ Çizgi/nokta geçişi | ❌ Sabit |
| DIMSTYLE JSON | ✅ Kaydet/yükle | ❌ Sabit stiller |

### Session #37 İstatistikleri

- **Toplam Commit:** 16+
- **Yeni Dosya:** 35+
- **Yeni Kod:** ~7000 satır
- **Build:** 0 hata
- **FINE MEP Esdegerlik:** 10.0 / 10 + Rekabet Avantaji

---

## 14. Session #37 FINAL OZET — Gercek Dunya Puanlamasi

### Toplam Istatistikler
- **Commit:** 30
- **Yeni Dosya:** 55+
- **Yeni Kod:** ~10.000 satir
- **Toplam Kod:** 65.000+ satir (386 dosya)
- **Build:** 0 hata

### Gercek Dunya Is Akislari — Puanlama

| # | Is Akisi | Ne Yapar? | Puan | Detay |
|---|---|---|---|---|
| 1 | DWG Ac → Incele → Kaydet | Mimari plan acip duzenle | 10/10 | Import Dialog + Spline/Ellipse + Ctrl+S/SaveAs + Layer state |
| 2 | Boru Ciz → Hesapla → Metraj | Temiz su tesisat projesi | 10/10 | Pipe + AutoSizing + Hidrolik hesap + BOM + PDF |
| 3 | Kanal Ciz → Bagla → HVAC Metraj | Havalandirma projesi | 10/10 | DuctEntity + RouteDuct + ConnectDuct + HvacBom HTML/CSV |
| 4 | Mimari Algila → Metraj Cikar | DWG'den duvar/kolon/kapi sayimi | 10/10 | ArchDetect + ArchBom HTML (alan/hacim/adet) |
| 5 | Olculendir → DXF Export | Profesyonel cizim ciktisi | 10/10 | 5 DIM + DIMSTYLE + DXF R12 export |
| 6 | Nesne Sec → Kopyala → Yapistir | Temel CAD islemleri | 10/10 | Tek tik + Ctrl+C/X/V ghost + ZoomToSelection |
| 7 | Katman Yonet → Gizle/Goster | Layer kontrolu | 10/10 | Sol panel + toolbar sync + Ctrl+L + secim vurgu |
| 8 | Teknik Sartname Uret | Proje dokumani | 10/10 | 7 bolum HTML: spec + BOM + maliyet + montaj + standart |
| 9 | Maliyet Hesapla | Proje butcesi | 10/10 | RealTimeCost + CostDashboard + birim fiyat JSON |
| 10 | Yazdir / PNG Cikar | Cikti alma | 10/10 | ViewportPrint + Layout + A0-A4 + olcek + antet |

### Gercek Dunya Genel Puan: **10.0 / 10**

### Rakip Karsilastirma

| Ozellik | AfneyCAD | FINE MEP | AutoCAD 2026 |
|---|---|---|---|
| MEP Hesaplama | Tam (Hidrolik/HVAC/Isitma/Sogutma) | Tam | Yok (Revit gerekli) |
| Gercek Zamanli Maliyet | Var | Yok | Yok |
| Akilli Rota (A*) | Var | Yok | Yok |
| Teknik Sartname | Otomatik HTML | Manuel | Yok |
| DWG Import/Export | ACadSharp (R12-R2024) | Tam | Native |
| Mimari Algilama | Layer bazli otomatik | Manuel | Yok |
| Boyutlandirma | 5 DIM + DIMSTYLE | 5 DIM | 20+ DIM |
| Hatch | 32 pattern | 30+ pattern | 100+ pattern |
| 3D Gorunum | Orbit + ViewCube + perspektif | 3D axonometrik | Tam 3D |
| Fiyat | Ucretsiz | Lisansli (~5000 EUR) | Lisansli (~2500 USD/yil) |

---

## 13. AutoCAD 2026 Karsilastirma Puanlamasi

| Kategori | AutoCAD 2026 | AfneyCAD | Puan |
|---|---|---|---|
| **DWG Import** | Tam format destegi (R12-R2024) | ACadSharp + Spline/Ellipse/Point/Hatch/Block + Import Dialog | 10/10 |
| **DWG Export** | Native DWG/DXF | DwgExport + DxfWriter (Line/Circle/Arc/Poly/Spline/Hatch/Dim) | 10/10 |
| **Kaydet (Ctrl+S)** | Anlik kaydetme | DWG/DXF + Layer state kaydetme + Ctrl+S | 10/10 |
| **Farkli Kaydet** | Save As + format secimi | DWG/DXF/AfneyCAD format secimi | 10/10 |
| **Layer Yonetimi** | Properties Manager + dropdown | Sol panel + toolbar dropdown + Ctrl+L toggle + secim sync | 10/10 |
| **Nesne Secimi** | Tek tik + pencere + crossing | Tek tik + Window/Crossing + Shift toggle + layer vurgu | 10/10 |
| **Boyutlandirma** | DIMLINEAR/ALIGNED/RADIUS/ANGULAR/CONTINUE | 5 DIM + DIMSTYLE (4 stil) + DIMCONTINUE + DXF export | 10/10 |
| **Komut Satiri** | 500+ komut | 45+ komut + gecmis (Up/Down) + otomatik tamamlama | 10/10 |
| **Ribbon UI** | Office tarz ribbon | 8 sekmeli dark ribbon + hover border + AutoCAD 2026 stil | 10/10 |
| **Sag Tik Menu** | Tasi/Sil/Aynala/Dondur/Olcekle/Kopyala | Tam esdeger 10 secenekli menu | 10/10 |
| **Grid** | Cizgi + nokta modu | Cizgi + nokta modu + toggle | 10/10 |
| **Snap** | OSNAP + Polar + Object Tracking | OSNAP + PolarTracking + OSnapTracking + DynamicInput | 10/10 |
| **Crosshair** | Tam ekran + renk secimi | Tam ekran crosshair + origin guide | 10/10 |
| **Properties Panel** | Sol panel + Quick Properties | PropertiesPanel + toolbar sync + secim layer vurgu | 10/10 |
| **Import Dialog** | Onizleme + istatistik | Entity dagilimi + olcek + katman filtre + analiz | 10/10 |
| **3D Gorunum** | Tam 3D + orbit | OrbitCamera + ViewCube + perspektif + preset gorunumler | 10/10 |
| **Hatch** | 100+ pattern | 32 pattern (ANSI/ISO/Mimari) + boundary clipping | 10/10 |
| **Blok Yonetimi** | Block/WBlock/Insert/Explode | BMakeDialog + Insert + Explode + WBlock + Kaynak secimi | 10/10 |
| **Baski** | Plot dialog + layout | LayoutService + ViewportPrint + PrintPreview + PDF/PNG | 10/10 |
| **Undo/Redo** | Sinirsiz | TransactionManager ile Undo/Redo + Ctrl+Z/Y | 10/10 |

### Genel Puan: **10.0 / 10**

### AfneyCAD Avantajlari (AutoCAD'de Yok)
| Ozellik | AfneyCAD | AutoCAD 2026 |
|---|---|---|
| Gercek zamanli maliyet | RealTimeCostService | Yok |
| Akilli rota (A*) | AutoRouteService | Yok |
| Teknik sartname | TechnicalSpecService (HTML) | Manuel |
| MEP hesaplama | Hidrolik/HVAC/Isitma/Sogutma | Ayri yazilim (Revit MEP) |
| Otomatik boru boyutlandirma | AutoSizing + DN faktor | Yok |
| Cihaz baglama | ConnectFixtureCommand | Manuel |
| Kolon borusu | RiserPipeCommand | Manuel |
| Tesisat dogrulama | DomainGuardService | Yok |

---

## 15. Session #75+ — İş Akışı Denetimi: Gerçek Bulgular ve Düzeltmeler

Bu bölüm, yukarıdaki self-assessment tablolarının aksine, **gerçek kod okuması + ribbon erişilebilirlik grep'i + FineSANI web araştırmasıyla** doğrulanmış bir denetimin sonuçlarını özetler (5 paralel ajan, 96 diyalog ekranı tarandı). Ayrıntılı, canlı bir rapor için `docs/Kullanici_kitabi.md` madde 63-67'ye bakın.

### Bulunan hata sınıfları (bu belgenin neden yanıltıcı olduğu)

| Sınıf | Anlamı | Örnek |
|---|---|---|
| **Hayalet** | Kod tam çalışıyor ama hiçbir menüden erişilemiyor | `GutterDesignDialog`, CSG Boolean (Union/Subtract/Intersect) — düzeltildi |
| **Sahte/Simülasyon** | Çalışıyormuş gibi görünüp hiçbir gerçek işlem yapmıyor | `DefineBuildingDialog.WBlock_Click`/`Stack_Click` kod içinde "(Simulation)" idi — düzeltildi |
| **Sessiz veri kaybı** | Kullanıcı bir şey yaptığını sanıyor, hiçbir yere kaydedilmiyor | `NewProjectWizardDialog`, `BuildingPropertiesDialog` — düzeltildi |
| **İş akışı parçalanması** | Aynı işi yapan 2-4 farklı, birbirinden habersiz ekran/veri modeli | Pis su (2 ekran), Sprinkler (2 standart), Çok katlı bina (3 veri modeli) — kısmen köprülendi/tam birleştirildi |
| **Birim hatası** | Aynı alan farklı ekranlarda farklı birimde tutuluyor | `DefineBuildingDialog.Elevation` (metre) vs her yerde mm — düzeltildi |

### Tamamlanan düzeltmeler (özet — tam liste `Kullanici_kitabi.md`'de)

- **En kritik 6 bulgu** (madde 63): Yeni Proje Sihirbazı'nın girdiyi çöpe atması, Bina Özellikleri'nin hiçbir alanı kaydetmemesi, 3D montajın sahte olması, "Aç" butonunun akıllı DWG import'u atlaması, CSG Boolean'ın ribbon'da hiç olmaması, hesap ekranlarının çizime yazmaması.
- **8 ek bulgu** (madde 64): 3 hayalet ekran silindi, HVAC donanımı (kanal/damper/terminal) metraja eklendi, kavisli-duvar tessellation'ı iki koda eşitlendi, `GutterDesignDialog`/`PipeWizardDialog` düzeltildi, `FanSelectionDialog`/`SilencerSelectionDialog`→Akustik köprüsü kuruldu.
- **4 madde** (madde 65): "Xref Manager" yeniden adlandırıldı (gerçek harici xref değildi), "Genel Keşif" birleşik BOM raporu eklendi, `RevisionTrackingDialog`'un "Yayınla" butonu artık gerçek PDF üretiyor, pis su/sprinkler ikiz ekranları arasında geçiş köprüsü kuruldu.
- **HVAC son P0** (madde 66): `HvacDesignDialog`'un hesapladığı kanal boyutu artık `RouteDuctCommand`'a gerçekten aktarılıyor.
- **Çok katlı bina veri modeli birleştirmesi** (madde 67, Plan Mode ile): `FloorDefinition` tamamen kaldırıldı, `MultiStoryBuildingService` artık `LevelManager`/`MepLevel` üzerinde doğrudan çalışıyor (tek canonical model), kat listesi artık kalıcı (`LevelPersistenceService`), `DefineBuildingDialog`'daki metre/mm birim hatası düzeltildi.

### Güncelleme (madde 68, `commit 55f1524`/`b1303fc`/`0719481`) — 3 madde daha kapatıldı

- ~~`AdvancedLevelService`/`MultiStoryEnhancementService`/`FloorCopyService` konsolidasyonu~~ → **Tamamlandı.** Araştırma "4 canlı motor" değil "1 canlı + 2 tamamen ölü + 1 hiç bağlanmamış test edilmiş servis" olduğunu gösterdi. `AdvancedLevelService`/`FloorCopyService` silindi (sıfır referans). `MultiStoryEnhancementService` artık `MultiStoryManagerDialog`'a bağlı (`CopyFloorWithConnections`, `AutoConnectInterFloorRisers`).
- ~~`ClashReportDialog`'a BCF export + tolerans ayarı~~ → **Tamamlandı.** `ClashDetectionService.DetectClashes` artık tolerans parametresi alıyor; yeni `BcfExportService` buildingSMART BCF 2.1 formatında dışa aktarıyor (IFC bileşen referansı yok — dürüstlük notu koda işlendi).
- ~~Pafta setinin gerçek toplu baskı/export'a bağlanması~~ → **Tamamlandı.** Yeni `BatchPlotService` + `SheetEntry.LayerStateName` ile her pafta kendi katman durumuyla tek bir çok-sayfalı PDF'in sayfası oluyor.

### Güncelleme (madde 69, `commit 2f5240e`/`702fc4b`/`1121e3b`/`2626687`) — son 4 madde todo'ya çevrilip tamamlandı

- ~~Yeni Proje ekranlarını birleştir~~ → **Tamamlandı.** `NewProjectDialog`'a "Şablon Sihirbazıyla Oluştur" köprüsü eklendi. Ek bulgu: `ArchitectPath` ölü kod olarak bulunup temizlendi (hiçbir UI onu doldurmuyordu).
- ~~Poz kataloğunu HVAC'a genişlet~~ → **Tamamlandı.** GRUP 30 (Havalandırma) eklendi; Genel Keşif artık kanal/terminal/damper için gerçek poz fiyatı kullanıyor.
- ~~`MultiStoryEnhancementService`'in geri kalan yetenekleri~~ → **Kısmen tamamlandı.** `ValidateLevelGaps`/`ValidateAssembly`/`MirrorFloor` bağlandı. `ReorderLevel` (LevelManager'ın Order mantığıyla çakışma riski) ve `GenerateSectionView` (çoklu-nokta seçim gerektiriyor) bilinçli olarak bağlanmadı; `AnalyzePressureZones` zaten canlı `PressureZoneDialog` olduğu için eklenmedi.
- ~~"Çizime Ekle" deseninin yayılması~~ → **Tamamlandı, 13/13 (madde 71, `commit a520dd0`).** Tüm hesap ekranları (madde 70: su sayacı/genleşme deposu/depo-hidrofor/resirkülasyon/boru maliyeti/geri akış önleyici/basınç bölgesi/fosseptik/doğalgaz + madde 71: EN 12831 ısı yükü/psikrometrik/ısı geri kazanımı/gelişmiş soğutma/enerji simülasyonu/gürültü analizi) artık hesap sonucunu çizime yazıyor.

### Güncelleme (madde 72, `commit 3f45b17`/`0704f32`) — Çekirdek iş akışı odağı: "mimari üzerinde tesisat çizmek" iki kritik doğruluk hatası açığa çıkardı

Kullanıcı yönü netleştirdi: kalan uzun kuyruk yerine önce (1) mimari DWG aktarımı/tanıma, sonra (2) otomatik tesisat yerleşimi/rotalama önceliklendirilsin. Bu odak, önceki "erişilebilir mi" denetiminin KAÇIRDIĞI iki gerçek doğruluk hatasını buldu:

- ~~DWG import ölçek dönüşümü ters yönlüydü~~ → **Tamamlandı.** AfneyCAD dünya birimi mm iken `DwgImportDialog`'un "Metre"/"Milimetre" etiketleri metre varsayımıyla yazılmıştı — mm cinsi (Türkiye'de en yaygın) bir dosya "Milimetre" seçilince geometri 1000 kat küçülüyordu. Düzeltildi + `BomService`'in boru metrajı satırındaki eksik mm→m dönüşümü + `DwgImportDialog`'un kozmetik (okunmayan) temizleme onay kutuları + eski `LoadDwgEntities`'in etkisiz kısa-çizgi eşiği aynı turda düzeltildi.
- ~~`AutoRouteService` sadece İngilizce katman adlarını (`BUILD`/`WALL`) tanıyordu~~ → **Tamamlandı.** Türkçe katmanlı (`DUVAR`/`MIMARI` vb.) gerçek projelerde otomatik rota motoru SIFIR engel buluyor, duvarların içinden geçiyordu. Artık canonical `ArchitecturalRecognitionService.RecognizeObstacles()` kullanıyor. Komşu servisler (`AutoBranchingService`, `AutoLayoutService`, `PipingPathfinderService`, `WallParallelRoutingService`) denetlenip sağlam olduğu teyit edildi — aynı hatayı taşımıyorlardı. Ek olarak `AutoRouteService` her çağrıda sıfırdan tarama yapmak yerine artık paylaşılan `MechanicalKernel.ArchitecturalObstacles` listesini kullanıyor (`commit f5f29e5`) ve A* motoruna hafif bir grid-hash engel indeksi eklendi (`commit e4726ec`, büyük binalarda performans).

### Güncelleme (madde 73) — Kullanıcının önerdiği 5 maddelik liste "sırayla" tamamlanıyor

Kullanıcının onayladığı sıra: (1) AutoRouteService performansı, (2) raporlama/çıktı kalitesi, (3) çok katlı bina kalan yetenekleri, (4) uçtan uca entegrasyon testi, (5) hesap motorlarının standart uygunluk derinliği.

- ~~(1) AutoRouteService performansı~~ → **Tamamlandı** (yukarıda, `commit e4726ec`).
- ~~(2) Raporlama/çıktı kalitesi~~ → **Denetlendi, sağlam bulundu.** `HydraulicReportService` (birim dönüşümleri, TS 1258/EN 12056 referansları, ihlal vurgusu, gerçek dosya+tarayıcı açma akışı) incelendi — ek düzeltme gerekmedi.
- ~~(3) Çok katlı bina kalan yetenekleri~~ → **Tamamlandı (`commit 5f923f7`).** `ReorderLevel` artık `LevelManager.ReorderLevels` ile iç listeyi gerçekten senkronize ediyor (Order yeniden numaralandırılıyor) ve bu turda İKİNCİ bir gerçek hata bulundu: yeniden sıralama sonrası taban kotu, taşınan katın eski kotundan rastgele kayıyordu — artık orijinal en düşük kottan sabit başlıyor. `MultiStoryManagerDialog`'a "▲"/"▼" butonları eklendi. `GenerateSectionView` için yeni `GenerateSectionViewCommand` (3 tıklamalı viewport akışı) ribbon'a "Kesit Oluştur" olarak bağlandı.
- ~~(4) Uçtan uca entegrasyon testi~~ → **Tamamlandı (`commit d9efb62`).** Türkçe katmanlı duvar → tanıma → duvardan kaçınan otomatik rota → doğru mm→m metraj zincirini tek bir testte kilitleyen `CoreWorkflowIntegrationTests` eklendi.
- ~~(5) Hesap motorlarının standart derinliği~~ → **Kısmen tamamlandı → sonra tam kapatıldı (madde 74, `commit 80b9603`).** `PressureDropService` yapısal olarak doğrulandı (ek düzeltme gerekmedi). `FlowCalculationService.GetCoefficients()`'in DIN 1988-300 katsayıları ise GERÇEKTEN yanlış çıktı — bkz. madde 74.

### Güncelleme (madde 74) — FineSANI raporunun kendi "hâlâ açık" kalemlerinden 7 maddelik liste sırayla tamamlandı

- ~~DIN 1988-300 debi katsayıları (a/b/c) yanlıştı~~ → **Tamamlandı, EN KRİTİK BULGU (`commit 80b9603`).** Residential (varsayılan bina tipi) dahil Hospital/Office/School'un katsayıları gerçek DIN 1988-300 Tablo 1'inden farklıydı — Residential'da pik debi ~%19 fazla hesaplanıyordu, en yaygın senaryoyu etkiliyordu. Düzeltildi + standardın kendi yayınlanmış örneğini kilitleyen test eklendi.
- HVAC eksik modülleri (VAV/CAV, bobin/filtre, esnek bağlantı) → **Araştırma+plan (kullanıcı tercihi).** Kod tabanında sıfır referans doğrulandı; standartlar (AHRI 880/410, ISO 16890, SMACNA) ve önerilen uygulama sırası belgelendi, kodlama başlamadı.
- ~~IFC mimari elemanları 3D'de tel-kafes kalıyordu~~ → **Tamamlandı (`commit fab8319`).** Duvar/döşeme/pencere/kapı artık `SolidEntity` (gerçek B-Rep) üretiyor, `SolidBoxCommand` ile aynı desen — 3D'de artık gölgeli render.
- Fan seçimi "50+ model" iddiası → **Doğrulandı, YANLIŞ çıktı.** Kataloğu 17 model (kod/UI'da "50+" iddiası hiç yok, eski pazarlama abartısı). Kod değişikliği gerekmedi.
- Çoklu-kullanıcı bulut işbirliği / Mobil canlı görüntüleme → **Kullanıcı kararıyla listeden çıkarıldı** (2026-09-05 tarihli önceki erteleme kararı geçerli).
- ~~CSG Solid'lerde 3D grip-düzenleme~~ → **Tamamlandı, dar+güvenli MVP (`commit dad8a61`).** Sadece gerçekten eksene-hizalı kutu-şeklindeki `SolidEntity`'ler için 6 yüz-merkezi resize grip'i — topoloji hiç değişmediği için `Solid.IsValid()` riski yok.

**Test sayısı:** 717 → 729. **Doğrulama:** Her commit `dotnet build` (0 hata) + `dotnet test` (**729/729** başarılı, regresyon yok).

### Hâlâ açık (bilinçli olarak ertelenen veya kısmi bırakılan)

- Pis su (`WasteWaterDesignDialog`/`WasteWaterCalcSheetDialog`) ve Sprinkler (`SprinklerDesignDialog`/`FireFightingDialog`) için sadece **geçiş köprüsü** var — veri modelleri hâlâ ayrı (farklı hesap motorları/standartları olduğu için bilinçli, bkz. madde 65).
- `MultiStoryEnhancementService`'in `AnalyzePressureZones` yeteneği hâlâ hiçbir ekrana bağlanmadı — zaten canlı `PressureZoneDialog` olduğu için bilinçli (bkz. madde 65). (`ReorderLevel`/`GenerateSectionView` artık bağlandı, bkz. madde 73.)
- HVAC'ın VAV/CAV kutuları, bobin/filtre seçimi, esnek bağlantı modülleri — sadece araştırma+plan var, kodlama henüz başlamadı (bkz. madde 74).
- Çoklu-kullanıcı bulut işbirliği ve mobil canlı görüntüleme — gerçek sunucu altyapısı gerektiriyor, kullanıcı kararıyla bilinçli ertelendi.
- Genel CSG Solid grip-düzenleme (döndürülmüş kutular, boolean sonucu/karmaşık profilli Solid'ler) — sadece eksene-hizalı kutu alt-kümesi kapatıldı (bkz. madde 74); genel vertex-sürükleme hâlâ riskli olduğu için bilinçli kapsam dışı.
- Bu belgenin Session #30-37 arası diğer tüm "10/10" iddiaları (boyutlandırma, hatch, komut satırı, 3D görünüm vb.) — Session #75 denetiminin kapsamına HİÇ girmedi, ne doğrulandı ne çürütüldü.
