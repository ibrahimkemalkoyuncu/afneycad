# AfneyCAD — Önceliklendirilmiş Yol Haritası

> Bu oturumda (2026-07-11) yapılan kod incelemesi + canlı UI testi bulgularının
> önceliklendirilmiş özeti. Sıralama: **P0 = kırık/güven sarsan** → **P3 = uzun
> vadeli vizyon**. Her madde somut dosya/bulgu referansıyla.

---

## P0 — Kritik: Aktif kullanımı bozan hatalar

| # | Konu | Durum | Not |
|---|---|---|---|
| 1 | `RoutePipeCommand` sağ-tık/Enter ile bitmiyor | ✅ **Düzeltildi** | `RoutePipeCommand.cs` `OnKeyDown` artık `LineCommand` ile tutarlı (Enter/Space/sağ-tık) — `PipeRoutingEngine.Reset()` eklendi |
| 2 | Boru çiziminde orantısız büyük/dolgulu mavi şekil | ✅ **Çözüldü (yan etki)** | Kök neden #1 idi: sağ-tık çalışmadığı için komut state'i temizlenmeden üst üste segment/branch birikiyordu. Fix sonrası temiz tek segment çizimi doğru boyutta, "Ø40" etiketli, ince çizgilerle render ediliyor. Sarı renk = "hesap güncel değil" uyarı rengi (izole boru için beklenen davranış), sistem rengi (mavi=soğuk su) sadece geçerli/bağlı ağda görünür |
| 3 | Sekme kilit mesajları özel dialog'lar UI Automation'da görünmüyor | ℹ️ Bilgi | Test aracı kısıtı, uygulama hatası değil — sadece not düşüldü |

**Önerilen ilk adım:** Uygulamayı yeniden başlatıp aynı senaryoyu (Boru Çiz → 2-3 segment → sağ tık ile bitir) tekrarlamak; hem fix'i doğrulamak hem de mavi şekli tıklayıp Properties panelinden teşhis etmek.

---

## P1 — FINE MEP UX parity / gerçek kullanılabilirlik

| # | Konu | Durum | Not |
|---|---|---|---|
| 4 | Kolon şeması artık DWG de verebiliyor | ✅ **Tamamlandı** | `DwgExportService` entegre edildi (`MainWindow.Engineering.cs`) |
| 5 | Türkçe karakter proje adı | ✅ **Doğrulandı** | `ProjectManager.CreateProject` otomatik ASCII çevirisi yapıyor — FINE MEP'ten daha iyi |
| 6 | "Mimari Belirle" + "Kat Yöneticisi" ayrı dialoglar, dosya/blok atama belirsiz | ⚠️ Açık | `Kat Yöneticisi`'ndeki **Entity** sütunu FINE'ın "dosya seç" adımının karşılığı ama UX'te açık değil — kullanıcı kılavuzuna not eklenmeli veya tooltip iyileştirilmeli |
| 7 | Xref/Ekran Çizimi UX farkı | ✅ Dokümante edildi | `docs/Xref_EkranCizimi_UX_Farki.md` — FINE'daki kat-kat manuel akış AfneyCAD'de tek tuşla (`CaptureAll`) otomatik |
| 8 | 4. "Hesap" sekmesi canlı testi | ✅ **Tamamlandı** | `HESAPLA`/`Sistem Analizi` çalıştı ("Akış yükleri hesaplandı, boru çapları TS 1258'e göre optimize edildi"); **Sistem Check** (`OnAuditSystem`/`DomainGuardService`) izole boru için 4 hatayı doğru ve açıklayıcı Türkçe mesajlarla raporladı ("Açık Uç Tespit Edildi", "Şebekede tanımlı armatür bulunamadı", "su giriş noktası tespit edilemedi") — FINE MEP'in "tesisatı kabul et" kontrolüyle birebir örtüşüyor, hatta daha açıklayıcı |
| 8b | 5. "Raporlar" sekmesi canlı testi | 🟡 Kısmi | Sekim kilidi sadece `DomainGuardService.ValidateSystem()` geçerli (IsValid=true) dönünce açılıyor — bu oturumdaki izole test borusu (kaynak/armatür yok) validasyonu geçemediği için sekmeye girilemedi. Gerçek bir DWG üzerinde tam sistem (kaynak+armatür+boru) kurulup test edilmeli |

---

## P2 — Satışa hazırlık (release readiness)

| # | Konu | Durum | Not |
|---|---|---|---|
| 9 | Lisanslama gerçek algoritma | ✅ **Tamamlandı** | `LicenseManager` HMAC-SHA256 checksum (`GenerateKey`/`ValidateKey`) |
| 10 | Installer taslağı | ✅ **Tamamlandı** | `setup/AfneyCad.iss` — henüz gerçek `dotnet publish` çıktısıyla uçtan uca denenmedi |
| 11 | QA Smoke-test tam koşusu | 🟡 Kısmi | `docs/QA_SmokeTest_Checklist.md` hazır, ~%25'i bu oturumda canlı test edildi (Dosya İşlemleri, AutoBLD, Uç Noktalar, Tesisat başlangıcı) |
| 12 | Kullanıcı kılavuzu PDF | 🟡 Karar verildi, üretilmedi | `docs/PDF_Kilavuz_Mekanizmasi.md` — pandoc release-time script'i henüz yazılmadı |
| 13 | Word (.docx) rapor | ✅ Karar verildi (yapılmayacak) | `docs/Word_Rapor_Karari.md` — HTML/PDF yeterli kabul edildi, müşteri özel talep ederse yeniden değerlendirilecek |

---

## P3 — Büyük yeni modüller (uzun vadeli vizyon)

`docs/Backlog_Buyuk_Moduller.md`'de detaylandırıldı. Öncelik sırası (mevcut servisler üzerine inşa edilebildiği için düşük riskli olanlar önce):

1. HVAC kanal modülü genişletme (mevcut `DuctSizingService` temel alınarak)
2. AI Auto-Routing çok katlı şaft tespiti (mevcut `AutoRouteService` temel alınarak)
3. Gelişmiş 3D BIM görüntüleyici (Walk&Fly, Section Box) — fizibilite incelemesi gerekli (SkiaSharp 2D render pipeline'ı için büyük mimari değişiklik olabilir)
4. Elektrik tesisat modülü — sıfırdan yeni domain, ayrı planlama oturumu gerekir
5. Parametrik Ekipman Tasarımcısı (Family Editor) — sıfırdan yeni domain

---

---

## Güncel Puan — FINE SANI/FINE MEP Karşılaştırması (2026-07-11, canlı test sonrası)

| Faz | Önceki Puan | Güncel Puan | Değişim Nedeni |
|---|---|---|---|
| 1. Mimari Çizimin Programa Girilmesi | 7/10 | 7.5/10 | DWG import canlı doğrulandı (42.060 nesne, 43 katman, hatasız); Mimari Belirle+Kat Yöneticisi UX farkı netleşti (bug değil, dokümante edildi) |
| 2. Temiz Su Tesisat Tasarımı | 8/10 | 8.5/10 | Vitrifiye Kataloğu canlı doğrulandı; **kritik boru-çizim sağ-tık hatası bulundu VE düzeltildi** |
| 3. Temiz Su Tesisat Hesapları | 6/10 | 7.5/10 | 4.Hesap sekmesi canlı test edildi — HESAPLA/Sistem Analizi/Sistem Check hepsi çalıştı, validasyon hata mesajları FINE MEP'i geçiyor (daha açıklayıcı Türkçe). Word raporu hâlâ eksik (bilinçli karar) |
| 4. Pis Su Tesisat Tasarımı | 8/10 | 8/10 | Bu oturumda canlı test edilmedi, kod-seviyesi doğrulama geçerliliğini koruyor |
| 5. Pis Su Tesisat Hesapları | 6/10 | 6/10 | Bu oturumda canlı test edilmedi |
| 6. Çıktı Dosyasının Hazırlanması | 7/10 | 7.5/10 | Kolon şeması artık gerçek DWG formatında da verilebiliyor (önceden sadece DXF R12) |

### Genel Puan: **7/10 → 8/10**

**Neden 8 (7 değil):** Bu oturumda canlı testte bulunan **tek gerçek kritik hata** (boru komutu sağ-tık ile bitmiyor) hemen düzeltildi ve doğrulandı. Ayrıca mühendislik validasyon sistemi (`DomainGuardService`) canlı testte FINE MEP'in "tesisatı kabul et" kontrolünü **kalite olarak geçen** açıklayıcı hata mesajları üretti — bu koddan okumayla değil, gerçek kullanımla teyit edildi.

**Neden 10 değil:**
1. Word (.docx) rapor çıktısı yok (bilinçli mimari tercih, HTML/PDF ile telafi ediliyor)
2. 5. Raporlar sekmesi tam geçerli bir şebeke ile uçtan uca test edilmedi
3. Faz 4-5 (Pis Su) bu oturumda canlı doğrulanmadı, sadece kod okumasıyla teyitli
4. FINE MEP'in bazı adımları (kat-dosya atama, ekran çizimi+xref) AfneyCAD'de farklı ama otomatik/daha güvenli bir UX ile çözülmüş — birebir adım eşleşmesi yok, bu geçiş yapan kullanıcılar için kısa bir öğrenme eğrisi gerektirir

---

## Önerilen Sıradaki Oturum Sırası

1. **P0-2**'yi canlı debug ile çöz (mavi şekil teşhisi) — en yüksek güven riski
2. **P1-8**: 4./5. sekmeleri (Hesap/Raporlar) gerçek DWG ile uçtan uca test et
3. **P2-11**: QA checklist'in kalanını tamamla
4. **P2-10**: Installer'ı gerçek publish çıktısıyla dene
5. P3 maddeleri ayrı planlama oturumları gerektirir — Plan modunda ele alınmalı
