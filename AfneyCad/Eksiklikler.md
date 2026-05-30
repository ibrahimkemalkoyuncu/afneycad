# AfneyCAD Geliştirme ve Eksiklik Analizi (Gap Analysis)

> **Son güncelleme:** 2026-05-30 — Session #28 sonrası durum  
> Bu belge, AfneyCAD'in mevcut yetenekleri ile endüstri standardı olan FINE MEP (AutoBUILD & ADAPT/FCALC) yazılımları arasındaki farkları özetlemektedir.

---

## 1. AutoBUILD (Mimari BIM Modelleme) Eksiklikleri

| Özellik | Durum | Not |
|---|---|---|
| IFC Import (Revit/ArchiCAD) | ✅ **Var** | `IfcImportService` — duvar/döşeme/pencere/kapı, IFC 2x3+4 (Session #20) |
| IFC Export | ✅ **Var** | `IfcExportService` |
| Parametrik BIM Nesneleri | ❌ Eksik | `ArchitecturalObstacle` sadece geometrik engel; U-value, malzeme katmanı yok |
| Akıllı Altlık / DWG→BIM | ❌ Eksik | 2B DWG üzerinden otomatik duvar→3B BIM dönüşümü yok |
| Geniş Mimari Kütüphane | ❌ Eksik | Kolon / çatı / döşeme / mobilya kütüphanesi ve yerleşim araçları yok |

---

## 2. ADAPT/FCALC (Hidrolik Hesaplama Motoru) Eksiklikleri

| Özellik | Durum | Not |
|---|---|---|
| Bağımsız Hesap Modu (CAD'siz) | ✅ **Var** | `CalculationTableWindow` — Manuel Giriş sekmesi (Session #20) |
| Çoklu Standart (ASPE/BS/ASHRAE) | ✅ **Var** | `PipeSizer` + `StandardSelectionService` — 4 norm (Session #21) |
| Pompa/Hidrofor Kapasite | ✅ **Var** | `PumpSelectionService`, `WaterTankService`, `DepoHidroforDialog` |
| Genleşme Tankı | ✅ **Var** | `ThermalExpansionService` (TS EN 13831) |
| Su Sayacı Seçimi | ✅ **Var** | `WaterMeterService` (TS EN 14154) |
| Geri Akış Önleyici | ✅ **Var** | `BackflowPreventerService` (TS EN 1717) |
| **Geri Besleme Döngüsü** | ⚠️ **Kısmi** | Manuel override çizime anlık yansımıyor; `AutoAnnotationService` var ama çift yönlü sync eksik |
| Hesap Tablosu Spreadsheet Entegrasyonu | ⚠️ **Kısmi** | Tesisat ekipmanları ayrı dialog'larda; merkezi spreadsheet'e bağlı değil |

---

## 3. Mühendislik ve Kullanılabilirlik (Genel)

| Özellik | Durum | Not |
|---|---|---|
| Kolon Şeması (Riser Diagram) | ✅ **Var** | `RiserDiagramExportDialog` — gerçek 3D model verisi (Session #28) |
| PDF Teknik Rapor | ✅ **Var** | `PdfExportService` — SkiaSharp, 2-sayfalı A4 (Session #28) |
| Topoloji Analizi | ✅ **Var** | `NetworkTopologyAnalysisService` — döngü/açık uç/kritik yol (Session #28) |
| Sistem Doğrulama | ✅ **Var** | `DomainGuardService` + `NetworkTopologyDialog` |
| Vana Kütüphanesi | ✅ **Var** | `ValveLibraryDialog` — boru üstüne snap+split (TS EN 1074, Session #28) |
| **PDF Antetli Rapor** | ❌ Eksik | Firma logosu + mühendis imzası + proje bilgileri bloğu yok |
| **Basınç Düşümü Haritası** | ❌ Eksik | Viewport overlay — boru hattında renk gradyanı ile basınç dağılımı |
| **Çizim ↔ Hesap Senkronu** | ❌ Eksik | Çap override → etiket anlık güncellenmesi tam değil |

---

## 4. Bir Sonraki Session (#29) Öncelikleri

Öncelik sırasına göre:

| # | Özellik | Zorluk | Standart |
|---|---------|--------|----------|
| 1 | **Basınç Düşümü Haritası** | Orta | — |
| 2 | **Hesap Tablosu ↔ Çizim Senkronizasyonu** | Orta | — |
| 3 | **PDF Antetli Rapor** | Düşük | — |
| 4 | **3D MEP-MEP Çakışma Tespiti** | Orta | — |
| 5 | **Doğalgaz Hesap → Hesap Tablosu** | Düşük | TS EN 1775 |

---

## 5. Uzun Vadeli Eksiklikler (Yol Haritası)

| Özellik | Öncelik | Açıklama |
|---|---|---|
| Parametrik BIM Nesneleri | Orta | U-value, malzeme katmanı, çift tık özellik penceresi |
| Akıllı DWG→BIM Dönüşüm | Yüksek | Mimari plan üzerinden otomatik duvar/kat tespiti |
| Geniş Mimari Kütüphane | Düşük | Kolon/çatı/döşeme/mobilya |
| Saf ADAPT/FCALC Modu | Orta | Tam bağımsız spreadsheet hesap modu |
| Çoklu Proje (MDI) | Orta | Birden fazla proje sekmesi aynı anda |
| Bulut Senkronizasyonu | Düşük | Proje dosyası cloud backup |
