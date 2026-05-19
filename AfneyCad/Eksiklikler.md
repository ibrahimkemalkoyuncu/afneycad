# AfneyCAD Geliştirme ve Eksiklik Analizi (Gap Analysis)

Bu belge, AfneyCAD'in mevcut yetenekleri ile endüstri standardı olan FINE MEP (AutoBUILD & ADAPT/FCALC) yazılımları arasındaki farkları ve geliştirilmesi gereken alanları özetlemektedir.

## 1. AutoBUILD (Mimari BIM Modelleme) Eksiklikleri

AutoBUILD, binanın 3B 'kabuğunu' oluşturan modüldür. Mevcut durumdaki eksiklikler:

*   **IFC Import (BIM Verisi İçeri Aktarma):** Şu an sadece dışa aktarım (`IfcExportService`) mevcuttur. Revit, ArchiCAD veya Allplan gibi yazılımlardan gelen IFC dosyalarını içeri aktarıp; duvarları, katları ve pencereleri otomatik olarak tanımlama özelliği eksiktir.
*   **Parametrik BIM Nesneleri:** Mevcut `ArchitecturalObstacle` yapısı, nesneleri sadece 'geometrik engel' olarak görür. FINE SANI'deki gibi duvarların ısıl geçirgenlik (U-value), katmanlı malzeme yapısı (sıva, tuğla, yalıtım) ve çift tıklandığında açılan parametrik özellik pencereleri eksiktir.
*   **Akıllı Altlık Yönetimi (Fast-Modeling):** 2B DWG/DXF mimari planlar üzerinden "akıllı seçim" ile saniyeler içinde duvarları 3B BIM nesnesine dönüştüren hızlandırıcı araçlar henüz mevcut değildir.
*   **Geniş Mimari Kütüphane:** Kolonlar, çatılar, döşemeler ve zengin iç mekan tefrişat (mobilya, bitki vb.) kütüphanesi ve bunların yerleşim araçları eksiktir.

## 2. ADAPT/FCALC (Hidrolik Hesaplama Motoru) Eksiklikleri

ADAPT/FCALC, tasarım ile hesaplama sayfaları arasındaki çift yönlü köprüdür. Mevcut durumdaki eksiklikler:

*   **Bağımsız (Standalone) Hesap Tablosu Modu:** Mevcut `CalculationTableWindow` tamamen CAD verisine bağımlıdır. ADAPT/FCALC'daki gibi CAD çizimi olmadan, verilerin manuel olarak tabloya girilip ekipman seçimi ve boru çaplandırmasının yapılabildiği "Saf Hesaplama" modu eksiktir.
*   **Gelişmiş Ekipman Kapasite Hesapları:** Pompa, hidrofor grubu, genleşme tankı ve boyler gibi kritik mekanik ekipmanların kapasite hesaplarının doğrudan ana hesap tablosu (spreadsheet) üzerinden interaktif ve dinamik olarak yönetilmesi eksiktir.
*   **Geri Besleme Döngüsü (Update Drawing):** Hesap tablosunda manuel olarak 'override' edilen veya optimize edilen verilerin (örn. çap değişimi, vana tipi değişimi) çizimdeki tüm etiketlere (`PipeLabelEntity`) ve 3B modellere hatasız, anlık ve çift yönlü senkronizasyonu geliştirilmelidir.
*   **Çoklu Standart Desteği:** Mevcut `PipeSizer` TS 1258 / DIN 1988 odaklıdır. Tablo seviyesinde farklı uluslararası standartların (ASPE, BS, ASHRAE vb.) seçilip hesap metodolojisinin anında değiştirilmesi özelliği eksiktir.

## 3. Mühendislik ve Kullanılabilirlik (Genel)

*   **Zengin Raporlama:** Hesap tablolarının teknik dosya formatında (PDF/Excel) daha detaylı, antetli ve mühendislik imzasına uygun formatta dışa aktarımı geliştirilmelidir.
*   **Çapraz Kontrol (Validation Gate):** Hesaplama öncesinde sistemdeki açık uçlar, tanımsız çaplar veya mantıksız bağlantılar için "Sistem Doğrulama Sihirbazı" eksiktir.
