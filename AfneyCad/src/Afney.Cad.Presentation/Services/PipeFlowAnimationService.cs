using System;
using System.Linq;
using System.Windows.Threading;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using SkiaSharp;

namespace Afney.Cad.Presentation.Services;

/*
   NE: Boru Ağı Akış Animasyonu Servisi (PipeFlowAnimationService)
   NEDEN: Tasarımcının akış yönünü ve göreli hızı görsel olarak doğrulaması için
          boruların üzerinde hareketli nokta animasyonu gösterir.

   ALGORİTMA:
   - Her boru için yön vektörü hesaplanır.
   - Animasyon fazı (0–1) zamana bağlı artar; bu faz boruların üzerinde
     eşit aralıklı noktaların konumunu belirler.
   - Nokta aralığı: DASH_SPACING_M = 0.4 m (dünya birimi).
   - Hız göstergesi: FlowRate yüksek borular daha büyük ve parlak nokta alır.

   ENTEGRASYON:
   - Start(database, invalidateCallback) → DispatcherTimer başlatır.
   - DrawOverlay(canvas, w, h, worldToScreen, zoom) → her frame çağrılır.
   - Stop() → timer durdurulur.
*/
public class PipeFlowAnimationService
{
    private const double DASH_SPACING_M = 0.4;
    private const double FPS            = 30.0;
    private const double SPEED_MULT     = 0.8;

    private DispatcherTimer? _timer;
    private CadDatabase? _database;
    private Action? _invalidate;
    private double _phase;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    // ── Başlat ───────────────────────────────────────────────────────────────────

    public void Start(CadDatabase database, Action invalidateViewport)
    {
        if (_isRunning) Stop();

        _database   = database;
        _invalidate = invalidateViewport;
        _phase      = 0;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / FPS) };
        _timer.Tick += OnTick;
        _timer.Start();
        _isRunning = true;
    }

    // ── Durdur ───────────────────────────────────────────────────────────────────

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _isRunning = false;
        _invalidate?.Invoke();
        _invalidate = null;
        _database   = null;
    }

    // ── Timer Tick ───────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        _phase = (_phase + SPEED_MULT / FPS) % 1.0;
        _invalidate?.Invoke();
    }

    // ── Overlay Çizimi ────────────────────────────────────────────────────────────
    // worldToScreen: dünya koordinatı → ekran koordinatı dönüşümü

    public void DrawOverlay(SKCanvas canvas, float viewW, float viewH,
                            Func<double, double, (float sx, float sy)> worldToScreen,
                            double zoom)
    {
        if (_database == null) return;

        var pipes = _database.GetAllEntities().OfType<PipeEntity>()
                             .Where(p => p.FlowRate > 0)
                             .ToList();
        if (pipes.Count == 0) return;

        double maxFlow = pipes.Max(p => p.FlowRate);

        using var paint = new SKPaint { IsAntialias = true };

        foreach (var pipe in pipes)
        {
            double dx  = pipe.EndPoint.X - pipe.StartPoint.X;
            double dy  = pipe.EndPoint.Y - pipe.StartPoint.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) continue;

            double ux = dx / len;
            double uy = dy / len;

            double norm = Math.Clamp(pipe.FlowRate / maxFlow, 0.1, 1.0);
            float  dotR = (float)Math.Clamp(4.0 * norm * zoom / 100.0, 2.0, 8.0);
            if (dotR < 1.5f) continue;

            paint.Color = FlowColor(pipe.SystemType, norm);

            double startDist = (len * _phase) % DASH_SPACING_M;
            for (double d = startDist; d < len; d += DASH_SPACING_M)
            {
                double wx = pipe.StartPoint.X + ux * d;
                double wy = pipe.StartPoint.Y + uy * d;
                var (sx, sy) = worldToScreen(wx, wy);

                if (sx < -10 || sy < -10 || sx > viewW + 10 || sy > viewH + 10) continue;
                canvas.DrawCircle(sx, sy, dotR, paint);
            }
        }
    }

    // ── Sistem Tipine Göre Renk ───────────────────────────────────────────────────

    private static SKColor FlowColor(MechanicalSystemType systemType, double norm)
    {
        byte a = (byte)(160 + 95 * norm);
        return systemType switch
        {
            MechanicalSystemType.DomesticColdWater => new SKColor(0x40, 0xC4, 0xFF, a),
            MechanicalSystemType.DomesticHotWater  => new SKColor(0xFF, 0x70, 0x43, a),
            MechanicalSystemType.WasteWater         => new SKColor(0x8D, 0x6E, 0x63, a),
            MechanicalSystemType.RainWater          => new SKColor(0x80, 0xDE, 0xEA, a),
            MechanicalSystemType.Gas                => new SKColor(0xFF, 0xCC, 0x02, a),
            MechanicalSystemType.FireProtection     => new SKColor(0xFF, 0x17, 0x44, a),
            _                                        => new SKColor(0xBD, 0xBD, 0xBD, a),
        };
    }
}
