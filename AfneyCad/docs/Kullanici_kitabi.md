# AfneyCAD Kullanıcı Kitabı
**Sürüm:** v2.8.0 — 21 Mayıs 2026  
**Kapsam:** Mimari Giriş · Temiz Su Tasarımı · Hidrolik Hesap · Pis Su / Yağmur Suyu · IFC İçeri Aktarma · Pompa Q-H Grafiği · Sıcak Su Resirkülasyon · DXF Dışa Aktarma · Uluslararası Norm Desteği · Basınç Bölgesi PRV · Yangın Hidrant & Hortum · Boru Maliyet Analizi · Viewport Baskı/PNG · BOM Maliyet Tablosu · Pompaj Grubu Q-H · Otomatik Boru Boyutlandırma

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

## Bölüm 4 — Pis Su Tesisat Tasarımı

### 4.1 Modül Seçimi ve Katman Yönetimi

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Uygulama Seç → **Pis Su** | **Mekanik → Pis Su/Yağmur Suyu Tasarımı** → Katmanlar sekmesi |
| Pis su modülüne geçince temiz su çizimleri kaybolur | `WasteWaterLayerService.SetActiveModule(WasteWater)` — diğer katmanlar gizlenir |
| Otonet → Uygulama Katmanlarını Seç | **Katmanlar sekmesi → Görünür Katmanlar** checkbox listesi |
| Temiz su + pis su aynı anda görülebilir | `ChkShowCold` + `ChkShowWaste` ikisi de işaretlenerek → Uygula |

**Katman isimlendirme konvansiyonu:**
| Katman | AfneyCAD İsmi |
|---|---|
| Temiz Soğuk Su | `MEP-COLD` (mavi) |
| Temiz Sıcak Su | `MEP-HOT` (kırmızı) |
| Pis Su | `MEP-WASTE` (kahverengi) |
| Yağmur Suyu | `MEP-RAIN` (mavi ton) |
| Yangın | `MEP-FIRE` (kırmızı) |
| Doğalgaz | `MEP-GAS` (turuncu) |

---

### 4.2 Cihaz Yerleşimi — Akıllı Bağlantı Noktaları

| OtoNET | AfneyCAD |
|---|---|
| ST Cihazları → **Akıllı Bağlantı Noktaları** | `FixtureLibraryDialog` → **"Sadece Bağlantı Noktası Ekle"** modu |
| Mimari planda cihaz varsa tam sembol yerine bağlantı noktası (yıldız) | Bağlantı stub'ı — pis su çıkışı noktası eklenir, cihaz sembolü çizilmez |
| Yer süzgeci gibi çizimi olmayan cihaz → "tümü" ile yerleştirilir | `FixtureLibraryService` → Kategori: **"Temizlik"** → DS-001 / DS-002 |

**Yeni eklenen armatürler (Session #19):**
- `DS-002` — Yer Süzgeci (Banyo) — DN50, DU=0.5
- `DS-003` — Döşeme Süzgeci (Ticari) — DN100, DU=1.0
- `YG-001` — Yağmur Gideri DN75 — max 1.5 lt/s
- `YG-002` — Yağmur Gideri DN100 — max 4.0 lt/s
- `YG-003` — Yağmur Gideri DN125 — max 8.0 lt/s
- `YG-004` — Balkon Taşma Borusu — DN75

---

### 4.3 Pis Su Kolon Boruları

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Kolon Borusu → Noktaya tıkla → Kot penceresi | **Mekanik → Kolon Ekle** → başlangıç/bitiş kotu girişi |
| Dik nokta yakalama zorunlu | OSNAP `PERPt` aktif olmalı |
| Fazla boru parçaları silinmeli | **Kolon Araçları → Seçimi Doğrula** |

**Bölünmüş Kolon (Alt katta teras senaryosu):**

OtoNET'te alt kolon (0-3 m) ve üst kolon (3-6 m) ayrı ayrı tanımlanıp yatay boru ile birleştirilir.

AfneyCAD karşılığı — **Kolon Araçları sekmesi → Bölünmüş Kolon Oluştur:**

| Alan | Açıklama | Örnek |
|---|---|---|
| Alt Bot (m) | Alt kolonun taban kotu | `0` |
| Alt Top (m) | Alt kolonun tepe kotu (bölünme noktası) | `3` |
| Üst Bot (m) | Üst kolonun taban kotu | `3` |
| Üst Top (m) | Üst kolonun tepe kotu | `6` |

Tamam'a basıldıktan sonra viewport'ta iki kolon noktası seçilir → `PipeRoutingEngine` yatay bağlantı borusunu dik nokta snap ile oluşturur.

---

### 4.4 Tesisat Kopyalama — Kural

> **Kritik OtoNET Kuralı:** Tesisat kopyalanırken **kolon boruları kesinlikle seçilmemeli** — aksi program hata verir.

AfneyCAD karşılığı — **Kolon Araçları sekmesi:**

1. **Seçimi Doğrula (Kolon Tespiti)** — seçili nesnelerde dikey `PipeEntity` (kolon) varsa kırmızı uyarı verir
2. **Kolonları Çıkar ve Kopyala** — `WasteWaterDesignService.ValidateCopySelection()` kolonları filtreler, geri kalanlar kopyalanır

**Teknik:** `IsVerticalPipe()` — boru Z bileşeni %80'den fazlaysa kolon sayılır.

---

### 4.5 Boşaltma Noktası (Rögar) Tanımlama

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Boşaltma Noktası → Boru uç noktasına tıkla | **Kolon Araçları → Pis Su Rögar Noktası Yerleştir** |
| Pis su kolonları zemin katta birleştirildikten sonra | `DrainageOutletEntity` (OutletType: SewerManhole) |
| Uç nokta yakalama kullanılır | OSNAP `ENDPt` aktif olmalı |

**DrainageOutletEntity özellikleri:**
- `Type` — SewerManhole (rögar), RainDrain (yağmur), Septic (fosseptik)
- `InvertLevel` — kanal taban kotu (metre)
- Render: çarpı içinde daire sembolü (standart pafta sembolü)

---

### 4.6 Yağmur Suyu Tesisatı

#### 4.6.1 Boru Türü Seçimi ve Yağmur Düşme Alanı

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Boru Cinsi → Yağmur Suyu | Katmanlar sekmesi → Aktif Modül: **Yağmur Suyu** |
| Otonet → Yağmur Düşme Alanı → Polygon çiz | **Yağmur Suyu sekmesi → Yağmur Düşme Alanı Çiz** → `RainfallCatchmentEntity` |
| Yüzey türü seçilir → Alan hesaplanır | Yüzey türü (FlatRoof, GreenRoof, GravelRoof, SlopedRoof) → C katsayısı otomatik |

**RainfallCatchmentEntity yüzey türleri:**
| Yüzey Türü | Akış Katsayısı C |
|---|---|
| Düz Çatı (FlatRoof) | 1.0 |
| Yeşil Çatı (GreenRoof) | 0.5 |
| Çakıl Çatı (GravelRoof) | 0.7 |
| Döşemeli Teras (PavedTerrace) | 0.9 |
| Eğimli Çatı (SlopedRoof) | 1.0 |

#### 4.6.2 Yağmur Gideri ve Kolon

| OtoNET | AfneyCAD |
|---|---|
| ST Cihazları → Yağmur Gideri yerleştir | `FixtureLibraryDialog` → Kategori: **"Yağmur Suyu"** → YG-001..YG-004 |
| Yağmur kolonları tanımlanır (0-6 m) | **Mekanik → Kolon Ekle** — SystemType: RainWater |
| Dik nokta yakalama ile giderler bağlanır | OSNAP `PERPt` |

#### 4.6.3 3D'de Kademeli Birleştirme

| OtoNET | AfneyCAD |
|---|---|
| Farklı hizadaki kolonlar 3D görünümde dik nokta ile yatayda bağlanır | 3D viewport → OSNAP `PERPt` → normal boru komutu ile bağlantı |

#### 4.6.4 Yağmur Boşaltma Noktası

| OtoNET | AfneyCAD |
|---|---|
| Uç nokta yakalama ile boşaltma noktası yerleştirilir | **Kolon Araçları → Yağmur Suyu Boşaltma Noktası** |
| | `DrainageOutletEntity` (OutletType: RainDrain) — mavi sembol |

---

### 4.7 Tesisatı Kabul Et (Pis Su ve Yağmur Suyu)

| OtoNET | AfneyCAD |
|---|---|
| Otonet → Tesisatı Kabul Et | **Pis Su/Yağmur Suyu Tasarımı → Tesisatı Kabul Et sekmesi** |
| Hata yoksa numaralandırılır, hesaplara hazır | Yeşil "BAŞARILI" mesajı → `ValidationGateService` tetiklenir |

**Kontrol edilen koşullar:**
- Her WasteWater ağında ≥1 `DrainageOutletEntity` (rögar)
- Her RainWater ağında ≥1 `DrainageOutletEntity` (yağmur boşaltma)
- Açık uç boru sonu yok
- Kolon boruları ağa bağlı

---

## Bölüm 5 — FINE MEP vs AfneyCAD Özellik Karşılaştırma Tablosu

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
| Pis Su Modülü (WasteWater) | Var | **Var** (`WasteWaterDesignService`, TS EN 12056-2) |
| Katman Yönetimi (Çoklu Modül) | Var | **Var** (`WasteWaterLayerService`) |
| Akıllı Bağlantı Noktası (Pis Su) | Var | **Var** (FixtureLibrary stub modu) |
| Yer Süzgeci / Yağmur Gideri | Var | **Var** (DS-002, YG-001..004 — Session #19) |
| Bölünmüş Kolon | Var | **Var** (`WasteWaterDesignService.CreateSplitColumn`) |
| Kopyalama Kolon Validasyonu | Var | **Var** (`ValidateCopySelection`, `IsVerticalPipe`) |
| Boşaltma Noktası (Rögar) | Var | **Var** (`DrainageOutletEntity`) |
| Yağmur Düşme Alanı | Var | **Var** (`RainfallCatchmentEntity`, Shoelace alan hesabı) |
| Yağmur Suyu Hesabı | Var (TS EN 12056-3) | **Var** (`WasteWaterDesignService.CalculateRainwaterFlow`) |
| IFC İçeri Aktarma | Kısmi | **Var** (`IfcImportService` — duvar/döşeme/pencere/kapı, IFC 2x3+4) |
| Bağımsız Hesap Modu (CAD'siz) | Var (ADAPT/FCALC) | **Var** (`CalculationTableWindow` — Manuel Giriş sekmesi) |
| Pis Su Hesap Föyü (TS EN 12056-2) | Var | **Var** (`CalculationTableWindow` — Pis Su sekmesi + HTML rapor) |
| Pompa Q-H Karakteristik Eğrisi | Var | **Var** (`PumpSelectionService.GetPumpCurvePoints`, çalışma noktası) |
| Kavitasyon Kontrolü (NPSHa/NPSHr) | Var | **Var** (`PumpSelectionService.CheckCavitation`) |
| Çap Güncelleme → Otomatik Etiket | Var | **Var** (`PipeDN_Changed` event → `AutoPipeLabeler`) |

---

## Bölüm 6 — Sık Karşılaşılan Sorunlar ve Çözümler

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

## Bölüm 7 — Geliştirme Tarihçesi

### Tamamlanan (Session #20 — 2026-05-19)

| # | Özellik | Durum | Ana Dosya |
|---|---|---|---|
| 1 | Pis Su Hesap Tablosu (TS EN 12056-2) | ✅ | `CalculationTableService.GenerateWasteWaterTable()` |
| 2 | Pis Su HTML Raporu (Manning + DU) | ✅ | `CalculationTableService.ExportWasteWaterToHtml()` |
| 3 | Hesap Tablosu — Pis Su Sekmesi | ✅ | `CalculationTableWindow.xaml` — TabControl |
| 4 | Bağımsız Hesap Modu (Manuel Giriş) | ✅ | `CalculationTableWindow` — Manuel Giriş sekmesi |
| 5 | DN Değişimi → AutoPipeLabeler Sync | ✅ | `PipeDN_Changed` event + `MainWindow` wire-up |
| 6 | Pompa Q-H Karakteristik Eğrisi | ✅ | `PumpSelectionService.GetPumpCurvePoints()` |
| 7 | Sistem Eğrisi + Çalışma Noktası | ✅ | `PumpSelectionService.CalculateDutyPoint()` |
| 8 | Kavitasyon Kontrolü (NPSHa/NPSHr) | ✅ | `PumpSelectionService.CheckCavitation()` |
| 9 | IFC İçeri Aktarma (2x3/4) | ✅ | `IfcImportService` — duvar/döşeme/pencere/kapı |
| 10 | DrainageOutletEntity derleme hataları | ✅ | Abstract üyeler tamamlandı |
| 11 | RainfallCatchmentEntity derleme hataları | ✅ | Abstract üyeler tamamlandı |
| 12 | WasteWaterLayerService API güncellemesi | ✅ | `LayerTable` → `GetLayers()`/`AddLayer()` |

### Tamamlanan (Session #21 — 2026-05-19)

| # | Özellik | Durum | Ana Dosya |
|---|---|---|---|
| 1 | Pompa Q-H Grafiği UI (SkiaSharp) | ✅ | `PumpSelectionDialog.xaml` — BEP/OP/Tasarım noktaları |
| 2 | IFC Import Dialog | ✅ | `IfcImportDialog.xaml` — dosya seçimi + katman önizleme |
| 3 | IfcImportService — AnalyzeFile + ScaleFactor | ✅ | `IfcImportService.AnalyzeFile()` — preview desteği |
| 4 | Sıcak Su Resirkülasyon Servisi | ✅ | `HotWaterCirculationService` — ısı kaybı + vana dengeleme |
| 5 | ASPE / BS 6700 / ASHRAE / IPC 2021 Normu | ✅ | `PipeSizer` + `StandardSelectionService` — 4 yeni norm |
| 6 | DXF R12 Dışa Aktarma | ✅ | `DxfWriterService` — LINE/TEXT/CIRCLE/ARC/katmanlar |
| 7 | MainWindow — PumpSelectionDialog wire-up | ✅ | `OnPumpSelection` → dialog ile (MessageBox kaldırıldı) |
| 8 | MainWindow — IFC Import + DXF Export komutları | ✅ | `OnIfcImportCommand`, `OnExportDxfCommand` |
| 9 | Ribbon — IFC İçeri / DXF Dışarı butonları | ✅ | Raporlar sekmesi → Dışa/İçeri grubu |

### Tamamlanan (Session #22 — 2026-05-19)

| # | Özellik | Durum | Ana Dosya |
|---|---|---|---|
| 1 | Sıcak Su Resirkülasyon Dialog | ✅ | `HotWaterCirculationDialog.xaml/.cs` — segment tablosu, HTML rapor, hız uyarısı |
| 2 | Basınç Bölgesi Servisi | ✅ | `PressureZoneService.cs` — TS EN 806-3, PRV zon hesabı, ExportToHtml |
| 3 | Basınç Bölgesi Dialog | ✅ | `PressureZoneDialog.xaml/.cs` — zon tablosu, özet, HTML rapor |
| 4 | Yangın Hidrant Hesabı | ✅ | `FireFightingService` +3 metot: `DesignHydrantSystem`, `DesignHoseReels`, `AnalyzeWaterSupply` |
| 5 | Boru Maliyet Analizi | ✅ | `PipeCostService.cs` — 35 malzeme fiyatı, proje maliyet özeti, HTML export |
| 6 | Boru Maliyet Dialog | ✅ | `PipeCostDialog.xaml/.cs` — giriş tablosu, sonuç, projeden yükle |
| 7 | Viewport Baskı / PNG Export | ✅ | `PrintViewportService.cs` — PrintDialog A3/A4, SkiaSharp PNG, başlık bloğu |
| 8 | MainWindow — yeni handler'lar | ✅ | `OnHotWaterCirculation`, `OnPressureZoneDesign`, `OnPipeCostAnalysis`, `OnPrintViewport`, `OnExportPng` |
| 9 | Ribbon — İleri Analiz grubu | ✅ | Resirkülasyon · Basınç Bölgesi · Maliyet · Yazdır · PNG |

### Tamamlanan (Session #23 — 2026-05-21)

| # | Özellik | Durum | Ana Dosya |
|---|---|---|---|
| 1 | FireFightingDialog 4-sekme yeniden tasarım | ✅ | Sprinkler · Hidrant · Hortum Makarası · Su Talebi — NFPA + TS EN 671 + TS EN 12845 |
| 2 | BOMDialog 3-sekme maliyet entegrasyonu | ✅ | `BOMDialog.xaml/.cs` — PipeCostService + vitrifiye fiyat katalogu + CSV/HTML export |
| 3 | AutoSizingService | ✅ | `AutoSizingService.cs` — TS EN 806-3 FU→Q→DN otomatik boyutlandırma, tüm ağ tek komut |
| 4 | PumpGroupDialog | ✅ | `PumpGroupDialog.xaml/.cs` — Paralel/seri Q-H SkiaSharp grafiği, çalışma noktası |
| 5 | PumpSelectionService.GetAllPumps() | ✅ | `PumpSelectionService.cs` — tüm pompa listesi API |
| 6 | MainWindow — Oto Boyut + Pompaj Grubu butonları | ✅ | İleri Analiz grubu genişletildi |

## Session #24 — Pis Su Hesap Föyü Tam Entegrasyon

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | WasteWaterCalcSheetService | ✅ | Manning + DU yöntemi, foseptik, pompa, HTML export |
| 2 | WasteWaterCalcSheetDialog | ✅ | 4-sekme: Hesap Föyü / Devre Seç. / Çukur-Pompa / Keşif |
| 3 | DrawingUpdateDialog | ✅ | Çizim üzerine etiket yazma — 7 özellik + Otomatik Yerleştirme |
| 4 | PrintContentDialog | ✅ | Baskı içeriği seçimi + HTML / Word raporu çıktısı |
| 5 | Kolon Şeması wiring | ✅ | RiserDiagramExportDialog'a bağlandı |
| 6 | WasteWaterCalcSheetDialog wiring | ✅ | UpdateDrawing + ExportHtml + RiserDiagram butonları bağlandı |

### Bir Sonraki Session Öncelikleri

1. **Basınç Düşümü Haritası** — Boru hattında renk gradyanı ile basınç dağılımı (viewport overlay)
2. **Tahliye Şeması** — Pis su kolonu izometrik diyagram otomatik üretimi
3. **Raporlama Motoru** — Tüm dialog çıktılarını tek PDF'e birleştirme
4. **Multi-floor Koordinasyon** — Kat tespiti ve çakışma kontrolü (clash detection)

---

## Session #25 — Pafta Düzeni ve Çıktı Hazırlama

| # | Özellik | Durum | OTONET Karşılığı |
|---|---------|-------|-----------------|
| 1 | FloorSnapshotService | ✅ | Kat tespiti + CaptureToBlock |
| 2 | ViewportCaptureDialog | ✅ | Ekran Çizimi → blok kaydet |
| 3 | XrefManagerDialog | ✅ | Dış Kaynak Yöneticisi — Bağla / Ayır |
| 4 | LayoutSheetDialog | ✅ | Pafta Düzeni — Blok Ekle + Patlat (tek kez kuralı) |
| 5 | MainWindow — Pafta & Çıktı grubu | ✅ | 3 yeni buton: Ekran Çizimi · Xref · Pafta |

### Bir Sonraki Session Öncelikleri

1. **Basınç Düşümü Haritası** — Boru hattında renk gradyanı ile basınç dağılımı (viewport overlay)
2. **Antet Şablonu** — Pafta sayfası için firma antet bloğu ekleme
3. **DXF/DWG Çıktı** — Pafta düzenini DXF olarak dışa aktar
4. **Multi-floor Koordinasyon** — Kat tespiti ve çakışma kontrolü (clash detection)

---

## Session #26 — Antet + Sistem Katman Görünürlüğü + DXF Çıktı

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | SystemLayerService | ✅ | MEP_TEMIZ_SU / MEP_PIS_SU / MEP_YANGIN vb. katman → renk eşleme |
| 2 | TitleBlockService | ✅ | ISO A0–A4 antet — çerçeve + firma + proje + pafta no + imza hücresi |
| 3 | TitleBlockDialog | ✅ | Tüm antet alanları + kağıt boyutu + konumlandırma |
| 4 | Sistem Katman Toggleları | ✅ | Görünüm tab'ı — 6 sistem butonu + Tümünü Göster (opacity ile görsel geri bildirim) |
| 5 | LayoutSheetDialog — Antet + DXF | ✅ | Antet Ekle ve DXF Çıktı butonları |
| 6 | Kat Senkron | ✅ | Tüm mekanik entity'lerin katmanını sistem tipine göre günceller |

### Bir Sonraki Session Öncelikleri

1. **Basınç Düşümü Haritası** — Boru hattında renk gradyanı ile basınç dağılımı (viewport overlay)
2. **Multi-floor Koordinasyon** — Kat tespiti ve çakışma kontrolü (clash detection)
3. **Raporlama Motoru** — Tüm dialog çıktılarını tek PDF'e birleştirme

---

---

## Session #27 — Entity Özellikleri + Excel/DWG Export + Doğalgaz + Duvar Rotalama

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | EntityPropertiesDialog | ✅ | Seçili entity'nin tüm özelliklerini düzenle — Katman, Renk, Çap, Sistem Tipi |
| 2 | GasCalcDialog (TS EN 1775) | ✅ | Doğalgaz boru hesabı — segment tablosu, Reynolds, Darcy-Weisbach, basınç kaybı |
| 3 | GasCalcSheetService | ✅ | TS EN 1775 / TS 7363 düşük basınç boru boyutlandırma motoru |
| 4 | RainWaterCalcDialog | ✅ | TS EN 12056-3 yağmur suyu hesabı — çatı alanı, yoğunluk, gider DN seçimi |
| 5 | UserFixtureCatalogDialog | ✅ | Vitrifiye kataloğunu CRUD yönet — Ekle/Sil/Çoğalt + JSON İçe/Dışa Aktar |
| 6 | FixtureLibraryService — mutations | ✅ | `AddOrUpdate`, `Delete`, `ImportJson`, `ExportJson`, `ResetToDefaults` |
| 7 | WallParallelRouteDialog | ✅ | Duvara paralel boru rotalama — otomatik layer tespiti + manuel koordinat modu |
| 8 | WallParallelRoutingService | ✅ | Mimari duvar boyunca boru yerleştirme motoru |
| 9 | FixtureLibraryDialog — CadDatabase entegrasyonu | ✅ | "+ Çizime Ekle" butonu aktif, veritabanına entity yazma |
| 10 | Excel/DWG Export geliştirmeleri | ✅ | ClosedXML ile geliştirilmiş BOM export |

### OtoNET/FINE MEP Karşılıkları (Session #27)

| OtoNET/FINE MEP | AfneyCAD | Dialog |
|---|---|---|
| Nesne Özellikleri (çift tık) | **Entity Özellikleri** | `EntityPropertiesDialog` |
| Doğalgaz Hesabı modülü | **⛽ Doğalgaz Hesabı** | `GasCalcDialog` |
| Yağmur Suyu hesap modülü | **🌧 Yağmur Suyu** | `RainWaterCalcDialog` |
| Armatür katalog düzenleme | **📚 Katalog** | `UserFixtureCatalogDialog` |
| Duvara paralel boru | **Parallel boru rotalama** | `WallParallelRouteDialog` |

### Bir Sonraki Session Öncelikleri (#28)

1. **Vana Kütüphanesi** — boru üstüne vana yerleştirme, pipe-split (TS EN 1074)
2. **RiserEngine gerçek veri** — hardcoded veriden kurtulup DB bağlantısı
3. **PDF Rapor** — SkiaSharp ile sistem özeti + metraj cetveli

---

## Session #28 — Vana Kütüphanesi + RiserEngine + PDF Rapor + Tesisat Ekipmanları + Topoloji

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | ConnectReceptorsService — DB sync fix | ✅ | `HashSet<Guid>` + `newEntitiesMap` ile split-of-split hatası giderildi |
| 2 | ValveLibraryDialog — boru üstüne yerleştirme | ✅ | Snap + pipe-split + TS EN 1074 standartlı katalog |
| 3 | RiserDiagramExportDialog — gerçek model verisi | ✅ | `MechanicalKernel.GetRiserSchemas()` ile 3D model → 2D şema |
| 4 | PdfExportService (SkiaSharp) | ✅ | 2-sayfalı A4 PDF: Sistem Özeti + Metraj Cetveli |
| 5 | WaterTankService | ✅ | TS 1258 — günlük ihtiyaç, depo hacmi, Walther pik debisi, hidrofor seçimi |
| 6 | WaterMeterService | ✅ | TS EN 14154 — DN/Qnom/Qmax/Δp katalog tablosu + standart sayaç önerisi |
| 7 | ThermalExpansionService | ✅ | TS EN 13831 — Δv (IAPWS), genleşme hacmi, precharge basıncı, membran tank seçimi |
| 8 | BackflowPreventerService | ✅ | TS EN 1717 — risk sınıfı 1-5 → cihaz tipi (DC/CA/BA/AA) + Δp |
| 9 | NetworkTopologyAnalysisService | ✅ | DFS döngü tespiti, BFS bağlantısız bileşen, Dijkstra kritik yol |
| 10 | MainWindow crash fix | ✅ | `Viewport.EntityDoubleClicked` wire-up `CreateNewDocument` öncesinden sonrasına taşındı |

### Yeni Dialog'lar (Session #28)

| Dialog | Açıklama | Standart |
|---|---|---|
| `ValveLibraryDialog` | Vana kütüphanesi — boruya snap+split ile yerleştirme | TS EN 1074 |
| `PdfExportDialog` | Proje adı + içerik seçimi + PDF oluşturma | — |
| `DepoHidroforDialog` | Su deposu hacmi + pik debi + hidrofor seçimi | TS 1258 |
| `WaterMeterDialog` | Su sayacı DN seçimi — DN15..DN50 katalog | TS EN 14154 |
| `ExpansionTankDialog` | Genleşme tankı — sistem hacmi + sıcaklık | TS EN 13831 |
| `BackflowPreventerDialog` | Geri akış önleyici — risk sınıfı → cihaz tipi | TS EN 1717 |
| `NetworkTopologyDialog` | Döngü / açık uç / kritik yol — "Kritik Yolu Seç" butonu | — |

### OtoNET/FINE MEP Karşılıkları (Session #28)

| OtoNET/FINE MEP | AfneyCAD | Dialog |
|---|---|---|
| Vana yerleştirme (boruya bağlı) | **🔧 Vana** → Tesisat tab'ı | `ValveLibraryDialog` |
| Kolon şeması (3D model → 2D) | **📐 Kolon Şeması** | `RiserDiagramExportDialog` |
| PDF teknik rapor | **📄 PDF Rapor** | `PdfExportDialog` |
| Su deposu / Hidrofor seçimi | **🏗️ Depo/Hidrofor** | `DepoHidroforDialog` |
| Su sayacı DN seçimi | **💧 Su Sayacı** | `WaterMeterDialog` |
| Genleşme tankı hesabı | **♻ Genleşme** | `ExpansionTankDialog` |
| Geri akış önleyici | **🛡 Geri Akış** | `BackflowPreventerDialog` |
| Sistem topoloji raporu | **🔗 Topoloji** | `NetworkTopologyDialog` |

### Bölüm 5 — Özellik Karşılaştırma Tablosu Güncellemesi

Aşağıdaki satırlar Bölüm 5 tablosuna eklenmiştir:

| Özellik | OtoNET/FINE MEP | AfneyCAD v3.0 |
|---|---|---|
| Vana Kütüphanesi + Yerleştirme | Var | **Var** (`ValveLibraryDialog`, pipe-split, TS EN 1074) |
| PDF Teknik Rapor | Var | **Var** (`PdfExportService`, SkiaSharp, A4) |
| Su Deposu / Hidrofor Seçimi | Var | **Var** (`WaterTankService`, Walther formülü, TS 1258) |
| Su Sayacı Seçimi | Var | **Var** (`WaterMeterService`, DN15-50, TS EN 14154) |
| Genleşme Tankı Hesabı | Var | **Var** (`ThermalExpansionService`, IAPWS-IF97, TS EN 13831) |
| Geri Akış Önleyici | Var | **Var** (`BackflowPreventerService`, Sınıf 1-5, TS EN 1717) |
| Topoloji Analizi | Var | **Var** (`NetworkTopologyAnalysisService`, DFS+BFS+Dijkstra) |
| RiserDiagram — Gerçek Model | Var | **Var** (3D → `MechanicalKernel.GetRiserSchemas()` → 2D SVG) |

### Bir Sonraki Session Öncelikleri (#29)

1. **Basınç Düşümü Haritası** — Viewport overlay: boru hattında renk gradyanı ile basınç dağılımı
2. **Hesap Tablosu ↔ Çizim Senkronizasyonu** — Çap/vana değişikliği çizimdeki etiketlere anlık yazar
3. **Doğalgaz Hesap Tablosu entegrasyonu** — GasCalcDialog → CalculationTableWindow entegrasyonu
4. **Antetli PDF** — Firma logosu + mühendis imzası + proje bilgileri bloğu
5. **3D Çakışma Tespiti geliştirme** — MEP-MEP çakışması (boru-boru, boru-vana)

---

---

## Session #29 — Basınç Haritası + Çizim Sync + Antetli PDF

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | PressureMapService | ✅ | Boru `PressureDrop` → yeşil/sarı/kırmızı gradyan; toggle ile entity.Color geçici override |
| 2 | DrawingSyncService | ✅ | `CalculationTableWindow.PipeDN_Changed` → Ø etiketi anında güncelleme (geri besleme döngüsü) |
| 3 | PdfExportService.TitleBlockInfo | ✅ | Firma / mühendis / proje no / imza / revizyon antet bloğu |
| 4 | PdfExportDialog | ✅ | Firma, Mühendis, Proje No, Adres, Çizen, Kontrol alanları |

### OtoNET Karşılıkları (Session #29)

| OtoNET/FINE MEP | AfneyCAD |
|---|---|
| Hesap tablosunda çap değiştir → çizimde güncellenir | `DrawingSyncService` — `PipeDN_Changed` event |
| Basınç düşümü haritası | `PressureMapService` — 🌡️ buton toggle |
| Antetli teknik rapor | `PdfExportDialog` — TitleBlockInfo alanları |

---

## Session #30 — Tüm Eksiklikler Tamamlandı

| # | Özellik | Durum | Standart/Detay |
|---|---------|-------|----------------|
| 1 | 3D MEP-MEP Çakışma Tespiti | ✅ | `ClashDetectionService` — 3D segment mesafe (GCD) + ValveEntity BBox; eski 2D yerine tam 3D |
| 2 | Doğalgaz Hesap Föyü Entegrasyonu | ✅ | `CalculationTableWindow` ⛽ sekmesi — giriş/min.basınç, DB boruları, HTML export (TS EN 1775) |
| 3 | Parametrik BIM Nesneleri | ✅ | `ArchitecturalObstacle` + `BimMaterialLayer`: U-değeri (ISO 6946), yangın (TS EN 13501-1), ses yalıtımı |
| 4 | BimPropertiesDialog | ✅ | Malzeme katmanı tablosu, U hesaplama, yangın sınıfı, 3 şablon (dış duvar/iç bölme/döşeme) |
| 5 | Akıllı DWG→BIM Dönüşüm | ✅ | `SmartBimConverterService` — LineEntity/LwPolyline → ArchitecturalObstacle; layer auto-detect |
| 6 | SmartBimConverterDialog | ✅ | Layer tarama, çoklu seçim, kalınlık/yükseklik, nesne tipi |
| 7 | Geniş Mimari Kütüphane | ✅ | `ArchitecturalLibraryService` — 20+ nesne: 4 kolon, 3 döşeme, 3 çatı, 9 mobilya, 2 ekipman |
| 8 | ArchitecturalLibraryDialog | ✅ | Kategori filtreli DataGrid + detay paneli + "BIM Ekle" |
| 9 | MDI + Sekme UI | ✅ | "+" yeni sekme butonu + "n/toplam" sayaç göstergesi |
| 10 | ADAPT/FCALC Saf Mod | ✅ | JSON kaydet/yükle + Excel (.xlsx) export (ClosedXML) — Manuel giriş sekmesi |

### OtoNET/FINE MEP Karşılıkları (Session #30)

| OtoNET/FINE MEP | AfneyCAD | Dialog |
|---|---|---|
| Nesne özellikleri (U-değeri, yangın) | **🏢 BIM Özellik** | `BimPropertiesDialog` |
| DWG altlıktan BIM dönüşüm | **🧱 DWG→BIM** | `SmartBimConverterDialog` |
| Mimari kütüphane (kolon/döşeme) | **🏗️ Mimari Ktph.** | `ArchitecturalLibraryDialog` |
| Doğalgaz boru hesabı | **⛽ Doğalgaz sekmesi** | `CalculationTableWindow` |
| AutoBUILD çakışma analizi | **💥 Çakışma Analizi** | `ClashDetectionService` (3D) |
| Çoklu proje | **+ sekme** | `DocumentTabs` MDI |
| FCALC veri kaydetme | **JSON kaydet/yükle** | Manuel giriş sekmesi |
| FCALC Excel çıktı | **📊 Excel export** | Manuel giriş sekmesi → ClosedXML |

### Bölüm 5 — Tamamlanan Özellik Tablosu Güncellemesi

| Özellik | OtoNET/FINE MEP | AfneyCAD v3.1 |
|---|---|---|
| Parametrik BIM (U-değeri/Yangın) | Var | **Var** (`BimMaterialLayer`, ISO 6946, TS EN 13501-1) |
| DWG→BIM otomatik dönüşüm | Var | **Var** (`SmartBimConverterService`) |
| Mimari kütüphane (20+ nesne) | Var | **Var** (`ArchitecturalLibraryService`) |
| Doğalgaz hesap föyü (TS EN 1775) | Var | **Var** (CalculationTableWindow ⛽ sekmesi) |
| 3D MEP-MEP çakışma | Var | **Var** (`ClashDetectionService` — 3D GCD + valve) |
| MDI Çoklu Proje | Var | **Var** (DocumentTabs + + butonu + sayaç) |
| Hesap tablosu kaydet/yükle | Var | **Var** (JSON + Excel ClosedXML) |

---

---

## Session #31 — Isıtma + HVAC + 6 Fine MEP Eksikliği Tamamlandı

| # | Özellik | Durum | Standart/Detay |
|---|---------|-------|----------------|
| 1 | HeatingSystemService | ✅ | TS 825 / TS EN 12831 — 24 radyatör katalogu, 18 şehir tasarım sıcaklığı, kazan+pompa önerisi |
| 2 | HeatingDesignDialog | ✅ | 3 sekme: Şehir/Bina · Oda Tablosu · Sonuçlar — Excel + HTML export |
| 3 | DuctSizingService | ✅ | TS EN 13779 — eşit sürtünme yöntemi, Darcy-Weisbach, 18 zone tipi DefaultAirChanges |
| 4 | HvacDesignDialog | ✅ | Zone tablosu, dikdörtgen/yuvarlak kanal seçimi, uzunluk tahmini, Excel export |
| 5 | GutterSizingService | ✅ | TS EN 12056-3 — 16 şehir yağış yoğunluğu, 8 yüzey C katsayısı, Manning yarım daire, DN50-DN160 |
| 6 | ExpansionLoopService | ✅ | TS EN 13480 / ASME B31.1 — 8 malzeme alfa, ΔL hesabı, U/Z/L-dirsek kol boyu |
| 7 | PipeNoiseService | ✅ | TS EN 14366 / DIN 4109 — Lw modeli, 3 DIN sınıfı, uyarı per-segment |
| 8 | MechanicalProjectSettings — Yaşlanma | ✅ | AWWA M11 — PipeAgeYears, AgingRateMmPerYear, EffectiveRoughness → PressureDropService |
| 9 | AdvancedToolsDialog (4 sekme) | ✅ | Yağmur Oluğu · Genleşme Kompansatörü · Boru Yaşlanması · Gürültü Analizi |
| 10 | CalculationTableWindow — Ekipmanlar sekmesi | ✅ | WaterTank + WaterMeter + ExpansionTank + BackflowPreventer → ClosedXML Excel export |
| 11 | CalculationTableWindow — Doğalgaz sekmesi | ✅ | TS EN 1775 — DB boruları, HTML export; GasCalcSheetService.ExportToHtml eklendi |

### Yeni Dialog'lar (Session #31)

| Dialog | Açıklama | Standart |
|---|---|---|
| `HeatingDesignDialog` | Isıtma yük hesabı — oda tablosu, radyatör/kazan seçimi, Excel/HTML | TS 825, TS EN 12831 |
| `HvacDesignDialog` | Havalandırma kanal boyutlandırma — zone tablosu, Excel | TS EN 13779 |
| `GutterDesignDialog` | Yağmur oluğu — çatı bölümü tablosu, şehir yağışı, DN seçimi | TS EN 12056-3 |
| `AdvancedToolsDialog` | 4 sekmeli ileri araçlar: Yağmur Oluğu · Kompansatör · Yaşlanma · Gürültü | TS EN 12056-3, TS EN 13480, TS EN 14366 |

### Yeni Servisler (Session #31)

| Servis | Algoritma | Standart |
|---|---|---|
| `HeatingSystemService` | Q = ΣU·A·ΔT + infiltrasyon; radyatör 60/40°C düzeltme (×0.69) | TS 825, TS EN 12831 |
| `DuctSizingService` | Eşit sürtünme yöntemi; Darcy-Weisbach (λ=0.02) | TS EN 13779 |
| `GutterSizingService` | Q = C·i·A; Manning yarım daire; akış oranı ≤0.5 | TS EN 12056-3 Ek NA |
| `ExpansionLoopService` | ΔL = α·L·ΔT; L_u = C·√(D·ΔL) U-dirsek; Z/L offset | TS EN 13480, ASME B31.1 |
| `PipeNoiseService` | L_w = K_base + 10·log(Q²/D) + ΔL_v + 1.5 fitting | TS EN 14366, DIN 4109 |

### OtoNET/FINE MEP Karşılıkları (Session #31)

| OtoNET/FINE MEP | AfneyCAD | Dialog |
|---|---|---|
| Isıtma yük hesabı (TS 825) | **Isıtma Tasarımı** | `HeatingDesignDialog` |
| HVAC kanal hesabı (TS EN 13779) | **Havalandırma** | `HvacDesignDialog` |
| Yağmur oluğu boyutlandırma | **İleri Araçlar → Yağmur Oluğu** | `AdvancedToolsDialog` |
| Genleşme kompansatörü | **İleri Araçlar → Genleşme** | `AdvancedToolsDialog` |
| Boru yaşlanma modeli (AWWA M11) | **İleri Araçlar → Yaşlanma** | `AdvancedToolsDialog` |
| Gürültü analizi (DIN 4109) | **İleri Araçlar → Gürültü** | `AdvancedToolsDialog` |
| Merkezi ekipman spreadsheet | **Ekipmanlar sekmesi** | `CalculationTableWindow` |

### Bölüm 5 — Tamamlanan Özellik Tablosu Güncellemesi

| Özellik | OtoNET/FINE MEP | AfneyCAD v3.2 |
|---|---|---|
| Isıtma Yük Hesabı | Var | **Var** (`HeatingSystemService`, 18 şehir, 24 radyatör, TS 825) |
| HVAC Kanal Boyutlandırma | Var | **Var** (`DuctSizingService`, TS EN 13779, eşit sürtünme) |
| Yağmur Oluğu Boyutlandırma | Var | **Var** (`GutterSizingService`, TS EN 12056-3, 16 şehir) |
| Genleşme Kompansatörü | Var | **Var** (`ExpansionLoopService`, TS EN 13480, U/Z/L dirsek) |
| Boru Yaşlanma Modeli | Var | **Var** (`MechanicalProjectSettings.EffectiveRoughness`, AWWA M11) |
| Gürültü Analizi | Var | **Var** (`PipeNoiseService`, TS EN 14366, DIN 4109 sınıfı) |
| Merkezi Ekipman Spreadsheet | Var | **Var** (`CalculationTableWindow` Ekipmanlar sekmesi + Excel) |

### Bir Sonraki Session Öncelikleri (#32)

1. **Real-time Çakışma Vurgusu** — `ClashDetectionService` → viewport'ta kırmızı overlay
2. **Bulut Senkronizasyonu** — Azure/GDrive proje yedekleme
3. **Boru Ağı Animasyonu** — akış yönü ve hız animasyonu (SkiaSharp)
4. **Mobil Görüntüleyici** — web/mobil read-only görüntüleme

---

---

## Session #32 — Real-time Çakışma Vurgusu

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | ClashHighlightService | ✅ | Çakışan entity'lere renk override: Critical=#FF2200 (kırmızı), Warning=#FF8800 (turuncu) |
| 2 | 🔴 Çakışma Vurgusu butonu | ✅ | Ribbon → Validasyon & Çakışma grubuna eklendi; toggle ile açılır/kapanır |
| 3 | Durum çubuğu özet | ✅ | Toplam çakışma · kritik sayısı · uyarı sayısı · etkilenen eleman bilgisi |

### Nasıl Çalışır

1. Ribbon → Validasyon & Çakışma → **🔴 Çakışma Vurgusu** butonuna tıklayın
2. `ClashHighlightService.Apply()` çalışır:
   - Mevcut mimari engeller (`MechanicalKernel.ArchitecturalObstacles`) ile tüm boru/dirsek/T-parçaları karşılaştırılır
   - Çakışan entity'lerin orijinal renkleri saklanır
   - Critical çakışmalar **kırmızı** (#FF2200), Warning çakışmalar **turuncu** (#FF8800) ile işaretlenir
3. Durum çubuğunda özet: "🔴 Çakışma Vurgusu: N çakışma · M kritik · K uyarı"
4. Tekrar tıklayarak vurguyu kapatın → orijinal renkler geri yüklenir

### OtoNET/FINE MEP Karşılığı

| OtoNET/FINE MEP | AfneyCAD |
|---|---|
| Çakışma raporunda kırmızı vurgulama | **🔴 Çakışma Vurgusu** — viewport'ta gerçek zamanlı renk override |

### Bir Sonraki Session Öncelikleri (#33)

1. **Bulut Senkronizasyonu** — Azure/GDrive proje yedekleme
2. **Boru Ağı Animasyonu** — akış yönü ve hız animasyonu (SkiaSharp)
3. **Mobil Görüntüleyici** — web/mobil read-only görüntüleme

---

*Son güncelleme: 2026-06-09 | AfneyCAD v3.3.0 — Session #32: Real-time Çakışma Vurgusu*
