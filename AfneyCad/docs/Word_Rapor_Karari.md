# Word (.docx) Rapor Çıktısı — Karar

## Durum
FINE MEP/OtoNET iş akışında hesap föyünden iki ayrı Word raporu üretiliyor
("yd re" — temiz su, "apox" — pis su). AfneyCAD'de `.docx` üretimi **yok**;
tüm raporlar HTML (`HydraulicReportService`, BOM/ArchBom/HvacBom ExportToHtml)
veya PDF (`PdfExportService`, SkiaSharp tabanlı) olarak üretiliyor.

## Karar: Şimdilik eklenmeyecek — HTML/PDF yeterli kabul edildi
**Gerekçe:**
1. HTML raporlar tarayıcıda "Yazdır → PDF" ile zaten Word'e yakın bir çıktı
   veriyor; PDF raporlar (`PdfExportService`) antetli/imzalı resmi doküman
   ihtiyacını zaten karşılıyor (bkz. `TitleBlockInfo`).
2. Word (.docx) üretimi için `DocumentFormat.OpenXml` gibi yeni bir bağımlılık
   eklemek ve HTML/PDF'te olmayan bir üçüncü rapor pipeline'ı bakımı gerekir —
   maliyet/fayda oranı düşük: müşterinin asıl ihtiyacı "düzenlenebilir belge"
   ise HTML zaten tarayıcıda kopyala-yapıştır ile Word'e aktarılabiliyor.
3. Hiçbir mevcut AfneyCAD kullanıcısından "Word şart" geri bildirimi gelmedi;
   bu adım salt FINE MEP geçmişinden kalma bir alışkanlık olabilir.

## Yeniden değerlendirme koşulu
Eğer bir müşteri/keşif sürecinde **düzenlenebilir Word şablonu** özel olarak
talep edilirse (örn. resmi kurum başvurusu Word formatı istiyorsa), o zaman
`DocumentFormat.OpenXml` ile HTML raporların üzerine ince bir dönüştürücü
eklenebilir — mevcut HTML üretim servisleri (`HydraulicReportService` vb.)
yeniden yazılmadan, sadece bir "HTML→docx" adaptörü olarak.

## Etkilenen dokümanlar
- `docs/Backlog_Buyuk_Moduller.md` ve QA checklist'e bu karar not düşüldü —
  Word ihtiyacı çıkarsa "Kullanıcı kılavuzu PDF" görevine benzer şekilde
  ayrı bir mekanizma kararı gerekecek.
