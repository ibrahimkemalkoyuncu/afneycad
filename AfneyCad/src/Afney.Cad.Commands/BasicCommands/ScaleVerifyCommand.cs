using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Ölçek Doğrulama Komutu (ScaleVerifyCommand)
   NEDEN: Kullanıcı isteği — mimardan gelen bir DWG'nin $INSUNITS'i yanlış/eksik olabilir
          (DwgImportService artık INSUNITS'i doğru okuyup uyguluyor, ama dosyanın KENDİSİ
          bu bilgiyi hiç taşımıyorsa veya yanlış taşıyorsa otomatik algılama işe yaramaz).
          Bu komut, DIST komutuyla AYNI 2-nokta seçim akışını kullanır ama mesafeyi
          raporlamak yerine, kullanıcının bildiği GERÇEK bir ölçüyle (ör. bir kapı
          genişliği) karşılaştırıp düzeltme çarpanı hesaplayacak bir dialog açılmasını
          tetikler (bkz. MainWindow.Engineering.cs: OnScaleVerifyCommand).

   KULLANICI AKIŞI:
   1. Komut başlar → "Bilinen bir ölçünün 1. noktasını seçin"
   2. 1. tıklama → "2. noktasını seçin"
   3. 2. tıklama → onMeasured(P1, P2) callback tetiklenir, komut biter (dialog UI katmanında açılır)
   4. ESC → iptal
*/
public class ScaleVerifyCommand : ICadCommand
{
    private readonly System.Action<Vector3D, Vector3D> _onMeasured;
    private Vector3D? _p1;
    private Vector3D _cursor;

    public string CommandName => "SCALE_VERIFY";
    public Vector3D? ActivePoint => _p1;

    public event System.Action<string>? OnFeedback;
    public event System.Action? OnCompleted;

    public ScaleVerifyCommand(System.Action<Vector3D, Vector3D> onMeasured)
    {
        _onMeasured = onMeasured;
    }

    public void Start()
    {
        _p1 = null;
        OnFeedback?.Invoke("Ölçek Doğrula: Gerçek uzunluğunu bildiğiniz bir ölçünün 1. noktasını seçin (ör. bir kapı kenarı).");
    }

    public void OnPointerPressed(Vector3D point)
    {
        if (_p1 == null)
        {
            _p1 = point;
            OnFeedback?.Invoke("Ölçek Doğrula: 2. noktayı seçin.");
        }
        else
        {
            var p1 = _p1.Value;
            _p1 = null;
            _onMeasured?.Invoke(p1, point);
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        _cursor = point;
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) Cancel();
    }

    public void Cancel()
    {
        _p1 = null;
        OnFeedback?.Invoke("Ölçek doğrulama iptal edildi.");
        OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context)
    {
        if (_p1 == null) return;
        context.DrawLine(_p1.Value, _cursor, 0xFFFF6600, 0, "Dashed", true);

        double dist = (_cursor - _p1.Value).Length();
        var mid = new Vector3D((_p1.Value.X + _cursor.X) / 2, (_p1.Value.Y + _cursor.Y) / 2, 0);
        string label = dist >= 1000 ? $"{dist / 1000.0:F3} m" : $"{dist:F1} mm";
        /*
           MÜHENDİSLİK: Sabit fontSize=150(mm) burada özellikle sorunlu — bu komutun asıl
           kullanım senaryosu KÜÇÜK bir referans ölçüyü (ör. 200mm'lik bir kapı pervazı)
           hassas tıklamak için yakın zoom yapmayı GEREKTİRİYOR; sabit değer zoom ile
           çarpılıp 300px cap'e vurunca ekranı kaplayan devasa bir etiket çıkıyordu
           (kullanıcı canlı testte yakaladı — DistCommand'da da aynı hata vardı, orada da
           düzeltildi). Artık ölçülen mesafeyle orantılı.
        */
        double fontSize = Math.Clamp(dist * 0.12, 20.0, 150.0);
        context.DrawText(label, mid, 0, fontSize, 0xFFFF6600);
    }
}
