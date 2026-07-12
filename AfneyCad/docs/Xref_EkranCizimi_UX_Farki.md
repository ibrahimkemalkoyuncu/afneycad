# Xref / "Ekran Çizimi" — FINE MEP ↔ AfneyCAD UX Farkı Kılavuzu

## FINE MEP (OtoNET) akışı — manuel, kat-kat
1. Her kat için ayrı ayrı: `Otob → Ekran Çizimi` → kat seçilir → benzersiz
   isim verilir (örn. "001") → yeni pencerede açılır.
2. O pencerede `Ekle → Dış Kaynak (Xref) Yöneticisi` açılır, referans
   "Bağla" ile bağımsız dosya haline getirilir.
3. Bu iki adım **her kat için ayrı ayrı** tekrarlanır.
4. Boş bir pafta sayfasında `Ekle → Blok → Dosyadan` ile bu kat dosyaları
   tek tek eklenir (ölçek sorulduğunda 3× Enter ile atlanır).

## AfneyCAD akışı — otomatik, tek adımda tüm katlar
AfneyCAD'de aynı sonuca **tek buton** ile ulaşılıyor, ayrı Xref bağlama
adımı gerekmiyor:

| AfneyCAD adımı | Karşılık geldiği FINE MEP adımları |
|---|---|
| `Pafta Düzeni → Tüm Katları Ekle` (`LayoutSheetDialog.CaptureAll_Click`, `FloorSnapshotService.DetectFloors`) | Ekran Çizimi (her kat) + Xref bağlama + Blok olarak pafta'ya ekleme — **hepsi tek adımda** |
| `Pafta Düzeni → Tümünü Patlat` (`ExplodeAll_Click`) | FINE'daki manuel "blok patlat" adımı — AfneyCAD ayrıca `Layer == EXPLODED` kontrolüyle **çift patlatmayı otomatik engelliyor** (FINE'da bu kullanıcı disiplinine bırakılmış bir risk) |
| `Pafta Düzeni → DXF Merge (Tümü)` (`ExportMerged_Click`) | Antetli şablona aktarım öncesi tek dosyada birleştirme |

`XrefService` / `XrefManagerDialog` AfneyCAD'de **ayrıca da mevcut** — DWG
import sırasında harici referans içeren dosyalar için kullanılıyor. Ama
"tüm katları paftaya toplama" iş akışında kullanıcının bunu manuel tetiklemesi
gerekmiyor; `CaptureAll` bunu içeride hallediyor.

## Kullanıcıya söylenecek (FINE MEP'ten geçenler için)
> "AfneyCAD'de Ekran Çizimi + Xref Bağlama + Blok Ekleme adımlarının hepsini
> tek tek yapmanıza gerek yok — Pafta Düzeni penceresinde **Tüm Katları Ekle**
> butonu bunların hepsini otomatik yapıyor. Xref Yöneticisi ayrı bir araç
> olarak hâlâ mevcut, ama bu iş akışı için gerekli değil."

## Doğrulanmadı / sonraki adım
`CaptureAll` çıktısının FINE'daki "her kat ayrı isimlendirilmiş blok" esnekliğini
(örn. sadece belirli katları seçme) sağlayıp sağlamadığı kod okumasıyla teyit
edilmedi — canlı testte kontrol edilmeli.
