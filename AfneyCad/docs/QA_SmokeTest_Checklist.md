# AfneyCAD — Satış Öncesi QA Smoke-Test Kontrol Listesi

> Amaç: Sürüm paketlenmeden önce her ana dialog/komutun açılıp temel işlevini
> hatasız yerine getirdiğini doğrulamak. Her madde: aç → tipik girdiyle çalıştır →
> hata/exception yok → sonucu (çizim/rapor/hesap) gözle kontrol et.

## 1. Dosya İşlemleri
- [ ] Yeni Proje / Yeni Dosya / Yeni Pencere
- [ ] DWG Aç (büyük dosya, outlier/Z-flatten testi)
- [ ] DXF Aç
- [ ] Kaydet (Ctrl+S) / Farklı Kaydet
- [ ] DWG/DXF Export
- [ ] IFC Import / Export
- [ ] Excel / PNG / PDF / HTML Viewer / Axonometrik export

## 2. Temel Çizim
- [ ] Line, Circle, Polyline, Rectangle
- [ ] Trim, Extend, Mirror, Explode
- [ ] Move, Copy, Offset, Hatch
- [ ] Undo/Redo (Ctrl+Z/Y) — her komuttan sonra

## 3. Boyutlandırma
- [ ] Linear, Aligned, Radius, Angular DIM
- [ ] Continue Dim, Dist komutu
- [ ] Metin boyutu Küçük/Orta/Büyük

## 4. Mekanik Çizim
- [ ] Boru çizimi (DrawPipe) + sistem/DN senkronu
- [ ] Vitrifiye yerleştirme (duvara snap)
- [ ] Connect Fixture, Riser Pipe, Source Point
- [ ] HVAC kanal rotalama + bağlama

## 5. Blok İşlemleri
- [ ] Block tanımlama (BMake)
- [ ] Insert
- [ ] WBlock — mimari kat sihirbazı

## 6. Mimari
- [ ] Arch Detect (layer bazlı duvar/kolon/kapı)
- [ ] Arch BOM raporu
- [ ] Define Building (çok katlı)

## 7. Katman Yönetimi
- [ ] Katman picker aç/kapat, isim değiştir
- [ ] Görünürlük / dondurma / kilit toggle
- [ ] Sistem katman toggle (Soğuk/Sıcak/Pis/Yangın/Gaz/Havalandırma)

## 8. Mühendislik Hesaplama
- [ ] Recalculate System (hidrolik analiz)
- [ ] Auto Pipe Sizing
- [ ] Flow / Pressure Drop hesabı
- [ ] Pompa seçimi
- [ ] Mahal tanımlama (manuel + smart detect + rect)
- [ ] Connect Receptors / Auto Branching / Riser Connection
- [ ] Clash Detection + highlight toggle
- [ ] BOM üretimi (Selection / HVAC / Genel)
- [ ] Hydraulic report (HTML)
- [ ] Isometric/Riser şeması (HTML + DXF + PNG çıktı üçü de)

## 9. Özel Hesap Modülleri
- [ ] Waste Water Design + Calc Sheet (poz/katalog override dahil)
- [ ] Rain Water Calc
- [ ] Gas Calc
- [ ] Septic Tank Design (TS 7880 emdirme çukuru)
- [ ] Fire Fighting Design
- [ ] Heating Design (TS 825)
- [ ] HVAC Design
- [ ] Cooling Design
- [ ] Hot Water Circulation
- [ ] Pressure Zone Design
- [ ] Pipe Cost Analysis
- [ ] TS 825 Isı Yalıtım Hesabı

## 10. Kütüphane / Katalog
- [ ] Fixture / Valve / Architectural Library
- [ ] Manage Catalog + Poz CSV içe aktarma
- [ ] Standard Selection (TS/DIN)
- [ ] Manufacturer Catalog

## 11. Görünüm ve Araçlar
- [ ] Zoom Extents, 2D/3D toggle
- [ ] OSNAP (master + tek tek flag) — çift bölme regresyon testi (bkz. 07d8771)
- [ ] Ortho (F8)
- [ ] Direct Distance Entry (komut aktifken mesafe/koordinat girişi)
- [ ] Pipe 3D View, Multi-Story Manager, Wall Parallel Route
- [ ] BIM Properties / Smart BIM Convert
- [ ] Flow Animation toggle
- [ ] Cloud Backup (yedek oluştur + geri yükle)
- [ ] Audit System (Flow Lock validasyonu)

## 12. Lisanslama (yeni)
- [ ] Trial durumunda uygulama açılışı (lisans dosyası yok)
- [ ] Geçersiz anahtar girişi → uyarı
- [ ] Geçerli üretilmiş anahtar (`LicenseManager.GenerateKey`) → Valid
- [ ] Demo key (`AFNEY-2026-ENTP-DEMO`) → Valid
- [ ] Lisans kaldırma (deactivate)

## Raporlama
Her koşu için: tarih, build numarası, bulunan hatalar (dosya:satır ile) —
`docs/QA_SmokeTest_Sonuclari.md` içine ekle (yoksa oluştur).
