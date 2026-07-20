using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Vortice.Mathematics;

namespace Afney.Cad.Render3D;

/*
   NE: D3D11 Viewport Kontrolü (Direct3DViewportControl) — Faz 1 pipeline doğrulama
   NEDEN: docs/Roadmap_3D_Render_Motoru.md Faz 1'in somut hedefi: sıfırdan yazılan D3D11
          render pipeline'ının (device + D3DImage köprüsü + shader + mesh) baştan sona
          çalıştığını kanıtlamak. Test mesh'i olarak `BRepBuilder.ExtrudeBox` +
          `BRepTessellator` KULLANILIYOR — bu oturumda kurulan B-Rep kernel'i ile yeni render
          motorunu aynı gerçek veri yoluyla (elle yazılmış test verisi değil) birbirine bağlıyor.

   KAPSAM: Sadece görüntüleme + orbit/pan/zoom (bkz. roadmap Faz 1-2). Seçim/düzenleme
   entegrasyonu (Faz 3-4) kapsam dışı.
*/
public partial class Direct3DViewportControl : UserControl, IDisposable
{
    private D3DImage _d3dImage = new();
    private D3D11DeviceResources? _device;
    private D3DImageBridge? _bridge;
    private Renderer? _renderer;
    private readonly Camera3D _camera = new();
    private readonly List<(MeshBuffer Mesh, Vector4 Color)> _meshes = new();

    private System.Windows.Point _lastMousePos;
    private bool _isOrbiting;
    private bool _isPanning;

    public Direct3DViewportControl()
    {
        InitializeComponent();
        SurfaceImage.Source = _d3dImage;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => Resize();

        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var parentWindow = Window.GetWindow(this);
            nint hwnd = parentWindow != null ? new WindowInteropHelper(parentWindow).Handle : IntPtr.Zero;

            _device = new D3D11DeviceResources();
            _bridge = new D3DImageBridge(hwnd);

            string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Basic.hlsl");
            _renderer = new Renderer(_device, shaderPath);

            // Faz 1 test mesh'i: B-Rep kernel'inden gerçek bir küp (2000x2000x2000mm).
            var testCube = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
            var (vertices, faces) = BRepTessellator.Tessellate(testCube);
            _meshes.Add((new MeshBuffer(_device.Device, vertices, faces), new Vector4(0.35f, 0.55f, 0.85f, 1f)));

            _camera.Target = Vector3.Zero;
            _camera.Distance = 6000f;

            Resize();
            CompositionTarget.Rendering += OnRendering;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"D3D11 başlatma hatası (OnLoaded):\n\n{ex}", "D3D11 Init Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Dispose();

    private void Resize()
    {
        if (_device == null || _bridge == null) return;
        int width = Math.Max(1, (int)ActualWidth);
        int height = Math.Max(1, (int)ActualHeight);
        _bridge.Resize(_device, width, height);
    }

    private bool _renderFailed;

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_renderFailed) return; // Bir kez hata verdiyse her karede aynı hatayı göstermeyi önle
        if (_device == null || _bridge == null || _renderer == null) return;
        if (_bridge.Width <= 0 || _bridge.Height <= 0) return;

        try
        {
            _renderer.RenderFrame(_bridge, _camera, _meshes, new Color4(0.051f, 0.055f, 0.090f, 1f));

            _d3dImage.Lock();
            _d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _bridge.Surface9.NativePointer);
            _d3dImage.AddDirtyRect(new Int32Rect(0, 0, _d3dImage.PixelWidth, _d3dImage.PixelHeight));
            _d3dImage.Unlock();
        }
        catch (Exception ex)
        {
            _renderFailed = true;
            MessageBox.Show($"D3D11 render hatası (OnRendering):\n\n{ex}", "D3D11 Render Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Orbit / Pan / Zoom ────────────────────────────────────────────────────────
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _lastMousePos = e.GetPosition(this);
        _isOrbiting = e.RightButton == MouseButtonState.Pressed;
        _isPanning = e.MiddleButton == MouseButtonState.Pressed;
        CaptureMouse();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isOrbiting = false;
        _isPanning = false;
        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var delta = pos - _lastMousePos;
        _lastMousePos = pos;

        if (_isOrbiting)
            _camera.Orbit((float)-delta.X * 0.01f, (float)delta.Y * 0.01f);
        else if (_isPanning)
            _camera.Pan(new Vector3((float)-delta.X * 2f, (float)delta.Y * 2f, 0));
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        float factor = e.Delta > 0 ? 0.9f : 1.1f;
        _camera.Zoom(factor);
    }

    public void Dispose()
    {
        CompositionTarget.Rendering -= OnRendering;
        foreach (var (mesh, _) in _meshes) mesh.Dispose();
        _meshes.Clear();
        _renderer?.Dispose();
        _bridge?.Dispose();
        _device?.Dispose();
    }
}
