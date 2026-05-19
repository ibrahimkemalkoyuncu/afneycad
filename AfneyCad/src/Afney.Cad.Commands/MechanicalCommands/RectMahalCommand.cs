using System;
using System.Collections.Generic;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   NE: Dikdörtgen Mahal Tanımlama Komutu (RectMahalCommand)
   NEDEN: 2 köşe noktası tıklayarak tam dikdörtgen bir mahal sınırı oluşturmak için.
          Kapı, pencere, boşluk fark etmez — dikdörtgen çizilir.

   KULLANICI AKIŞI:
   1. Komut başlar → "1. köşeyi tıklayın"
   2. 1. tıklama → kırmızı köşe noktası sabitlenir, ghost dikdörtgen başlar
   3. 2. tıklama (çapraz köşe) → VEYA Enter/Sağ Tık → mahal oluşturulur
   4. ESC → iptal

   MÜHENDİSLİK NOTU:
   - Polygon köşeleri: P1(X1,Y1), P2(X2,Y1), P3(X2,Y2), P4(X1,Y2) — saat yönü
   - WallChainBuilder gerekmez; 4 köşe doğrudan polygon
   - Draw() her frame imleçle birlikte dikdörtgen ghost çizer
*/
public class RectMahalCommand : ICadCommand
{
    // ─── Bağımlılıklar ─────────────────────────────────────────────────────────
    private readonly Action<MahalEntity> _onRoomCreated;

    // Durumlar
    private Vector3D? _firstCorner;   // 1. tıklama noktası
    private Vector3D  _cursor;        // Anlık imleç (ghost için)

    // ─── ICadCommand ───────────────────────────────────────────────────────────
    public string    CommandName  => "RECT_MAHAL";
    public Vector3D? ActivePoint  => _cursor;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    // ─── Constructor ───────────────────────────────────────────────────────────
    public RectMahalCommand(Action<MahalEntity> onRoomCreated)
    {
        _onRoomCreated = onRoomCreated;
    }

    // ─── Komut Yaşam Döngüsü ───────────────────────────────────────────────────

    public void Start()
    {
        _firstCorner = null;
        Serilog.Log.Information("[RectMahal] Dikdörtgen mahal komutu başlatıldı.");
        OnFeedback?.Invoke("📍 1. köşeyi tıklayın");
    }

    /*
       NE: Tıklama (OnPointerPressed)
       NEDEN:
         - İlk tıklama → 1. köşeyi sabitle
         - İkinci tıklama → dikdörtgen oluştur (2. köşe = çapraz köşe)
    */
    public void OnPointerPressed(Vector3D position)
    {
        if (_firstCorner == null)
        {
            // 1. köşe belirlendi
            _firstCorner = new Vector3D(position.X, position.Y, 0);
            Serilog.Log.Information("[RectMahal] 1. köşe: ({X:F0}, {Y:F0})", position.X, position.Y);
            OnFeedback?.Invoke("✅ 1. köşe belirlendi. 📍 2. köşeyi (çapraz) tıklayın veya [Enter/Sağ Tık] ile bitirin.");
        }
        else
        {
            // 2. köşe → dikdörtgen oluştur
            FinalizeRect(new Vector3D(position.X, position.Y, 0));
        }
    }

    /*
       NE: Tuş Basımı (OnKeyDown)
       NEDEN: Enter/Space → mevcut imleç konumunu 2. köşe olarak kullan.
              Escape → iptal.
    */
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter || key == InputKey.Space)
        {
            if (_firstCorner == null)
            {
                OnFeedback?.Invoke("Önce bir köşe tıklayın.");
                return;
            }
            FinalizeRect(_cursor);
        }
        else if (key == InputKey.Escape)
        {
            Cancel();
        }
    }

    /*
       NE: Dikdörtgen Oluştur (FinalizeRect)
       NEDEN: 2 köşeden 4 köşeli dikdörtgen polygon üretip MahalEntity callback'i tetiklemek.

       KÖŞE SIRASI (saat yönü, CCW polygon):
           P1 — P2
           |        |
           P4 — P3
       Yani: (x1,y1), (x2,y1), (x2,y2), (x1,y2)
    */
    private void FinalizeRect(Vector3D secondCorner)
    {
        var p1 = _firstCorner!.Value;
        var p2 = secondCorner;

        double x1 = Math.Min(p1.X, p2.X);
        double y1 = Math.Min(p1.Y, p2.Y);
        double x2 = Math.Max(p1.X, p2.X);
        double y2 = Math.Max(p1.Y, p2.Y);

        if (Math.Abs(x2 - x1) < 1.0 || Math.Abs(y2 - y1) < 1.0)
        {
            OnFeedback?.Invoke("Dikdörtgen çok küçük. Daha geniş bir alan seçin.");
            return;
        }

        var polygon = new List<Vector3D>
        {
            new(x1, y1, 0),   // Sol-alt
            new(x2, y1, 0),   // Sağ-alt
            new(x2, y2, 0),   // Sağ-üst
            new(x1, y2, 0),   // Sol-üst
        };

        double w = (x2 - x1) / 1000.0; // mm → m
        double h = (y2 - y1) / 1000.0;
        double area = w * h;

        var mahal = new MahalEntity(polygon, "Yeni Mahal");
        Serilog.Log.Information("[RectMahal] Dikdörtgen mahal oluşturuldu: {W:F2}m × {H:F2}m = {Area:F2}m²", w, h, mahal.Area);

        OnFeedback?.Invoke($"Dikdörtgen mahal oluşturuldu: {mahal.Area:F2} m²");
        _onRoomCreated?.Invoke(mahal);
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D position)
    {
        _cursor = position;
    }

    public void Cancel()
    {
        _firstCorner = null;
        Serilog.Log.Information("[RectMahal] Komut ESC ile iptal edildi.");
        OnFeedback?.Invoke("Dikdörtgen mahal tanımlama iptal edildi.");
        OnCompleted?.Invoke();
    }

    /*
       NE: Anlık Çizim (Draw)
       NEDEN: 1. köşe belirlendikten sonra imleç konumuna göre ghost dikdörtgen göstermek için.
    */
    public void Draw(IRenderContext context)
    {
        const uint GhostColor  = 0x88FF6600; // turuncu yarı saydam
        const uint CornerColor = 0xFFFF3300; // kırmızı — 1. köşe işareti
        const double CrossSize = 150.0;      // mm

        // 1. Köşe sabitlenmişse kırmızı + işareti çiz
        if (_firstCorner.HasValue)
        {
            var p = _firstCorner.Value;
            context.DrawLine(new Vector3D(p.X - CrossSize, p.Y, 0), new Vector3D(p.X + CrossSize, p.Y, 0), CornerColor, 2.0);
            context.DrawLine(new Vector3D(p.X, p.Y - CrossSize, 0), new Vector3D(p.X, p.Y + CrossSize, 0), CornerColor, 2.0);

            // Ghost dikdörtgen (2. köşe = imleç)
            double x1 = Math.Min(p.X, _cursor.X);
            double y1 = Math.Min(p.Y, _cursor.Y);
            double x2 = Math.Max(p.X, _cursor.X);
            double y2 = Math.Max(p.Y, _cursor.Y);

            var tl = new Vector3D(x1, y2, 0);
            var tr = new Vector3D(x2, y2, 0);
            var br = new Vector3D(x2, y1, 0);
            var bl = new Vector3D(x1, y1, 0);

            context.DrawLine(tl, tr, GhostColor, 1.5);
            context.DrawLine(tr, br, GhostColor, 1.5);
            context.DrawLine(br, bl, GhostColor, 1.5);
            context.DrawLine(bl, tl, GhostColor, 1.5);
        }
    }
}
