using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace Afney.Cad.Render3D;

/*
   NE: Direct3D11 Cihaz Kaynakları (D3D11DeviceResources)
   NEDEN: docs/Roadmap_3D_Render_Motoru.md — sıfırdan, gerçek GPU hızlandırmalı 3D motorun
          temel taşı. WPF'in kendi Viewport3D/Media3D sahne grafiği KULLANILMIYOR — burada
          oluşturulan ID3D11Device/ID3D11DeviceContext, motorun tüm render işlemlerinin
          (mesh çizimi, shader, render target) üzerine kurulduğu ham GPU erişim noktasıdır.

   NOT (geçmiş yanlış iz — kayıt için): Daha önce burada D3D9Ex ile "aynı fiziksel GPU"yu
   garanti etmek için adaptörü DXGI LUID'iyle eşleştiren bir mekanizma vardı. Bu, E_INVALIDARG
   hatasının GERÇEK nedeni DEĞİLDİ — bu makinede D3D9Ex ve D3D11'in varsayılan adaptör seçimleri
   zaten aynı fiziksel GPU'ya (aynı LUID) denk geliyordu; bu yerel olarak (`dotnet run` ile bir
   üretim ortamında iki cihazın LUID'leri karşılaştırılarak) doğrulandı. Gerçek neden
   `D3DImageBridge.Resize()`'daki paylaşılan D3D11 texture'ın `BindFlags.ShaderResource`
   içermemesiydi (bkz. o dosyadaki not) — Vortice.Windows'un kendi resmi örneği
   (`src/Vortice.Wpf/DrawingSurface.cs`) bunu doğruluyor: adaptör eşleştirmesi HİÇ yapmıyor
   (`adapter: null, DriverType.Hardware` direkt kullanılıyor), ama paylaşılan texture'ı
   `BindFlags.RenderTarget | BindFlags.ShaderResource` ile oluşturuyor. Gereksiz karmaşıklık
   olduğu için adaptör eşleştirme kodu kaldırıldı, referans örnekle birebir aynı yalın desene
   dönüldü.
*/
public sealed class D3D11DeviceResources : IDisposable
{
    public ID3D11Device Device { get; }
    public ID3D11DeviceContext Context { get; }
    public FeatureLevel FeatureLevel { get; }

    public D3D11DeviceResources()
    {
        var flags = DeviceCreationFlags.BgraSupport; // D3D9Ex paylaşımlı texture için gerekli
#if DEBUG
        if (D3D11.SdkLayersAvailable())
            flags |= DeviceCreationFlags.Debug;
#endif
        var featureLevels = new[]
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0,
        };

        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            flags,
            featureLevels,
            out ID3D11Device device,
            out FeatureLevel featureLevel,
            out ID3D11DeviceContext context).CheckError();

        Device = device;
        Context = context;
        FeatureLevel = featureLevel;
    }

    public void Dispose()
    {
        Context.Dispose();
        Device.Dispose();
    }
}
