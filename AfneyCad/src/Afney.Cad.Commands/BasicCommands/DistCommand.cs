using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

public class DistCommand : ICadCommand
{
    private Vector3D? _p1;
    private Vector3D _cursor;

    public string    CommandName => "DIST";
    public Vector3D? ActivePoint => _p1;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public void Start() => OnFeedback?.Invoke("DIST: Birinci noktayı seçin.");

    public void OnPointerPressed(Vector3D point)
    {
        if (_p1 == null)
        {
            _p1 = point;
            OnFeedback?.Invoke("DIST: İkinci noktayı seçin.");
        }
        else
        {
            ReportDistance(_p1.Value, point);
            _p1 = null;
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        _cursor = point;
        if (_p1 != null)
        {
            var d = point - _p1.Value;
            double dist = d.Length();
            double angle = Math.Atan2(d.Y, d.X) * 180.0 / Math.PI;
            OnFeedback?.Invoke($"Mesafe = {FormatDist(dist)},  Açı = {angle:F1}°,  ΔX = {d.X:F4},  ΔY = {d.Y:F4}");
        }
    }

    public void OnKeyDown(InputKey key) { }

    public void Draw(IRenderContext ctx)
    {
        if (_p1 == null) return;
        ctx.DrawLine(_p1.Value, _cursor, 0xFF00FF00, 0, "Dashed", true);

        double dist = (_cursor - _p1.Value).Length();
        var mid = new Vector3D((_p1.Value.X + _cursor.X) / 2, (_p1.Value.Y + _cursor.Y) / 2, 0);
        /*
           MÜHENDİSLİK: Sabit fontSize=150(mm), zoom ile çarpılıp ekran pikseline çevriliyor
           (bkz. SkiaRenderContext.DrawText: fontSize*zoomFactor, 300px'te clamp). Kullanıcı
           küçük bir detayı (ör. 200mm) ölçmek için çok yakın zoom yaptığında bu sabit değer
           300px cap'e vurup ekranı kaplayan devasa bir etiket üretiyordu. Artık ölçülen
           mesafeyle orantılı (böylece yakın zoom'da da makul kalıyor), büyük mesafelerde
           eski davranışla aynı üst sınırda kalıyor.
        */
        double fontSize = Math.Clamp(dist * 0.12, 20.0, 150.0);
        ctx.DrawText(FormatDist(dist), mid, 0, fontSize, 0xFF00FF00);
    }

    public void Cancel() { _p1 = null; }

    private void ReportDistance(Vector3D a, Vector3D b)
    {
        var d = b - a;
        double dist = d.Length();
        double xyAngle = Math.Atan2(d.Y, d.X) * 180.0 / Math.PI;
        OnFeedback?.Invoke(
            $"Mesafe = {FormatDist(dist)},  XY Düzleminde Açı = {xyAngle:F1}°,  " +
            $"Delta X = {d.X:F4},  Delta Y = {d.Y:F4},  Delta Z = {d.Z:F4}");
    }

    private static string FormatDist(double d) =>
        d >= 1000 ? $"{d / 1000.0:F3} m" : $"{d:F4}";
}
