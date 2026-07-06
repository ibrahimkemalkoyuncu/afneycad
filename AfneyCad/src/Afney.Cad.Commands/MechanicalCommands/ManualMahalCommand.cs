using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   NE: Manuel Mahal Tanımlama Komutu (ManualMahalCommand)
   NEDEN: Kullanıcının duvarları tek tek tıklayarak veya boşluklara (kapı/pencere)
          serbest nokta ekleyerek kapalı mahal sınırı oluşturabilmesi için.

   KULLANICI AKIŞI:
   1. Komut başlar → Status: "Duvarları veya boşluk noktalarını tıklayın [Enter/Sağ Tık=Bitir, ESC=İptal]"
   2. Tıklama duvar üzerinde → duvar seçilir (yeşil vurgu)
   3. Tıklama boşlukta (kapı/pencere arası) → serbest nokta eklenir (sarı kare)
   4. Enter / Sağ Tık → WallChainBuilder ile polygon oluşturulur
   5. Başarılıysa → onRoomCreated callback → dialog açılır

   MÜHENDİSLİK NOTU:
   - Duvar pick toleransı: 500mm
   - Boş alan tıklaması → _freePoints listesine nokta eklenir (gap köprüleme)
   - Zincir: duvar endpoint'leri + free point'ler birlikte kullanılır
   - Draw() her frame duvarları, serbest noktaları ve zinciri gösterir
*/
public class ManualMahalCommand : ICadCommand
{
    // ─── Bağımlılıklar ─────────────────────────────────────────────────────────
    private readonly CadDatabase _database;
    private readonly Action<MahalEntity> _onRoomCreated;
    private readonly WallChainBuilder _chainBuilder;

    // Seçili duvar entity'leri (toggle davranışı)
    private readonly List<CadEntity> _selectedWalls = new();

    // Serbest noktalar: kapı/pencere boşluklarını köprülemek için
    private readonly List<Vector3D> _freePoints = new();

    // Anlık imleç konumu (ghost çizimi için)
    private Vector3D _cursor;

    // ─── ICadCommand ───────────────────────────────────────────────────────────
    public string CommandName => "MANUEL_MAHAL";
    public Vector3D? ActivePoint => _cursor;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    // ─── Constructor ───────────────────────────────────────────────────────────
    public ManualMahalCommand(CadDatabase database, Action<MahalEntity> onRoomCreated, double gapTolerance = 2500.0)
    {
        _database = database;
        _onRoomCreated = onRoomCreated;
        _chainBuilder = new WallChainBuilder { GapTolerance = gapTolerance };
    }

    // ─── Komut Yaşam Döngüsü ───────────────────────────────────────────────────

    public void Start()
    {
        _selectedWalls.Clear();
        _freePoints.Clear();
        Serilog.Log.Information("[ManualMahal] Komut başlatıldı.");
        OnFeedback?.Invoke("Oda duvarlarını tıklayın (kapı/pencere boşlukları otomatik köprülenir) → [Enter] Mahal oluştur | [ESC] İptal");
    }

    /*
       NE: Tıklama (OnPointerPressed)
       NEDEN:
         - Tıklanan konum bir duvar segmentine yakınsa (< 500mm) → duvar seçilir (toggle)
         - Uzaktaysa → serbest "gap noktası" olarak eklenir (kapı/pencere boşluğu köprüleme)
       BU SAYEDE: Kullanıcı kapı/pencere arasındaki boşluğu düz konturla tamamlayabilir.
    */
    public void OnPointerPressed(Vector3D position)
    {
        // Tüm Line ve LwPolyline entity'lerini al (potansiyel duvar)
        var candidates = _database.GetAllEntities()
            .Where(e => e is LineEntity || e is LwPolylineEntity)
            .ToList();

        const double PickTolerance = 500.0;

        // En yakın entity'yi bul
        CadEntity? nearest = null;
        double nearestDist = double.MaxValue;

        foreach (var ent in candidates)
        {
            double d = SegmentDistance(ent, position);
            if (d < nearestDist) { nearestDist = d; nearest = ent; }
        }

        if (nearest != null && nearestDist <= PickTolerance)
        {
            // ── Duvar içinde: toggle seç/kaldır ──────────────────────────────
            var existing = _selectedWalls.FirstOrDefault(w => w.Id == nearest.Id);
            if (existing != null)
            {
                _selectedWalls.Remove(existing);
                Serilog.Log.Information("[ManualMahal] Duvar seçimden çıkarıldı: {Id}", nearest.Id);
            }
            else
            {
                _selectedWalls.Add(nearest);
                Serilog.Log.Information("[ManualMahal] Duvar eklendi: {Id}", nearest.Id);
            }
            int total = _selectedWalls.Count + _freePoints.Count;
            OnFeedback?.Invoke($"{_selectedWalls.Count} duvar + {_freePoints.Count} boşluk noktası. [Enter] ile bitirin.");
        }
        else
        {
            // ── Boşluk: serbest nokta ekle (kapı/pencere gap köprüleme) ──────
            _freePoints.Add(new Vector3D(position.X, position.Y, 0));
            Serilog.Log.Information("[ManualMahal] Serbest gap noktası eklendi: ({X:F0},{Y:F0})", position.X, position.Y);
            int total = _selectedWalls.Count + _freePoints.Count;
            OnFeedback?.Invoke($"{_selectedWalls.Count} duvar + {_freePoints.Count} boşluk noktası. [Enter] ile bitirin.");
        }
    }

    /*
       NE: Tuş Basımı (OnKeyDown)
       NEDEN: Enter/Space → polygon oluştur. Escape → iptal.
    */
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter || key == InputKey.Space)
            FinalizeRoom();
        else if (key == InputKey.Escape)
            Cancel();
    }

    /*
       NE: Mahal Oluştur (FinalizeRoom)
       NEDEN: Seçilen duvarlardan + serbest gap noktalarından WallChainBuilder ile
              kapalı polygon üretip MahalEntity callback'ini tetiklemek için.

       MANTIK:
       1. Duvarlar → segment endpoint'leri çıkarılır
       2. Serbest gap noktaları → tek noktalı "sıfır uzunluklu segment" olarak eklenir
       3. WallChainBuilder greedy chain algoritmasıyla hepsini dizer
       4. Polygon 3+ köşeliyse → MahalEntity oluşturulur
    */
    private void FinalizeRoom()
    {
        if (_selectedWalls.Count < 2)
        {
            OnFeedback?.Invoke("En az 2 duvar seçilmeli. Devam edin.");
            return;
        }

        // Duvar segmentlerini çıkar
        var segments = WallChainBuilder.ExtractSegments(_selectedWalls);

        // Serbest gap noktalarını "nokta segmenti" olarak ekle
        foreach (var pt in _freePoints)
            segments.Add((pt, pt)); // Başlangıç = bitiş; chain builder nokta olarak işler

        // Zincir → kapalı polygon
        var polygon = _chainBuilder.Build(segments, out string status);

        if (polygon == null || polygon.Count < 3)
        {
            OnFeedback?.Invoke($"HATA: {status} — Daha fazla duvar veya boşluk noktası ekleyin.");
            Serilog.Log.Warning("[ManualMahal] Polygon oluşturulamadı: {Status}", status);
            return;  // Seçimler korunuyor, kullanıcı düzeltebilir
        }

        // MahalEntity oluştur (MahalDetailsDialog ile uyumlu)
        var mahal = new MahalEntity(polygon, "Yeni Mahal");
        Serilog.Log.Information("[ManualMahal] MahalEntity oluşturuldu: {Count} köşe, Alan: {Area:F2}m²",
            polygon.Count, mahal.Area);

        OnFeedback?.Invoke($"{status} Alan: {mahal.Area:F2} m²");
        _onRoomCreated?.Invoke(mahal);
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D position)
    {
        _cursor = position;
    }

    /*
       NE: İptal (Cancel)
       NEDEN: ESC tuşuyla komut iptal edildiğinde tüm geçici seçimler temizlenir.
              Sağ tık artık Cancel() DEĞİL OnKeyDown(Enter) çağırıyor (Viewport'ta düzeltildi).
    */
    public void Cancel()
    {
        _selectedWalls.Clear();
        _freePoints.Clear();
        Serilog.Log.Information("[ManualMahal] Komut ESC ile iptal edildi.");
        OnFeedback?.Invoke("Manuel mahal tanımlama iptal edildi.");
        OnCompleted?.Invoke();
    }

    /*
       NE: Anlık Çizim (Draw)
       NEDEN: Seçili duvarları yeşil, serbest gap noktalarını sarı kare,
              ve son elemana kadar ghost line göstermek için.
    */
    public void Draw(IRenderContext context)
    {
        const uint SelectedColor = 0xBB00CC44;  // yeşil yarı saydam
        const uint FreePointColor = 0xFFFFCC00; // sarı nokta (gap)
        const uint GhostColor = 0x662299FF;     // mavi, zincir önizleme

        // 1. Seçili duvarları yeşil vurgula
        foreach (var ent in _selectedWalls)
        {
            if (ent is LineEntity line)
            {
                context.DrawLine(line.StartPoint, line.EndPoint, SelectedColor, 3.0);
            }
            else if (ent is LwPolylineEntity poly)
            {
                for (int i = 0; i < poly.Vertices.Count - 1; i++)
                    context.DrawLine(poly.Vertices[i], poly.Vertices[i + 1], SelectedColor, 3.0);
                if (poly.IsClosed && poly.Vertices.Count > 2)
                    context.DrawLine(poly.Vertices.Last(), poly.Vertices[0], SelectedColor, 3.0);
            }
        }

        // 2. Serbest gap noktalarını sarı kare olarak göster
        foreach (var pt in _freePoints)
        {
            // Köşegen çapraz (X) - 150mm boyutunda
            double d = 150.0;
            context.DrawLine(new Vector3D(pt.X - d, pt.Y - d, 0), new Vector3D(pt.X + d, pt.Y + d, 0), FreePointColor, 2.0);
            context.DrawLine(new Vector3D(pt.X - d, pt.Y + d, 0), new Vector3D(pt.X + d, pt.Y - d, 0), FreePointColor, 2.0);
        }

        // 3. Son elemandan imlece ghost line
        var lastPoint = GetLastPoint();
        if (lastPoint.HasValue)
        {
            context.DrawLine(lastPoint.Value, _cursor, GhostColor, 1.0);
        }
    }

    // ─── Yardımcılar ───────────────────────────────────────────────────────────

    /// <summary>Son seçili eleman (duvar sonu veya serbest nokta) konumunu döndürür.</summary>
    private Vector3D? GetLastPoint()
    {
        // Hangi eleman daha sonra eklendi: duvar mı yoksa serbest nokta mı?
        // İkisini birlikte sıralı tutmuyoruz; en pratik yaklaşım:
        // son eklenen duvar bitiş noktası ile son serbest nokta arasında
        // hangisi varsa onu döndür (her ikisi de varsa duvarı önceliklendir)
        if (_selectedWalls.Count > 0)
        {
            var lastEnt = _selectedWalls[_selectedWalls.Count - 1];
            return lastEnt is LineEntity l ? l.EndPoint
                 : lastEnt is LwPolylineEntity p ? p.Vertices.Last()
                 : lastEnt.GetBoundingBox().Center;
        }
        if (_freePoints.Count > 0)
            return _freePoints[_freePoints.Count - 1];

        return null;
    }

    /*
       NE: Entity'den Noktaya Minimum Mesafe (SegmentDistance)
       NEDEN: Tıklanan noktaya en yakın duvarı bulmak için segment bazlı mesafe hesabı.
    */
    private static double SegmentDistance(CadEntity ent, Vector3D point)
    {
        if (ent is LineEntity line)
            return PointToSegmentDistance(point, line.StartPoint, line.EndPoint);

        if (ent is LwPolylineEntity poly && poly.Vertices.Count >= 2)
        {
            double minD = double.MaxValue;
            for (int i = 0; i < poly.Vertices.Count - 1; i++)
            {
                double d = PointToSegmentDistance(point, poly.Vertices[i], poly.Vertices[i + 1]);
                if (d < minD) minD = d;
            }
            return minD;
        }

        return point.DistanceTo(ent.GetBoundingBox().Center);
    }

    /*
       NE: Noktadan Segmente Dik Mesafe (PointToSegmentDistance)
       NEDEN: Segment ortasına tıklarken de doğru mesafeyi bulmak için dik projeksiyon kullanılır.
    */
    private static double PointToSegmentDistance(Vector3D p, Vector3D a, Vector3D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return p.DistanceTo(a);

        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0.0, 1.0);
        var proj = new Vector3D(a.X + t * dx, a.Y + t * dy, 0);
        return p.DistanceTo(proj);
    }
}
