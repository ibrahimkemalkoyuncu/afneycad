# AfneyCAD Kullanıcı Kitabı
**Sürüm:** v2.0 — 19 Mayıs 2026  
**Kapsam:** Mimari Giriş · Temiz Su Tasarımı · Hidrolik Hesap

> Bu kitap, OtoNET/FINE MEP eğitimlerinde öğretilen iş akışlarının AfneyCAD'deki karşılıklarını göstermektedir.  
> Her bölüm: "OtoNET'te nasıl yapılır?" → "AfneyCAD'de nasıl yapılır?" formatındadır.

---

## Bölüm 1 — Mimari Çizimin Programa Girilmesi

### 1.1 Yeni Proje Oluşturma

| OtoNET/FINE MEP | AfneyCAD |
|---|---|
| Dosya → Yeni Proje | **Dosya → Yeni Proje** (NewProjectDialog) |
| Türkçe karakter kullanılmaz | Aynı kural geçerlidir; proje klasörü `C:\AfneyCadProjects\<ProjeAdi>\` altında oluşur |
| CC klasörüne otomatik klasör açılır | `AppData\AfneyCAD\Projects\` altında proje klasörü oluşturulur |

**Adımlar:**
1. Üst menüden **Dosya → Yeni Proje**'ye tıklayın.
2. Proje adını girin (nokta, boşluk, Türkçe karakter kullanmayın; `Merkez_Konut_A1` gibi).
3. Tamam'a basın — proje klasörü otomatik açılır.
4. Çalışılacak mimari DWG/DXF dosyasını bu klasöre kopyalayın.
5. **Dosya → Aç** ile mimari planı ekrana getirin.

---

### 1.2 Mimari Çizimin Metre Bazına Getirilmesi

| OtoNET/FINE MEP | AfneyCAD |
|---|---|
| "Uzaklık" komutu ile duvar kalınlığı ölçülür | **Otomatik:** `ArchitecturalScaleService` açılışta birimi tespit eder |
| Değer 0.2 ise metre bazındadır | Durum çubuğunda `Birim: METRE` veya `Birim: MM` gösterilir |
| Farklıysa tüm çizim "Ölçekle" ile metre bazına getirilir | **Bina → Ölçek Düzelt** komutu otomatik 0.001 / 0.01 katsayısı uygular |

**Nasıl çalışır:**  
AfneyCAD, DWG açılırken tüm `LineEntity` uzunluklarını analiz eder. Ortalama uzunluk:
- 1000+ birim → Milimetre (× 0.001 ölçeklenir)
- 50–1000 birim → Santimetre (× 0.01 ölçeklenir)
- <50 birim → Metre (ölçek uygulanmaz)

Otomatik tespite güvenmiyorsanız: **Araçlar → Mesafe Ölç** ile bir duvarın kalınlığını kontrol edin; `0.2` çıkması gerekmektedir.

---

### 1.3 WBlock — Katı Bloklama ve Referans Noktası

OtoNET'teki **W Block** penceresinin AfneyCAD karşılığı: **Bina → Mimariyi Blokla (WBlock)**

Bu işlem `WBlockWizard` penceresini açar. 4 adımlı sihirbaz:

| Adım | OtoNET Karşılığı | AfneyCAD |
|---|---|---|
| **1. Referans Noktası Seç** | "Nokta Seç" — kolon köşesi seçilir | "Hizalama Noktası Seç" butonuna basın, çizimde kolon/asansör köşesini tıklayın |
| **2. Nesneleri Seç** | "Nesne Seç" ile kata ait çizim tamamı seçilir | "Nesneleri Seç" butonuna basın, çizimi tamamen seçin, Enter'a basın |
| **3. Dosya Adı** | "..." ile proje klasörüne girip kata isim verilir | "Gözat..." ile proje klasörüne gidip `BodrumKat.dwg` gibi kaydedilir |
| **4. Onayla** | "Tamam" | "Bitir ve Kaydet" — sihirbaz kapanır |

> **Önemli:** Her kat için bu işlem tekrarlanır. Mimari planı aynı olan katlarda aynı blok dosyası farklı katlara atanabilir (bkz. §1.4).

---

### 1.4 Katların Tanımlanması — Kat Yöneticisi

OtoNET'teki **"Mimari Belirle"** penceresinin karşılığı: **Bina → Kat Yöneticisi** (`LevelManagerDialog`)

Açılan pencerede her kat için girilmesi gereken bilgiler:

| Alan | Açıklama | Örnek |
|---|---|---|
| **Kat No** | 1'den başlar | `1`, `2`, `3`... |
| **Kot (Z)** | Katın metre cinsinden yüksekliği | `-2.8` (bodrum), `0` (zemin) |
| **İsim** | Serbest metin | `Bodrum Kat`, `Zemin Kat`, `1. Normal Kat` |
| **Mimari Dosya** | WBlock ile kaydedilen .dwg bloğu | `BodrumKat.dwg` |
| **Kat Yüksekliği** | Döşemeden döşemeye (mm) | `2800`, `3000` |

**Hızlı Bina Oluştur:** `MultiStoryManagerDialog` üst satırındaki "Standart Bina Oluştur" butonu ile kat sayısı, yükseklik ve bodrum seçeneği girilerek tüm kat iskeletini tek seferde oluşturabilirsiniz.

**Yenile / Güncelle:** OtoNET'teki "Yenile" butonu → AfneyCAD'de "Kaydet" düğmesi — kat tabloya işlenir.

---

### 1.5 3D Hizalama Kontrolü

Tüm katlar tanımlandıktan sonra:
1. Sol paneldeki **3D Görünüm** sekmesine geçin.
2. Tüm katların seçilen referans noktasından (asansör köşesi vb.) Z ekseninde üst üste hizalandığını kontrol edin.
3. Hizalama bozuksa ilgili katta **Kat Yöneticisi → Düzenle** ile referans noktasını yeniden seçin.

> Bu adım tamamlandıktan sonra tesisat tasarımına geçilebilir.

---

## Bölüm 2 — Temiz Su Tesisat Tasarımı

### 2.1 Modül Seçimi

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Uygulama Seç → **Temiz Su** | Mekanik menüsü → **Sistem Türü: Temiz Su** (MechanicalSystemType.ColdWater / HotWater) |

Sistem türü seçimi sonrası araç çubuğu ve boru kütüphanesi otomatik olarak temiz su moduna geçer.

---

### 2.2 Cihaz (Armatür) Yerleştirme

| OtoNET | AfneyCAD |
|---|---|
| ST Cihazları → Cihaz Seçimi → Kütüphane penceresi | **Mekanik → Armatür Kütüphanesi** (`FixtureLibraryDialog`) |
| Debi bazında filtreleme | Kütüphanede "Debi (LU)" sütununa göre sıralama |
| 1. tık: konum, 2. tık: yön | Aynı: 1. tık konum, 2. tık yön |
| Sağ tık → Taşı | Nesne seçip **Ctrl+G** veya sağ tık → Taşı |

**Akıllı Bağlantı Noktası (Alt Katlar için):**  
OtoNET'te üst katlarda mimari plan mevcutsa "Akıllı Bağlantı Noktası" seçeneği kullanılır.  
AfneyCAD karşılığı: Kütüphane penceresinde **"Sadece Bağlantı Noktası Ekle"** seçeneği — tam cihaz bloğu yerine yalnızca boru bağlantı çıkışı (hot/cold stub) yerleştirilir.

**Özel Cihazlar (Şofben vb.):**  
`FixtureLibraryDialog` → kategori: **"Su Isıtıcıları"** → kapasiteye (6 kW, 12 kW, 18 kW) göre seçim.

---

### 2.3 Boru Çizimi

| OtoNET | AfneyCAD |
|---|---|
| "Soğuk Su Borusu" komutu | **Mekanik → Boru Çiz → Soğuk Su** veya `PipeRoutingEngine` |
| "Sıcak Su Borusu" komutu | **Mekanik → Boru Çiz → Sıcak Su** |
| Borular referans amaçlı — duvardan geçebilir | Aynı kural — `DomainGuardService` uyarır ama engel olmaz |
| Sıcak su şofbenden başlar, ucu açık bırakılır | Şofbenin çıkış portuna başlatın (orta nokta snap), uzak ucu bağlantısız bırakın |

**Snap Kullanımı:**  
Durum çubuğundaki **OSNAP araç çubuğu** (son commit'le eklendi) aktif olmalıdır:
- `ENDPt` — boru ucunu cihaz bağlantı noktasına tam oturt
- `PERPt` — kolon borusuna dik bağlantı için

---

### 2.4 Cihazları Tesisata Bağlama

| OtoNET | AfneyCAD |
|---|---|
| Boru komutu → "Cihazları Tesisata Bağla" | **Mekanik → Otomatik Bağla** (`ConnectReceptorsService` / `AutoBranchingService`) |
| Cihaza tıkla → Enter → Ana hattı seç | Cihaz(lar)ı seç → Enter → Ana boru hattını seç |
| Birden fazla cihaz aynı anda bağlanabilir | Çoklu seçim desteklenir |

---

### 2.5 Tesisatı Kopyalama (Aynı Mimari Katlara)

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Tesisat Kopyala | **Mekanik → Katlar Arası Kopyala** (`MultiStoryBuildingService`) |
| Tüm tesisat seçilir → Enter → Hedef kat numarası → Enter | Kaynak kat seçilir → Hedef kat(lar) seçilir → Kopyala |
| Fazla şofben kopyalandıysa silinir | Cihaz tipine göre filtrelenmiş silme yapılabilir |

---

### 2.6 Kolon Boruları

| OtoNET | AfneyCAD |
|---|---|
| Kolon Boruları sekmesi → Kolon noktası seçilir | **Mekanik → Kolon Ekle** (`PipeRoutingEngine.AddRiserColumn`) |
| Taban ve tepe yükseklikleri girilir | Başlangıç kotu ve bitiş kotu (metre) — kat yöneticisinden otomatik önerilir |
| Dik nokta yakalama zorunlu | OSNAP: `PERPt` aktif olmalı |
| Fazla boru parçaları silinmeli | Sistem doğrulama: **Mekanik → Sistemi Doğrula** (`ValidationGateService`) açık uç/kesişim hataları listeler |

---

### 2.7 Başlangıç Noktaları (Kaynak Tanımlama)

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Başlangıç Noktası → Soğuk Su | **Mekanik → Sistem Kaynağı Tanımla → Soğuk Su** |
| Sayaç/pompa konumuna, boru uç noktasına yerleştirilir | OSNAP `ENDPt` ile borunun en uç noktasına tıklayın |
| Otonet → Başlangıç Noktası Sıcak Su | **Mekanik → Sistem Kaynağı Tanımla → Sıcak Su** |
| Şofbenin altındaki açık boru ucuna yerleştirilir | Sıcak su borusunun şofben tarafındaki uç noktasına tıklayın |

**Çok Daireli Binalarda:**  
Her daire için ayrı kaynak noktası tanımlanabilir → Her daire bağımsız hesaplanır.  
AfneyCAD: Başlangıç noktası eklerken **"Daire/Ünite"** alanını doldurun.

---

### 2.8 Tesisatı Kabul Et (Sistem Doğrulama)

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Tesisatı Kabul Et | **Mekanik → Sistemi Doğrula ve Numaralandır** (`ValidationGateService`) |
| Hata çıkarsa düzelt, tekrarla | Hata listesi penceresinde sorunlu elemana çift tıkla → çizimde otomatik konumlanır |
| Hatasız sonuç → Hesaplara geçilir | Yeşil onay → `HydraulicNetwork` hazır → Hesap modülü açılabilir |

---

## Bölüm 3 — Temiz Su Tesisat Hesapları

### 3.1 Hesap Modülünü Açma

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Hesaplara Git | **Mekanik → Hidrolik Hesap Tablosu** (`CalculationTableWindow`) |
| Pencereler → Hesap Föyü | Pencere zaten hesap föyü olarak açılır |
| Dosyalar → Çizimden Güncelle | **"Çizimden Yükle"** butonu — `CalculationTableService.LoadFromDrawing()` |
| Hesapla butonu | **"Hesapla"** butonu — `HydraulicCalculationService.Calculate()` |

---

### 3.2 Devre Seçenekleri (Norm ve Boru Parametreleri)

| OtoNET | AfneyCAD |
|---|---|
| Seçenekler → Devre Seçenekleri | **Hesap Tablosu → Seçenekler** (`StandardSelectionDialog`) |
| Bina tipi, boru cinsi, pürüzlülük, max hız | Aynı parametreler; `PipeSizer` sınıfı kullanır |
| DIN normu veya Musluk Birimi seçimi | **TS 1258 / DIN 1988 / ASPE** (Eksiklikler.md §2 — uluslararası standart desteği geliştiriliyor) |
| Normla birlikte tüm hesaplar yenilenir | Seçim değiştiğinde `CalculationTableService.Recalculate()` otomatik tetiklenir |

---

### 3.3 Hesap Föyü Tablosu

| Sütun (OtoNET) | AfneyCAD Karşılığı | Açıklama |
|---|---|---|
| Devre Parçası No | Segment ID | `ValidationGateService` tarafından atanır |
| Boru Boyu | Uzunluk (m) | Çizimdeki çizgisel boru mesafesi |
| Cihaz Nominal Debisi | Toplam LU | Bağlı armatürlerin birikimli yük birimi |
| Hesap Debisi | Q (L/s) | LU'dan katsayı ile dönüştürülmüş gerçek debi |
| Seçilen Boru Çapı | Çap (mm) | Otomatik veya manuel override |

**Manuel Çap Override:**  
`CalculationTableWindow`'daki **"Seçilen Çap"** sütununa tıklayarak açılır liste → Çap değiştirince o satırdaki hız ve basınç kaybı anında yenilenir.

---

### 3.4 Ek Hesaplar (Pencereler Menüsü)

| OtoNET | AfneyCAD | Dialog |
|---|---|---|
| Pencereler → Hidrofor Hesabı | **Mekanik → Pompa/Hidrofor Seçimi** | `PumpSelectionService` / pompa karakteristik eğrisi |
| Pencereler → Basınç Kaybı | **Hesap Tablosu → Basınç Düşümü Raporu** | `PressureDropReportWindow` |
| Pencereler → Keşif Listesi | **Mekanik → Metraj (BOM)** | `BOMDialog` / `BomReportWindow` |

**Basınç Kaybı Raporu:**  
`PressureDropReportWindow` her devredeki düşümü gösterir ve kritik devre hattını vurgular.

---

### 3.5 Raporlama — Word/PDF Çıktısı

| OtoNET | AfneyCAD |
|---|---|
| Baskı İçeriği → Özellikleri işaretle/kaldır | `ReportExportDialog` → bölüm bazında checkbox seçimi |
| Dosyalar → Word Dosyası Oluştur | **Rapor Dışa Aktar → DOCX / PDF** (`ReportExportService`) |
| Rapor proje klasöründe "yd re" ismiyle kaydedilir | `<ProjeKlasörü>\Raporlar\TemisSuRaporu_<Tarih>.docx` |

---

### 3.6 Hesap Sonuçlarını Çizime Yazdırma

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Çizimi Güncelle | **Mekanik → Çizimi Etiketle** (`AutoAnnotationService` / `AutoPipeLabeler`) |
| Tümünü işaretle | Varsayılan: tüm segmentler |
| Çap, debi, uzunluk vb. seç | Etiket içerik seçimi: çap ✓, debi ✓, hız ✗, basınç ✗ (özelleştirilebilir) |
| Otomatik Yerleştirme | **"Otomatik Etiketle"** — etiketler boru ortalarına yerleştirilir |
| Başlangıç noktasına "1" numarası verilir | Kaynak noktası "S" (Source) etiketi alır |

---

### 3.7 Kolon Şeması (Riser Diagram)

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Kolon Şeması → Oluştur | **Mekanik → Kolon Şeması Oluştur** (`RiserDiagramExportDialog`) |
| Ara pencere kapanır, .dwg açılır | `RiserDrawingService` → yeni viewport/sekme olarak açılır |
| İstenilen değişiklik yapılabilir | Şema üzerinde standart CAD düzenleme araçları çalışır |
| Proje klasöründe "yv" uzantılı dosya | `<ProjeKlasörü>\Schematics\KolonSemasi_<Tarih>.afney` |

---

## Bölüm 4 — FINE MEP vs AfneyCAD Özellik Karşılaştırma Tablosu

| Özellik | OtoNET/FINE MEP | AfneyCAD v2.0 |
|---|---|---|
| DWG/DXF İçeri Aktarma | Var | **Var** (`DxfReader`, `DwgImportService`) |
| Otomatik Birim Tespiti | Manuel (Uzaklık komutu) | **Otomatik** (`ArchitecturalScaleService`) |
| WBlock / Kat Bloklama | Var (W Block penceresi) | **Var** (`WBlockWizard` — 4 adımlı) |
| Kat Yönetimi | Mimari Belirle penceresi | **Var** (`LevelManagerDialog`, `MultiStoryManagerDialog`) |
| 3D Hizalama Kontrolü | Manuel görsel | **Var** (3D viewport, referans noktası bazlı) |
| Temiz Su Cihaz Kütüphanesi | Var (debi bazlı) | **Var** (`FixtureLibraryService`, LU/debi bazlı) |
| Akıllı Bağlantı Noktası | Var | **Var** (bağlantı stub modu) |
| Otomatik Cihaz Bağlama | Var | **Var** (`AutoBranchingService`, `ConnectReceptorsService`) |
| Katlar Arası Tesisat Kopyalama | Var | **Var** (`MultiStoryBuildingService`) |
| Kolon Borusu Oluşturma | Var | **Var** (`PipeRoutingEngine.AddRiserColumn`) |
| Sistem Doğrulama | Tesisatı Kabul Et | **Var** (`ValidationGateService`) |
| Hidrolik Hesap Tablosu | Hesap Föyü | **Var** (`CalculationTableWindow`) |
| Manuel Çap Override | Var | **Var** (CalculationTable satır düzenleme) |
| DIN / TS 1258 Normu | Var | **Var** (`PipeSizer`) |
| ASPE / BS / ASHRAE Normu | Var | Geliştiriliyor (bkz. `Eksiklikler.md §2`) |
| Hidrofor/Pompa Hesabı | Var | **Var** (`PumpSelectionService`) |
| Basınç Kaybı Raporu | Var | **Var** (`PressureDropReportWindow`) |
| Metraj (BOM/Keşif Listesi) | Var | **Var** (`BOMDialog`, `BomReportWindow`) |
| Rapor (Word/PDF) | Word çıktı | **Var** (`ReportExportService`, DOCX+PDF) |
| Çizime Hesap Etiketleme | Var | **Var** (`AutoPipeLabeler`, `AutoAnnotationService`) |
| Kolon Şeması | Var (.dwg) | **Var** (`RiserDiagramExportDialog`) |
| IFC İçeri Aktarma | Kısmi | **Geliştiriliyor** (bkz. `Eksiklikler.md §1`) |
| Bağımsız Hesap Modu (CAD'siz) | Var (ADAPT/FCALC) | **Geliştiriliyor** (bkz. `Eksiklikler.md §2`) |

---

## Bölüm 5 — Sık Karşılaşılan Sorunlar ve Çözümler

### S1: DWG açıldığında çizim çok küçük veya çok büyük görünüyor
**Neden:** Dosya mm veya cm bazında kaydedilmiş.  
**Çözüm:** Durum çubuğunu kontrol edin. `Birim: MM` yazıyorsa **Bina → Ölçek Düzelt** → Otomatik uygula.

### S2: WBlock sonrası katlar üst üste gelmiyor
**Neden:** Her katta farklı referans noktası seçilmiş.  
**Çözüm:** `WBlockWizard`'ı tekrar açın, tüm katlarda **aynı fiziksel noktayı** (örn. aynı kolon köşesi) seçin.

### S3: "Tesisatı Kabul Et" / "Sistemi Doğrula" hata veriyor
**Yaygın hatalar:**
- Açık uç (bağlantısız boru sonu) → Açık uçları kapatın veya kaynak noktası ekleyin
- Döngüsel bağlantı → `ValidationGateService` hata listesinde "cycle" satırına çift tıklayın
- Numaralandırma hatası → Fazla boru parçalarını silin

### S4: Hesap tablosu boş geliyor
**Çözüm:** `CalculationTableWindow` içinde **"Çizimden Yükle"** butonuna tıklamayı unutmuş olabilirsiniz. Önce doğrulama, sonra yükleme yapın.

### S5: Kolon şeması oluşturulmuyor
**Neden:** Kolon boruları kaynak noktasına bağlı değil.  
**Çözüm:** Her kolon borusunun `HydraulicNetwork`'teki kaynak düğümüne ulaşabildiğini doğrulayın (Sistemi Doğrula).

---

## Bölüm 6 — Bir Sonraki Session Öncelikleri

Aşağıdaki özellikler `Eksiklikler.md` ile uyumlu olarak öncelik sırasına göre listelenmiştir:

### Öncelik 1 — Bağımsız Hesap Tablosu Modu (Kritik)
- `CalculationTableWindow`'a "CAD'siz mod" eklenmeli
- Kullanıcı boru boyunu, debiyi ve cihaz sayısını manuel girebilmeli
- OtoNET'teki ADAPT/FCALC davranışı hedeflenmeli
- **İlgili dosyalar:** `CalculationTableWindow.xaml`, `CalculationTableService.cs`

### Öncelik 2 — Çift Yönlü Hesap-Çizim Senkronizasyonu
- Override edilen çap değerleri `PipeLabelEntity`'leri ve 3D modeli anlık güncellemeli
- `AutoPipeLabeler`'ın live-update mekanizması geliştirilmeli
- **İlgili dosyalar:** `AutoPipeLabeler.cs`, `AutoAnnotationService.cs`, `IsoSyncService.cs`

### Öncelik 3 — Gelişmiş Ekipman Hesapları (Pompa/Hidrofor/Boyler)
- `PumpSelectionService`'e pompa karakteristik eğrisi ve kavitasyon kontrolü eklenmeli
- Genleşme tankı ve boyler kapasitesi hesabı interaktif hale getirilmeli

### Öncelik 4 — Çoklu Uluslararası Standart Desteği
- `StandardSelectionService` / `PipeSizer`'a ASPE ve BS EN 806 standartları eklenmeli
- Tablo seviyesinde norm değiştirme → anında yeniden hesap

### Öncelik 5 — IFC İçeri Aktarma
- `IfcExportService` tamamlandı; `IfcImportService` yazılmalı
- Revit/ArchiCAD'den gelen duvar, kat, pencere verisi tanınmalı

---

*Son güncelleme: 2026-05-19 | AfneyCAD v2.0.0*
