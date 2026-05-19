# AfneyCAD Kullanım Kılavuzu (User Manual)
Sürekli Güncellenen Mühendislik Rehberi

## 1. Sisteme Giriş ve Temel Arayüz
AfneyCAD, mekanik ve mimari projelerinizi BIM tabanlı yönetmek için geliştirilmiş özel bir platformdur. 
- **Sol Panel (Project Navigator):** Binanın katlarını, sıhhi tesisat ağaçlarını ve katmanları (Layers) gizleyip göstermek için kullanılır.
- **Sağ Panel (Intelligence Panel):** Tıkladığınız boru veya vitrifiyenin "Özelliklerini" canlı olarak gösteren ve değiştirmenizi sağlayan aktif bir paneledir. Buradan boru çapını veya malzeme tipini değiştirdiğinizde, AfneyCAD hidrolik ağacını otomatik olarak baştan hesaplar.
- **Orta Alan (CadViewport):** Sınırsız zoom ve yüksek FPS ile çalışan endüstriyel SkiaSharp çizim motorudur.

---

## 2. Mimari Katların Hizalanması (Alignment) ve 3D'ye Geçiş
DWG planınızda yan yana duran katmanları (Bodrum, Zemin vb.) gerçekçi bir şekilde üst üste (Z ekseninde) dizebilmek için AfneyCAD size 2 farklı yöntem sunar:

### Yöntem A: Otomatik Kılavuz (Auto-Align Origin) - *[Tavsiye Edilen]*
AfneyCAD, çizim alanının orta noktasında **Sonsuz Kırmızı (X) ve Yeşil (Y)** çizgilerden oluşan devasa bir **Orijin (0,0)** işaretleyicisine sahiptir.
1. Mimari planı seçin (Move komutu ile tutun).
2. Binanızın tüm katlarda değişmeyen "Sabit bir noktasını" (Örn: Asansör şaftının alt köşesi) belirleyin.
3. Çizimi sürükleyip doğrudan o devasa Kırmızı-Yeşil Artı (+) işaretine getirin.
> [!NOTE] 
> Sistem otomatik olarak Orijin (Sıfır) noktasına **kenetlenecektir (Snap)**.
4. Çiziminizi `Bodrum Kat.afney` olarak kaydedin veya doğrudan "Kat Yöneticisi"nden bulunduğu yüksekliği seçin. Siz tüm katları o Orjin'e sürükledikçe, sistem asansör şaftlarını otomatik olarak Z ekseninde üst üste dizecektir.

### Yöntem B: WBlock (Manual Referans Noktası)
1. Üst menüden **Bina -> Mimariyi Blokla (WBlock)** seçeneğine tıklayın.
2. Sabit noktanıza (Referans Point) tıklayın.
3. Objelerinizi seçin ve hangi kata ait olduklarını (Zemin, Bodrum) belirtip kaydedin.

---

## 3. Otomatik Bina ve Oda (Mahal) Algılama
DWG üzerinden gelen mimari sınırları el ile yeniden çizmeniz gerekmez.
- Menüden **Bina -> Akıllı Mahal Tespiti** butonuna bastığınızda, AfneyCAD sınır çizgileri (Boundary) analiz algoritmasını kullanarak odaları kapatır.
- Odaların merkezlerine, şeffaf bir çerçeve içinde `MUTFAK [12m²]` gibi profesyonel etiketler bırakır.
- Eğer oda içinde klozet veya lavabo bloğu varsa, sistem onlara otomatik "Sıhhi Tesisat Yük Birimi (LU)" atar.

---

## 4. Akıllı Tesisat (Routing) ve Otomatik Bağlama
- Üstteki Bar'dan "Çizgi Çiz (Line)" veya "Boru Çiz (Pipe)" aracını alıp kendi hattınızı çekebilirsiniz. Boruların çapı sistemin debi hesabına göre otomatik olarak değişecektir.
- **Otomatik Branşman:** Menüden "Otomatik Bağla" komutunu çalıştırırsanız, sistem seçtiğiniz en yakın boru hattından en yakın lavaboya/klozete, duvarların köşelerini (Dirsek) takip ederek kendisi hat çeker.

---

## 5. Analiz, Hesaplanma ve Çıktılar (BOM & Raporlar)
AfneyCAD tamamen standartlaştırılmış bir **Mekanik Motor (Kernel)** barındırır.
1. Sağ panelden cihazın Yük Birimini oynadığınızda her şey canlı hesaplanır.
2. Tesisat işiniz bitince ekrana "Metraj (BOM)" yazdırmak için komut girebilir, saniyeler içinde HTML formatında Dirsek/T/Boru sayımı alabilirsiniz.
3. Riser Diagram (Kolon Şeması) butonuyla tüm sistemi üç boyuttan kesip 2 boyutlu hidrolik borulama paftası alabilirsiniz.
4. Son olarak **IFC Export** butonuna basarak, mimarlarınızla Revit veya Navisworks üzerinden paylaşılacak 3 Boyutlu Binayı (LOD 200/300 standardında) hazırlamış olursunuz.
