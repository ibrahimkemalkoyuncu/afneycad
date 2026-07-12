# Kullanıcı Kılavuzu PDF Çıktısı — Mekanizma Kararı

## Durum
`docs/Kullanici_Rehberi.md` (940 satır) ve `docs/Kullanici_kitabi.md` (2455 satır,
session-bazlı) markdown olarak mevcut. Uygulama içi `PdfExportService` proje
raporları (metraj/hesap) için SkiaSharp tabanlıdır ve markdown render etmez —
kılavuz PDF'i için ayrı, yayın-zamanı (release-time) bir araç kullanılmalı;
uygulamaya yeni bir bağımlılık eklemeye gerek yok.

## Karar: Pandoc (release script, uygulama dışı)
- Pandoc kurulum makinesinde bulunmuyor (`where pandoc` → bulunamadı) — release
  hazırlayan makineye bir kerelik kurulmalı: https://pandoc.org/installing.html
- Komut:
  ```
  pandoc docs/Kullanici_Rehberi.md -o dist/AfneyCAD_Kullanici_Kilavuzu.pdf ^
    --pdf-engine=wkhtmltopdf --toc --toc-depth=2 -V lang=tr
  ```
  (`wkhtmltopdf` da ayrıca kurulmalı; alternatif motor: `--pdf-engine=weasyprint`)
- Bu adım `setup/AfneyCad.iss` derlemesinden önce, dağıtım hazırlığı script'ine
  eklenmeli (örn. `build_release.ps1` — henüz yok, installer taslağıyla birlikte
  oluşturulmalı).

## Neden uygulama-içi bir servis DEĞİL
- Kılavuz PDF'i kullanıcıya değil, satış/dağıtım paketine dahil edilen statik bir
  belge — çalışma zamanında üretilmesine gerek yok.
- Markdown→PDF için SkiaSharp'ta sıfırdan bir renderer yazmak (tablo, başlık,
  kod bloğu desteğiyle) haftalarca sürer; pandoc bunu ücretsiz ve olgun şekilde
  çözüyor.

## Sıradaki adım
`docs/Kullanici_Rehberi.md` içeriği güncel mi kontrol edilmeli (son session
notları `Kullanici_kitabi.md` içinde, rehbere henüz taşınmamış olabilir) —
PDF üretmeden önce iki dosyanın senkron olduğu doğrulanmalı.
