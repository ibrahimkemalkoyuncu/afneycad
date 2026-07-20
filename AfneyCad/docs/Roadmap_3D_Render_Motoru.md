# Roadmap — Ana Render Motorunu 3D'ye Taşıma (Sıfırdan Motor, Direct3D11)

> **Durum:** Planlama → İmplementasyon başladı — 2026-07-19, Session #53
> **Bağlam:** `docs/Denetim_Gecmisi.md`'de 3D/B-Rep kategorisinin en büyük açığı (Ana render
> motoru 3/10, sabit): `Afney.Cad.Render` (Skia) tamamen 2D, gerçek 3D sadece izole
> `Pipe3DViewWindow` dialogunda (WPF `Viewport3D`) var.
>
> **KULLANICI KARARI:** Mevcut kod SİLİNMEYECEK (Skia 2D motoru ve `Pipe3DViewWindow` aynen
> kalıyor, üstüne EKLENİYOR). WPF'in kendi `Viewport3D`/Media3D sahne grafiği KULLANILMAYACAK —
> bunun yerine **sıfırdan, gerçek GPU hızlandırmalı bir 3D motor** yazılıyor:
> **Direct3D11 (Vortice.Windows binding'i ile), WPF'e `D3DImage` köprüsüyle entegre.** Kamera,
> ışıklandırma, rasterizasyon pipeline'ının tamamı bizim kontrolümüzde (kendi HLSL shader'larımız,
> kendi vertex/index buffer yönetimimiz).

## Mevcut Durum (doğrulanmış)

- **`Afney.Cad.Render/Engines/SkiaRenderContext.cs`** — `SKCanvas` üzerine çizen, tamamen 2D
  vektör render motoru. `Camera.cs` sadece world↔screen affine (pan/zoom) dönüşümü yapıyor,
  perspektif/z-buffer yok.
- **`Pipe3DViewWindow.xaml.cs`** — bu oturumda genişletildi (duvar+kanal B-Rep dahil), gerçek
  WPF `Viewport3D`/`MeshGeometry3D` (DirectX destekli) kullanıyor — **kanıtlanmış, çalışan bir
  3D render deseni** ama ayrı bir pencere, ana çizim tuvaline entegre değil.
- **`IsometricRenderService.cs`** / `AxonometricExportService.cs` — gerçek 3D değil, sabit açılı
  2.5D nokta projeksiyonu (kabinetik axonometri).
- **B-Rep kaynağı:** `WallBRepService`, `DuctBRepService`, `Pipe3DModelService` — entity'leri
  gerçek üçgen mesh'e çeviriyor (bkz. `BRepTessellator`), ama sadece Wall/Duct/Pipe/Elbow/Tee/
  Reducer kapsıyor — Fixture/Door/Window/Hatch/Block gibi tipler 3D'de temsil edilmiyor.

## Mimari — Yeni Proje: `Afney.Cad.Render3D`

Mevcut `Afney.Cad.Render` (Skia, 2D) dokunulmadan kalır. Yeni bir proje eklenir:

- **NuGet:** `Vortice.Direct3D11`, `Vortice.DXGI`, `Vortice.D3DCompiler` (veya Vortice.Windows
  meta-paketi) — MIT lisanslı, aktif bakımlı SharpDX halefi, native DirectX header'larının
  doğrudan C# binding'i (COM interop, ekstra bir "motor" değil, ham API erişimi).
- **`D3D11Device.cs`** — `ID3D11Device`/`ID3D11DeviceContext` oluşturma, WPF ile paylaşılan
  bir Direct3D9Ex/D3D11 köprü texture'ı (klasik `D3DImage` interop deseni: D3D11 texture'a
  render et → paylaşılan handle üzerinden D3D9Ex texture'a kopyala → `D3DImage.SetBackBuffer`).
- **`Shaders/Basic.hlsl`** — minimal vertex+pixel shader (world-view-proj transform, tek yönlü
  ışık, düz renk/malzeme).
- **`MeshBuffer.cs`** — `BRepTessellator`'ın (`List<Vector3D> Vertices, List<(int,int,int)> Faces`)
  çıktısını D3D11 vertex/index buffer'a yükleyen köprü — mevcut B-Rep/tessellasyon koduna
  SIFIR değişiklik gerektirmez, sadece yeni bir tüketici.
- **`Camera3D.cs`** — orbit/pan/zoom, view+projection matrisleri (System.Numerics.Matrix4x4).
- **`Direct3DViewportControl.xaml`** — `Image` kontrolü + `D3DImage` kaynağı, WPF `CadViewport`
  içine mevcut Skia tuvaliyle YAN YANA (mod değiştirmeli, `Visibility` toggle) eklenir.

## Kademeli Plan

### İlerleme Durumu (2026-07-19, Session #53)

**Faz 1 TAMAMLANDI — derleme seviyesinde doğrulandı, GÖRSEL doğrulama BEKLİYOR.**
- ✅ `Afney.Cad.Render3D` projesi oluşturuldu, `Afney.CadEngine.slnx`'e eklendi.
- ✅ Vortice.Direct3D11/DXGI/D3DCompiler/Direct3D9 (v3.6.2) paketleri eklendi — bir web
  araştırma ajanı Vortice.Windows'un GERÇEK GitHub kaynağını (`src/Vortice.Wpf/`,
  `samples/HelloDirect3D11.Wpf/`) okuyup tam API imzalarını doğruladıktan sonra kod buna göre
  düzeltildi (ilk taslak tahminle yazılmıştı, 21 derleme hatası vermişti — hepsi giderildi).
- ✅ `D3D11DeviceResources.cs` — cihaz/context oluşturma.
- ✅ `D3DImageBridge.cs` — D3D9Ex↔D3D11 paylaşımlı texture köprüsü (WPF D3DImage için).
- ✅ `Camera3D.cs` — orbit/pan/zoom, System.Numerics matrisleri.
- ✅ `MeshBuffer.cs` — `BRepTessellator` çıktısını doğrudan GPU vertex buffer'a yüklüyor (bu
  oturumun B-Rep kernel'iyle birebir bağlı, ayrı test verisi YOK).
- ✅ `Shaders/Basic.hlsl` + `Renderer.cs` — WVP dönüşümü + Lambertian ışık, derleme başarılı.
- ✅ `Direct3DViewportControl.xaml(.cs)` — WPF UserControl, D3DImage host, orbit/pan/zoom fare
  kontrolleri, test mesh'i olarak `BRepBuilder.ExtrudeBox` ile üretilen gerçek bir küp.
- ✅ `dotnet build Afney.CadEngine.slnx` → **0 hata** (tüm çözüm, Render3D dahil).

**GÖRSEL DOĞRULAMA HÂLÂ YAPILAMADI (dürüstçe belirtilmeli):** Bu ortamda GPU'lu bir Windows
masaüstü oturumu çalıştırıp uygulamayı görsel olarak test etmek mümkün değil — sadece derleme
seviyesinde (API çağrılarının doğru tipte/imzada olduğu) doğrulama yapılabildi.

**GÜNCELLEME — Ana uygulamaya BAĞLANDI:** `Direct3DViewportControl`, `Afney.Cad.Presentation`
projesine eklendi (`Dialogs/Direct3DTestWindow.xaml`) ve komut satırından **`d3dtest`** (veya
`d3d11`) yazılarak açılabiliyor (bkz. `MainWindow.Commands.cs` → `OnD3D11EngineTest`,
`MainWindow.Engineering.cs`). Tam çözüm (`Afney.CadEngine.slnx`) 0 hata ile derleniyor, tam
test suite 286/286.

**GÜNCELLEME — E_INVALIDARG çalışma zamanı hatası GİDERİLDİ (gerçek kök neden, yerel
tekrar-üretimle doğrulandı):**
Kullanıcı uygulamayı çalıştırıp `d3dtest` komutunu denediğinde `IDirect3DDevice9Ex.CreateTexture`
çağrısında `E_INVALIDARG` ile karşılaştı (bkz. yığın izi: `D3DImageBridge.Resize`).

*İki yanlış teşhis (kayıt için):* İlk iki düzeltme denemesi "D3D11 ve D3D9Ex farklı fiziksel
adaptörlere bağlanıyor" varsayımına dayanıyordu (önce DXGI index-0 eşleştirmesi, sonra LUID
eşleştirmesi denendi) — ikisi de derlendi ama sorunu GİDERMEDİ, çünkü teşhis baştan yanlıştı.

*Gerçek kök neden:* Bu ortamda GPU'lu bir geliştirme makinesi olduğu fark edilip küçük bir
WinForms tabanlı tekrar-üretim programıyla (gerçek pencere handle'ı + gerçek D3D9Ex/D3D11
cihazları) hata BİREBİR tekrar üretildi. Adaptör LUID'leri karşılaştırıldığında zaten
eşleştikleri görüldü (adaptör teorisi tamamen elendi). Vortice.Windows'un kendi resmi örneği
(`src/Vortice.Wpf/DrawingSurface.cs`, `CreateAndBindTargets()`) satır satır incelenince gerçek
fark bulundu: paylaşılan D3D11 texture `BindFlags.RenderTarget | BindFlags.ShaderResource` ile
oluşturuluyor — bizim kodda sadece `BindFlags.RenderTarget` vardı. Tekrar-üretim programında
`ShaderResource` bayrağı eklenince `_device9.CreateTexture` başarıyla döndü. **Çözüm:**
`D3DImageBridge.cs`'teki paylaşılan texture açıklaması artık `BindFlags.RenderTarget |
BindFlags.ShaderResource` kullanıyor; gereksiz adaptör-eşleştirme kodu (`D3D11DeviceResources`,
`D3DImageBridge`) kaldırılıp basit, referansla birebir aynı hale (`adapter: null,
DriverType.Hardware`) döndürüldü.

**GÜNCELLEME — Endüstriyel standart yükseltmeleri (kullanıcı isteği: "3D motorunu güncel
endüstriyel standartlara çıkar"):**
- **MSAA (4x, donanım desteklemezse 2x'e otomatik düşer):** `Renderer.cs`, D3D9 ile paylaşılan
  hedefin (D3DImageBridge) multisample OLAMAMASI kısıtı nedeniyle önce ayrı bir MSAA renk
  dokusuna render edip, çizim bittikten sonra `ResolveSubresource` ile tek-örnekli paylaşılan
  dokuya çözüyor (profesyonel CAD 3D viewport'larının standart deseni). Derinlik tamponu MSAA
  renk hedefiyle aynı örnekleme sayısını taşıyor (D3D11 zorunluluğu). `CheckMultisampleQualityLevels`
  ile donanım desteği çalışma zamanında sorgulanıyor.
- **Gamma-doğru (linear-space) aydınlatma:** `Shaders/Basic.hlsl` PSMain artık aydınlatmayı
  lineer uzayda hesaplayıp çıkışta `pow(color, 1/2.2)` ile gamma-encode ediyor — aksi halde
  orta tonlar gerçekte olduğundan koyu/kontrastsız görünür (yaygın acemi hatası, modern render
  motorlarının hepsinde düzeltilmiş standart pratik).
- Shader profili `vs_4_0`/`ps_4_0`'dan `vs_5_0`/`ps_5_0`'a yükseltildi (zaten hedeflenen
  FeatureLevel 11.x donanımıyla tutarlı, daha güncel Shader Model).
- `dotnet build Afney.CadEngine.slnx` → **0 hata** (bu değişikliklerden sonra doğrulandı).

**GÜNCELLEME — Küp SİYAH görünüyordu (kök neden bulundu, yerel GPU repro ile doğrulandı):**
Kullanıcı `d3dtest`'i denedi — artık çökme yoktu (bir önceki E_INVALIDARG düzeltmesi kalıcıydı)
ama viewport tamamen SİYAH kaldı, küp hiç görünmedi. Bu ortamda gerçek bir GPU olduğu daha önce
keşfedilmişti (bkz. E_INVALIDARG bölümü) — bu kez `Afney.Cad.Render3D`'in GERÇEK sınıflarını
(`D3D11DeviceResources`, `D3DImageBridge`, `Renderer`, `Camera3D`, `MeshBuffer`, gerçek
`BRepTessellator` çıktısı) referans alan bir konsol harness'ı yazılıp bir WinForms penceresiyle
uçtan uca çalıştırıldı, `Renderer.RenderFrame` sonrası paylaşılan D3D11 texture'ı bir staging
texture'a kopyalanıp PİKSEL BAZINDA okundu. Sistematik bisection (yüz sayısını 1→12 arası
kademeli artırarak) ile hata TAM OLARAK küpün kameraya dönük yüzlerinin SİYAH (arkaplan rengi
DEĞİL) çizildiğini gösterdi — yani render pipeline çalışıyordu ama piksel shader'ın okuduğu
renk/ışık verisi sıfır geliyordu. Kök neden: `Renderer.RenderFrame`, constant buffer'ı SADECE
Vertex Shader aşamasına bağlıyordu (`context.VSSetConstantBuffer(0, _constantBuffer)`) —
`context.PSSetConstantBuffer(0, _constantBuffer)` çağrısı HİÇ YOKTU. D3D11'de her GPU aşaması
(VS, PS) kendi ayrı constant buffer slotlarına sahiptir; aynı HLSL dosyasında `register(b0)`
ile tanımlanmış olmaları Pixel Shader'ın otomatik erişimini SAĞLAMAZ. Piksel shader'ın okuduğu
`BaseColor`/`LightDirection` bu yüzden bağlanmamış (sıfır) bir slottan okunuyor, küp tamamen
siyah çiziliyordu. **Çözüm:** `Renderer.cs`'e eksik `context.PSSetConstantBuffer(0,
_constantBuffer);` satırı eklendi. Yerel harness'ta doğrulandı: küp artık doğru mavi tonlarında
(gamma-doğru aydınlatmayla) render ediliyor.

**Sıradaki adım tamamen sizde:** Uygulamayı çalıştırıp komut satırına `d3dtest` yazarak Faz 1'in
gerçekten çalışıp çalışmadığını (küp görünüyor mu, kenarları MSAA ile pürüzsüz mü, sağ fare ile
döndürülebiliyor mu) doğrulamanız gerekiyor — bu ortamda GPU'lu görsel test yapılamıyor.

### Faz 1 — Pipeline doğrulama (tek üçgen/küp)
- Vortice paketleri eklenir, `Afney.Cad.Render3D` projesi oluşturulur.
- D3D11 device + swapchain-siz render-to-texture + `D3DImage` köprüsü kurulur.
- Sabit bir test küpü (elle yazılmış vertex/index verisi) render edilip WPF penceresinde
  göründüğü doğrulanır — **bu fazın tek başarı kriteri: bir üçgen/küp ekranda dönüyor.**

### Faz 2 — Gerçek B-Rep mesh render + kamera
- `MeshBuffer` ile `WallBRepService`/`DuctBRepService`/`Pipe3DModelService` çıktıları render
  edilir (aynı veri, `Pipe3DViewWindow`'ın tükettiğiyle birebir — sadece render hedefi farklı).
- Orbit/pan/zoom kamera + ışıklandırma.
- `OnToggle3DView` komutu (zaten var) ana `CadViewport`'ta bu yeni motoru açar.
- **Genelleştirilmiş B-Rep adaptörü:** Fixture/Door/Window/Room tabanı için de mesh üretimi
  (şu an sadece Wall/Duct/Pipe var).

### Faz 3 — Seçim senkronizasyonu
- 3D moddaki mouse-click'ten ray-casting (dünya-uzayı ışın + mesh üçgen kesişimi, elle
  yazılır — D3D11'in kendi picking API'si yok) → hangi `CadEntity`'ye tıklandığını bul →
  `SelectionManager`'a bildir.

### Faz 4 — Tam düzenleme paritesi
- Move/Copy/Rotate gibi komutların 3D'de de çalışması (ray-plane kesişimiyle dünya-uzayı
  nokta belirleme). En riskli faz — muhtemelen sadece belirli komutlar 3D'de aktif olur.

## Performans Notu
WPF `Viewport3D` GPU-instancing yapmaz — binlerce entity'li büyük projelerde (ör. çok katlı
bina + tüm MEP) performans sorunu olabilir. Faz 1 sonunda gerçek bir proje ile performans
testi yapılmalı; gerekirse LOD (uzak entity'ler için düşük poligon) veya frustum culling
(`Afney.Cad.SpatialIndex`'teki R-Tree zaten var, 3D'ye taşınabilir) eklenmeli.

## Doğrulama Kriteri (her faz sonunda)
- `dotnet build` 0 hata.
- Gerçek bir proje dosyasıyla manuel görsel doğrulama (bu tür UI değişiklikleri otomatik
  testle tam kapsanamaz).
- Bağımsız bir ajanla kod incelemesi + `docs/Denetim_Gecmisi.md`'ye yeni girdi.
