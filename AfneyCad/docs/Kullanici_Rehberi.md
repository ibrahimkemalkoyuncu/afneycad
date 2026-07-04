# AfneyCAD Kullanıcı Rehberi
**Sürüm:** 4.0 | **Tarih:** Temmuz 2026 | **Platform:** Windows 10/11

> Bu rehber, AfneyCAD'i ilk kez açan bir kullanıcının bile hiçbir adımı atlamadan takip edebilmesi için yazılmıştır. Her araç için "ne işe yarar, nasıl başlatılır, adım adım nasıl kullanılır" anlatılmıştır. Teknik terimler yerine gündelik dil kullanılmıştır (örneğin "nesne", "çizim alanı", "katman").

---

## İçindekiler

1. [Kurulum ve İlk Açılış](#bölüm-1-kurulum-ve-i̇lk-açılış)
2. [Temel Çizim Araçları](#bölüm-2-temel-çizim-araçları)
3. [DWG/DXF Dosya İşlemleri](#bölüm-3-dwgdxf-dosya-i̇şlemleri)
4. [Katman (Layer) Yönetimi](#bölüm-4-katman-layer-yönetimi)
5. [MEP Tesisat Çizimi — Ana İş Akışı](#bölüm-5-mep-tesisat-çizimi--ana-i̇ş-akışı)
6. [Hidrolik Hesap](#bölüm-6-hidrolik-hesap)
7. [Raporlar ve Çıktılar](#bölüm-7-raporlar-ve-çıktılar)
8. [Pafta Düzeni ve Çıktı Alma](#bölüm-8-pafta-düzeni-ve-çıktı-alma)
9. [Poz Kataloğu ve Birim Fiyat](#bölüm-9-poz-kataloğu-ve-birim-fiyat)
10. [Kısayollar ve İpuçları](#bölüm-10-kısayollar-ve-i̇puçları)

---

## Bölüm 1: Kurulum ve İlk Açılış

**Bu bölümde ne öğreneceğiz:** Programı çalıştırmak için bilgisayarınızda ne olması gerektiğini ve ilk açılışta ekranda gördüğünüz her parçanın ne işe yaradığını.

### 1.1 Sistem Gereksinimleri

| Gereksinim | Açıklama |
|------------|----------|
| İşletim sistemi | Windows 10 veya Windows 11 (64-bit) |
| Çalışma ortamı | .NET 10 Masaüstü Çalışma Zamanı (Desktop Runtime) |
| Ekran | En az 1366 × 768 çözünürlük (1920 × 1080 önerilir) |
| Fare | Tekerlekli (scroll) fare şiddetle önerilir — yakınlaştırma ve kaydırma için |
| Disk | Program + projeler için yaklaşık 500 MB boş alan |

> 💡 **İpucu:** AfneyCAD yalnızca Windows üzerinde çalışır. Program açılmıyorsa, büyük olasılıkla **.NET 10 Desktop Runtime** yüklü değildir. Microsoft'un resmi sitesinden ücretsiz indirip kurabilirsiniz.

### 1.2 Programı Açmak

1. AfneyCAD kısayoluna **çift tıklayın**.
2. Program tam ekran (maximize) olarak açılır.
3. Sol üstte mavi renkli **AfneyCAD v4.0** logosunu görürsünüz.

### 1.3 Ana Pencere Bileşenleri

İlk açılışta ekran şu ana bölgelerden oluşur (yukarıdan aşağıya, soldan sağa):

```
┌─────────────────────────────────────────────────────────────┐
│  [Hızlı Erişim: 📄 📁 💾 📂 ℹ 🖨 🕐]           AfneyCAD v4.0  │  ← Hızlı Erişim Çubuğu
├─────────────────────────────────────────────────────────────┤
│ 1.Sistem | 2.Uç Noktalar | 3.Tesisat | 4.Hesap | 5.Raporlar │  ← Ribbon Sekmeleri
│ 📐 Boyut | 🏗 AutoBLD | 👁 Görünüm                            │
├──┬──────┬────────────────────────────────────────┬──────────┤
│Ak│ Sol  │                                        │  Sağ     │
│ti│Panel │            ÇİZİM ALANI                  │  Panel   │
│vi│(Proje│         (Viewport / Ekran)             │(Akıllı   │
│te│/Kat- │                                        │ Asistan) │
│  │man)  │                                        │          │
├──┴──────┴────────────────────────────────────────┴──────────┤
│ ▶ [Komut Girişi]  Hazır...    ORTHO SNAP POLAR   Zoom:100%  │  ← Durum Çubuğu
└─────────────────────────────────────────────────────────────┘
```

**1. Ribbon (Şerit Menü) — En üstte**
Tüm araçlar sekmeler halinde burada toplanır. Sekmeler numaralıdır çünkü AfneyCAD sizi **adım adım** yönlendirir:
- **1. Sistem** — projenin temel ayarları (standart, bina, aktif katman)
- **2. Uç Noktalar** — odalar (mahal) ve vitrifiye yerleştirme
- **3. Tesisat** — boru çizimi ve sistem tasarımı
- **4. Hesap** — hidrolik hesap ve doğrulama
- **5. Raporlar** — metraj, şema, çıktı alma
- **📐 Boyut** — ölçü çizgileri
- **🏗 AutoBLD** — mimari çizim işlemleri (blok, WBlock, mimari tanıma)
- **👁 Görünüm** — yakınlaştırma, 2D/3D, sistem görünürlüğü

> ⚠️ **Dikkat:** İlk açılışta **2. Uç Noktalar, 3. Tesisat, 4. Hesap ve 5. Raporlar** sekmeleri gri (pasif) görünür. Bunlar önceki adımı tamamlayıp yeşil **"✅ Onayla"** butonuna bastıkça sırayla açılır. Bu, yanlış sırada işlem yapmanızı engelleyen bir güvenlik akışıdır.

**2. Çizim Alanı (Viewport) — Ortada**
Çizimlerinizi gördüğünüz büyük siyah alan. Bir CAD programının "kağıdı" burasıdır. Üstünde sekmeler bulunur; her açık proje ayrı bir sekmede gösterilir (tıpkı internet tarayıcısı gibi).

**3. Aktivite Çubuğu — En solda (dar dikey şerit)**
VS Code tarzı dört ikon:
- 📁 **Proje Gezgini** — proje ağacı
- 🗂 **Katman Yöneticisi** — katman listesi
- 🔍 **Mahal Analizi** — oda inceleme
- 🧠 **Akıllı Asistan** — sağ paneli açar

**4. Sol Panel**
Aktivite çubuğundan seçtiğinize göre Proje Gezgini veya Katman Yöneticisi'ni gösterir. İlk açılışta gizlidir; ikonlara tıklayınca açılır.

**5. Sağ Panel (Akıllı Asistan / Intelligence)**
Seçili nesnenin özelliklerini ve sistem uyarılarını gösterir.

**6. Komut Satırı — Sol altta**
İçinde ▶ işareti olan kutu. AutoCAD'e alışkın kullanıcılar için: buraya `L` yazıp Enter'a basınca çizgi komutu başlar. Komutların tam listesi [Bölüm 10](#bölüm-10-kısayollar-ve-i̇puçları)'da.

**7. Durum Çubuğu — En altta**
- Sol: Komut girişi kutusu
- Orta: Durum metni (program o an ne yapmanızı beklediğini yazar — buraya sık sık bakın!)
- Sağ: **ORTHO / OSNAP / POLAR** açma-kapama düğmeleri ve **Zoom** yüzdesi

> 💡 **İpucu:** Bir şeyin nasıl yapılacağından emin değilseniz, ekranın alt-orta kısmındaki **durum metnini** okuyun. Program size sürekli "şimdi şunu tıklayın" der.

### Sık Sorulan Sorular (Bölüm 1)

**S: Program açıldı ama sekmelerin çoğu gri, tıklayamıyorum. Bozuk mu?**
C: Hayır. Bu normaldir. AfneyCAD sizi sırayla yönlendirir. Önce **1. Sistem** sekmesindeki ayarları yapıp yeşil **"✅ Ayarları Onayla"** butonuna basın; sonraki sekme açılacaktır.

**S: Ekranımda hiçbir çizim yok, tamamen siyah. Ne yapmalıyım?**
C: Yeni ve boş bir proje açtınız demektir. Bir DWG dosyası açmak için sol üstteki 📁 (Aç) düğmesine tıklayın veya doğrudan çizmeye başlayın.

---

## Bölüm 2: Temel Çizim Araçları

**Bu bölümde ne öğreneceğiz:** Çizgi, daire, dikdörtgen gibi temel nesneleri çizmeyi ve trim/uzat/ayna gibi düzenleme araçlarını kullanmayı.

Temel çizim araçlarının çoğu **3. Tesisat** sekmesindeki "Yardımcı Çizim" bölümünde ve **📐 Boyut** sekmesinde bulunur. Ayrıca hepsini komut satırından kısayolla da başlatabilirsiniz.

> 💡 **Genel Kural:** Bir çizim komutu başladığında, çizim alanında noktaları **sol tıkla** belirlersiniz. İşlemi bitirmek için **Enter** veya **Sağ tık**, iptal etmek için **Esc** kullanılır.

### 2.1 Çizgi (Line)

- **Ne işe yarar:** İki nokta arasına düz çizgi çizer.
- **Nasıl başlatılır:** **3. Tesisat → Yardımcı Çizim → ✏ Çizgi** butonu, veya komut satırına `L` yazıp Enter.
- **Adım adım:**
  1. **✏ Çizgi** butonuna tıklayın.
  2. Çizim alanında başlangıç noktasını **tıklayın**.
  3. Bitiş noktasını **tıklayın**.
  4. Devam eden çizgiler çizmek için tıklamaya devam edin.
  5. Bitirmek için **Enter** veya **Esc**.

### 2.2 Daire (Circle)

- **Ne işe yarar:** Merkez ve yarıçap ile daire çizer.
- **Nasıl başlatılır:** **⬤ Daire** butonu, veya `C` + Enter.
- **Adım adım:**
  1. **⬤ Daire** butonuna tıklayın.
  2. Merkez noktasını **tıklayın**.
  3. Fareyi dışarı doğru hareket ettirin, yarıçapı belirleyip **tıklayın**.

### 2.3 Polyline (Çoklu Çizgi)

- **Ne işe yarar:** Birbirine bağlı çok segmentli tek bir nesne çizer. Duvar, mahal sınırı gibi kapalı şekiller için idealdir.
- **Nasıl başlatılır:** Komut satırına `PL` (veya `polyline`) + Enter.
- **Adım adım:**
  1. `PL` yazıp Enter'a basın.
  2. İlk noktayı tıklayın, sonra köşe köşe tıklamaya devam edin.
  3. Kapatmak/bitirmek için **Enter**.

### 2.4 Dikdörtgen (Rectangle)

- **Ne işe yarar:** İki köşe noktasıyla dikdörtgen çizer.
- **Nasıl başlatılır:** Komut satırına `rect` + Enter.
- **Adım adım:**
  1. `rect` yazıp Enter.
  2. Bir köşeyi tıklayın.
  3. Karşı köşeyi tıklayın.

### 2.5 Trim (Buda) — `TR`

- **Ne işe yarar:** Çizgilerin taşan uçlarını, kesişim noktasına kadar kırpar.
- **Nasıl başlatılır:** **✂ Buda (TR)** butonu, veya `TR` + Enter.
- **Adım adım:**
  1. **✂ Buda** butonuna tıklayın.
  2. Silmek istediğiniz çizgi parçasının üzerine **tıklayın** — o parça kesişime kadar silinir.
  3. Bitirmek için **Esc**.

### 2.6 Extend (Uzat) — `EX`

- **Ne işe yarar:** Kısa kalan bir çizgiyi, en yakın nesneye kadar uzatır.
- **Nasıl başlatılır:** **↔ Uzat (EX)** butonu, veya `EX` + Enter.
- **Adım adım:** Uzatmak istediğiniz çizginin ucuna yakın kısmına tıklayın; çizgi otomatik uzar.

### 2.7 Mirror (Ayna) — `MI`

- **Ne işe yarar:** Seçili nesnelerin bir eksene göre simetrik kopyasını oluşturur. Simetrik banyo/WC yerleşimlerinde çok işe yarar.
- **Nasıl başlatılır:** Önce nesneleri seçin, sonra **⟺ Ayna (MI)** butonu veya `MI` + Enter.
- **Adım adım:**
  1. Aynalanacak nesneleri seçin (üzerlerine tıklayın veya çerçeveyle çevirin).
  2. **⟺ Ayna** butonuna basın.
  3. Ayna ekseninin **birinci** noktasını tıklayın.
  4. Ayna ekseninin **ikinci** noktasını tıklayın. Simetrik kopya oluşur.

### 2.8 Move (Taşı) — `M` ve Copy (Kopyala) — `CO`

- **Ne işe yarar:** Move seçili nesneleri taşır, Copy çoğaltır.
- **Adım adım:**
  1. Önce taşınacak/kopyalanacak nesneyi **seçin**.
  2. `M` (taşı) veya `CO` (kopyala) yazıp Enter.
  3. Referans (tutma) noktasını tıklayın.
  4. Hedef noktayı tıklayın.

### 2.9 Offset (Ötele) — `O`

- **Ne işe yarar:** Bir çizgiyi belirli mesafede paralel çoğaltır. Duvar iç/dış yüzü gibi.
- **Adım adım:**
  1. Önce ötelenecek nesneyi **seçin**.
  2. `O` yazıp Enter.
  3. Ötelemek istediğiniz yönü tıklayın.

> ⚠️ **Dikkat:** Offset komutu, işlem başlatmadan **önce** nesne seçilmesini ister. Seçim yoksa durum çubuğunda "Lütfen önce ötelenecek nesneleri seçin" uyarısı çıkar.

### 2.10 Explode (Patlat) — `X`

- **Ne işe yarar:** Blok, polyline gibi birleşik nesneleri tekil parçalarına ayırır.
- **Adım adım:** Nesneyi seçin → **⛏ Patlat (X)** butonu veya `X` + Enter.

### 2.11 Hatch (Tarama)

- **Ne işe yarar:** Kapalı bir alanı desenle (çapraz çizgi, dolu vb.) doldurur.
- **Nasıl başlatılır:** Komut satırına `H` + Enter.
- **Adım adım:**
  1. `H` yazıp Enter — Tarama ayar penceresi açılır.
  2. Desen ve ölçek seçip **Tamam**.
  3. Doldurulacak kapalı alanın içine tıklayın.

### 2.12 Boyutlandırma (Ölçü Çizgileri)

Boyut araçları **📐 Boyut** sekmesindedir.

| Araç | Ne yapar | Komut |
|------|----------|-------|
| **↔ Doğrusal Ölçü** | Yatay/dikey mesafe ölçüsü | `DIM` |
| **↗ Hizalı Ölçü** | Eğik segmentlerin gerçek mesafesi | `DIMA` |
| **⊙ Yarıçap Ölçü** | Daire/yay yarıçapı | `DIMR` |
| Açısal Ölçü | İki çizgi arası açı | `DIMANG` |

**Ölçü çizmenin 3 adımı** (sekmenin sağındaki bilgi kutusunda da yazar):
1. Boyut türünü seçin (örn. **↔ Doğrusal Ölçü**).
2. Ölçülecek iki noktayı **tıklayın**.
3. Ölçü çizgisinin konumunu **tıklayın**.

**Metin Boyutu:** Aynı sekmedeki **A Küçük (125 mm) / A Normal (250 mm) / A Büyük (500 mm)** butonlarıyla ölçü yazısının yüksekliğini ayarlarsınız. Ölçüyü çizmeden önce seçin.

> 💡 **İpucu:** Zincirleme ölçüler için `DCO` (Devam Ölçüsü) komutunu kullanın — önceki ölçünün bittiği noktadan otomatik devam eder.

### Sık Sorulan Sorular (Bölüm 2)

**S: Çizgi çizerken yatay/dikey tam düz gitmiyor, hafif eğik oluyor.**
C: Alt sağdaki **ORTHO** düğmesini açın (veya **F8**'e basın). Ortho modu çizimi 90 derece açılara kilitler.

**S: Nesnenin tam köşesine/uç noktasına yapışamıyorum.**
C: Nesne yakalama (OSNAP) açık olmalı. Alt çubuktaki **OSNAP** göstergesine ve fareyle yaklaştığınızda beliren yakalama işaretlerine dikkat edin.

**S: Yanlış çizdim, nasıl geri alırım?**
C: **Ctrl+Z** (Geri Al). Tekrar getirmek için **Ctrl+Y** (İleri Al).

---

## Bölüm 3: DWG/DXF Dosya İşlemleri

**Bu bölümde ne öğreneceğiz:** Yeni proje açmayı, mimarın gönderdiği DWG dosyasını programa almayı, kaydetmeyi ve farklı formatlara (DXF, PNG, PDF, IFC) çıktı vermeyi.

### 3.1 Yeni Proje Oluşturma

1. Sol üstteki 📂 (**Yeni Proje**) düğmesine tıklayın.
2. Açılan pencerede proje adını yazın.
3. İsteğe bağlı: mimari DWG dosyasının yolunu seçin — proje klasörüne kopyalanır.
4. **Tamam**'a basın. Yeni bir çizim sekmesi açılır.

Boş bir çizim sekmesi için ise 📄 (**Yeni**) düğmesi veya **Ctrl+N** yeterlidir.

### 3.2 DWG/DXF Dosyası Açma

1. Sol üstteki 📁 (**Aç**) düğmesine tıklayın veya **Ctrl+O**.
2. Dosya türünü seçin: **Autocad DWG (*.dwg)** veya **Autocad DXF (*.dxf)**.
3. Dosyayı seçip **Aç**'a basın.
4. Alt durum çubuğunda "Dosya yükleniyor... Lütfen bekleyin." yazar. Büyük dosyalarda birkaç saniye sürebilir (program arka planda paralel işler, ekran donmaz).
5. Yükleme bitince çizim otomatik ekrana sığdırılır (Zoom Extents).

> 💡 **İpucu:** AfneyCAD dosyayı yüklerken **çok uzaktaki hatalı nesneleri otomatik temizler** (bazı DWG'lerde çizimden çok uzakta kalan "hayalet" çizgiler olur) ve nesneleri tek düzleme oturtur. Bu sayede "çizim minicik görünüyor, bulamıyorum" sorunu yaşamazsınız.

> ⚠️ **Dikkat:** Bir dosya açmak, o sekmedeki mevcut çizimin yerine geçer. Kaydedilmemiş çalışmanız varsa önce kaydedin.

### 3.3 Son Açılan Dosyalar

Sol üstteki 🕐 düğmesine tıklayın — en son açtığınız dosyaların listesi çıkar. Herhangi birine tıklayarak hızlıca açabilirsiniz.

### 3.4 Kaydetme ve Farklı Kaydet

- **Kaydet:** 💾 düğmesi veya **Ctrl+S**.
- **Farklı Kaydet:** Program size dosya adı ve konumu sorar.
- Kaydetme formatı DWG veya DXF olabilir.

### 3.5 Dışa Aktarma (Export)

Bu araçlar **5. Raporlar → Dışa / İçeri** bölümündedir:

| Buton | Ne yapar |
|-------|----------|
| **📤 DXF** | Çizimi DXF R12 formatında dışa aktarır (her programda açılır) |
| **💾 DWG** | DWG formatında dışa aktarır |
| **📊 Excel** | Tabloları/metrajı Excel dosyasına aktarır |
| **🖼 PNG** | Çizimi resim (PNG) olarak kaydeder |
| **🌐 Mobil** | Telefonda açılabilen HTML görüntüleyici üretir |
| **📄 PDF Rapor** | PDF çıktısı üretir |

**Örnek — PNG çıktı:** **5. Raporlar → 🖼 PNG** → kaydetme yeri seçin → çizimin resmi oluşur.

### 3.6 IFC (BIM) İçe/Dışa Aktarma

IFC, mimari ve mekanik programların ortak konuştuğu BIM formatıdır.

- **IFC İçe Aktar:** **5. Raporlar → 📥 IFC** butonu (veya `ifcimport` komutu).
- **IFC Dışa Aktar:** Komut satırına `ifc` (veya `bim`) yazıp Enter. Dosya masaüstüne `Project_tarih.ifc` adıyla kaydedilir.

### Sık Sorulan Sorular (Bölüm 3)

**S: DWG açtım ama ekran bomboş görünüyor.**
C: **👁 Görünüm → 🔎 Zoom Extents** butonuna basın (tüm çizimi ekrana sığdırır). Çoğu zaman çizim ekranın görünmeyen bir köşesindedir.

**S: Dosyada "okunabilir nesne bulunamadı" hatası aldım.**
C: DWG dosyası boş veya desteklenmeyen bir sürümde olabilir. Mimardan DXF olarak tekrar isteyin veya farklı bir sürümde kaydettirin.

**S: Çıktı aldığım PNG çok küçük/büyük çıkıyor.**
C: PNG almadan önce çizimi ekranda uygun şekilde konumlandırın (Zoom Extents iyi bir başlangıçtır).

---

## Bölüm 4: Katman (Layer) Yönetimi

**Bu bölümde ne öğreneceğiz:** Çizimi düzenli tutmanın anahtarı olan katmanları — oluşturmayı, renk vermeyi, gizlemeyi, dondurmayı ve MEP sistem katmanlarını yönetmeyi.

Katman, çizim nesnelerini gruplara ayıran "şeffaf kağıt tabakaları" gibidir. Örneğin duvarlar bir katmanda, soğuk su boruları başka bir katmanda tutulur; böylece istediğinizi tek tıkla gizleyebilirsiniz.

### 4.1 Aktif Katman Seçme

**1. Sistem** sekmesinin sağında **Katman** bölümü vardır. Buradaki açılır düğme (yanında renk noktası olan) o an **aktif katmanı** gösterir — çizdiğiniz her yeni nesne bu katmana gider.

1. Katman düğmesine (▾) tıklayın — katman listesi açılır.
2. Listeden bir katmana tıklayarak aktif yapın.

### 4.2 Katman Yöneticisini Açma

- **1. Sistem → Katman → 🗂** düğmesi, veya **Ctrl+L**, veya soldaki aktivite çubuğundan 🗂 ikonu.
- Sol panelde katmanların tam listesi açılır.

### 4.3 Katman Listesindeki Simgeler

Her katman satırında dört kontrol vardır:

| Simge | İşlev | Açıklama |
|-------|-------|----------|
| 👁 | **Görünürlük** | Katmanı gizler/gösterir |
| ❄ | **Dondur (Freeze)** | Katmanı gizler VE işlemlerden hariç tutar (performans için) |
| 🔒 | **Kilit (Lock)** | Katman görünür kalır ama nesneleri düzenlenemez/seçilemez |
| ▪ (renk kutusu) | **Renk** | Katmanın çizim rengi |

- **Katmanı gizlemek:** İlgili satırdaki 👁 simgesine tıklayın.
- **Adına tıklamak:** Katman adına tıklarsanız o katman **aktif** olur.

### 4.4 Gizli Katman Durumu Kaydedilir

Bir katmanı gizleyip dosyayı kaydederseniz, dosyayı tekrar açtığınızda o katman yine gizli gelir. Program gizli katman durumunu hatırlar.

### 4.5 MEP Sistem Katmanları

AfneyCAD tesisat çizerken her sistemi otomatik olarak kendi katmanına koyar. Bu katmanları tek tıkla açıp kapatabilirsiniz. **👁 Görünüm → Sistem Görünürlüğü** bölümünde:

| Buton | Katman | İçerik |
|-------|--------|--------|
| 💧 Temiz Su | `MEP_TEMIZ_SU` | Soğuk su boruları |
| 🔴 Sıcak Su | `MEP_SICAK_SU` | Sıcak su boruları |
| 🟤 Pis Su | `MEP_PIS_SU` | Atık su boruları |
| 🔥 Yangın | `MEP_YANGIN` | Yangın tesisatı |
| ⛽ Gaz | `MEP_GAZ` | Doğalgaz hattı |
| 🌀 Havaland. | `MEP_HAVALANDIRMA` | HVAC kanalları |

- Bir sistemi göster/gizle: ilgili butona tıklayın.
- **✅ Tümünü Göster:** Bütün sistem katmanlarını yeniden görünür yapar.

> 💡 **İpucu:** Karmaşık bir projede sadece pis su hattına odaklanmak isterseniz, diğer tüm sistemleri gizleyip yalnızca 🟤 Pis Su'yu açık bırakın.

**Kat Senkron:** **5. Raporlar → 🔄 Kat Senkron** butonu, çok katlı projelerde MEP katmanlarını katlar arası eşitler.

### Sık Sorulan Sorular (Bölüm 4)

**S: Bir katmanı gizledim ama nesneleri hâlâ seçime takılıyor.**
C: Gizlemek yerine **Dondur (❄)** kullanın — dondurulan katman işlemlerden tamamen hariç kalır. Ya da düzenlemeyi engellemek için **Kilit (🔒)** kullanın.

**S: Yeni çizdiğim boru yanlış renkte/katmanda çıkıyor.**
C: Çizmeden önce **aktif katmanı** doğru seçtiğinizden emin olun. MEP boruları zaten sistem tipine göre otomatik katmanlanır.

---

## Bölüm 5: MEP Tesisat Çizimi — Ana İş Akışı

**Bu bölümde ne öğreneceğiz:** Gerçek bir projeyi baştan sona: mimariyi programa almak, odaları tanımlamak, vitrifiye yerleştirmek, boruları çizmek ve kolonları kurmak.

Bu bölüm rehberin kalbidir. Örnek senaryomuz: **3 katlı bir konut binası, her katta bir banyo ve bir mutfak.** Ribbon sekmeleri (1→2→3→4→5) tam da bu iş akışının sırasını izler.

### 5.1 Mimari Çizimi Programa Almak

**Amaç:** Mimarın DWG'sini açıp, program içinde "duvar", "kolon", "kapı" olarak tanınır hale getirmek.

**Adım 1 — Dosyayı açın.**
📁 (Aç) → mimari DWG dosyasını seçin. (Bkz. [Bölüm 3.2](#32-dwgdxf-dosyası-açma))

**Adım 2 — Ölçeği kontrol edin.**
Çizimin gerçek ölçüde olduğundan emin olun:
1. **🏗 AutoBLD → 📏 Uzaklık** butonu (veya `DIST` komutu).
2. Bildiğiniz bir mesafenin (örneğin bir kapı genişliği, ~90 cm) iki ucunu tıklayın.
3. Durum çubuğunda çıkan değer 900 mm civarındaysa ölçek doğrudur. Çok farklıysa mimardan doğru ölçekte dosya isteyin.

**Adım 3 — Mimari elemanları tanıtın.**
1. **🏗 AutoBLD → 🧱 Eleman Tanı** butonu (veya `archdetect` / `AD` komutu).
2. Program, DWG katman adlarında `DUVAR / KOLON / KAPI / PENCERE / KIRIS` gibi anahtar kelimeleri arar ve bunları gerçek mimari nesnelere dönüştürür.
3. Sonuç penceresi kaç duvar, kolon, kapı, pencere bulunduğunu bildirir.

> ⚠️ **Dikkat:** Eleman tanıma, DWG katmanlarının anlamlı adlandırılmış olmasına bağlıdır. "Layer1", "0" gibi adlarda tanıma yapılamaz. Bu durumda duvarları elle çizmeniz veya mimardan düzgün katmanlı dosya istemeniz gerekir.

**Adım 4 — (Çok katlı için) katı bloklayın.**
Her katı ayrı bir "kat dosyası" olarak hazırlamak için:
1. **🏗 AutoBLD → 💾 WBlock** butonu (veya `wblock` komutu). Sihirbaz açılır.
2. **Taban noktası** seçin (tüm katlarda aynı referans nokta olmalı — örneğin asansör kuyusu köşesi).
3. Kata ait nesneleri **seçin**.
4. Kat adını verip **kaydedin**. Program ölçeği otomatik algılar ve kat dosyasını üretir.

**Adım 5 — Bina ayarlarını yapın.**
1. **1. Sistem → Bina Ayarları → 🏠 Özellikler** ile kat yüksekliği gibi bina bilgilerini girin.
2. **📶 Kat Yön.** ile katları tanımlayın (örn. Zemin 0 m, 1. Kat +3.0 m, 2. Kat +6.0 m).
3. **1. Sistem → ✅ Ayarları Onayla** butonuna basın. **Bu, 2. Uç Noktalar sekmesinin kilidini açar.**

### 5.2 Mahal (Oda) Tanımlama

**Amaç:** Her odayı program içinde "banyo", "mutfak" gibi tanımlamak — böylece uygun vitrifiye seti otomatik gelir.

Bu araçlar **2. Uç Noktalar → Mahal Tanımı** bölümündedir. Dört yöntem vardır:

**Yöntem A — Otonom (en hızlı):**
1. **🤖 Otonom** butonuna basın.
2. Program tüm mimariyi tarar ve kapalı tüm odaları otomatik bulur.

**Yöntem B — Akıllı (tek oda):**
1. **🏠 Akıllı** butonuna basın.
2. Tanımlamak istediğiniz odanın **içine tıklayın**.
3. Program duvarları takip ederek oda sınırını otomatik bulur.
4. Açılan **Mahal Etiketi** penceresinde oda tipini seçin (Banyo, WC, Mutfak, vb.) ve **Tamam**.
5. Program sorar: *"Odaya uygun vitrifiyeler otomatik yerleştirilsin mi?"* → **Evet** derseniz banyoya lavabo, klozet, duş otomatik yerleşir (TS 1258 standart seti).

**Yöntem C — Manuel:**
1. **📍 Manuel** butonu.
2. Oda köşelerini tek tek tıklayarak sınırı elle çizin.

**Yöntem D — Dikdörtgen:**
1. **⬛ Dikdörtgen** butonu.
2. İki köşe tıklayarak dikdörtgen oda oluşturun.

**Onaylama:** Tüm odaları tanımladıktan sonra **2. Uç Noktalar → ✅ Yükleri Onayla** butonuna basın. **Bu, 3. Tesisat sekmesinin kilidini açar.**

> 💡 **İpucu:** Tanımladığınız bir odayı incelemek için soldaki aktivite çubuğundan 🔍 (Mahal Analizi) ikonuna basıp odaya tıklayın — alan, vitrifiye sayısı gibi detayları gösterir.

### 5.3 Boru Çizimi

**Amaç:** Sistemleri (soğuk/sıcak/pis su vb.) borularla çizmek.

Boru araçları **3. Tesisat → Boru Çizimi** bölümündedir. Çizmeden önce üç ayarı yapın:

1. **Sistem:** Açılır listeden sistem tipini seçin — Soğuk Su, Sıcak Su, Pis Su, Yangın, Gaz veya Yağmur. (Program malzemeyi otomatik seçer: temiz/sıcak su için PPRC, pis/yağmur için PVC, yangın/gaz için çelik.)
2. **DN:** Boru çapını seçin — 15, 20, 25, 32, 40, 50, 65, 75, 80, 100, 125, 150 (mm).
3. **Eğim %:** Pis su için eğim seçin (örn. %2). Basınçlı sular için 0 bırakın.

**Boru çizmek:**
1. **🔵 Boru Çiz** butonu (veya `P` komutu).
2. Borunun başlangıç noktasını **tıklayın**.
3. Sonraki noktaları **tıklayarak** hattı çizin.
4. Bitirmek için **Enter** / **Esc**.

**Örnek:** Banyoda soğuk su hattı için → Sistem: **Soğuk Su**, DN: **20**, Eğim: **0** → **🔵 Boru Çiz** → kolondan lavaboya doğru tıklayın.

**Diğer boru araçları:**
- **⇉ Çift Hat:** Sıcak ve soğuk suyu tek işlemde paralel çizer (TS 1258).
- **⬌ Paralel Boru:** Boruyu bir duvara paralel çizer.

> 💡 **İpucu:** Boruları düz çizmek için **ORTHO** modunu açın (**F8**). Köşe ve uç noktalara tam yapışmak için OSNAP işaretlerine dikkat edin.

### 5.4 Vitrifiye (Armatür) Yerleştirme

**Amaç:** Lavabo, klozet, duş gibi cihazları çizime koymak.

Bu araçlar **2. Uç Noktalar → Kütüphane** bölümündedir.

**Duvara cihaz yerleştirme:**
1. **🔧 Fixture** (Duvara Cihaz Yerleştir) butonu.
2. Cihazı yerleştirmek istediğiniz duvarı/noktayı **tıklayın** — cihaz duvara hizalı oturur.

**Kütüphaneden seçme:**
1. **📚 Library** (Reseptör Kütüphanesi) butonu — mevcut cihaz tiplerini gösterir.
2. **🗂 Katalog** ile kendi vitrifiye kataloğunuzu yönetebilirsiniz.

**Armatürleri ana hatta bağlama:**
1. **3. Tesisat → Akıllı Bağlantı → 🔗 Bağla** butonu.
2. Program tüm armatürleri en yakın ana boru hattına TS 1258'e göre otomatik bağlar.

Alternatif — belirli cihazları bağlamak:
1. Bağlanacak cihazları **seçin**.
2. **3. Tesisat → 🤖 Oto Bağlantı** butonu.

### 5.5 Kolon (Riser) Tasarımı

**Amaç:** Katlar arası dikey boru hatlarını (kolon) kurmak ve suyun geldiği kaynak noktayı tanımlamak.

**Kolon çizimi:**
1. Komut satırına `kolon` (veya `riser`) yazıp Enter.
2. Kolonun geçtiği noktayı tıklayın; program dikey hattı oluşturur.

**Kolon konumu önerisi:** **3. Tesisat → 📍 Kolon Konumu** butonu, vitrifiyelerin merkezine göre en uygun kolon (XY) konumunu önerir.

**Kaynak (Source) noktası:**
1. Komut satırına `source` (veya `sp`) yazıp Enter.
2. Suyun sisteme girdiği noktayı (ana giriş, saat/vana) tıklayın. Hesaplama bu noktadan başlar.

**Yatay-dikey bağlantı:** **3. Tesisat → 📐 Kolon Bağlantısı** butonu, yatay kat hattını dikey kolona bağlar.

**Çok katlı — kat kopyalama:**
Bir katın tesisatını bitirdikten sonra aynısını diğer katlara kopyalayabilirsiniz:
1. **🏗 AutoBLD → 📋 Kat Kopyala** (Çok Katlı Yöneticisi) butonu.
2. Kaynak katı ve hedef kat(lar)ı seçin; tesisat yukarı kopyalanır.
3. Tüm katları 3D birleştirip görmek için **1. Sistem → 🏢 Çok Katlı** yöneticisini kullanın.

**Onaylama:** Tesisatı bitirince **3. Tesisat → ✅ Boruyu Onayla** butonuna basın. **Bu, 4. Hesap sekmesinin kilidini açar.**

### Sık Sorulan Sorular (Bölüm 5)

**S: "Eleman Tanı" hiçbir şey bulamadı.**
C: DWG katman adları anlamlı değildir (örn. hepsi "0" katmanında). Duvarları elle çizin ya da mimardan `DUVAR`, `KOLON` gibi adlandırılmış katmanlı dosya isteyin.

**S: Akıllı oda tanıma sınırı bulamıyor.**
C: Oda tam kapalı olmalı. Duvarlarda boşluk (kapı yeri açık) varsa program sınırı kapatamaz. Kapı boşluklarını geçici çizgiyle kapatın veya **Manuel/Dikdörtgen** yöntemini kullanın.

**S: Bir sonraki sekmeye geçemiyorum, gri kalıyor.**
C: Bulunduğunuz sekmedeki yeşil **✅ ... Onayla** butonuna basmayı unutmayın. Akış: Ayarları Onayla → Yükleri Onayla → Boruyu Onayla.

---

## Bölüm 6: Hidrolik Hesap

**Bu bölümde ne öğreneceğiz:** Çizdiğiniz tesisatın mühendislik hesaplarını yaptırmayı — debi, boru çapı, basınç kaybı ve pompa seçimi.

Hesap araçları **4. Hesap** sekmesindedir (3. Tesisat'ta "✅ Boruyu Onayla" sonrası açılır).

### 6.1 Sistemi Doğrulama (Önce Bunu Yapın)

Hesaptan önce sistemin hatasız olduğundan emin olun:
1. **4. Hesap → 🛡 Sistem Check** butonu.
2. Program açık uçları, ters yönlü debileri ve bağlantısız cihazları arar.
3. Sorun varsa listeler; düzeltip tekrar deneyin.

### 6.2 Tüm Sistemi Hesaplama (TS 1258)

1. **4. Hesap → ⚡ HESAPLA** butonu.
2. Durum çubuğunda "Hidrolik analiz yapılıyor (TS 1258)..." yazar ve alt kısımda ilerleme çubuğu belirir.
3. İşlem arka planda yapılır (ekran donmaz). Bitince özet penceresi çıkar:
   - Akış yükleri (FU) hesaplandı
   - Boru çapları otomatik optimize edildi
   - Kritik hat basınç kayıpları güncellendi

### 6.3 Otomatik Boru Çaplandırma

Sadece boruları standartlara göre otomatik boyutlandırmak için:
1. **4. Hesap → 📊 Sistem Analizi** butonu.
2. Program tüm boruları TS 1258'e göre yeniden çaplandırır (her boru debisine uygun DN'ye ayarlanır).

### 6.4 Basınç Kaybı (Kritik Hat) Raporu

1. **4. Hesap → 📉 Basınç Kaybı** butonu.
2. Program en zorlu (kritik) hattı bulur ve segment segment basınç kaybını gösteren bir rapor penceresi açar.

> ⚠️ **Dikkat:** Rapor için sistemde en az bir **kolon (riser)** veya giriş noktası bulunmalıdır. Yoksa "kritik hat tespit edilemedi" uyarısı gelir — bağlantıların eksiksiz olduğundan emin olun.

### 6.5 Pompa Seçimi

1. **4. Hesap → 💧 Pompa Seçimi** butonu.
2. Program gerekli debi (Q) ve basma yüksekliğini (H) hesaplayıp Q-H eğrisine göre uygun pompayı önerir.

### 6.6 Çakışma Analizi

MEP borularının mimariyle veya birbiriyle çakışmasını bulmak için:
1. **4. Hesap → 💥 Çakışma Analizi** butonu.
2. **🔴 Çakışma Vurgusu** ile çakışan noktaları çizim üzerinde kırmızı vurgulayabilirsiniz.
3. **🌡 Basınç Haritası** ile sistemdeki basınç dağılımını renkli görebilirsiniz.

### Sık Sorulan Sorular (Bölüm 6)

**S: Hesapla'ya bastım ama çaplar değişmedi.**
C: Önce **🛡 Sistem Check** ile açık uç olmadığından emin olun. Bağlantısız borular hesaba katılmaz.

**S: Basınç kaybı raporu boş çıkıyor.**
C: Sistemde kaynak (source) noktası ve kolon tanımlı olmalı. [Bölüm 5.5](#55-kolon-riser-tasarımı)'e dönüp kaynak noktası ekleyin.

---

## Bölüm 7: Raporlar ve Çıktılar

**Bu bölümde ne öğreneceğiz:** Projeden metraj (malzeme listesi), hesap föyleri, HTML raporlar ve keşif listesi üretmeyi.

Rapor araçları **5. Raporlar** sekmesindedir.

### 7.1 Metraj (BOM / BOQ) — Malzeme Listesi

- **Ne yapar:** Projedeki tüm boru, vitrifiye ve ekipmanı sayıp poz numarası ve birim fiyatla listeler.
- **Nasıl:** **5. Raporlar → 📝 Metraj (BOQ)** butonu (veya `metraj` / `bom` komutu).
- Açılan pencerede malzeme kalemleri, miktarlar, poz numaraları ve toplam maliyet görünür.

**Seçime göre metraj:** Sadece belirli nesnelerin metrajı için:
1. İlgili nesneleri **seçin**.
2. Komut satırına `secimmetraj` (veya `sm`) yazıp Enter.
3. HTML rapor isteyip istemediğiniz sorulur.

**HVAC metrajı:** Kanal sistemleri için `hvacbom` komutu.

### 7.2 Pis Su Hesap Föyü

- **Ne yapar:** Pis su borularını Manning formülü ve DU (sarfiyat birimi) yöntemiyle hesaplar; foseptik, emdirme çukuru ve pompa hesabı da içerir.
- **Nasıl:** **3. Tesisat → 📋 Pis Su Hesap Föyü** butonu.

Açılan pencerede **4 sekme** vardır:

**Sekme 1 — Hesap Föyü:** Her boru segmenti için boy, DU, debi (Q), DN, eğim, hız, doluluk oranı ve uyarılar tablo halinde. Üstteki **⚙ Hesapla** ile hesaplanır, **📥 Çizimden Güncelle** ile çizimdeki güncel borular alınır. Alttaki özet şerit toplam segment, toplam boy ve uyarı sayısını gösterir.

**Sekme 2 — Devre Seçenekleri:** Hesap yöntemi (DU / DIN), bina tipi (Konut/Ofis/Otel/Hastane/Endüstriyel), boru cinsi, Manning pürüzlülük katsayısı, ana eğim ve maksimum doluluk gibi parametreler.

**Sekme 3 — Çukur / Foseptik / Pompa:** Üç bağımsız hesap:
- **Foseptik/Kapalı Çukur (TS 8358):** Kişi sayısı, kişi başı günlük su (örn. 150 lt), bekleme süresi ve çamur faktörü girip **Foseptik Hesapla**.
- **Emdirme Çukuru — Perkolasyon (TS 7880):** Perkolasyon hızı, güvenlik faktörü, çukur derinliği ve çapı girip **Emdirme Hesapla**.
- **Pis Su Pompası:** Giriş debisi, statik yük ve sump hacmi girip **Pompa Hesapla**.

**Sekme 4 — Keşif Listesi:** **🔄 Listele** ile poz no + birim fiyatlı keşif tablosu üretilir. **📊 CSV Dışa Aktar** veya **🌐 HTML Keşif** ile dosyaya alınır. (Fiyatlar dahili 2024 katalogundan gelir — bkz. [Bölüm 9](#bölüm-9-poz-kataloğu-ve-birim-fiyat).)

Pencere altındaki butonlarla **📄 HTML Rapor**, **🏛 Kolon Şeması** üretebilir ve **✏ Çizimi Güncelle** ile hesaplanan çapları çizime yansıtabilirsiniz.

> 💡 **Örnek:** 10 kişilik bir konut için foseptik: Kişi Sayısı **10**, Günlük Su **150** lt, Bekleme **3** gün → **Foseptik Hesapla** → gerekli hacim önerilir.

### 7.3 Hidrolik Rapor (HTML)

- **Nasıl:** **5. Raporlar → 📈 Hidrolik Rapor** butonu.
- Tarayıcıda açılan detaylı HTML rapor üretir (debiler, çaplar, basınç kayıpları).

### 7.4 Hesaplama Tablosu ve Sistem Raporu

- **📊 Hesaplama Tab.:** Hesap sonuçlarını çizime tablo olarak ekler (DN senkronlu).
- **📄 Sistem Raporu:** HTML/CSV/RTF formatında sistem raporu.

### 7.5 Kolon Şeması

- **Ne yapar:** Katlar arası dikey tesisat şemasını çizer.
- **Nasıl:** **5. Raporlar → 📐 Kolon Şeması** butonu. HTML/DXF/PNG olarak alınabilir.
- **İzometrik Şema:** **📊 İzometrik Şema** butonu 3B izometrik görünüm üretir.

### 7.6 TS 825 Isı Yalıtım Hesabı

- **Ne yapar:** Bina elemanlarının ısı yalıtımını, U değerini ve yıllık enerji ihtiyacını TS 825'e göre hesaplar.
- **Nasıl:** **3. Tesisat → 🧱 TS 825 Isı Yalıtım** butonu.

Pencere üç kolondan oluşur:
- **Sol (Girdiler):** İklim bölgesi (1. Bölge İzmir/Antalya … 4. Bölge Erzurum/Kars), yapı elemanı (Dış Duvar / Çatı / Döşeme / Pencere), alan, iç sıcaklık, dış sıcaklık ve yalıtım λ değeri. Sol altta seçilen elemanın TS 825 sınır U değeri gösterilir.
- **Orta (Katmanlar):** Yapı katmanlarını (iç → dış) malzeme, λ ve kalınlıkla girersiniz. **➕ Örnek Katmanlar** ile hazır set gelir.
- **Sağ (Sonuçlar):** **🧮 Hesapla** ile U değeri, ısı kaybı ve yıllık enerji hesaplanır.

Alttan **🌐 HTML Rapor** üretilir veya **📝 Çizime Metin Ekle** ile sonuç çizime yazılır.

### 7.7 Diğer Raporlar

| Buton | Ne yapar |
|-------|----------|
| **📜 Lejant** | Çizimde kullanılan sembollerin açıklama tablosunu ekler |
| **📋 Şartname** | Teknik şartname metni üretir |
| **📐 Çapları Yazdır** | Boru çaplarını çizime otomatik etiketler |
| **🗑️ Temizle** | Otomatik etiketleri siler |

### Sık Sorulan Sorular (Bölüm 7)

**S: Metraj boş / eksik çıkıyor.**
C: Önce **4. Hesap → ⚡ HESAPLA** ile sistemi hesaplatın. Hesaplanmamış boruların çapı belirsiz olabilir.

**S: HTML rapor açılmıyor.**
C: Rapor varsayılan tarayıcınızda açılır. Dosya kaydedildiği klasörde durur; oradan da çift tıklayarak açabilirsiniz.

---

## Bölüm 8: Pafta Düzeni ve Çıktı Alma

**Bu bölümde ne öğreneceğiz:** Kat çizimlerini tek bir pafta sayfasında toplayıp antetli, baskıya hazır çıktı üretmeyi.

### 8.1 Ekran Çizimi (Viewport Capture)

- **Ne yapar:** O an ekranda görünen kat çizimini bir "blok" olarak yakalar. Her kat için ayrı bir anlık görüntü alırsınız.
- **Nasıl:** **5. Raporlar → 📷 Ekran Çizimi** butonu.
- Her katı ekrana getirip sırayla yakalayın; yakaladığınız bloklar pafta düzeninde kullanılacaktır.

### 8.2 Pafta Düzeni Penceresi

**5. Raporlar → 📄 Pafta Düzeni** butonuyla açılır. Pencere iki liste içerir:

**Sol — Mevcut Bloklar:** Yakaladığınız kat çizimleri. Her satır ad, nesne sayısı ve durumu gösterir.
- **X / Y Offset (mm):** Bloğun paftadaki konumunu elle girebilirsiniz. Boş bırakırsanız program otomatik yerleştirir.
- **➕ Seçiliyi Ekle:** Soldan seçtiğiniz bloğu paftaya koyar.
- **🏗 Tüm Katları Ekle:** Tüm katları yakalayıp paftaya ızgara (grid) düzeninde otomatik yerleştirir — en pratik yöntem.

**Sağ — Paftadaki Bloklar:** Paftaya yerleştirdiğiniz bloklar; blok adı, X/Y konum, ölçek ve durumla listelenir.
- **💥 Patlat (Bir Kez!):** Seçili bloğu tekil çizgilere ayırır.
- **🗑 Kaldır:** Bloğu paftadan çıkarır.

### 8.3 Antet Ekleme

Pencere altındaki **🗒 Antet Ekle** butonu, paftaya proje adı/tarih/ölçek gibi bilgileri içeren antet kutusu ekler. (Antet ayrıca **🏗 AutoBLD → 🗒 Pafta Antet** ve **5. Raporlar → 🗒 Antet** üzerinden de eklenebilir.)

### 8.4 DXF Çıktısı ve Merge

- **💾 DXF Çıktı:** Paftayı DXF olarak kaydeder.
- **📤 DXF Merge (Tümü):** Paftadaki tüm blokları patlatıp **tek bir DXF dosyasında** birleştirir — başka programlara (AutoCAD dahil) temiz aktarım için idealdir.
- **💥 Tümünü Patlat:** Tüm blok referanslarını tek seferde patlatır.

> ⚠️ **Dikkat:** **Patlat işlemi yalnızca bir kez yapılmalıdır.** Patlatılmış bir bloğu tekrar patlatmayın; çizim bozulabilir. Pencere alt kısmında bu uyarı sürekli gösterilir.

### 8.5 Doğrudan Yazdırma

- **5. Raporlar → 🖨️ Yazdır** butonu ile çizimi A3/A4 yazıcıya gönderebilirsiniz.
- **Baskı Önizleme:** Sol üstteki 🖨 (Baskı Önizleme) düğmesi.

### Sık Sorulan Sorular (Bölüm 8)

**S: Katları yakaladım ama pafta düzeninde görünmüyorlar.**
C: Pafta Düzeni penceresinde **🔄 Yenile** butonuna basın. Ayrıca her katı yakalarken ekranda o katın göründüğünden emin olun.

**S: Patlat butonu pasif (gri).**
C: Önce sağ listeden bir blok seçmelisiniz. Blok seçili değilken patlat/kaldır butonları kapalıdır.

---

## Bölüm 9: Poz Kataloğu ve Birim Fiyat

**Bu bölümde ne öğreneceğiz:** Keşif ve metrajda kullanılan birim fiyatların nereden geldiğini ve kendi fiyat listenizi nasıl yükleyeceğinizi.

### 9.1 Dahili 2024 Katalog

AfneyCAD, Çevre-Şehircilik Bakanlığı (ÇŞİB) 2024 birim fiyat listesine dayalı **dahili bir poz kataloğu** ile gelir (KDV hariç, yaklaşık değerler). İş grupları:

| Grup | İçerik | Örnek |
|------|--------|-------|
| 22 | Temiz Su boruları | PP-R PN20 DN20 — 180 ₺/m; Çelik boru DN50 — 1.300 ₺/m |
| 23 | Sıcak Su boruları | PP-R PN25 DN25 — 280 ₺/m |
| 27 | Pis Su / Yağmur / Vitrifiye | PVC-U DN100 — 520 ₺/m; Lavabo — 2.800 ₺/adet; Alafranga klozet — 5.500 ₺/adet |
| 28 | Yangın tesisatı | Galvanizli çelik DN50 — 1.450 ₺/m |
| 29 | Gaz tesisatı | Çelik gaz borusu DN25 — 750 ₺/m |

Keşif listesi ve metraj oluşturduğunuzda, program boru çapına ve cihaz tipine göre uygun poz kalemini ve fiyatını **otomatik eşleştirir**.

> 💡 **İpucu:** Fiyatlar "anlık" (snapshot) çalışır: keşif oluşturulduğu andaki fiyat proje kaydında korunur. Katalog sonradan güncellense bile eski keşif bozulmaz.

### 9.2 Kendi Kataloğunuzu CSV ile Yükleme

Güncel/kurumsal birim fiyatlarınızı yüklemek için:

1. **5. Raporlar → 📥 Poz CSV İçe** butonu.
2. CSV dosyanızı seçin.

**CSV formatı** (başlık satırı zorunlu, ayraç `;` veya `,` olabilir):

```
PozNo;Tanim;Birim;BirimFiyat;IsGrubu
22.010/1;PP-R PN20 boru DN20;m;195;22-Temiz Su
27.103;Lavabo - seramik;adet;2950;27-Vitrifiye
```

- **PozNo, Tanim, Birim, BirimFiyat** zorunludur; **IsGrubu** boş kalırsa "Import" atanır.
- `#` ile başlayan satırlar ve boş satırlar atlanır.
- Aynı poz numarası varsa **sizin CSV'niz dahili değerin yerine geçer** (override). Diğer dahili pozlar korunur.
- İçe aktarma sonunda kaç kalem eklendiği/atlandığı bildirilir.

### Sık Sorulan Sorular (Bölüm 9)

**S: CSV yükledim ama fiyatlar değişmedi.**
C: Başlık satırının doğru olduğundan emin olun (`PozNo;Tanim;Birim;BirimFiyat;IsGrubu`). Zorunlu sütun eksikse program dosyayı reddeder ve hangi başlıkların bulunduğunu gösterir. Ayrıca fiyatı ondalıklıysa `1,5` yerine `1.5` de kabul edilir.

**S: Dahili katalog fiyatları güncel mi?**
C: 2024 yaklaşık değerleridir. Kesin teklif için kendi güncel CSV'nizi yükleyin.

---

## Bölüm 10: Kısayollar ve İpuçları

**Bu bölümde ne öğreneceğiz:** İşinizi hızlandıran tüm klavye kısayolları, komut satırı komutları ve çizim yardımcıları.

### 10.1 Klavye Kısayolları

| Kısayol | İşlev |
|---------|-------|
| **Ctrl+N** | Yeni dosya/sekme |
| **Ctrl+O** | Dosya aç |
| **Ctrl+S** | Kaydet |
| **Ctrl+Z** | Geri al |
| **Ctrl+Y** | İleri al (yeniden yap) |
| **Ctrl+C** | Kopyala |
| **Ctrl+X** | Kes |
| **Ctrl+V** | Yapıştır |
| **Ctrl+L** | Katman yöneticisi |
| **Ctrl+F** | Bul/ara |
| **F8** | Ortho modu aç/kapat |
| **Esc** | Aktif komutu iptal et |
| **Enter** | Komutu onayla/bitir |

### 10.2 Komut Satırı Komutları

Alt soldaki komut kutusuna yazıp **Enter**'a basın. En sık kullanılanlar:

**Temel çizim:**
| Komut | İşlev |
|-------|-------|
| `L` / `line` | Çizgi |
| `C` / `circle` | Daire |
| `PL` / `polyline` | Polyline |
| `rect` | Dikdörtgen |
| `O` / `offset` | Ötele |
| `TR` / `trim` | Buda |
| `EX` / `extend` | Uzat |
| `MI` / `mirror` | Ayna |
| `M` / `move` | Taşı |
| `CO` / `copy` | Kopyala |
| `X` / `explode` | Patlat |
| `H` / `hatch` | Tarama |
| `MT` / `text` | Metin |

**Ölçü:**
| Komut | İşlev |
|-------|-------|
| `DIM` / `diml` | Doğrusal ölçü |
| `DIMA` | Hizalı ölçü |
| `DIMR` | Yarıçap ölçü |
| `DIMANG` | Açısal ölçü |
| `DCO` | Devam ölçüsü |
| `DIST` | Uzaklık ölç |

**Mekanik/MEP:**
| Komut | İşlev |
|-------|-------|
| `P` / `pipe` | Boru çiz |
| `kolon` / `riser` | Kolon borusu |
| `source` / `sp` | Kaynak noktası |
| `bagla` / `cf` | Cihaz bağla |
| `mahal` / `ma` | Mahal tanımla |
| `man` | Mahal analizi |
| `ks` | Kolon şeması |
| `duct` / `kanal` | HVAC kanal |
| `dc` | Kanal bağla |

**Mimari / Blok:**
| Komut | İşlev |
|-------|-------|
| `block` / `b` | Blok tanımla |
| `insert` / `i` | Blok ekle |
| `wblock` | WBlock sihirbazı |
| `rec` | Mimari tanı |
| `AD` / `archdetect` | Mimari eleman algıla |
| `mb` / `archbom` | Mimari metraj |

**Rapor / Dosya:**
| Komut | İşlev |
|-------|-------|
| `metraj` / `bom` | Metraj raporu |
| `sm` / `secimmetraj` | Seçim metrajı |
| `leg` / `legend` | Lejant |
| `spec` / `sartname` | Şartname |
| `dxf` / `saveas` | DXF dışa aktar |
| `ifc` / `bim` | IFC dışa aktar |
| `ifcimport` | IFC içe aktar |
| `print` / `plot` | Yazdır |
| `kabul` / `validate` | Tesisatı doğrula |

### 10.3 OSNAP (Nesne Yakalama)

OSNAP, farenin mevcut nesnelerin özel noktalarına (uç, orta, merkez, kesişim) tam olarak yapışmasını sağlar. Doğru çizim için şarttır.
- Alt çubuktaki **OSNAP** göstergesi yakalamanın durumunu gösterir.
- Fareyi bir çizginin ucuna yaklaştırdığınızda beliren küçük kare/işaret, oraya yapışacağınızı gösterir.

> 💡 **İpucu:** Borular ve çizgiler tam köşeye oturmuyorsa, büyük olasılıkla yakalama işaretini görmeden tıklıyorsunuzdur. Tıklamadan önce yakalama işaretinin belirmesini bekleyin.

### 10.4 Ortho Modu (F8)

Ortho açıkken çizim yalnızca yatay ve dikey (90°) yönlerde ilerler. Düz boru ve duvar çizerken açın, serbest/eğik çizim için kapatın.

### 10.5 Zoom ve Pan (Kaydırma)

| Hareket | İşlev |
|---------|-------|
| Fare tekerleği ileri/geri | Yakınlaş / uzaklaş |
| Tekerleğe basılı tutup sürükle | Ekranı kaydır (Pan) |
| **👁 Görünüm → 🔎 Zoom Extents** | Tüm çizimi ekrana sığdır |

> 💡 **İpucu:** "Çizimi kaybettim" hissine kapılırsanız panik yapmayın — **Zoom Extents** her şeyi geri ekrana getirir.

### 10.6 2D / 3D Görünüm

- **👁 Görünüm → 2D Görünüm:** Standart plan görünümü (üstten).
- **👁 Görünüm → 3D Görünüm:** Aksonometrik 3B görünüm.
- **🧊 3D Boru:** Boruları hacimli (3B) gösterir.

### 10.7 Genel Çalışma İpuçları

1. **Sırayı takip edin:** 1. Sistem → 2. Uç Noktalar → 3. Tesisat → 4. Hesap → 5. Raporlar. Bu akış sizi hatasız sonuca götürür.
2. **Durum çubuğunu okuyun:** Program bir sonraki adımı hep alt-orta kısımda yazar.
3. **Sık kaydedin:** Ctrl+S alışkanlık olsun. (Program AutoSave de yapar, ama garanti olsun.)
4. **Katmanları düzenli tutun:** Sistem katmanlarını göster/gizle özelliğiyle karmaşayı yönetin.
5. **Önce doğrula, sonra hesapla:** 🛡 Sistem Check, hatalı hesabın önüne geçer.

### Sık Sorulan Sorular (Bölüm 10)

**S: Komut satırına yazdım ama "Bilinmeyen komut" diyor.**
C: Komutu yanlış yazmış olabilirsiniz. Bu tablodaki kısaltmaları birebir kullanın (örn. boru için `P`, buda için `TR`).

**S: F8'e bastım ama bir şey değişmedi.**
C: Ortho modu bir "sessiz" ayardır. Değişimi alt çubuktaki **ORTHO** düğmesinin renginden (aktifken mavi/parlak) anlayabilirsiniz. Fark, çizim yaparken belli olur.

**S: Programı en verimli nasıl öğrenirim?**
C: Küçük bir örnek projeyle (tek katlı, tek banyolu) [Bölüm 5](#bölüm-5-mep-tesisat-çizimi--ana-i̇ş-akışı)'i baştan sona bir kez uygulayın. Tüm akışı bir kez yaşadığınızda gerisi çok kolaylaşır.

---

## Kapanış

Bu rehber AfneyCAD'in tüm ana özelliklerini kapsar. Unutmayın: program sizi adım adım yönlendiren bir akışla (numaralı sekmeler ve "✅ Onayla" butonları) tasarlanmıştır. Kaybolduğunuzu hissederseniz durum çubuğundaki mesajları okuyun ve akışın sırasını takip edin.

İyi çalışmalar!

---
*AfneyCAD — MEP Tesisat CAD Yazılımı · Sürüm 4.0 · Windows 10/11*
