using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Services;
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
    private CadDatabase? _pendingDatabase;

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

            if (_pendingDatabase != null)
            {
                RebuildMeshesFromDatabase(_pendingDatabase);
            }
            else
            {
                // Veritabanı verilmediyse (ör. bağımsız test) Faz 1 test mesh'i: B-Rep
                // kernel'inden gerçek bir küp (2000x2000x2000mm) — pipeline'ın kendisini
                // (device+köprü+shader) doğrulamak için minimal, veriden bağımsız bir örnek.
                var testCube = BRepBuilder.ExtrudeBox(new Vector3D(-1000, -1000, -1000), Vector3D.XAxis, Vector3D.YAxis, Vector3D.ZAxis, 2000, 2000, 2000);
                var (vertices, faces) = BRepTessellator.Tessellate(testCube);
                _meshes.Add((new MeshBuffer(_device.Device, vertices, faces), new Vector4(0.35f, 0.55f, 0.85f, 1f)));

                _camera.Target = Vector3.Zero;
                _camera.Distance = 6000f;
            }

            Resize();
            CompositionTarget.Rendering += OnRendering;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"D3D11 başlatma hatası (OnLoaded):\n\n{ex}", "D3D11 Init Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /*
       NE: Gerçek Proje Verisini Yükle (LoadFromDatabase) — Faz 2
       NEDEN: Faz 1'in sabit test küpü yerine, mevcut B-Rep servislerinin (WallBRepService/
              DuctBRepService/Pipe3DModelService — zaten vardı; DoorWindowBRepService/
              FixtureBRepService/RoomBRepService — bu oturumda eklendi, "genelleştirilmiş
              B-Rep adaptörü") ürettiği GERÇEK proje geometrisini render eder. Renk şeması
              `Pipe3DViewWindow.xaml.cs`'teki (mevcut, kanıtlanmış WPF Viewport3D görüntüleyici)
              SystemType→renk eşlemesiyle BİREBİR aynı tutuluyor — iki görüntüleyici arasında
              tutarlı bir görsel dil.
       NASIL: Kontrol henüz `OnLoaded` çalışmadıysa (cihaz yok), veritabanı `_pendingDatabase`'e
              saklanır ve `OnLoaded` içinde otomatik yüklenir — çağıran (ör. Direct3DTestWindow)
              bu metodu constructor'dan hemen sonra çağırabilir, Loaded event sırasını bilmesine
              gerek yok.
    */
    public void LoadFromDatabase(CadDatabase database)
    {
        _pendingDatabase = database;
        if (_device != null)
            RebuildMeshesFromDatabase(database);
    }

    private void RebuildMeshesFromDatabase(CadDatabase database)
    {
        if (_device == null) return;

        foreach (var (mesh, _) in _meshes) mesh.Dispose();
        _meshes.Clear();

        // Kamerayı içeriğe odaklamak için gerçek dünya-uzayı sınırlayıcı kutusu — MeshBuffer
        // ham vertex verisini GPU'ya yükledikten sonra CPU'da SAKLAMADIĞI için (bellek tasarrufu,
        // bkz. MeshBuffer.cs), bu min/max burada, vertex listeleri hâlâ elimizdeyken biriktiriliyor.
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        void ExpandBounds(IReadOnlyList<Vector3D> verts)
        {
            foreach (var v in verts)
            {
                var p = new Vector3((float)v.X, (float)v.Y, (float)v.Z);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        void AddSolid(Solid solid, Vector4 color)
        {
            var (verts, faces) = BRepTessellator.Tessellate(solid);
            if (verts.Count < 3 || faces.Count == 0) return;
            ExpandBounds(verts);
            _meshes.Add((new MeshBuffer(_device.Device, verts, faces), color));
        }

        // Borular/dirsekler/te'ler/redüksiyonlar — Pipe3DModelService kendi üçgenlemesini üretir (BRepTessellator'a gerek yok).
        var pipeResult = new Pipe3DModelService(database).GenerateAll3DModels(Pipe3DModelService.LevelOfDetail.LOD200);
        foreach (var model in pipeResult.Models)
        {
            if (model.Vertices.Count < 3 || model.Faces.Count == 0) continue;
            Vector4 color = model.SystemType switch
            {
                "DomesticColdWater" => new Vector4(0.12f, 0.56f, 1.00f, 1f), // DodgerBlue
                "DomesticHotWater"  => new Vector4(1.00f, 0.27f, 0.00f, 1f), // OrangeRed
                "WasteWater"        => new Vector4(0.50f, 0.50f, 0.50f, 1f), // Gray
                _                   => new Vector4(0.82f, 0.82f, 0.82f, 1f)  // LightGray
            };
            ExpandBounds(model.Vertices);
            _meshes.Add((new MeshBuffer(_device.Device, model.Vertices, model.Faces), color));
        }

        // Kanallar
        foreach (var solid in new DuctBRepService(database).GenerateAllDuctSolids())
            AddSolid(solid, new Vector4(0.24f, 0.70f, 0.44f, 1f)); // MediumSeaGreen

        // Duvarlar
        foreach (var solid in new WallBRepService(database).GenerateAllWallSolids())
            AddSolid(solid, new Vector4(0.78f, 0.78f, 0.78f, 0.85f));

        // Kapı/Pencere — Faz 2 genelleştirilmiş B-Rep adaptörü (yeni)
        foreach (var door in database.GetAllEntities().OfType<Afney.Cad.Mechanical.Entities.DoorEntity>())
            AddSolid(new DoorWindowBRepService(database).GenerateDoorSolid(door), new Vector4(0.87f, 0.72f, 0.53f, 1f)); // BurlyWood
        foreach (var window in database.GetAllEntities().OfType<Afney.Cad.Mechanical.Entities.WindowEntity>())
            AddSolid(new DoorWindowBRepService(database).GenerateWindowSolid(window), new Vector4(0.31f, 0.76f, 0.97f, 0.55f)); // Cam (yarı saydam)

        // Sıhhi tesisat cihazları
        foreach (var solid in new FixtureBRepService(database).GenerateAllSolids())
            AddSolid(solid, new Vector4(0.95f, 0.95f, 0.95f, 1f));

        // Mahal zemin döşemeleri
        foreach (var solid in new RoomBRepService(database).GenerateAllSolids())
            AddSolid(solid, new Vector4(0.55f, 0.45f, 0.85f, 0.35f)); // yarı saydam mor — sadece sınır göstergesi

        // NE/NEDEN: Genel katı cisimler (SolidEntity — CSG Boolean UNION/SUBTRACT/INTERSECT
        // sonuçları, bkz. Afney.Cad.Domain.Entities.Basic.SolidEntity). Önceden bu entity
        // tipi burada HİÇ ele alınmıyordu — CadDatabase'e eklenen bir Solid, 2D wireframe
        // (SolidEntity.Draw) DIŞINDA hiçbir yerde görünmüyordu, 3D görünümde tamamen
        // GÖRÜNMEZDİ. Diğer tüm Wall/Duct/Door/Fixture/Room satırlarıyla AYNI mevcut
        // BRepTessellator→MeshBuffer pipeline'ı (AddSolid) doğrudan kullanılabiliyor, çünkü
        // SolidEntity zaten kendi Topology.Solid'ini taşıyor — ayrı bir üretim servisi
        // (WallBRepService vb.) gerekmiyor.
        foreach (var solidEntity in database.GetAllEntities().OfType<SolidEntity>())
            AddSolid(solidEntity.Solid, new Vector4(0.85f, 0.65f, 0.13f, 1f)); // amber/altın — CSG solid'leri ayırt edici renk

        FrameCameraToBounds(min, max);
    }

    /// <summary>Kamerayı verilen dünya-uzayı sınırlayıcı kutunun merkezine odaklar, kutunun çaplı uzaklığına göre geri çeker.</summary>
    private void FrameCameraToBounds(Vector3 min, Vector3 max)
    {
        if (_meshes.Count == 0 || min.X > max.X)
        {
            _camera.Target = Vector3.Zero;
            _camera.Distance = 6000f;
            return;
        }

        var center = (min + max) / 2f;
        float diagonal = Vector3.Distance(min, max);
        _camera.Target = center;
        // Çap sıfıra yakınsa (tek küçük entity) yine de makul bir minimum uzaklık kullan.
        _camera.Distance = Math.Max(diagonal * 1.2f, 1500f);
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
        if (!IsVisible) return; // 2D moda geçildiğinde (Visibility=Collapsed) GPU döngüsünü boşa harcama
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
