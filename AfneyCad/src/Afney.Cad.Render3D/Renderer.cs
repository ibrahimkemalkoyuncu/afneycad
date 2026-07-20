using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Afney.Cad.Render3D;

[StructLayout(LayoutKind.Sequential)]
internal struct TransformConstants
{
    public Matrix4x4 WorldViewProjection;
    public Matrix4x4 World;
    public Vector3 LightDirection;
    public float Padding;
    public Vector4 BaseColor;
}

/*
   NE: D3D11 Render Orkestratörü (Renderer)
   NEDEN: Shader derleme, input layout, constant buffer, derinlik tamponu (z-buffer) ve
          çizim komutlarını (Draw) yöneten sıfırdan yazılmış render döngüsü — WPF Viewport3D
          kullanılmıyor, tüm pipeline burada elle kuruluyor. API imzaları bir web araştırma
          ajanının Vortice.Windows'un GERÇEK GitHub kaynağını (samples/HelloDirect3D11) okuyup
          doğrulamasıyla teyit edildi.
*/
public sealed class Renderer : IDisposable
{
    private readonly D3D11DeviceResources _resources;
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly ID3D11InputLayout _inputLayout;
    private readonly ID3D11Buffer _constantBuffer;
    private readonly ID3D11DepthStencilState _depthStencilState;
    private readonly ID3D11RasterizerState _rasterizerState;

    private ID3D11Texture2D? _depthTexture;
    private ID3D11DepthStencilView? _depthView;
    private ID3D11Texture2D? _msaaColorTexture;
    private ID3D11RenderTargetView? _msaaColorView;
    private int _depthWidth, _depthHeight;
    private readonly uint _sampleCount;
    private readonly uint _sampleQuality;

    public Renderer(D3D11DeviceResources resources, string shaderPath)
    {
        _resources = resources;
        (_sampleCount, _sampleQuality) = DetermineMsaaSampleDescription(resources.Device, Format.B8G8R8A8_UNorm, preferredSampleCount: 4);

        ReadOnlyMemory<byte> vsBytes = Compiler.CompileFromFile(shaderPath, "VSMain", "vs_5_0");
        ReadOnlyMemory<byte> psBytes = Compiler.CompileFromFile(shaderPath, "PSMain", "ps_5_0");

        _vertexShader = resources.Device.CreateVertexShader(vsBytes.Span);
        _pixelShader = resources.Device.CreatePixelShader(psBytes.Span);

        var inputElements = new[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
        };
        _inputLayout = resources.Device.CreateInputLayout(inputElements, vsBytes.Span);

        _constantBuffer = resources.Device.CreateBuffer(
            new BufferDescription((uint)Marshal.SizeOf<TransformConstants>(), BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));

        _depthStencilState = resources.Device.CreateDepthStencilState(DepthStencilDescription.Default);

        _rasterizerState = resources.Device.CreateRasterizerState(new RasterizerDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.Back,
            FrontCounterClockwise = true, // BRepBuilder CCW-dışa-dönük konvansiyonuyla tutarlı
        });
    }

    private static (uint Count, uint Quality) DetermineMsaaSampleDescription(ID3D11Device device, Format format, int preferredSampleCount)
    {
        for (uint count = (uint)preferredSampleCount; count > 1; count /= 2)
        {
            uint quality = device.CheckMultisampleQualityLevels(format, count);
            if (quality > 0)
                return (count, quality - 1);
        }
        return (1, 0); // MSAA desteklenmiyorsa (nadir) sorunsuz düşüş
    }

    private void EnsureRenderTargets(int width, int height)
    {
        if (_depthTexture != null && _depthWidth == width && _depthHeight == height) return;

        _depthView?.Dispose();
        _depthTexture?.Dispose();
        _msaaColorView?.Dispose();
        _msaaColorTexture?.Dispose();

        _depthWidth = width;
        _depthHeight = height;

        // mipLevels açıkça 1 belirtilmeli — varsayılan (0="tam mip zinciri") ShaderResource
        // olmadan DepthStencil texture için E_INVALIDARG üretir (bkz. D3DImageBridge.Resize notu).
        // Derinlik tamponu, MSAA renk hedefiyle AYNI örnekleme sayısını taşımalı (D3D11 zorunluluğu).
        var depthDesc = new Texture2DDescription(
            Format.D24_UNorm_S8_UInt,
            (uint)width,
            (uint)height,
            arraySize: 1,
            mipLevels: 1,
            bindFlags: BindFlags.DepthStencil,
            sampleCount: _sampleCount,
            sampleQuality: _sampleQuality);

        _depthTexture = _resources.Device.CreateTexture2D(depthDesc);
        _depthView = _resources.Device.CreateDepthStencilView(_depthTexture);

        // Endüstri standardı: kenar yumuşatma (MSAA). D3D9 ile paylaşılan hedef (D3DImageBridge)
        // multisample OLAMAZ (interop kısıtı) — bu yüzden ÖNCE bu ayrı (paylaşılmayan) MSAA
        // dokuya render edilir, sonra tek-örnekli paylaşılan dokuya "resolve" edilir.
        var msaaDesc = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            (uint)width,
            (uint)height,
            arraySize: 1,
            mipLevels: 1,
            bindFlags: BindFlags.RenderTarget,
            sampleCount: _sampleCount,
            sampleQuality: _sampleQuality);

        _msaaColorTexture = _resources.Device.CreateTexture2D(msaaDesc);
        _msaaColorView = _resources.Device.CreateRenderTargetView(_msaaColorTexture);
    }

    public void RenderFrame(D3DImageBridge target, Camera3D camera, IReadOnlyList<(MeshBuffer Mesh, Vector4 Color)> meshes, Color4 backgroundColor)
    {
        EnsureRenderTargets(target.Width, target.Height);

        var context = _resources.Context;
        context.OMSetRenderTargets(_msaaColorView, _depthView);
        context.RSSetViewport(0, 0, target.Width, target.Height);
        context.ClearRenderTargetView(_msaaColorView, backgroundColor);
        context.ClearDepthStencilView(_depthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);

        context.OMSetDepthStencilState(_depthStencilState);
        context.RSSetState(_rasterizerState);
        context.IASetInputLayout(_inputLayout);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
        context.VSSetConstantBuffer(0, _constantBuffer);

        float aspect = target.Height > 0 ? (float)target.Width / target.Height : 1f;
        var view = camera.GetViewMatrix();
        var proj = camera.GetProjectionMatrix(aspect);
        var world = Matrix4x4.Identity;

        foreach (var (mesh, color) in meshes)
        {
            var constants = new TransformConstants
            {
                WorldViewProjection = world * view * proj,
                World = world,
                LightDirection = Vector3.Normalize(new Vector3(-0.4f, -0.5f, -0.8f)),
                BaseColor = color,
            };

            var mapped = context.Map(_constantBuffer, MapMode.WriteDiscard);
            Marshal.StructureToPtr(constants, mapped.DataPointer, false);
            context.Unmap(_constantBuffer);

            context.IASetVertexBuffer(0, mesh.VertexBuffer, (uint)Marshal.SizeOf<Vertex>());
            context.Draw((uint)mesh.VertexCount, 0);
        }

        // MSAA renk hedefini D3D9 ile paylaşılan tek-örnekli dokuya çöz (resolve).
        context.ResolveSubresource(target.RenderTarget11, 0, _msaaColorTexture, 0, Format.B8G8R8A8_UNorm);

        context.Flush();
    }

    public void Dispose()
    {
        _msaaColorView?.Dispose();
        _msaaColorTexture?.Dispose();
        _depthView?.Dispose();
        _depthTexture?.Dispose();
        _rasterizerState.Dispose();
        _depthStencilState.Dispose();
        _constantBuffer.Dispose();
        _inputLayout.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
    }
}
