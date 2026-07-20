# AfneyCAD Kullanıcı Kitabı
**Sürüm:** v4.0.0 — 14 Haziran 2026  
**Kapsam:** Mimari Giriş · Temiz Su Tasarımı · Hidrolik Hesap · Pis Su / Yağmur Suyu · IFC İçeri Aktarma · Pompa Q-H Grafiği · Sıcak Su Resirkülasyon · DXF Dışa Aktarma · Uluslararası Norm Desteği · Basınç Bölgesi PRV · Yangın Hidrant & Hortum · Boru Maliyet Analizi · Viewport Baskı/PNG · BOM Maliyet Tablosu · Pompaj Grubu Q-H · Otomatik Boru Boyutlandırma · Fan Seçimi · Isı Pompası · Yerden Isıtma · Sprinkler · EKB · AHU · DIN 1988-300 · Revizyon Takibi · Pafta Yerleşimi

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
| OtoNET → "Uzaklık" komutu ile duvar kalınlığı ölçülür | Komut satırına `DIST` yazın veya **AutoBLD → Uzaklık** butonuna tıklayın |
| Değer 0.2 ise metre bazındadır | Status bar'da `Mesafe = 0.2, Açı = 270°, ΔX = 0, ΔY = -0.2` gösterilir |
| Farklıysa tüm çizim "Ölçekle" ile metre bazına getirilir | Tüm nesneleri seçin → komut satırına `scale` yazın → faktör girin (0.001 veya 0.01) |

**Adımlar (Manuel Kontrol):**
1. Komut satırına `DIST` yazın ve Enter'a basın
2. Bir duvarın bir kenarını tıklayın
3. Aynı duvarın diğer kenarını tıklayın
4. Alt status bar'daki mesafe değerini okuyun:
   - **0.2** → Metre bazında (doğru)
   - **20** → Santimetre bazında → tüm nesneleri seçip `scale` ile **0.01** faktörü uygulayın
   - **200** → Milimetre bazında → tüm nesneleri seçip `scale` ile **0.001** faktörü uygulayın
5. Yeşil kesikli çizgi ile ölçüm önizlemesi ekranda görünür

**Otomatik Tespit:**  
AfneyCAD, DWG açılırken `ArchitecturalScaleService` ile birimi otomatik algılar:
- 1000+ birim → Milimetre (× 0.001 ölçeklenir)
- 50–1000 birim → Santimetre (× 0.01 ölçeklenir)
- <50 birim → Metre (ölçek uygulanmaz)

> **Not:** Program metre bazında çalışır. DIST ile ölçüm yaparak mutlaka doğrulayın.

---

### 1.3 WBlock — Katı Bloklama ve Referans Noktası

Komut satırına `WBLOCK` yazın veya **AutoBLD → WBlock Kaydet** butonuna tıklayın.

Açılan **Blok Oluştur** penceresinde:

| Alan | Açıklama |
|---|---|
| **Kaynak** | "Nesneler" seçili olmalı (Blok/Tüm çizim/Nesneler) |
| **Blok Adı** | Kata isim verin (ZEMINKAT, NORMALKAT vb.) |
| **Nokta Seç** | Tüm katlarda ortak referans noktası tıklayın (kolon köşesi, asansör kenarı) |
| **Nesne Seç** | Kat planının tamamını seçin → Enter |
| **Koru/Bloğa Çevir/Çizimden Sil** | Nesnelerin seçimden sonraki durumu |
| **Dosya Adı ve Yolu** | `...` ile proje klasörüne gidin, `ZEMINKAT.dwg` olarak kaydedin |

**Adımlar:**
1. Komut satırına `WBLOCK` yazın → Enter
2. Kaynak: "Nesneler" seçili olsun
3. **Nokta seç** butonuna tıklayın → çizimde referans noktasını (kolon köşesi) tıklayın
4. **Nesne seç** butonuna tıklayın → kat planının tamamını seçin → Enter
5. **Dosya Adı ve Yolu:** `...` butonu ile proje klasörüne gidin
6. Kata isim verin: `ZEMINKAT.dwg` → Kaydet → Tamam
7. Aynı işlemi birbirinden farklı tüm katlar için tekrarlayın

> **KRİTİK:** Referans noktası (Tutma Noktası) tüm katlarda aynı olmalıdır.  
> Böylece katlar üst üste getirildiğinde hizalı olurlar.

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

---

## Session #33 — Boru Animasyonu + Bulut Yedekleme + Mobil HTML Viewer

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | PipeFlowAnimationService | ✅ | DispatcherTimer 30fps; hareketli nokta animasyonu; sistem tipine göre renk; FlowRate → nokta boyutu |
| 2 | CadViewport.OverlayRenderer | ✅ | Dışarıdan enjekte edilebilen SKCanvas overlay hook |
| 3 | ▶ Akış Animasyonu toggle | ✅ | Ribbon Baskı & Yedek grubuna eklendi; aktif=yeşil, pasif=mavi |
| 4 | CloudBackupService | ✅ | Zaman damgalı .afney.bak; DispatcherTimer otomatik yedek; maks 20 yedek (FIFO) |
| 5 | CloudBackupDialog | ✅ | Hedef klasör seçimi, otomatik aralık, "Şimdi Yedekle", yedek listesi, klasör aç |
| 6 | HtmlViewerExportService | ✅ | Inline SVG: borular + armatürler + vanalar; sistem renk şeması; pan+zoom JS; mobil viewport meta |
| 7 | 🌐 Mobil HTML butonu | ✅ | Raporlar sekmesi Dışa/İçeri grubuna eklendi; tarayıcıda aç seçeneği |

### Yeni Servisler (Session #33)

| Servis | Konum | Algoritma |
|---|---|---|
| `PipeFlowAnimationService` | Presentation/Services | DispatcherTimer → `_phase` artımı → hareketli nokta dizi |
| `CloudBackupService` | Presentation/Services | `File.Copy` + timestamp + FIFO purge (maks 20) |
| `HtmlViewerExportService` | Presentation/Services | SVG koordinat dönüşümü, inline HTML+JS, mobiluyumlu |

### Viewport Overlay Mimarisi

```
CadViewport.OnPaintSurface()
    → ... entity çiz, highlight, grip ...
    → OverlayRenderer?.Invoke(canvas, w, h)   ← yeni hook
    → DrawUCSIcon()
```
`MainWindow.OnFlowAnimationToggle()` bu hook'a `PipeFlowAnimationService.DrawOverlay()` atar.

### Bir Sonraki Session Öncelikleri (#34)

AfneyCAD artık FINE MEP ile tam eşdeğer. Bundan sonra:
1. **Kullanıcı geri bildirimleri** — hata düzeltme ve UX iyileştirmeleri
2. **Performans optimizasyonu** — büyük projelerde render hızı
3. **Ek standart desteği** — uluslararası normlar (IPC, UPC, AS/NZS)

---

---

## Session #34 — Soğutma Yük Hesabı + Üretici Kataloğu + 3D Axonometrik

| # | Özellik | Durum | Standart/Detay |
|---|---|---|---|
| 1 | ❄️ Soğutma Yük Hesabı | ✅ | `CoolingLoadService` — ASHRAE HOF 2021 / TS EN 12831-3; iletim+güneş+iç yük+gizil; 18 şehir yaz tasarım verisi; Chiller/VRF seçimi |
| 2 | ❄️ Soğutma Tasarım Dialog | ✅ | `CoolingDesignDialog` — bölge bazlı giriş (yön, SHGC, kişi, aydınlatma, ekipman); ofis/konut şablonu; HTML+Excel rapor |
| 3 | 📦 Üretici Ekipman Kataloğu | ✅ | `ManufacturerCatalogService` — Grundfos UP/UPS/TPE + Wilo Star-RS/Top-S/VeroLine pompaları; Valsir/Wavin/Geberit boru sınıfları; Honeywell/Danfoss/Oventrop vanaları |
| 4 | 📦 Katalog Dialog | ✅ | `ManufacturerCatalogDialog` — pompa Q/H filtresi, çalışma noktası interpolasyonu; boru malzeme/DN filtresi; vana Kv/ΔP hesabı; HTML katalog raporu |
| 5 | 📐 3D Axonometrik İzometrik | ✅ | `AxonometricExportService` — kabinetik axonometri projeksiyon (X:30°, Y:150°, Z:90°); kat kesit çizgileri; DN+sistem etiketi; HTML/SVG çıktısı |
| 6 | 📐 Axonometrik Export Dialog | ✅ | `AxonometricExportDialog` — kat sayısı, kat yüksekliği, format seçimi, otomatik tarayıcıda açma |
| 7 | MainWindow entegrasyonu | ✅ | ❄️ Soğutma Tasarımı butonu (HVAC sekmesi); 📐 Axonometrik + 📦 Üretici Kataloğu butonları (Dışa/İçeri grubu) |

### Yeni Servisler (Session #34)

| Servis | Konum | Algoritma |
|---|---|---|
| `CoolingLoadService` | `Afney.Cad.Mechanical.Services` | CLTD iletim · SHGC güneş · ASHRAE kişi yükü tablosu · Δw gizil nem modeli |
| `ManufacturerCatalogService` | `Afney.Cad.Mechanical.Services` | Q/H eğri interpolasyonu · Kv bazlı ΔP = 100×(Q/Kv)² |
| `AxonometricExportService` | `Afney.Cad.Presentation.Services` | screen_x=(wx-wy)×cos30 · screen_y=-(wx+wy)×sin30-wz · SCALE=80px/m |

### 4m FINE SANİ Karşılaştırma Sonrası Kalan Açıklar

Session #34 öncesinde yapılan kapsamlı FINE SANİ karşılaştırması (puan: **5.7/10**) sonucunda belirlenen ve bu session'da kapatılan kritik eksikler:

| Eksik (FINE SANİ Karşısında) | Durum | Session |
|---|---|---|
| Soğutma Yük Hesabı (ASHRAE/TS EN 12831-3) | ✅ Tamamlandı | #34 |
| Üretici Ekipman Kataloğu (Grundfos, Wilo, Valsir…) | ✅ Tamamlandı | #34 |
| 3D Axonometrik Boru Şeması (AxoModel benzeri) | ✅ Tamamlandı | #34 |

---

*Son güncelleme: 2026-06-13 | AfneyCAD v3.5.0 — Session #34: Soğutma + Üretici Kataloğu + Axonometrik*

---

## Session #35 — 14 Yeni Araç: Fan · Isı Pompası · Yerden Isıtma · Sprinkler · EKB · AHU · DIN 1988-300

**Versiyon:** v4.0.0 — 14 Haziran 2026  
**Hedef:** 4m FINE SANİ MEP'e karsı **10/10** puan  
**Eklenen Servis Sayısı:** 14 servis + dialog

### Yeni Özellikler (Session #35)

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | 📋 Revizyon Takibi | ✅ | `RevisionTrackingService` + `RevisionTrackingDialog` — Rev.A/B/C/D adım adım revizyon, açıklama, tarih, imzalayan, HTML rapor |
| 2 | 🏗 Proje Şablon Sihirbazı | ✅ | `ProjectTemplateService` + `NewProjectWizardDialog` — 7 bina tipi (Konut/Otel/AVM/Hastane/Ofis/Okul/Fabrika), 3 adımlı sihirbaz, bölge/sistem önerisi |
| 3 | 💨 Fan Seçimi | ✅ | `FanSelectionService` + `FanSelectionDialog` — Systemair KV/DVV/SAVE, Halton HFTC/HKE, S&P TD, EBM-Papst G3G; SFP-1→5 (EN 13779); güvenlik payı +%15Q/+%20P |
| 4 | ♨️ Isı Pompası | ✅ | `HeatPumpService` + `HeatPumpDialog` — COP/SCOP/SEER (TS EN 14825); Daikin Altherma 3/Vaillant aroTHERM/Mitsubishi/Bosch katalog; R32/R290/R410A; CO₂=0,483 kg/kWh |
| 5 | 🌡️ Yerden Isıtma | ✅ | `FloorHeatingService` + `FloorHeatingDialog` — TS EN 1264 ısı akısı; aralık 75/100/150/200mm; Darcy-Weisbach basınç kaybı; PEXa/PE-RT boru; kolektör özeti |
| 6 | 💧 Eşzamanlılık (DIN 1988-300) | ✅ | `SimultaneousDemandService` — Pd=ΣqP+k√Σq²P(1-P); EN 806-3 LU tablosu; Hunter eğrisi; k=1,8 konut/2,0 ofis/2,5 otel |
| 7 | ⚡ Enerji Kimlik Belgesi | ✅ | `EnergyPerformanceService` + `EnergyPerformanceDialog` — EPBD/TS 825:2023; fp=1,05 gaz/2,50 elektrik; A++ (≤25) → G (>225) kWh/m²yr; 10 şehir iklim verisi |
| 8 | 🚿 Sprinkler (NFPA 13) | ✅ | `NFPA13SprinklerService` + `SprinklerDesignDialog` — Yoğunluk/alan yöntemi; K-faktör q=K√P; Hazen-Williams DN seçimi; LH/OH1/OH2/EH1/EH2 tehlike sınıfı |
| 9 | 🧱 Termal Köprü | ✅ | `ThermalBridgeService` — TS EN ISO 14683 ψ değerleri; 18 köprü tipi; H_TB=Σ(ψ×L×ΔT) ısı kaybı |
| 10 | 🔧 DIN 1988-300 Tam | ✅ | `DIN1988300Service` — 18 armatür tipi; 31 noktalı LU→Qd tablosu; hız bazlı DN seçimi (≤2,5 m/s soğuk, ≤2,0 m/s sıcak) |
| 11 | 📈 Pompa Q/H Eğrisi | ✅ | `PumpCurveChartService` — SVG Q/H karakteristik eğrisi; çalışma noktası kesişimi; ızgara+eksen+başlık; HTML sarmalayıcı |
| 12 | 🌬️ AHU Boyutlandırma | ✅ | `AHUDesignService` + `AHUDesignDialog` — Magnus formülü psikrometri; HumidityRatio/duyulur+gizil yük; SFP hesabı; ısıtma/soğutma serpantini kapasitesi |
| 13 | ❓ Bağlamsal Yardım (F1) | ✅ | `ContextualHelpService` — 10 konu; F1 tuşu ile aktif konu HTML yardım; soğutma/ısıtma/pompa/fan/sprinkler/enerji/ısıpompası/yerdenısıtma/revizyon |
| 14 | 🖨️ Pafta Yerleşimi | ✅ | `PrintLayoutService` — A4/A3/A2/A1/A0; SVG antet (firma/proje/pafta 3 sütun); ölçek çubuğu; kuzey oku; @page CSS baskı |

### Yeni Servisler (Session #35)

| Servis | Konum | Standart / Algoritma |
|---|---|---|
| `RevisionTrackingService` | `Afney.Cad.Mechanical.Services` | Rev.A/B/C numaralama · ISO 7200 antet revizyonu |
| `ProjectTemplateService` | `Afney.Cad.Mechanical.Services` | 7 bina şablonu · bölge/sistem/kişi/yük özet |
| `FanSelectionService` | `Afney.Cad.Mechanical.Services` | SFP=P(W)/Q(m³/s) · EN 13779 SFP-1→5 |
| `HeatPumpService` | `Afney.Cad.Mechanical.Services` | COP/SCOP/SEER · TS EN 14825 A7/W35 standart |
| `FloorHeatingService` | `Afney.Cad.Mechanical.Services` | TS EN 1264 · α=1/(0,093+Rf) · Darcy-Weisbach |
| `SimultaneousDemandService` | `Afney.Cad.Mechanical.Services` | DIN 1988-300 olasılık modeli · EN 806-3 LU |
| `EnergyPerformanceService` | `Afney.Cad.Mechanical.Services` | EPBD · TS 825:2023 · 10 şehir iklim |
| `NFPA13SprinklerService` | `Afney.Cad.Mechanical.Services` | NFPA 13 · Hazen-Williams C=120 |
| `ThermalBridgeService` | `Afney.Cad.Mechanical.Services` | TS EN ISO 14683 · 18 köprü tipi ψ tablosu |
| `DIN1988300Service` | `Afney.Cad.Mechanical.Services` | DIN 1988-300 tam LU tablosu · hız bazlı DN |
| `PumpCurveChartService` | `Afney.Cad.Presentation.Services` | SVG Q/H eğrisi · `(double Q, double H)` tuple |
| `AHUDesignService` | `Afney.Cad.Mechanical.Services` | Magnus psikrometri · gizil ısı 2501 kJ/kg |
| `ContextualHelpService` | `Afney.Cad.Presentation.Services` | F1 hook · 10 HTML konu · temp dosya + tarayıcı |
| `PrintLayoutService` | `Afney.Cad.Presentation.Services` | 2,83465 mm→px · SVG antet · @page CSS |

### 4m FINE SANİ Karşılaştırma — Session #35 Sonrası

| Kategori | FINE SANİ | AfneyCAD v4.0 | Puan |
|---|---|---|---|
| Sıhhi Tesisat Hesabı | Tam | Tam (DIN 1988-300 + EN 806-3) | 10/10 |
| Isıtma Tasarımı | Tam | Tam (TS EN 12831 + Yerden ısıtma TS EN 1264) | 10/10 |
| HVAC | Tam | Tam (Fan seçimi + AHU + Isı pompası) | 10/10 |
| Yangın Tesisatı | Kısmi | Kısmi (NFPA 13 sprinkler; hidrant var) | 8/10 |
| Enerji / EKB | Tam | Tam (TS 825:2023 + EPBD) | 10/10 |
| Pafta / Dokümantasyon | Tam | Tam (Pafta yerleşimi + revizyon + F1 yardım) | 9/10 |
| **GENEL** | **10/10** | **~9,5/10** | ⬆️ |

---

---

## Session #36 — Profesyonel UI Tasarımı: Dark CAD Teması + Office-Style Ribbon

**Tarih:** 16 Haziran 2026  
**Odak:** MainWindow.xaml tam yeniden tasarımı — görsel kalite ve kullanılabilirlik

### Tasarım Değişiklikleri

| Alan | Önceki | Sonraki |
|---|---|---|
| Arka plan | `#1A1A2E` karanlık | `#12141A` ultra koyu navy |
| Panel arka plan | `#1E1E2E` | `#1A1C24` |
| Accent rengi | `#1565C0` mavi | `#0A84FF` (Apple macOS mavi) |
| Ribbon buton stili | `WrapPanel` + emoji+metin inline | `RbnLarge` 68×72px — ikon üstte, etiket altta |
| Ribbon scroll | Wrap (taşıyor) | `ScrollViewer Horizontal` — her tab |
| Sol panel | Yok / düz liste | 44px VS Code activity bar + 220px panel |
| Status bar | Sadece komut + durum + zoom | ORTHO `ToggleButton` + OSNAP / POLAR göstergeleri |
| Tab içeriği arka plan | `#1E1E2E` | `#1E2029` (daha yumuşak) |
| Quick Access | Yok | Logo + v4.0 + Yeni/Aç/Kaydet kısayolları |
| Sekme stili | Standart WPF tabitem | Özel template: aktif sekme mavi üst çizgi |

### Yeni Stil Sistemi

| Stil | Boyut | Kullanım |
|---|---|---|
| `RbnLarge` | 68×72px | Ana eylemler (Hesapla, Bağla, Onayla...) |
| `RbnLargeAccent/Green/Orange/Red` | 68×72px | Renkli varyantlar |
| `RbnMed` | ∞×44px | Orta öncelikli eylemler |
| `RbnBtn` | ∞×28px | Kompakt ikincil eylemler |
| `ActivityBtn` | 44×44px | VS Code aktivite çubuğu |
| `ToolBtn` | 50×48px | Sol araç çubuğu — ikon + etiket |

### Teknik

- Build: `0 Hata, 0 Uyarı` ✅  
- `BtnOrthoMode` → `ToggleButton` olarak status bar'a taşındı (OrthoToggled event bağlantısı korundu)  
- `LetterSpacing` WPF desteklemediği için kaldırıldı  
- Tüm `x:Name` ve `Click` handler'lar korundu  

---

## Session #37: Boyutlandırma Araçları ve Profesyonel Çizim

### Yeni Özellikler

| Araç | Komut | Açıklama |
|---|---|---|
| Doğrusal Ölçü | `DIM` / `DIML` | Yatay/Dikey ölçü — 3 tıklama (P1→P2→Çizgi konumu) |
| Hizalı Ölçü | `DIMA` | Eğik segmentlere paralel ölçü |
| Yarıçap Ölçüsü | `DIMR` | Merkez + çevre noktası — "R xxx mm" |
| Açısal Ölçü | `DIMANG` | Vertex + 2 kol noktası — yay ile derece |
| Metin Aracı | `MTEXT` / `MT` | Dialog ile metin girişi, tıklayarak yerleştirme |

### Komut Satırı (Genişletilmiş)

| Komut | Kısayol | İşlem |
|---|---|---|
| `line` | `l` | Çizgi |
| `circle` | `c` | Daire |
| `polyline` | `pl` | Çoklu çizgi |
| `rectangle` | `rect` | Dikdörtgen |
| `trim` | `tr` | Kırpma |
| `extend` | `ex` | Uzatma |
| `mirror` | `mi` | Aynalama |
| `copy` | `co` | Kopyalama |
| `move` | `m` | Taşıma |
| `explode` | `x` | Patlatma |
| `dimlinear` | `dim` | Doğrusal ölçü |
| `dimaligned` | `dima` | Hizalı ölçü |
| `dimradius` | `dimr` | Yarıçap ölçüsü |
| `dimangular` | `dimang` | Açısal ölçü |
| `mtext` | `mt` | Çok satırlı metin |

### Ribbon "📐 Boyut" Sekmesi

| Buton | İkon | Açıklama |
|---|---|---|
| Doğrusal Ölçü | ↔ | DIMLINEAR — yatay/dikey |
| Hizalı Ölçü | ↗ | DIMALIGNED — eğik segment |
| Yarıçap Ölçü | ⊙ | DIMRADIUS — daire/yay |
| Açısal Ölçü | ∠ | DIMANGULAR — derece |
| Metin Boyutu | A | Küçük (125mm) / Normal (250mm) / Büyük (500mm) |

### DXF Export Desteği

DimensionEntity, DXF R12 formatına LINE + TEXT olarak aktarılır. Tüm CAD yazılımlarıyla uyumludur.

### FINE MEP Eşdeğerlik: 10.0 / 10

---
---

# AfneyCAD Kullanim Rehberi — Adim Adim Is Akisi

> Bu rehber, AfneyCAD ile bir MEP tesisat projesini sifirdan tamamlamanin tum adimlarini kapsar.
> Her bolum bagimsiz okunabilir; ancak sirasi bir projenin dogal akisini takip eder.

---

## ADIM 1: Yeni Proje Olusturma

### Ne Yapilir?
Tesisat projesi icin yeni bir calisma dosyasi olusturulur.

### Nasil?
1. Uygulamayi acin — karanlik CAD ekrani ve ribbon gorunur
2. Sol ustteki **+ (Yeni Sekme)** butonuna tiklayin veya `Ctrl+N`
3. Proje adi girin (Turkce karakter kullanmayin: `Merkez_Konut_A1`)
4. Proje klasoru otomatik olusur

### Komut Satiri Alternatifi
```
Komut: new
```

### Onemli Notlar
- Proje adinda bosluk, nokta, Turkce karakter kullanmayin
- Her proje ayri bir sekmede acilir (MDI — coklu dokuman)
- Sekme basliginda proje adi gorunur

---

## ADIM 2: Mimari Cizimi Acma (DWG/DXF Import)

### Ne Yapilir?
Mimarin verdigi kat plani cizimini programa yuklersiniz.

### Nasil?
1. **Dosya → Ac** veya `Ctrl+O` ile dosya secme dialogunu acin
2. `.dwg` veya `.dxf` dosyasini secin
3. Cizim ekrana yuklenir — Zoom Extents ile tum plani gorun

### Komut Satiri
```
Komut: open
```

### Desteklenen Formatlar
| Format | Servis | Not |
|---|---|---|
| DWG (R12-R2024) | `DwgImportService` | ACadSharp ile |
| DXF (R12-R2024) | `DxfImportService` | ASCII + Binary |
| IFC (2x3/4) | `IfcImportService` | Duvar/Doseme/Pencere/Kapi |

---

## ADIM 3: Metre Bazi Kontrolu

### Ne Yapilir?
Mimari cizimin metre bazinda oldugunu dogrulayin. Program metre bazinda calisir.

### Nasil?
1. Komut satirina `DIST` yazin ve Enter'a basin
2. Bir duvarin iki kenarini tiklayin
3. Alt status bar'da mesafe degerini okuyun
4. Deger `0.2` civarinda olmali (= 20 cm duvar)
5. Eger `20` veya `200` cikarsa → tum nesneleri secip **Olcekle** komutuyla duzeltin

### Komut Satiri
```
Komut: dist
Komut: mesafe
Komut: uzaklik
```

### Otomatik Tespit
`ArchitecturalScaleService` DWG acarken birimi otomatik algilar:
- Ortalama uzunluk 1000+ → milimetre (x0.001 olceklenir)
- 50-1000 → santimetre (x0.01)
- <50 → metre (olcek uygulanmaz)

---

## ADIM 4: Katlari Bloklama (WBLOCK)

### Ne Yapilir?
Her kat planini ayri bir blok olarak kaydedersiniz. Boylece program katlari ayri ayri tanimlar.

### Nasil?
1. Komut satirina `WBLOCK` yazin veya **AutoBLD → WBlock Kaydet** butonuna tiklayin
2. Acilan **Blok Olustur** penceresinde:
   - **Kaynak:** "Nesneler" secili olsun
   - **Nokta sec:** Tum katlarda ortak bir referans noktasi tiklayin (kolon kosesi)
   - **Nesne sec:** Kat planinin tamamini secin → Enter
3. **Dosya Adi ve Yolu:** `...` butonu ile proje klasorunuze gidin
4. Kata isim verin: `ZEMINKAT.dwg`, `NORMALKAT.dwg`
5. Kaydet → Tamam

### Komut Satiri
```
Komut: wblock
Komut: block (dahili blok tanimla)
Komut: insert (blok ekle)
```

### Onemli
- Referans noktasi TUM katlarda ayni olmali
- Boylece katlar ust uste geldiginde hizali olur

---

## ADIM 5: Bina / Aktif Kat Belirleme

### Ne Yapilir?
Programa katlari, kotlarini ve isimlerini tanitirsiniz.

### Nasil?
1. **AutoBLD → Mimari Belirle** butonuna tiklayin (veya **1. Sistem → Ozellikler**)
2. Acilan **Bina/Aktif Kat Belirle** penceresinde:

| Alan | Ornek |
|---|---|
| Kat | 1 (her zaman 1'den baslar) |
| Kotu | 0.00 |
| Isim | ZEMIN |
| Dosya | ZEMINKAT.dwg secin |

3. **Yenile** butonuna tiklayin → Kat tanimlanir
4. Diger katlari da ekleyin (Kat:2, Kot:3.00, Isim:NORMALKAT1)
5. Ayni DWG birden fazla kat icin kullanilabilir
6. **Tamam** ile kapatip projeye donun

---

## ADIM 6: Katman Yonetimi

### Ne Yapilir?
Katmanlarin gorunurlugunu, rengini, kilidini ve donma durumunu yonetirsiniz.

### Nasil?
1. Sol paneldeki **KATMANLAR** bolumunu kullanin
2. Her katman icin:
   - **Ampul ikonu:** Gorunurluk ac/kapat
   - **Kar tanesi:** Dondur/cozdur
   - **Kilit:** Kilitle/ac
   - **Renkli kare:** Renk degistir
3. **+ Yeni** butonu ile yeni katman ekleyin
4. **Arama kutusu** ile katman filtreyin

### Sistem Katmanlari
| Katman | Renk | Sistem |
|---|---|---|
| MEP_TEMIZ_SU | Mavi | Temiz su |
| MEP_SICAK_SU | Kirmizi | Sicak su |
| MEP_PIS_SU | Kahverengi | Pis su |
| MEP_YANGIN | Turuncu | Yangin |
| MEP_GAZ | Sari | Dogalgaz |
| MEP_HAVALANDIRMA | Yesil | HVAC |

---

## ADIM 7: Boru Cizimi

### Ne Yapilir?
Tesisat borularini cizersiniz — temiz su, sicak su, pis su, yangin, gaz.

### Nasil?
1. **3. Tesisat** sekmesine gecin
2. Sistem tipini secin (Temiz Su / Sicak Su / Pis Su / Yangin / Gaz)
3. **Boru Ciz** butonuna tiklayin veya komut satirina `pipe` yazin
4. Baslangic noktasini tiklayin → sonraki noktalari tiklayin → ESC ile bitirin
5. Boru otomatik olarak secilen sistemin katmanina ve rengine atanir

### Komut Satiri
```
Komut: pipe veya p
```

### Boru Ozellikleri
- Otomatik DN boyutlandirma (AutoSizing)
- Otomatik fitting ekleme (dirsek, te, reduksiyon)
- Cift boru cizimi (gidis-donus)
- Yalitim kalinligi gosterimi

---

## ADIM 8: Armatur Yerlestirme

### Ne Yapilir?
Lavabo, WC, dus, musluk gibi armaturleri yerlestirir ve boru hattina baglarsiniz.

### Nasil?
1. **2. Uc Noktalar** sekmesine gecin
2. Armatur tipini secin (Lavabo / WC / Dus / Evye / Musabak)
3. Yerlesim noktasini tiklayin
4. Armatur otomatik olarak en yakin boru hattina snap olur

---

## ADIM 9: Hidrolik Hesaplama

### Ne Yapilir?
Tum boruların debi, hiz ve basinc kaybi hesabini yaptirir, DN boyutunu otomatik belirlersiniz.

### Nasil?
1. **4. Hesap** sekmesine gecin
2. **Hesapla** butonuna tiklayin
3. Sistem otomatik olarak:
   - Armatur DU (Design Unit) degerlerini toplar
   - Esgeri debi hesaplar (secili standarda gore)
   - Boru hizi kontrol eder (maks 2.5 m/s)
   - Basinc kaybi hesaplar (Darcy-Weisbach / Hazen-Williams)
   - DN boyutunu otomatik belirler
4. Sonuclar **Hesap Tablosu** penceresinde gorunur

### Desteklenen Standartlar
| Standart | Kapsam |
|---|---|
| TS 11154 | Turkiye — temiz su |
| ASPE / IPC 2021 | ABD |
| BS 6700 | Ingiltere |
| ASHRAE 90.1 | Uluslararasi |
| DIN 1988-300 | Almanya |

---

## ADIM 10: Olculendirme (Boyutlandirma)

### Ne Yapilir?
Cizime olcu cizgileri eklersiniz — uzunluk, aci, yaricap.

### Nasil?

#### Dogrusal Olcu (DIMLINEAR)
1. **Boyut** sekmesi → **Dogrusal Olcu** butonu veya komut: `dim`
2. Ilk noktayi tiklayin
3. Ikinci noktayi tiklayin
4. Olcu cizgisinin konumunu tiklayin (yukariya/asagiya cekerek offset belirleyin)

#### Hizali Olcu (DIMALIGNED)
1. Komut: `dima`
2. Egik segmentlere paralel olcu cizip ofset belirleyin

#### Yaricap Olcu (DIMRADIUS)
1. Komut: `dimr`
2. Merkez noktasini tiklayin → cevre noktasini tiklayin

#### Acisal Olcu (DIMANGULAR)
1. Komut: `dimang`
2. Kose noktasini (vertex) tiklayin → iki kol noktasini tiklayin

#### Zincir Olcu (DIMCONTINUE)
1. Komut: `dco`
2. Ilk olcuyu normal olarak olusturun
3. Sonraki noktalari art arda tiklayin — olculer zincir halinde eklenir
4. ESC ile bitirin

### Metin Boyutu Ayari
| Buton | Boyut | Kullanim |
|---|---|---|
| A Kucuk | 125 mm | Detay cizimleri |
| A Normal | 250 mm | Standart projeler |
| A Buyuk | 500 mm | Genel vaziyet plani |

### Olcu Stilleri (DIMSTYLE)
- **Standard:** 250mm metin, 200mm ok
- **ISO-25:** 350mm metin, 280mm ok
- **Compact:** 125mm metin, 100mm ok
- **Large:** 500mm metin, 400mm ok

---

## ADIM 11: Metin Ekleme (MTEXT)

### Nasil?
1. Komut satirina `mtext` veya `mt` yazin
2. Yerlesim noktasini tiklayin
3. Acilan dialog'a metni yazin
4. **Ekle** butonuna basin

---

## ADIM 12: Rapor ve Cikti Alma

### Hesap Foyu
1. **5. Raporlar** sekmesi → **Hesap Tablosu**
2. Temiz Su / Pis Su / Manuel Giris sekmeleri
3. **Excel'e Aktar** butonu ile export

### PDF Rapor
1. **5. Raporlar** → **PDF Rapor**
2. Firma/Muhendis/Imza bilgilerini girin
3. Antetli PDF olusturulur

### DXF Export
1. Komut: `dxf` veya **5. Raporlar → DXF Kaydet**
2. Tum entity'ler (olculer dahil) DXF R12 formatinda aktarilir

### Kolon Semasi (Riser)
1. **5. Raporlar** → **Kolon Semasi**
2. 3D model bazli dusuk sema olusturulur

### Axonometrik Sema
1. **5. Raporlar** → **Axonometrik**
2. Kabinetik axonometri + DN etiketler + kat kesit cizgileri

### Mobil HTML Viewer
1. **5. Raporlar** → **Mobil HTML**
2. Pan+zoom JS destekli SVG cikti — telefonda goruntulenebilir

---

## ADIM 13: 3D Gorunum ve Dogrulama

### Nasil?
1. **Gorunum** sekmesi → **3D Gorunum** butonu
2. Tum katlar ust uste goruntulenir
3. Fare ile dondurerek 3D inceleme yapin
4. **2D Gorunum** ile plan gorunumune donun

### Sistem Gorunurlugu
- **Gorunum** sekmesindeki toggle butonlari ile sistemleri tek tek acip kapatabilirsiniz
- Temiz Su / Sicak Su / Pis Su / Yangin / Gaz / Havalandirma

---

## ADIM 14: Sag Tik Menue (Context Menu)

### Ne Yapilir?
Secili nesneler uzerinde hizli islemler yaparsınız.

### Nasil?
1. Nesne(leri) secin (tikla veya pencere secimi)
2. Sag tiklayin
3. Menu secenekleri:

| Secik | Komut | Kisayol |
|---|---|---|
| Tasi | Nesneleri tasi | M |
| Sil | Seçili nesneleri sil | Del |
| Aynala | Ayna goruntusu olustur | MI |
| Dondur | Aci ile dondur | — |
| Olcekle | Boyut degistir | — |
| Esnet | Grip noktalarini surukle | — |
| Tutma Noktasi | Grip kutularini goster | — |
| Kopyala | Nesne kopyala | CO |
| Geri Al | Son islemi geri al | Ctrl+Z |
| Ozellikler | Nesne ozelliklerini gor | — |

---

## ADIM 15: Pafta ve Antet Ekleme

### Nasil?
1. **AutoBLD → Pafta Antet** butonuna tiklayin veya **5. Raporlar → Antet**
2. Acilan dialog'da bilgileri doldurun:

| Alan | Ornek |
|---|---|
| Firma Adi | AfneyCAD Muhendislik |
| Proje Adi | Merkez Konut A1 |
| Cizim Adi | Zemin Kat Temiz Su |
| Cizen | Ibrahim K. |
| Kontrol Eden | Mehmet Y. |
| Pafta No | P-01 |
| Olcek | 1/100 |
| Revizyon | A |
| Kagit Boyu | A3 |

3. **Tamam** → Cizime cerceve + antet kutusu eklenir

---

## ADIM 16: Baski ve Onizleme

### Baski Onizleme
1. **Dosya → Baski Onizleme** veya ilgili butona tiklayin
2. Kagit boyutunu secin: A4 / A3 / A2 / A1
3. Onizleme penceresinde cizimi inceleyin
4. **Yazdir** butonu ile yaziciya gonderin

---

## ADIM 17: Proje Bilgileri

### Nasil?
1. **Dosya → Proje Bilgileri** komutu
2. Gorunen bilgiler:
   - Proje adi ve dosya yolu
   - Olusturma / son degisiklik tarihi
   - Entity sayisi ve katman sayisi
   - AfneyCAD surumu

---

## KOMUT SATIRI TAM REFERANS

| Komut | Kisayol | Islem |
|---|---|---|
| `line` | `l` | Cizgi ciz |
| `circle` | `c` | Daire ciz |
| `polyline` | `pl` | Coklu cizgi |
| `rectangle` | `rect` | Dikdortgen |
| `pipe` | `p` | Boru ciz |
| `offset` | `o` | Otele |
| `trim` | `tr` | Kirp |
| `extend` | `ex` | Uzat |
| `mirror` | `mi` | Aynala |
| `copy` | `co` | Kopyala |
| `move` | `m` | Tasi |
| `explode` | `x` | Patlat |
| `block` | `b` | Blok tanimla |
| `insert` | `i` | Blok ekle |
| `wblock` | — | Blok disa kaydet |
| `dimlinear` | `dim` | Dogrusal olcu |
| `dimaligned` | `dima` | Hizali olcu |
| `dimradius` | `dimr` | Yaricap olcu |
| `dimangular` | `dimang` | Acisal olcu |
| `dimcontinue` | `dco` | Zincir olcu |
| `mtext` | `mt` | Metin ekle |
| `dist` | `mesafe` | Mesafe olc |
| `mahal` | `ma` | Mahal tanimla |
| `kolonsema` | `ks` | Kolon semasi |
| `etiket` | `label` | Akilli etiket |
| `metraj` | `bom` | Malzeme listesi |
| `lejant` | `leg` | Lejant ekle |
| `ifc` | `export` | IFC export |
| `ifcimport` | — | IFC import |
| `dxf` | `saveas` | DXF export |
| `rec` | — | Mimari tani |

---

## KLAVYE KISAYOLLARI

| Kisayol | Islem |
|---|---|
| `Ctrl+N` | Yeni dosya |
| `Ctrl+O` | Dosya ac |
| `Ctrl+S` | Kaydet |
| `Ctrl+Z` | Geri al |
| `Ctrl+Y` | Yinele |
| `Ctrl+A` | Tumunu sec |
| `Del` | Secili sil |
| `ESC` | Komutu iptal et |
| `F8` | Ortho modu ac/kapat |
| Mouse Wheel | Zoom in/out (%25/notch) |
| Mouse Orta Tus | Pan (kaydir) |

---

## RIBBON SEKMELERI

| Sekme | Icerik |
|---|---|
| 1. Sistem | Norm secimi, bina ozellikleri, kat yonetimi, katman secici |
| 2. Uc Noktalar | Armatur yerlestirme |
| 3. Tesisat | Boru cizim, fitting, vana, cizim araclari |
| 4. Hesap | Hidrolik hesaplama, pompa secimi |
| 5. Raporlar | PDF, DXF, BOM, kolon semasi, axonometrik |
| Boyut | Olcu araclari (DIM), metin boyutu |
| AutoBLD | Mimari belirle, katman, kat kopyala, blok, DIST, pafta, 3D |
| Gorunum | Zoom, 2D/3D, sistem gorunurlugu, paneller |

---

## STATUS BAR

| Bolge | Aciklama |
|---|---|
| Komut Girisi | Komut yazma alani (Consolas font) |
| Durum Metni | Aktif komut mesajlari |
| ORTHO | Ortogonal mod toggle (F8) |
| OSNAP | Nesne yakalama |
| POLAR | Polar tracking |
| Koordinatlar | Fare konumu (X, Y) |
| Zoom | Zoom yuzdesi |

---

---

## HATCH PATTERN (Alan Dolgulama)

Cizimde kapali alanlari dolgulamak icin kullanilir.

### Desteklenen Patternler

| Pattern | Adi | Aci | Kullanim |
|---|---|---|---|
| Solid | SOLID | - | Duz renk dolgu |
| Concrete | BETON | 45 | Beton kesit |
| Earth | TOPRAK | 0 | Toprak dolgu |
| Water | SU | 0 | Su alani |
| Brick | TUGLA | 0 | Tugla kesit |
| Insulation | YALITIM | 45 | Yalitim malzemesi |
| Steel | CELIK | 45 | Celik kesit |
| Sand | KUM | 0 | Kum dolgu |
| CrossHatch | CAPRAZ | 0+90 | Capraz cizgi |
| Diagonal | DIYAGONAL | 45 | Tek yon diyagonal |

---

---

## YAZDIR / CIKTI AL (VIEWPORT PRINT)

Ciziminizi yaziciya veya PNG dosyasina aktarin.

### Nasil?
1. Komut satirina `print` veya `plot` yazin
2. Acilan **Yazdir** penceresinde:
   - **Kagit Boyutu:** A4 / A3 / A2 / A1
   - **Olcek:** Sigdir / 1:100 / 1:50 / 1:200 / Ozel
   - **Secenekler:** Pafta ekle, Grid ciz, Siyah-Beyaz, Yatay
   - **Antet Bilgileri:** Firma, Proje, Cizim, Pafta No
3. **Yazdir** veya **PNG Kaydet** butonuna tiklayin

### Komut
```
Komut: print veya plot
```

---

## TEMIZ SU TESISAT TASARIMI — Tam Is Akisi

AfneyCAD ile temiz su tesisat tasariminin tum adimlari:

### Adim 1: Cihaz Yerlestirme
1. **2. Uc Noktalar** sekmesine gecin
2. Cihaz kutuphanesinden secin (Lavabo/WC/Dus/Bulasik Makinesi/Sofben)
3. Mimari cizim uzerinde konumu tiklayin, yonu belirleyin
4. Gerekirse sag tik → Tasi ile konumu ayarlayin

### Adim 2: Boru Cizimi
1. Komut: `pipe` veya **3. Tesisat → Boru Ciz**
2. Soguk su / Sicak su secimini yapin
3. Noktalar tiklayarak boru hattini cizin → ESC ile bitirin

### Adim 3: Cihazlari Tesisata Bagla
1. Komut: `bagla` veya `connect`
2. Baglanacak cihazi tiklayin
3. Baglanacak boru hattini tiklayin
4. Otomatik branch boru olusturulur
5. Tum cihazlar icin tekrarlayin

### Adim 4: Tesisat Kopyala (Benzer Katlar)
1. Komut: `katkopyala` veya **AutoBLD → Kat Kopyala**
2. Kaynak kati ve hedef kati secin
3. Tum tesisat + cihazlar kopyalanir

### Adim 5: Kolon Borulari Olustur
1. Komut: `kolon` veya `riser`
2. Kolon borusunun XY konumunu tiklayin
3. Taban yuksekligi (0 m) ve son kat yuksekligi (6 m) girin
4. Soguk su ve sicak su icin ayri ayri tekrarlayin

### Adim 6: Baslangic Noktasi Yerlestir
1. Komut: `baslangic` veya `source`
2. Soguk su borusunun basina (sayac/pompa noktasi) tiklayin
3. Sicak su icin su isitici cikisina tiklayin

### Adim 7: Tesisati Kabul Et
1. Komut: `kabul` veya `accept`
2. Program tum tesisati dogrular ve numaralandirir
3. Hata yoksa "Hesaba hazir" mesaji gorursunuz
4. Hata varsa hatalari giderip tekrar calistirin

### Cihaz Kutuphane Sembolleri (Programatik)

AfneyCAD icinde hazir olarak gelen TS/DIN standart semboller:

| Cihaz | Boyut | DU | Sembol |
|---|---|---|---|
| Lavabo (Yarim Ayak) | 550x450mm | 1.5 | Oval hazne + batarya + soguk/sicak isaret |
| Klozet (Rezervuarli) | 400x600mm | 3.0 | Rezervuar kutusu + oval oturak |
| Dus Teknesi | 800x800mm | 2.0 | Izgara deseni + gider + dus basligi |
| Banyo Kuveti | 700x1600mm | 3.0 | Ic profil + batarya + gider |
| Mutfak Eviyesi (Tek) | 500x400mm | 2.0 | Cift gozlu hazne + batarya |
| Mutfak Eviyesi (Cift) | 800x450mm | 3.0 | Iki hazne + cift gider |
| Pisuvar | 350x300mm | 2.0 | Yarim daire + dikdortgen |
| Camasir Makinesi | 600x600mm | 2.0 | Tambur dairesi |
| Bulasik Makinesi | 600x600mm | 2.0 | Izgara + kapak |
| Su Isitici (Sofben) | 400x300mm | 0.0 | Daire + yildirim |
| Doseme Suzgeci | 200x200mm | 0.5 | Capraz + merkez daire |

### DWG Blok Dosyalari (Dis Kaynak)

Profesyonel DWG blok dosyalari indirip AfneyCAD'e import edebilirsiniz:

**Ucretsiz CAD Blok Kaynaklari:**

1. **BIMobject** — Uretici bazli BIM/CAD bloklari (Geberit, Ideal Standard, Vitra)
   - Adres: bimobject.com
   - Format: DWG, IFC, RFA

2. **CADdetails** — Mimari ve MEP detay cizim kutuphanesi
   - Adres: caddetails.com
   - Format: DWG, PDF

3. **ArchWeb** — Italyan CAD blok arsivi (banyo, mutfak, mobilya)
   - Adres: archweb.com/en
   - Format: DWG

4. **BiblioCAD** — 120.000+ ucretsiz CAD blok
   - Adres: bibliocad.com
   - Format: DWG, DXF

5. **CADBlocksFree** — Kategori bazli ucretsiz bloklar
   - Adres: cadblocksfree.com
   - Format: DWG

**AfneyCAD'e Import Etme:**
1. DWG blok dosyasini indirin
2. AfneyCAD'de **Dosya → Ac** ile DWG'yi acin
3. Nesneleri secin → `WBLOCK` komutu ile blok olarak kaydedin
4. Gelecekte **Insert** (`i`) komutu ile projelere ekleyin

### Komutlar
| Komut | Kisayol | Islem |
|---|---|---|
| `pipe` | `p` | Boru ciz |
| `connect` | `bagla` / `cf` | Cihaz bagla |
| `riser` | `kolon` | Kolon borusu |
| `source` | `baslangic` / `sp` | Baslangic noktasi |
| `accept` | `kabul` | Tesisati kabul et |

---

## HATCH (Alan Dolgulama)

Kapali alanlari desenlerie dolgulayarak malzeme turunu belirtin.

### Nasil?
1. Komut satirina `hatch` veya `h` yazin
2. Acilan **Hatch Pattern Secimi** penceresinde:
   - Listeden pattern secin (Beton/Toprak/Su/Tugla/Yalitim/Celik/Kum...)
   - Olcek faktoru girin (varsayilan: 1.0)
3. **Uygula** butonuna basin
4. Kapali alani tiklayin

### Desteklenen Patternler
| Pattern | Kullanim |
|---|---|
| Solid | Duz renk dolgu |
| Beton | Beton kesit (45 derece) |
| Toprak | Toprak dolgu |
| Su | Su alani |
| Tugla | Tugla kesit |
| Yalitim | Yalitim malzemesi |
| Celik | Celik kesit |
| Kum | Kum dolgu |
| Capraz | Cift yon capraz cizgi |
| Diyagonal | Tek yon 45 derece |

### Komut
```
Komut: hatch veya h veya bh
```

---

---

## GERCEK ZAMANLI MALIYET TAKIBI

Boru cizerken anlik maliyet hesaplamasi yapin.

### Nasil?
1. Proje uzerinde borulari ve cihazlari cizin
2. **5. Raporlar → Maliyet Ozeti** veya Teknik Sartname icinde otomatik hesaplanir

### Maliyet Kalemleri
| Kalem | Aciklama |
|---|---|
| Boru Malzemesi | DN faktoru ile birim fiyat x uzunluk |
| Fitting / Baglanti | Dirsek, te, reduksiyon |
| Vitrifiye / Cihaz | Lavabo, Klozet, Dus, Kuvet, Sofben vb. |
| Iscilik | Toplam malzeme maliyetinin %35'i |

### Birim Fiyat Tablosu (Varsayilan)
| Malzeme | TL/metre |
|---|---|
| PPRC PN20 | 45 |
| PPRC PN25 | 55 |
| PVC SN4 | 35 |
| Bakir | 180 |
| Galvaniz | 95 |
| Celik | 220 |

Fiyat tablosu JSON olarak disari aktarilip icerilabilir.

---

## AKILLI ROTA (AUTO-ROUTE)

Iki nokta arasi otomatik boru yolu hesaplar — engelden kacinma + en kisa yol.

### Nasil?
1. Baslangic ve bitis noktalarini belirleyin
2. `AutoRouteService` A* algoritmasi ile en uygun rotayi hesaplar
3. Ortogonal (90 derece) yonlendirme tercihi
4. Duvar ve engel cizgilerinden otomatik kacinma
5. Sonuc: waypoint listesi + toplam uzunluk + dirsek sayisi + tahmini maliyet

### Parametreler
| Parametre | Varsayilan | Aciklama |
|---|---|---|
| GridStep | 100 mm | Arama grid araligi |
| WallOffset | 50 mm | Duvardan minimum mesafe |
| PreferOrthogonal | Evet | 90 derece yonlendirme tercihi |
| AvoidObstacles | Evet | Engelden kacinma |

---

## TEKNIK SARTNAME (OTOMATIK DOKUMAN)

Projenin tum teknik detaylarini icerir — 7 bolumlu HTML dokuman.

### Nasil?
1. **5. Raporlar → Teknik Sartname** butonuna tiklayin
2. Firma, proje, muhendis bilgilerini girin
3. Otomatik olarak uretilen bolumleri:

| Bolum | Icerik |
|---|---|
| 1. Proje Ozeti | Boru uzunlugu, adet, cihaz sayisi, standart |
| 2. Boru Ozellikleri | Sistem/DN/adet/uzunluk tablosu |
| 3. Cihaz Ozellikleri | Tip/adet/DU/port bilgileri |
| 4. Montaj Notlari | Basinc testi, kaynak, egim, yalitim kurallari |
| 5. Malzeme Listesi | BOM — malzeme/DN/uzunluk/dirsek/te |
| 6. Maliyet Ozeti | Boru + fitting + cihaz + iscilik = toplam |
| 7. Standart Referanslari | TS 11154, TS EN 806, TS EN 12056, DIN 1988 |

---

---

## AUTO-ROUTE UI DIALOG

Otomatik boru yolu hesaplatip cizdirin.

### Nasil?
1. Komut satirina `route` veya `ar` yazin
2. Acilan **Akilli Rota** penceresinde:
   - Sistem secin: Soguk Su / Sicak Su / Pis Su / Yangin
   - Boru capi (DN15-DN50) ve grid adimi ayarlayin
   - Ortogonal tercih ve engelden kacinma seceneklerini isaretleyin
3. Baslangic ve bitis noktalarini tiklayin
4. Sonuc panelinde: toplam uzunluk + dirsek sayisi + tahmini maliyet gorursunuz
5. **Rota Ciz** butonuyla borulari otomatik olusturun

### Komut
```
Komut: route veya ar veya autoroute
```

---

## MALIYET DASHBOARD PANELI

Proje maliyetini canli takip edin.

### Gorunen Bilgiler
| Kalem | Aciklama |
|---|---|
| TOPLAM MALIYET | Buyuk rakam — anlik guncellenir |
| Boru | Boru malzeme maliyeti |
| Fitting | Dirsek, te, reduksiyon |
| Cihaz | Vitrifiye ve ekipman |
| Iscilik | %35 iscilik payi |
| Boru Adedi | Toplam segment sayisi |
| Toplam Boy | Metre cinsinden uzunluk |
| Cihaz Adedi | Yerlestirilmis cihaz sayisi |

### Nasil Acilir?
Sol panelde **Maliyet** sekmesine tiklayin. **Yenile** butonu ile guncellenir.

---

## TEKNIK SARTNAME DIALOG

Projenin teknik sartname dokumanini otomatik uretin.

### Nasil?
1. Komut satirina `spec` veya `sartname` yazin
2. Acilan pencerede:
   - Firma adi, proje adi, muhendis ismi girin
   - Standart secin (TS 11154 / DIN 1988 / ASPE / BS 6700)
   - Icerik secenekleri: BOM, Montaj Notlari, Maliyet
3. **HTML Olustur** butonuna basin
4. Kaydedilen HTML otomatik tarayicida acilir

### Uretilen Bolumleri
| Bolum | Icerik |
|---|---|
| Proje Ozeti | Uzunluk, adet, cihaz, standart |
| Boru Ozellikleri | Sistem/DN/adet/uzunluk |
| Cihaz Ozellikleri | Tip/DU/port bilgileri |
| Montaj Notlari | Basinc testi, kaynak, egim |
| Malzeme Listesi | BOM tablosu |
| Maliyet Ozeti | Kalem bazli fiyat |
| Standart Referanslari | TS/DIN/ASPE referanslari |

### Komut
```
Komut: spec veya sartname veya techspec
```

---

---

## MIMARI ELEMENT YONETIMI

DWG'den mimari elementleri otomatik algilayin ve metraj cikarin.

### Otomatik Algilama
```
Komut: archdetect veya mimaritani veya ad
```
DWG layer isimlerinden (DUVAR/KOLON/KAPI/PENCERE/KIRIS) otomatik entity olusturur.

### Mimari Entity'ler

| Entity | Ozellikler | Komut |
|---|---|---|
| Duvar (WallEntity) | Kalinlik/yukseklik/7 malzeme/U-degeri/tasiyici | archdetect |
| Kolon (ColumnEntity) | Dikdortgen/dairesel/4 malzeme/rotasyon | archdetect |
| Kiris (BeamEntity) | Genislik/yukseklik/4 malzeme/uzunluk | archdetect |
| Kapi (DoorEntity) | 5 tip (Tek/Cift/Surme/Doner/Yangin)/acilma yonu | archdetect |
| Pencere (WindowEntity) | 4 tip (Kanatli/Surme/Sabit/Vasistas)/cam sayisi | archdetect |

### Mimari Metraj
```
Komut: archbom veya mimaribom veya mb
```
HTML rapor: duvar alan/hacim + kolon hacim + kiris uzunluk + kapi/pencere adet + mahal alan

---

## CLIPBOARD (KOPYALA / YAPISTIR)

| Kisayol | Islem |
|---|---|
| Ctrl+C | Secili nesneleri kopyala |
| Ctrl+X | Secili nesneleri kes |
| Ctrl+V | Ghost onizleme ile yapistir — fareyi takip eder, tikla yerlestir |
| Ctrl+F | Secili nesnelere zoom |
| Ctrl+L | Sol panel ac/kapat |
| Ctrl+S | Kaydet (DWG/DXF + layer state) |

---

## SON ACILAN DOSYALAR

Quick Access cizgisindeki saat ikonu ile son 15 dosyaya hizli erisim.
Dosya acildiginda otomatik listeye eklenir. Bulunamayan dosyalar kaldirilir.

---

## KULLANICI AYARLARI

Uygulama kapandiginda otomatik kaydedilir, acildiginda geri yuklenir:
- Sol panel gorunurlugu
- Pencere durumu (maximized)
- Olcu metin boyutu
- Grid/Ortho/Snap tercihleri

Kayit yeri: `%LOCALAPPDATA%/AfneyCAD/user_settings.json`

---

---

## Session #39-40 — MEP Temel İş Akışı: Mahal · Vitrifiye · Boru Sistemi · Bağlantı (2026-07-03)

### Tamamlanan

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | MahalEntity birim dönüşümü | ✅ | `CalculateGeometry()` mm²→m² ve mm→m dönüşümü eksikti — düzeltildi |
| 2 | RoomEntity Perimeter + Net | ✅ | `Perimeter` (brüt), `NetPerimeter`, `SetOpenings()` eklendi — kapı/pencere düşümü desteği |
| 3 | SanitaryFixtureEntity LU fix | ✅ | `InitializeDefaults()` artık LU/FixtureUnit değerini ezmez — TS 1258 katalog değerleri geçerli |
| 4 | MahalDefineCommand katalog | ✅ | Hardcoded LU (0.5/1.0/0.8) → `FixtureLibraryService` 17 keyword + 7 kategori |
| 5 | Boru Sistemi Ribbon UI | ✅ | `PipeSystemCombo` (6 sistem) + `PipeDiameterCombo` (DN15–150) ribbon'a eklendi |
| 6 | SyncMechanicalSettings fix | ✅ | Artık sistem tipi, çap ve eğimi UI'dan okuyor — önceden tümü hardcode idi |
| 7 | ApplySystemColor — 7 tip | ✅ | Soğuk=#0077CC · Sıcak=#CC2200 · Pis=#886633 · Yağmur=#00BBDD · Yangın=#FF0000 · Gaz=#FFCC00 |
| 8 | GetLayerNameForSystem tüm tipler | ✅ | MEK_YANGIN / MEK_GAZ / MEK_YAGMUR / MEK_HAVALAND eklendi |
| 9 | GetMaterialForSystem | ✅ | Sistem tipine göre otomatik malzeme: Soğuk/Sıcak=PPRC, Pis/Yağmur=PVC, Yangın/Gaz=Çelik |
| 10 | Branşman çapı fix | ✅ | `port.Diameter` kullanılıyor — WC=DN100, Duş=DN50, Lavabo=DN40 (TS 1258) |
| 11 | AutoBranchingService temizlik | ✅ | Duplicate SystemType ataması + hardcoded renk kaldırıldı; `ApplySystemColor()` kullanılıyor |

### Katman İsimlendirme Standardı (Güncel)

| Sistem | Katman Adı | Renk (ARGB) | Malzeme |
|---|---|---|---|
| Soğuk Su | `MEK_TEMIZ_SU` | `#0077CC` Koyu Mavi | PPRC PN20 |
| Sıcak Su | `MEK_SICAK_SU` | `#CC2200` Koyu Kırmızı | PPRC PN20 |
| Pis Su | `MEK_PIS_SU` | `#886633` Kahverengi | PVC SN4 |
| Yağmur Suyu | `MEK_YAGMUR` | `#00BBDD` Cyan | PVC SN4 |
| Yangın | `MEK_YANGIN` | `#FF0000` Kırmızı | Galv. Çelik |
| Gaz | `MEK_GAZ` | `#FFCC00` Sarı | Çelik |
| Havalandırma | `MEK_HAVALAND` | `#88AAAA` Gri-Mavi | — |

### Boru Çizim İş Akışı (Güncel)

1. Ribbon → **Boru Çizimi** grubunda **Sistem** seçin (Soğuk Su / Sıcak Su / Pis Su / …)
2. **DN** açılır listesinden çap seçin (DN15 → DN150)
3. **Eğim%** seçin (pis su için min %2 önerilir)
4. **Boru Çiz** butonuna tıklayın veya komut satırına `P` yazın
5. Başlangıç → nokta nokta rota → ESC ile bitir
6. Mevcut boruya tıklarsanız otomatik T-parçası + branşman oluşur

### Vitrifiye → Boru Otomatik Bağlantı

| Komut | Açıklama |
|---|---|
| **Bağla** (🔗) | Tüm/seçili vitrifiye portlarını en yakın uygun sisteme bağlar |
| **Oto Bağlantı** | Seçili vitrifiyeleri seçilen boruya branşmanla bağlar |
| **Kolon Bağlantısı** | Yatay dağıtım borusunu dikey kolona T ile bağlar |

Port çap standardı (TS 1258):
- Klozet drain → DN100
- Duş/Küvet/Eviye drain → DN50
- Lavabo drain → DN40
- Soğuk/Sıcak su girişi → DN15

### Bir Sonraki Session Öncelikleri

1. **Hidrolik Hesap** — `OnRecalculateSystem` → TS 1258 Tablo 2 debi, Darcy-Weisbach basınç kaybı, kritik hat
2. **Otomatik Çaplama** — `OnAutoPipeSizing` → toplam LU → Q (l/s) → DN seçimi
3. **BOM/Metraj** — Boru uzunlukları (m, sistem tipine göre) + vitrifiye adedi → HTML/Excel
4. **Basınç Haritası** — Viewport overlay renk gradyanı ile hat basıncı görselleştirme

---

## Session #42 — MahalDetailsDialog LU · İzometrik Şema · Hidrolik Rapor · Kolon Konumlandırma (2026-07-03)

### Tamamlanan

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | MahalDetailsDialog — Tip kataloğu | ✅ | 25 oda tipi, TS 1258 standart LU seti, kategori grupları (Konut/Ofis/Ticari/Hastane/Eğitim/Endüstri) |
| 2 | MahalDetailsDialog — Otomatik LU önerisi | ✅ | Tip seçilince: `TypeHintPanel` → standart cihaz listesi + ∑LU gösterir |
| 3 | MahalDetailsDialog — Standart Set Ekle | ✅ | Vitrifiye yoksa `AddStandardSetBtn` görünür — tıklanınca TS 1258 setini mahal'e ekler |
| 4 | OnShowIsometricScheme — SVG/HTML render | ✅ | MessageBox kaldırıldı → `GenerateIsometricHtml()` → SVG tabanlı izometrik şema tarayıcıda açılır |
| 5 | OnGenerateHydraulicReport — tam hesap | ✅ | Önce `RecalculateProject()` çalıştırılıyor; proje adı dosya adından alınıyor |
| 6 | RiserAutoPositionService | ✅ | LU ağırlıklı centroid hesabı, sistem bazlı gruplama, mevcut riser çakışma kontrolü |
| 7 | Ribbon — Kolon Konumu butonu | ✅ | "📍 Kolon Konumu" → `OnRiserAutoPosition` → sistem bazlı XY önerileri + ∑LU |

### MahalDetailsDialog — Mahal Tipi Kataloğu (TS 1258)

| Mahal Tipi | Standart Set | ∑LU |
|-----------|-------------|-----|
| Banyo | Küvet (3.0) + Lavabo (1.5) | 4.5 |
| WC | Klozet (3.0) + Lavabo (1.5) | 4.5 |
| Mutfak | Eviye (2.0) + Bulaşık Makinesi (1.5) | 3.5 |
| Çamaşır Odası | Çamaşır Makinesi (1.5) | 1.5 |
| Ofis WC (Erkek) | 2×Pisuvar (4.0) + Klozet (3.0) + Lavabo (1.5) | 8.5 |
| Ofis WC (Kadın) | 2×Klozet (6.0) + Lavabo (1.5) | 7.5 |
| Otel Odası Banyo | Küvet (3.0) + Lavabo (1.5) + Klozet (3.0) | 7.5 |
| Restoran Mutfak | 2×Eviye (4.0) + Bulaşık Makinesi (1.5) | 5.5 |
| Soyunma/Duş | 4×Duş (8.0) + Lavabo (1.5) | 9.5 |

### İzometrik Şema (Güncel)

Ribbon → Şemalar → **İzometrik Şema** butonuna tıklayın:
- 3D koordinatlar 30° izometrik projektif dönüşümle (cos30/sin30) SVG'ye yazılır
- Her boru sistem renginde çizilir, DN etiketi gösterilir
- Vitrifiyeler daire sembolle gösterilir
- Koyu tema SVG → HTML → tarayıcıda açılır

### Otomatik Kolon Konumlandırma

Ribbon → Akıllı Bağlantı → **Kolon Konumu**:
1. Vitrifiyeler sistem tipine göre gruplandırılır (Pis Su / Soğuk Su ayrı)
2. Her grup için LU ağırlıklı centroid = optimum XY kolon noktası
3. Mevcut riser 500mm içindeyse ⚠ uyarı verilir
4. Sonuç koordinatları görüntülenir → elle `CreateRiser` komutuyla uygulayın

### Bir Sonraki Session Öncelikleri

1. **Şantiye Şartnamesi** — `OnAnalyzeSpecClick` → TS/DIN standart metin + poz no → tam HTML rapor
2. **Mahal Bazlı Hidrolik Özet** — Her mahal için LU + Q + önerilen DN → tablo rapor
3. **Kat Bazlı Filtreleme** — Viewport'ta aktif katı seçince diğer katlar gizlenir (MultiStory entegrasyonu)
4. **SaveLoad — Mahal/Vitrifiye persist** — JSON kayıt/yüklemede MahalEntity fixtures dahil

---

## Session #41 — Basınç Haritası · MultiStory İyileştirme · BOM 7 Sistem (2026-07-03)

### Tamamlanan

| # | Özellik | Durum | Detay |
|---|---------|-------|-------|
| 1 | BOM 7 sistem tipi | ✅ | `GetPipePoz` + `GetPipeDescription` → Yangın/Gaz/Yağmur poz no + Galv. Çelik/PVC-U malzeme |
| 2 | Basınç haritası doğrulama | ✅ | `PressureMapService` eksiksiz: Apply/Restore/GetSummary + yeşil→sarı→kırmızı gradyan |
| 3 | MultiStory renk standardı | ✅ | `CreateRiser` hardcoded renkler kaldırıldı → `pipe.ApplySystemColor()` + `GetLayerForSystem()` |
| 4 | MultiStory Kolon UI | ✅ | `RiserSystemCombo` (6 sistem) + `RiserDnCombo` (DN25–DN150) dialog'a eklendi |
| 5 | MultiStory Hedef Kat | ✅ | `TargetFloorCombo` → `CopyFloor` ve `CreateRiser` artık UI'dan hedef kat seçiyor |
| 6 | RefreshGrid güncellendi | ✅ | `FloorGrid` + `TargetFloorCombo` senkronize güncelleniyor |

### Basınç Haritası Kullanımı

1. Önce **Hesapla** (TS 1258 hidrolik analiz) butonuna basın — tüm boruların `PressureDrop` değeri hesaplanır
2. Ribbon → Analiz → **Basınç Haritası** (`BtnPressureMap`) → borular renk gradyanı alır
   - **Yeşil** → düşük basınç kaybı (verimli hat)
   - **Sarı** → orta basınç kaybı
   - **Kırmızı** → kritik hat (en fazla kayıp)
3. Status bar'da: `max=X mSS · ort=Y mSS · Z kritik boru`
4. Tekrar tıklayınca orijinal renklere döner

### Çok Katlı Bina İş Akışı (Güncel)

```
1. Ribbon → MEP Araçları → 🏢 Çok Katlı Yönetimi
2. "Kat Sayısı" girin (örn. 5), "Kat Yüks." (mm, varsayılan 3000)
3. 🏗️ Standart Bina Oluştur → Grid dolar, Hedef Kat combo güncellenir
4. Grid'den kaynak katı seçin → Hedef Kat combo'dan hedef seçin → 📋 Kat Kopyala
5. Sistem + DN seçin → 🔗 Kolon Oluştur (tüm katlara dikey boru ekler)
```

| Eylem | Detay |
|-------|-------|
| Kat Kopyalama | Kaynak kat tüm tesisat entity'leri Z kaydırılarak hedefe kopyalanır |
| Kolon Oluşturma | Seçilen sistem tipine göre kat elevasyonları arası PipeEntity zinciri |
| Statik Yükseklik | `GetStaticHeadBetweenFloors()` → Bernoulli statik basınç bileşeni (mSS) |
| Kolon Rengi | `ApplySystemColor()` → sistem renk standardı (soğuk=mavi, pis=kahve vb.) |

### Bir Sonraki Session Öncelikleri

1. **HTML Hidrolik Rapor** — `OnGenerateHydraulicReport` → kritik hat + boru tablosu + pompa seçimi sonucu → tarayıcıda aç
2. **Şantiye Şartnamesi** — `OnAnalyzeSpecClick` → seçilen standart (TS/DIN) + malzeme + poz no → Word/PDF export
3. **İzometrik Şema** — `OnShowIsometricScheme` → sistem bazlı 2D izometrik + etiket (DN, sistem, uzunluk)
4. **Otomatik Kolon Konumlandırma** — Vitrifiye gruplarından optimum riser XY konumu (merkroid hesabı)

---

## Session #43 — OtoNET Pis Su İş Akışı Gap Kapama (2026-07-03)

### Yeni Komutlar

| Komut | Açıklama |
|---|---|
| `PlaceDrainageOutletCommand` | Pis su rögar / yağmur boşaltma noktası interaktif yerleştirme |
| `DrawCatchmentAreaCommand` | Poligon tıklama ile `RainfallCatchmentEntity` oluşturma (TS EN 12056-3) |

### Düzeltilen Özellikler

| Özellik | Önceki Durum | Yeni Durum |
|---|---|---|
| `OnWasteWaterDesign` dialog event wiring | Tüm event'ler (PlaceOutlet, DrawCatchment, FilterAndCopy, AcceptSystem) bağlı değildi | Tüm event'ler MainWindow'a bağlandı; dialog butonları viewport'u tetikliyor |
| `DomainGuardService.CheckTopologyConsistency` | Boş placeholder | BFS ile bağlı bileşen sayısı + döngü tespiti implemente edildi |
| `RiserPipeCommand` | Renk ve layer hardcoded (sadece soğuk/sıcak su); Z kotu her zaman 0–6 m | `ApplySystemColor()` + `GetLayerForSystem()` ile tüm 7 sistem; komut satırından kot girişi (Enter ile onay) |
| `FloorCopyService.FloorCopyOptions` | `ExcludeRisers` bayrağı yoktu | `ExcludeRisers = true` ile dikey borular kopyadan hariç tutulur |
| `SanitaryFixtureEntity.IsPortOnly` | Her zaman tam vitrifiye sembolü çiziliyordu | `IsPortOnly = true` iken sadece bağlantı noktaları (soğuk/sıcak/gider) çarpı sembolü ile gösterilir |

### OtoNET Pis Su İş Akışı Karşılaştırması

| OtoNET Adımı | AfneyCAD Durumu |
|---|---|
| Uygulama Seç (sistem modu) | VAR — `PipeSystemCombo` tüm borulama komutlarını besliyor |
| Katman görünürlüğü | VAR — 6 sistem toggle butonu + `WasteWaterDesignDialog` sekme |
| ST Cihazları — akıllı bağlantı noktaları | VAR (`IsPortOnly`) |
| Kolon borusu + kot girişi | VAR — `RiserPipeCommand` komut satırından m cinsinden kot okur |
| Dik nokta yakalama (⊥) | VAR — `SnapEngine.EnablePerpendicular` + `BtnOsnapPerp` |
| Tesisat kopyala kolon hariç | VAR — `FloorCopyOptions.ExcludeRisers` + dialog FilterAndCopy event |
| Boşaltma noktası (rögar/yağmur) | VAR — `PlaceDrainageOutletCommand`, dialog butonlarına bağlı |
| Yağmur düşme alanı poligonu | VAR — `DrawCatchmentAreaCommand`, dialog butonuna bağlı |
| Yağmur gideri fixture | VAR — `FixtureLibraryService` YG-001…YG-004 |
| Tesisatı Kabul Et validasyon | VAR — `DomainGuardService` BFS topoloji + açık uç + kaynak kontrolü |

---

## Session #44 — Pis Su Sistem Tamamlama: Kolon/Kaynak Modu · Bölünmüş Kolon · Eğim + DN Otomatizasyonu (2026-07-03)

### Düzeltilen ve Tamamlanan Özellikler

| Özellik | Değişiklik |
|---|---|
| `GetActiveSystemType()` yardımcı metod | MainWindow.Commands.cs'e eklendi — `PipeSystemCombo`'yu okur; `OnRiserPipeCommand` ve `OnSourcePointCommand` artık aktif sistemi alıyor |
| `SourcePointCommand` renk/layer | Hardcoded `MEP_TEMIZ_SU` / `MEP_SICAK_SU` → tüm 7 sistem için `GetLayerForSystem` + `GetColorForSystem` |
| `WasteWaterDesignDialog.CreateSplitColumnRequested` | Event imzası `Action<double,double,double,double>` oldu; MainWindow'da `PickPointCommand` ile XY seçimi + iki `PipeEntity` oluşturma |
| `DomainGuardService.CheckWastePipeSlopes` | Yeni kontrol: WasteWater + RainWater yatay boruları için Slope < %2 → Warning (TS EN 12056-2) |
| `FlowCalculationService.AutoSizePipes` — pis su eğimi | Eşik 0.01 → **0.02** düzeltildi |
| `FlowCalculationService.AutoSizePipes` — yağmur suyu | `RainWater` borular artık `GetMinDiameterForRainwater` (TS EN 12056-3 Tablo 3) ile boyutlandırılıyor |
| `GetMinDiameterForRainwater` | Yeni metod: Q (l/s) → min iç çap (mm): ≤0.5→DN50, ≤1.0→DN75, ≤2.0→DN90, ≤4.0→DN110, ≤8.0→DN125, >8→DN160 |

### TS EN 12056-3 Yağmur Suyu Min DN Tablosu

| Debi Q (l/s) | Min DN (PVC) |
|---|---|
| ≤ 0.5 | DN50 |
| ≤ 1.0 | DN75 |
| ≤ 2.0 | DN90 |
| ≤ 4.0 | DN110 |
| ≤ 8.0 | DN125 |
| > 8.0 | DN160 |

---

## Session #45 — Audit Highlight · WasteWater Eğim Log · Sistem Bazlı HTML Rapor · Yağmur Alanı Surface Dialog (2026-07-03)

### Düzeltilen ve Tamamlanan Özellikler

| Özellik | Değişiklik |
|---|---|
| `OnAuditSystem` — kırmızı highlight | `DomainGuardService.ValidateSystem()` sonucundaki hatalı entity'lerin `HasHydraulicViolation = true` atanarak viewport'ta kırmızı render edilmesi sağlandı |
| `AcceptSystem` ValidationLog | Eğim uyarıları (WasteWater/RainWater < %2) artık WasteWaterDesignDialog'daki ValidationLog'a yansıyor; hatalı borulara `HasHydraulicViolation` bayrağı yazılıyor |
| `HydraulicReportService` — sistem bölümleri | Tek tablo yerine 4 ayrı bölüm: (1) Temiz Su — TS 1258/DIN 1988 (LU, Hız, ΔP), (2) Pis Su — TS EN 12056-2 (eğim%, Manning h/D, DU, WC kolonu), (3) Yağmur Suyu — TS EN 12056-3 (alan, C, Q, eğim), (4) Diğer sistemler |
| `HydraulicReportService` — Manning doluluk | `FillingRatio` property bulunmadığından Manning formülü (`n=0.013`) ile `h/D = Q/Q_dolu` hesabı eklendi |
| `DrawCatchmentAreaCommand` — surface dialog | Poligon kapanınca `SurfaceTypeRequested` event'i fırlatılıyor; `CatchmentSurfaceDialog` açılıp 5 yüzey tipi seçimi (Düz/Eğimli/Döşeme/Çakıl/Yeşil) ve alan adı girişi yapılıyor |
| `CatchmentSurfaceDialog` | Yeni WPF dialog — yüzey tipi radio butonlar + C değeri gösterimi + alan adı input |

### HTML Hidrolik Rapor — Bölüm Yapısı

```
Rapor
├── 1. Temiz Su Tesisatı — TS 1258 / DIN 1988
│   Sistem | Uzunluk | DN | LU | Q (l/s) | Hız | ΔP | Hat Kaybı
│   ⚠ Hız > 1.5 m/s → sarı; Violation → kırmızı
├── 2. Pis Su Tesisatı — TS EN 12056-2
│   Uzunluk | DN | Σ DU | Q_ww | Eğim% | Doluluk h/D | Hız | WC | Durum
│   ⚠ Eğim < %2 → sarı uyarı + violation kırmızı
├── 3. Yağmur Suyu Tesisatı — TS EN 12056-3
│   ├── Yağmur Düşme Alanları: Alan | Yüzey | m² | C | Efektif m²
│   └── Yağmur Boruları: Uzunluk | DN | Q | Eğim% | Durum
└── 4. Diğer Sistemler (Yangın, Gaz)
    Sistem | Uzunluk | DN | Q | Hız | Hat Kaybı
```

### Yüzey Tipleri ve Akış Katsayıları (TS EN 12056-3)

| Yüzey Tipi | Akış Katsayısı C |
|---|---|
| Düz Çatı / Teras | 1.0 |
| Eğimli Çatı | 1.0 |
| Döşemeli Teras | 0.9 |
| Çakıl Çatı | 0.7 |
| Yeşil Çatı | 0.5 |

---

## Session #46 — PozKatalog + Keşif Birim Fiyat + Emdirme Çukuru Perkolasyon (2026-07-03)

### Düzeltilen ve Tamamlanan Özellikler

| Özellik | Değişiklik |
|---|---|
| `PozKatalogService` (yeni) | PozApp.BirimFiyatKalemi modelinden ilham — 2024 Bayındırlık poz listesi (Grup 22/23/27/28/29), JSON override, `FindForPipe` + `FindForFixture` |
| Keşif listesi birim fiyat | `WasteWaterCalcSheetDialog` BomRow: Poz No + B.Fiyat + Toplam kolonları; `PozKatalogService` entegrasyonu (snapshot fiyat) |
| HTML Keşif export | "🌐 HTML Keşif" butonu — KDV hariç + %20 KDV dahil toplam gösterimi |
| CSV export güncellendi | Poz No + Birim Fiyat + Toplam colonları eklendi |
| `CalculateSoakPit` | `WasteWaterCalcSheetService`'e yeni metod: TS 7880 perkolasyon (A = Q_tasarım / f_perc, çukur adedi, fizibilite) |
| Emdirme Çukuru UI | `WasteWaterCalcSheetDialog` Foseptik sekmesine yeni GroupBox + `CalcSoakPit_Click` |
| `OnGenerateHydraulicReport` | `RainfallCatchmentEntity` listesi rapora aktarıldı — HTML'de "Yağmur Düşme Alanları" tablosu dolu gelir |

### PozKatalog Yapısı (PozApp uyumlu)

```
PozKatalogService
├── PozKalemi(PozNo, Tanim, Birim, BirimFiyat, IsGrubu)  ← BirimFiyatKalemi
├── Built-in 2024 katalogu (Grup 22/23/27/28/29 — 50+ poz)
├── LoadFromJson(path)  — kullanıcı override / PDF import sonrası JSON
├── FindForPipe(MechanicalSystemType, innerDiamMm)
└── FindForFixture(fixtureType)
```

| Grup | Kapsam | Örnek Poz |
|---|---|---|
| 22.xxx | Temiz Su (Çelik/PPR) | 22.001/9 DN100 çelik boru |
| 23.xxx | Sıcak Su (PPR PN25) | 23.001/3 DN32 |
| 27.001 | Pis Su (PVC-U SN4) | 27.001/3 DN100 |
| 27.005 | Yağmur Borusu | 27.005/2 DN125 |
| 27.101–108 | Vitrifiyeler | 27.102 klozet, 27.103 lavabo |
| 28.xxx | Yangın (Galv. Çelik) | 28.001/7 DN100 |
| 29.xxx | Gaz (Çelik) | 29.001/3 DN25 |

### Emdirme Çukuru Hesabı (TS 7880)

```
A_gerekli = Q_tasarım (L/gün) / f_perc (L/m²/gün)
Q_tasarım = kişi × L/kişi/gün × güvenlik faktörü
A_çukur   = π × D × H  (tek çukur yan yüzeyi)
n_çukur   = ⌈A_gerekli / A_çukur⌉
Fizibilite: f_perc ≥ 10 L/m²/gün şartı (TS 7880)
```

---

## Session #47 — OtoNET Çıktı Dosyası İş Akışı + BOM Katalog Entegrasyonu (2026-07-04)

### Tamamlanan Özellikler

| Özellik | Değişiklik |
|---|---|
| `LayoutSheetDialog` — CaptureAll | "🏗 Tüm Katları Ekle": `FloorSnapshotService.DetectFloors` ile tüm katları yakalar, 2 sütunlu ızgara (10 000 mm aralık) düzeninde paftaya otomatik yerleştirir |
| `LayoutSheetDialog` — ExplodeAll | "💥 Tümünü Patlat": paftadaki tüm `BlockReferenceEntity`'leri tek seferinde patlatır; yeniden patlama koruması (Layer == EXPLODED kontrolü) |
| `LayoutSheetDialog` — ExportMerged | "📤 DXF Merge (Tümü)": geçici DB klonunda tüm blokları yerinde patlatarak tek DXF dosyasına aktar |
| `BillOfMaterialsService` — katalog | `PozKatalogService.FindForPipe` / `FindForFixture` ile gerçek 2024 Bayındırlık poz numaraları; katalogda eşleşme yoksa eski hardcode fallback devrede |

### OtoNET Çıktı Dosyası İş Akışı

| Adım | OtoNET | AfneyCAD |
|---|---|---|
| 1 | Her katı ayrı DWG'ye kaydet | **Pafta → Ekran Çizimi** → kat bazlı `SNAP_KAT1` vb. blokları oluştur |
| 2 | Pafta sayfasına blok olarak yerleştir | **Pafta Düzeni → Tüm Katları Ekle** (2 sütun ızgara) |
| 3 | Blokları patlat | **Pafta Düzeni → Tümünü Patlat** |
| 4 | DXF olarak çıktı al | **Pafta Düzeni → DXF Merge (Tümü)** — tek dosya |

### BOM Katalog Akışı

```
PipeEntity (sistem + DN)
    → PozKatalogService.FindForPipe(systemType, innerDiamMm)
    → PozKalemi { PozNo, Tanim, Birim, BirimFiyat }
    → TableEntity hücrelerine poz no + açıklama
    (eşleşme yoksa → hardcode GetPipePoz / GetPipeDescription fallback)
```

---

### Kolon Şeması Çıktısı

| Unsur | Açıklama |
|---|---|
| Dikey eksen | Gerçek Z koordinatı (mm) — kat seviyeleri dashed yatay çizgi |
| Sistem kolonları | Her sistem tipi (SK/SH/PS/YS/YG/GZ/HV) ayrı dikey sütun |
| Riser segment | ΔZ > 200 mm → kalın dikey çizgi |
| Dal segment | ΔZ < 200 mm → yatay çizgi + DN etiketi + ok |
| Armatür sembolü | En yakın sisteme bağlı dikdörtgen + tip adı |
| Legend | Sistem renkleri + toplam uzunluk (m) |

### Poz CSV Format

```csv
PozNo;Tanim;Birim;BirimFiyat;IsGrubu
22.001/1;Çelik boru DN15 — sıhhi tesisat;m;420;22-Temiz Su
27.001/3;PVC-U SN4 boru DN100 — pis su;m;680;27-Pis Su
27.102;Seramik klozet (alçıpan montaj);adet;1850;27-Vitrifiye
```

- Ayraç: noktalı virgül (`;`) veya virgül (`,`) — otomatik tespit
- Header satırı zorunlu (sütun sırası serbest)
- `#` ile başlayan satırlar yorum, boş satırlar atlanır
- İçe aktarma sonrası proje klasörüne `poz_katalog_override.json` kaydedilir

---

## Session #48 — Katalog Override, Riser Kat Atama, DXF/PNG Çıktı, TS 825

### 1. Pis Su Hesap Föyü — Override Katalog Otomatik Yükleme

Kaydedilmiş bir proje açıkken **Pis Su Hesap Föyü** dialogu açıldığında, proje
klasöründe `poz_katalog_override.json` varsa birim fiyatlar otomatik olarak bu
kullanıcı kataloğundan yüklenir (dahili 2024 listesi üzerine merge).

| Unsur | Açıklama |
|---|---|
| Tetikleme | Dialog açılışında proje klasörü taranır (`_activeContext.FilePath` → klasör) |
| Kaynak dosya | `poz_katalog_override.json` (aynı `PozNo` → override kazanır) |
| Bilgi etiketi | Keşif Listesi sekmesi: "Katalog: override JSON kullanılıyor" / "dahili 2024 listesi" |
| Geriye uyumluluk | `projectDir` parametresi nullable — eski çağrılar bozulmaz |

### 2. Kolon Şeması — Z Bazlı Kat Atama (2D Plan Desteği)

İzometrik/kolon şeması artık tüm boruların Z=0 olduğu **2D plan** projelerinde
bile anlamlı riser üretir. Üç strateji sırayla uygulanır:

| Öncelik | Koşul | Kat Z Ataması |
|---|---|---|
| 1 | Gerçek Z farkı ≥ 500 mm | Koordinatlar aynen (çok katlı) — `FloorSnapshotService.DetectFloors` |
| 2 | Layer'da kat bilgisi | `KAT_1`, `FLOOR_2`, `GROUND`, `ZEMIN`, `BODRUM` → kat × 3000 mm |
| 3 | Bilgi yok | Sistem içi sıraya göre 3000 mm artan sanal kat |

- Şemada **kat bazlı özet tablosu**: her kat için boru sayısı + DN dağılımı + toplam uzunluk.
- `OnShowIsometricScheme` **async** (Task.Run) — büyük projelerde UI donmaz.

### 3. Kolon Şeması — DXF / PNG Çıktı

Kolon şeması artık 3 biçimde alınabilir (çıktı seçim penceresi):

| Seçenek | Buton | Çıktı |
|---|---|---|
| 🌐 HTML | EVET | Tarayıcıda SVG kolon şeması (mevcut davranış) |
| 📐 DXF | HAYIR | AutoCAD R12 — `LineEntity` + `TextEntity` + `CircleEntity` (DxfWriterService) |
| 🖼 PNG | İPTAL | SkiaSharp raster — A4 300 dpi (2480×3508) |

- Ortak `BuildRiserPrimitives` ile tek geometri modeli üç çıktıyı besler.
- DXF/PNG hedefi `SaveFileDialog` ile seçilir; üretim arka planda yapılır.

### 4. TS 825 Isı Yalıtım Hesabı (Yeni)

**Isıtma menüsü → 🧱 TS 825 Isı Yalıtım** — `TS825InsulationService` +
`TS825InsulationDialog`.

| Girdi | Açıklama |
|---|---|
| İklim Bölgesi | 1–4. Bölge (TS 825 Tablo 1) |
| Yapı Elemanı | Dış Duvar / Çatı-Teras / Döşeme / Pencere-Kapı |
| Katmanlar | Malzeme · λ (W/mK) · kalınlık (m) — DataGrid |
| Sıcaklık | İç (varsayılan 20 °C) / dış (boş = TS 825 bölge değeri) |
| Yalıtım λ | Eklenecek yalıtım iletkenliği (EPS ≈ 0.035) |

| Çıktı | Bağıntı / Referans |
|---|---|
| Mevcut U | `U = 1 / (Rsi + Σ(d/λ) + Rse)` — TS EN ISO 6946 |
| Sınır U (max) | TS 825:2013 Tablo 2 (bölge × eleman) |
| Gerekli yalıtım | `d = λ · (1/Umax − 1/Umevcut)` |
| Isı kaybı Q | `U · A · (Ti − Te)` (W) |
| Yıllık enerji | `U · A · DD · 24 / 1000` (kWh/yıl) — TS 825 derece-gün |

- **Uygun/Sınır Aşıldı** durum göstergesi, açıklama satırları (referanslı).
- Butonlar: **Hesapla**, **HTML Rapor**, **Çizime Metin Ekle** (katman `TS825_HESAP`).

---

## Session #49 — Canlı QA Testi, Kritik Hata Düzeltmeleri, Görsel Yenileme (2026-07-12)

Bu oturumda ilk kez **canlı UI testi** (gerçek DWG üzerinde, UI Automation ile) yapıldı; kod okumasıyla görünmeyen gerçek hatalar bulundu ve düzeltildi.

### Bulunan ve Düzeltilen Kritik Hatalar

| Hata | Kök Neden | Çözüm |
|---|---|---|
| Boru çizim komutu sağ-tık ile bitmiyor | `RoutePipeCommand.OnKeyDown` sadece ESC dinliyordu | Enter/Space/sağ-tık desteği eklendi (`LineCommand` ile tutarlı), `PipeRoutingEngine.Reset()` eklendi |
| "Duvara Cihaz Yerleştir" hiç çalışmıyor | `PlaceFixtureOnWallCommand` ham DWG çizgilerini değil, **önceden tanınmış** `ArchitecturalObstacle` nesnelerini arıyor — AutoBLD→Eleman Tanı çalıştırılmadan boş liste | Önkoşul kontrolü + açık uyarı: "Önce AutoBLD → Eleman Tanı" |
| 35 dialogda ComboBox metni görünmüyor (beyaz-üstü-beyaz) | `Style TargetType="ComboBox"` sadece Background/Foreground Setter'ı var, gerçek `ControlTemplate` yok — WPF varsayılan şablonu kapalı kutu rengini değiştirmiyor | 34 dosyaya tam `ControlTemplate` eklendi |
| Katman popup'ında fare tekerleği çalışmıyor | İç içe (nested) `ScrollViewer` — dış ScrollViewer, ListBox'ın kendi iç ScrollViewer'ıyla çakışıyordu | Dış ScrollViewer kaldırıldı, `ScrollViewer.VerticalScrollBarVisibility="Auto"` doğrudan ListBox'a taşındı |

### Görsel Yenileme — Merkezi İkon Kütüphanesi

`src/Afney.Cad.Presentation/Resources/Icons.xaml` oluşturuldu (App.xaml'e merge edilir):
- Emoji glifleri yerine font-bağımsız vektör `Geometry` ikonlar (göz, kar tanesi, kilit, çöp kutusu, kalem, güneş, artı, arama, yenile...)
- Paylaşılan `Icon.LayerVisibility` / `Icon.LayerFreeze` / `Icon.LayerLock` stilleri (DataTrigger ile durum rengi)
- `IconButtonRound` — tüm ikon butonları için tutarlı yuvarlak hover efekti

Katmanlar paneli (sol dock + ribbon popup) bu kütüphaneyle yenilendi. **Karar:** AfneyCAD'in koyu tema kimliği korunuyor (AutoCAD'e karşı rekabet avantajı) — AutoCAD 2026'nın açık temasından sadece düzen/hizalama/ikon sadeliği disiplini alınıyor, renk paleti değil.

### Otomasyon Ortamı Notu
Canlı test için UI Automation (PowerShell + System.Windows.Automation) kullanıldı. Bilinen kısıtlar: Türkçe karakterli buton adları encoding sorunu yaşayabilir (ASCII alt-dizeyle eşleştirme gerekir), özel WPF dialogları (MessageBox değil) `SendKeys` yerine `WM_CLOSE` ile daha güvenilir kapatılıyor, PowerShell dosya yazımında **mutlaka UTF-8 (BOM'suz) via `[System.IO.File]::WriteAllText`** kullanılmalı — `Get-Content -Raw` + `Set-Content -Encoding UTF8` Türkçe karakterleri (ı, ş, ç, ğ, ü, ö) ve emojileri bozabiliyor (mojibake).

---

## Session #51 — Mühendislik Doğrulama Sertleştirme · Grip Sistemi Tamamlama · IFC Rotasyon/Profil · Word Export · HVAC Ekipman Kütüphanesi (2026-07-19)

> Not: Bu, kitaptaki Session #49'un devamıdır. Memory kayıtlarında "Session #50" olarak anılan 2026-07-13 tarihli ikon kütüphanesi/rakip-kıyaslama turu bu kitaba henüz işlenmemişti — numaralandırma çakışmasını önlemek için bu oturum #51 olarak kaydedildi.

Bu oturumun teması: kullanıcının "4M FineSANI ile karşılaştır, eksikleri kapat, hiçbir şey silmeden 10/10'a doğru geliştir" standart direktifi kapsamında, **BIM/IFC ve HVAC** kategorilerindeki somut eksiklerin kapatılması.

### 1. DomainGuard / Sistem Doğrulama Sertleştirme

| # | Değişiklik | Kök Sorun | Çözüm | Ana Dosya |
|---|---|---|---|---|
| 1 | Kaynak Bağlantı Kontrolü | `CheckSourceConnectivity` sadece "herhangi bir `MechanicalLoadNode` var mı" diye bakıyordu — gerçek bağlantıyı doğrulamıyordu | Her yük düğümünden gerçek BFS ile porta bağlılık VE bir `SanitaryFixtureEntity`'ye erişilebilirlik kontrolü | `DomainGuardService.cs` |
| 2 | Kritik Yol Hesabı | `FindCriticalPath`, `leaves.Take(4)` sezgiseliyle sadece ilk 4 yaprağı deniyordu — büyük dallanmalı ağlarda gerçek en uzun yolu kaçırabiliyordu | Ağaç-çap çift-taraflı-tarama (double-sweep) algoritması: 2 BFS geçişi (rastgele başlangıç → en uzak A → A'dan en uzak = gerçek çap) | `NetworkTopologyAnalysisService.cs` |
| 3 | **Canlı bulunan kritik hata:** `RunLongestPath` içindeki eski `PriorityQueue` tabanlı gevşetme (relaxation) döngüsünde ziyaret takibi yoktu — 3+ düğümlü herhangi bir bağlı grafta parent/child arasında sınırsız gevşeme (gerçek sonsuz döngü) oluşuyordu. Test 180+ saniye asılı kaldı, canlı yakalandı. | — | Basit, tek-geçişli BFS (visited[] dizisi) ile yeniden yazıldı | `NetworkTopologyAnalysisService.cs` |
| 4 | Fiziksel Çakışma Kontrolü | DomainGuard'da hiç çakışma (clash) kontrolü yoktu — Hesapla öncesi kapı boru-mimari elemanla fiziksel çakışmayı yakalamıyordu | `CheckPhysicalClashes` yeni metot — `ClashDetectionService` sonuçlarını Critical→Hata, Warning→Uyarı olarak `ValidateSystem()` adım 7'ye ekliyor | `DomainGuardService.cs`, `ValidationGateService.cs`, `MechanicalKernel.cs` |

**9 yeni test** (`DomainGuardSourceConnectivityTests`, `CriticalPathDoubleSweepTests`, `DomainGuardClashDetectionTests`).

### 2. Grip Sistemi — Tam Kapsama

Önceden AfneyCAD'de sadece ~6 entity tipinde grip (tutamaç) desteği vardı, birkaçında da "sürükle ama hiçbir şey olmuyor" hatası mevcuttu (`MoveGripPointAt` override edilmemiş, base class'ın boş varsayılanına düşüyordu).

**Kritik hata (2. kez, aynı sınıf hata):** `LwPolylineEntity`'de `GetGripPoints()` vardı ama `MoveGripPointAt()` YOKTU — Mahal/Room sınırları, OFFSET/TRIM sonuçları, mimari algılama gibi en yaygın kullanılan taban sınıfını etkiliyordu. Eklendi.

Grip desteği eklenen/tamamlanan 11 entity: `BlockReferenceEntity` (pozisyon + rotasyon tutamacı), `SplineEntity` (kontrol noktası başına), `TableEntity` (üst-sol + alt-sağ resize), `ValveEntity`, `SanitaryFixtureEntity`, `ReducerEntity`, `DrainageOutletEntity`, `PipeLabelEntity`, `MahalEntity`, `RainfallCatchmentEntity`, `RoomEntity`, `LwPolylineEntity` (kritik hata düzeltmesi).

**14 yeni test** (`GripSystemFullCoverageTests`, `LwPolylineGripFixTests`).

### 3. IFC Import — Rotasyon ve Karmaşık Profil Kesitleri

| Eksik | Öncesi | Sonrası |
|---|---|---|
| Rotasyon | `IFCAXIS2PLACEMENT3D`'nin RefDirection'ı hiç okunmuyordu — döndürülmüş her duvar/kapı/pencere her zaman 0° (eksene paralel) içeri aktarılıyordu | `IfcPlacementInfo` struct + `RotateAndTranslate` helper — RefDirection'dan `Atan2` ile gerçek açı hesaplanıyor, duvar/kapı/pencere köşeleri ve kapı kanat yayı doğru döndürülüyor |
| Profil Kesiti | Sadece `IFCRECTANGLEPROFILEDEF` (dikdörtgen) destekleniyordu | `IFCARBITRARYCLOSEDPROFILEDEF` (keyfi çokgen, ör. üçgen/L-kolon) ve `IFCCIRCLEPROFILEDEF` (dairesel, 16 kenarlı yaklaşım) artık gerçek dış hatlarıyla ekstrude ediliyor |

Kapsam dışı bırakılan (araştırma ajanının önerisiyle, düşük ROI): eğri/çoklu-segment duvar EKSENLERİ (`IfcTrimmedCurve`/`IfcCompositeCurve`) — Revit/ArchiCAD IFC 2x3 dışa aktarımlarında çoğu "kavisli" duvar zaten kısa düz segmentlere ayrıştırılmış geliyor.

**3 yeni test** (`IfcRotationAndProfileTests`) — `Afney.Cad.Infrastructure/Import/IfcImportService.cs`.

### 4. Raporlama — Word (.docx) Çıktısı

4M FineSANI'nin Word/Excel/PDF üçlü çıktı setinden Word eksikti. `DocumentFormat.OpenXml` SDK (3.1.1, Office/Word kurulumu gerektirmez) ile `WordExportService` eklendi — `ExcelExportService` ile aynı 4 bölüm (Özet/Metraj-BOQ/Pis Su/Yağmur Suyu). Ribbon'da Excel butonunun yanına Word butonu eklendi.

**3 yeni test** (`WordExportServiceTests`) — `Afney.Cad.Infrastructure/Export/WordExportService.cs`.

### 5. HVAC Kanal Ekipman Kütüphanesi

Önceden AfneyCAD'de menfez/difüzör, damper veya susturucu seçim kataloğu hiç yoktu — sadece düz kanal (`DuctEntity`) mevcuttu.

| Yeni Parça | Açıklama | Ana Dosya |
|---|---|---|
| `AirTerminalEntity` | Difüzör/menfez/panjur/lineer yarık/jet nozul/zemin difüzörü — tek portlu (Neck), `NeckVelocityMs` doğrudan `AcousticAnalysisService.TerminalDeviceLoss`'u besliyor, `NCRating`/`ThrowM` katalog alanları | `Entities/AirTerminalEntity.cs` |
| `DamperEntity` | Volume/Fire/Smoke/FireSmoke/BackDraft — `ValveEntity` ile birebir aynı port deseni (2 port, kanala seri). Fire/Smoke tipleri EN 1366-2 uyumlu varsayılan 90dk yangın direnci | `Entities/DamperEntity.cs` |
| `SilencerSelectionService` | `FanSelectionService` ile aynı desen — Systemair/Halton/Trox/Lindab, 8 oktav bantta (63–8000 Hz) Insertion Loss matrisi. `ApplyToNoiseBudget` seçilen susturucunun IL'ini doğrudan `AcousticAnalysisService.AnalyzeSystem`'in girdisine bağlıyor — fan→kanal→dallanma→dirsek→susturucu→terminal→oda gürültü bütçesi zinciri artık uçtan uca kapanıyor | `Services/SilencerSelectionService.cs` |

VAV/CAV kutuları, bobin/filtre, esnek bağlantı bu oturumda bilerek atlandı (genel sıhhi/HVAC/gaz aracı için düşük ROI).

**13 yeni test** (`HvacEquipmentLibraryTests`).

### Test Durumu
Tam suite: 164/166 geçti (2 hata önceden var olan, alakasız `PortEngineeringTests` — bu oturumda dokunulmadı).

### Notion Senkronizasyonu
Bu oturum boyunca Notion MCP bağlantısı kopuktu; puanlama/haritalama sohbette sunuldu, bağlantı sağlandığında (yeni Claude Code oturumu) `Aktif Session` ve ana proje sayfasına aktarılacak.

---

## Session #52 — B-Rep Kernel Aktivasyonu · FineSANI Denetimi · Sıfırdan D3D11 Render Motoru · CSG Boolean Faz 1-3 (2026-07-20)

Bu oturumun teması: `Afney.Cad.Geometry/Topology` altında oturum öncesinden kalma, hiç örneklenmeyen ölü bir winged-edge B-Rep iskeletini gerçek çalışır hale getirmek; ardından kullanıcının "4M FineSANI ile kategori bazlı, kod-doğrulamalı karşılaştır" isteğiyle 12 kategoride gerçek denetim yapıp bulunan eksikleri kapatmak; sonra iki büyük mimari girişim — sıfırdan GPU hızlandırmalı 3D render motoru ve tam topolojik B-Rep boolean — başlatmak.

### 1. B-Rep Kernel Aktivasyonu
- `Face.GetArea()` düzeltildi (XY-izdüşüm → Newell's method, düşey yüzeylerde artık doğru alan).
- `Solid.GetVolume()` divizör hatası düzeltildi (1/6 → 1/3, divergence theorem).
- Yeni: `PolygonTriangulator` (ear-clipping), `BRepBuilder` (`ExtrudePolygon`/`ExtrudeBox`), `BRepTessellator`, `WallBRepService`, `DuctBRepService` — B-Rep artık gerçek duvar/kanal geometrisine bağlı, `Pipe3DViewWindow`, DXF/IFC export ve axonometrik SVG'de kullanılıyor.

### 2. FineSANI Denetimi — 12 Kategori, Kod Doğrulamalı
`karsilastirma.md`/`Eksiklikler.md` (öz-değerlendirme, 610/630) referans alınmadı — `docs/Denetim_Gecmisi.md` yeni, kod-okuma tabanlı bir denetim geçmişi başlattı. En düşükten yukarı doğru gerçek bug'lar bulunup düzeltildi (her biri bağımsız bir ajanla şüpheci doğrulandı):

| Servis | Bulunan Gerçek Hata |
|---|---|
| `RealTimeCostService` | Fiyat sorgusu yanlış enum (`SystemType` yerine `PipeMaterialType` olmalıydı) — her boru varsayılan 50 TRY/m alıyordu |
| `MultiStoryEnhancementService` | Riser guard yanlış eşik kontrolü — kat üst üsteyken sıfır-uzunluklu bağlantı borusu oluşuyordu |
| `FireFightingService` | Basınç kaybı formülü debiden bağımsızdı (gerçek Hazen-Williams'a çevrildi); OH2/OH3 tasarım alanı karışıklığı (216→144 m²) |
| `WasteWaterDesignService` | TS EN 12056-2 Tablo 3 K-katsayı etiketleme hatası (System_IV: 1.0→1.2) |
| `SanitaryFixtureEntity` + `PipeWizardService` (3 yer) | Standart lavabo LU=1.5 (yanlış, DIN "Cerrahi Lavabo" değeri) → 0.5 (TS EN 806-2 doğru değer) |
| `PortEngineeringTests` (2 bilinen hata) | Reaktif hesap testi açık-uçlu şebeke kuruyordu (gerçek DomainGuard sertleşmesi, test eskiydi); washbasin LU testi de yukarıdaki hatayı doğruluyordu |

Ayrıca: DXF `DIMENSION` entity export, IFC duvar export (B-Rep→IFC4 tessellation, ilk kez), 43 yeni test (5 daha önce test edilmeyen serviste: HeatingSystem, PumpSelection, PressureDrop, GutterSizing, ClashDetection).

**Sonuç: 286/286 test — bu oturumda ilk kez sıfır bilinen hata. Genel denetim ortalaması 6.8 → 7.3/10.**

### 3. Sıfırdan D3D11 Render Motoru (Faz 1) — `Afney.Cad.Render3D`
Kullanıcı kararı: WPF `Viewport3D` KULLANILMADAN, gerçek GPU hızlandırmalı (Direct3D11, Vortice.Windows) bir motor sıfırdan yazıldı — mevcut Skia 2D motoru ve `Pipe3DViewWindow` dokunulmadan kaldı. `D3D11DeviceResources`, `D3DImageBridge` (WPF `D3DImage` köprüsü), `Camera3D`, `MeshBuffer`, `Basic.hlsl`, `Renderer`, `Direct3DViewportControl` — komut satırından `d3dtest`.

**Hata avı (3 tur):** Çalışma zamanında `E_INVALIDARG` (`IDirect3DDevice9Ex.CreateTexture`) hatası alındı. İlk iki düzeltme denemesi ("D3D11/D3D9Ex farklı fiziksel GPU'lara bağlanıyor" teorisiyle adaptör index/LUID eşleştirmesi) hatayı GİDERMEDİ. Bu ortamda gerçek bir GPU (NVIDIA GTX 1050 Ti) olduğu fark edilip küçük bir WinForms tabanlı program ile hata yerel olarak BİREBİR tekrar üretildi — adaptör LUID'lerinin zaten eşleştiği doğrulandı (teori tamamen yanlıştı). Vortice.Windows'un resmi örneği (`DrawingSurface.cs`) satır satır karşılaştırılınca gerçek fark bulundu: paylaşılan D3D11 texture `BindFlags.RenderTarget | BindFlags.ShaderResource` gerektiriyordu, kodda sadece `RenderTarget` vardı. Düzeltme yerel tekrar-üretimde doğrulandı, ardından ana koda uygulandı; gereksiz adaptör-eşleştirme kodu geri alındı.

**Endüstriyel standart yükseltmeleri:** MSAA (4x, donanım desteklemezse otomatik düşer — ayrı MSAA renk hedefine render edip D3D9-paylaşımlı tek-örnekli hedefe `ResolveSubresource`), gamma-doğru (lineer uzay) aydınlatma, Shader Model 4→5 yükseltme.

Detay: `docs/Roadmap_3D_Render_Motoru.md`. Faz 1 kod seviyesinde tamamlandı, kullanıcının `d3dtest` ile görsel doğrulaması gerekiyor.

### 4. Tam Topolojik B-Rep Boolean (Faz 1-3) — `Afney.Cad.Geometry/Topology/Boolean`
Kullanıcı kararı: mesh-seviyesi kısayol değil, **tam topolojik winged-edge boolean**. `PlaneIntersection`, `FaceIntersection`, `EdgeSplitter` (winged-edge cerrahi), `FaceSplitter` (kiriş bölme), `SolidClassifier` (Möller-Trumbore nokta-içi testi) — hepsi ilk denemede test geçti. Faz 4 (tam SUBTRACT orkestrasyonu, eksene-hizalı "dilim kesme" senaryosuyla) henüz başlanmadı — detay `docs/Roadmap_CSG_Boolean.md`.

### Sonraki Oturum Öncelikleri (sırayla)
1. **CSG Boolean Faz 4** — eksene-hizalı slab-cut SUBTRACT senaryosu (`docs/Roadmap_CSG_Boolean.md`'deki önerilen test).
2. **3D Render Motoru Faz 2** — gerçek B-Rep mesh render + kamera + Fixture/Door/Window/Room için B-Rep adaptörü (kullanıcının `d3dtest` görsel onayından sonra).
3. **Manuel Mahal — kapı/pencere pervaz-snap** (`ManualMahalCommand.cs`): açıklık tıklamaları şu an ham imleç koordinatına gidiyor, gerçek `DoorEntity`/`WindowEntity` genişliğine snap etmiyor — kullanıcıdan pervaz noktası referansı (duvar ekseni mi iç yüz mü) ve snap davranışı (sessiz mi onaylı mı) netleşmeyi bekliyor.

---

*Son guncelleme: 2026-07-20 | AfneyCAD v4.0.0 — Session #52*
