using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Afney.Cad.Render3D;

/*
   NE: WPF D3DImage Köprüsü (D3DImageBridge)
   NEDEN: WPF'in donanım hızlandırmalı görüntü yüzeyi (`System.Windows.Interop.D3DImage`)
          SADECE bir Direct3D9 yüzeyini (`IDirect3DSurface9`) arka tampon olarak kabul eder —
          Direct3D11 doğrudan bağlanamaz. Klasik köprü deseni: D3D11 tarafında
          `ResourceOptionFlags.Shared` bayraklı bir texture oluşturulur, GPU'daki AYNI belleği
          işaret eden bir Direct3D9Ex texture bu paylaşılan handle ile oluşturulur.

   AKIŞ: D3D11'e render et → D3DImage.Lock() → SetBackBuffer(D3D9 yüzeyi) → AddDirtyRect() →
         D3DImage.Unlock() (WPF composition thread'i o kareyi ekrana basar).

   GERÇEK KÖK NEDEN (yerel olarak reprodüklenip doğrulandı — bkz. altta): `_device9.CreateTexture`
   E_INVALIDARG hatası vermesinin sebebi adaptör uyuşmazlığı DEĞİLDİ (iki farklı adaptör-eşleştirme
   denemesi de bunu doğruladı, hiçbiri hatayı gidermedi). Vortice.Windows'un kendi resmi örneği
   (`src/Vortice.Wpf/DrawingSurface.cs`, `CreateAndBindTargets()`) paylaşılan D3D11 texture'ı
   `BindFlags.RenderTarget | BindFlags.ShaderResource` ile oluşturuyor — bizim kodumuzda SADECE
   `RenderTarget` vardı. Bu eksik `ShaderResource` bayrağı gerçek nedendi: bu makinede (2 GPU'lu
   bir Optimus dizüstü, NVIDIA GTX 1050 Ti + Intel HD 630) küçük bir tekrar-üretim programıyla
   (WinForms penceresi + gerçek D3D9Ex/D3D11 cihazları) hata birebir tekrarlandı; `ShaderResource`
   bayrağı eklenince `_device9.CreateTexture` başarıyla paylaşılan handle'ı açtı. Adaptör LUID'leri
   zaten bu makinede eşleşiyordu (ikisi de aynı NVIDIA GPU'yu seçiyordu) — yani önceki iki adaptör
   düzeltme denemesi baştan yanlış bir teşhise dayanıyordu.
*/
public sealed class D3DImageBridge : IDisposable
{
    private readonly Vortice.Direct3D9.IDirect3D9Ex _d3d9;
    private readonly Vortice.Direct3D9.IDirect3DDevice9Ex _device9;

    public ID3D11Texture2D RenderTarget11 { get; private set; } = null!;
    public ID3D11RenderTargetView RenderTargetView { get; private set; } = null!;
    private Vortice.Direct3D9.IDirect3DTexture9? _renderTarget9;
    public Vortice.Direct3D9.IDirect3DSurface9 Surface9 { get; private set; } = null!;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public D3DImageBridge(nint parentWindowHandle)
    {
        _d3d9 = Vortice.Direct3D9.D3D9.Direct3DCreate9Ex();

        var presentParams = new Vortice.Direct3D9.PresentParameters
        {
            Windowed = true,
            SwapEffect = Vortice.Direct3D9.SwapEffect.Discard,
            DeviceWindowHandle = parentWindowHandle,
            PresentationInterval = Vortice.Direct3D9.PresentInterval.Default,
        };

        _device9 = _d3d9.CreateDeviceEx(
            0, // D3DADAPTER_DEFAULT
            Vortice.Direct3D9.DeviceType.Hardware,
            IntPtr.Zero,
            Vortice.Direct3D9.CreateFlags.HardwareVertexProcessing | Vortice.Direct3D9.CreateFlags.Multithreaded | Vortice.Direct3D9.CreateFlags.FpuPreserve,
            presentParams);
    }

    /*
       NE: Verilen boyutta paylaşılan D3D11↔D3D9 render hedefi (yeniden) oluşturur.
       NEDEN: `Direct3DViewportControl` boyutu değiştiğinde (pencere resize) çağrılır.
    */
    public void Resize(D3D11DeviceResources d3d11, int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (Width == width && Height == height && RenderTarget11 != null) return;

        RenderTargetView?.Dispose();
        RenderTarget11?.Dispose();
        Surface9?.Dispose();
        _renderTarget9?.Dispose();

        Width = width;
        Height = height;

        // mipLevels açıkça 1 belirtilmeli — varsayılan (0="tam mip zinciri") ShaderResource
        // olmadan E_INVALIDARG üretir. BindFlags.ShaderResource ASIL DÜZELTME: D3D9Ex'in
        // paylaşılan handle'ı açabilmesi için D3D11 texture'ının sadece RenderTarget değil,
        // RenderTarget|ShaderResource ile oluşturulması gerekiyor (bkz. sınıf başı NEDEN notu).
        var desc11 = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            (uint)width,
            (uint)height,
            arraySize: 1,
            mipLevels: 1,
            bindFlags: BindFlags.RenderTarget | BindFlags.ShaderResource,
            miscFlags: ResourceOptionFlags.Shared); // D3D9 ile paylaşılabilmesi için ZORUNLU

        RenderTarget11 = d3d11.Device.CreateTexture2D(desc11);
        RenderTargetView = d3d11.Device.CreateRenderTargetView(RenderTarget11);

        using var dxgiResource = RenderTarget11.QueryInterface<IDXGIResource>();
        nint sharedHandle = dxgiResource.SharedHandle;

        _renderTarget9 = _device9.CreateTexture(
            (uint)width, (uint)height, 1,
            Vortice.Direct3D9.Usage.RenderTarget,
            Vortice.Direct3D9.Format.A8R8G8B8,
            Vortice.Direct3D9.Pool.Default,
            ref sharedHandle);

        Surface9 = _renderTarget9.GetSurfaceLevel(0);
    }

    public void Dispose()
    {
        Surface9?.Dispose();
        _renderTarget9?.Dispose();
        RenderTargetView?.Dispose();
        RenderTarget11?.Dispose();
        _device9?.Dispose();
        _d3d9?.Dispose();
    }
}
