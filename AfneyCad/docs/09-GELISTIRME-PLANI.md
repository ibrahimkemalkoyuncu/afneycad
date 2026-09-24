# 09 — Geliştirme Planı (Kalan İşler)

Kural: önce P0 → P1 → P2 → P3; her adım Analiz → Baseline → İzole değişiklik → Derleme → Test → Doğrulama.

## Tamamlanan
P0 (8 madde), P1 (6 madde), P2'nin ilk 5 maddesi — bkz. `10-DEGISIKLIK-GUNLUGU.md`.

## Sıradaki (öneri sırası)
1. **TB-01 Grip düzenleme undo**: MouseDown'da ilk durumu (clone) sakla, MouseUp'ta `TransformEntityOperation`/`ModifyEntityPropertyOperation` ile işle, ESC'de eski konuma dön. Risk: canlı stretch (bağlı boru sündürme) mantığı ile etkileşim — önce test yaz.
2. **TB-09 Doğrulama**: kalan 28 MechanicalCommands + ~15 BasicCommands okunacak; RouteDuct/RiserPipe undo yolları öncelikli.
3. **TB-02/03 Format kapsamı**: DWG export'a Spline/Hatch/Dimension dönüşümü (AcadSharp), import'ta desteklenmeyen tipler için kullanıcı raporu, düz `TEXT` için `\U+` çözümü. Round-trip testleriyle.
4. **TB-04 UI donması**: `LoadDwgEntities` son işlemesini arka plana al (ilerleme çubuğu ile).
5. **TB-07 Dialog dayanıklılığı**: ortak "güvenli hesapla" sarmalayıcı + girdi doğrulama (ParseDouble sessiz varsayılanı yerine hata).
6. **Konsolidasyon (TB-B)**: her çift için önce fark tablosu; sonra tek uygulamaya yönlendirme (kullanıcı onayı gerekir, davranış korunacak).
7. **Doküman**: `CLAUDE.md` satır sayıları/envanter güncellemesi; kalan `docs/01–07` (envanter, özellik matrisi, mimari, CAD motoru, veri modeli, iş kuralları, test stratejisi) denetim raporlarından türetilecek.

## Kullanıcı kararı gerektirenler
- Ölü kod silme (`CadEngine`, `AdvancedSnapService`, yetim yazıcı/okuyucular) — şu an reddedildi.
- Lisans: mevcut anahtarlar yeni formatta yeniden üretilmeli (`tools/Afney.Cad.LicenseTool`).
- Oturum sonu rutini: GitHub push + Notion + `Kullanici_kitabi.md` (henüz yapılmadı).
