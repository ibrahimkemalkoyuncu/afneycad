using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using System;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Nokta Seçme Komutu (InsertPointCommand)
    NEDEN: Kullanıcıdan tek bir nokta (Koordinat) almak için genel amaçlı yardımcı komut.
    KULLANIM: Kolon şeması yerleşimi, tablo yerleşimi vb. işlemlerde hedef noktayı belirlemek için.
*/
public class InsertPointCommand : ICadCommand
{
    private readonly Action<Vector3D> _onPointSelected;

    public string CommandName => "PICK_POINT";
    public Vector3D? ActivePoint { get; private set; }

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public InsertPointCommand(Action<Vector3D> onPointSelected)
    {
        _onPointSelected = onPointSelected;
    }

    public void Start()
    {
        OnFeedback?.Invoke("Bir nokta seçin...");
    }

    public void OnPointerPressed(Vector3D point)
    {
        try
        {
            _onPointSelected?.Invoke(point);
            OnFeedback?.Invoke($"Nokta seçildi: {point}");
        }
        catch (Exception ex)
        {
            OnFeedback?.Invoke($"Hata: {ex.Message}");
        }
        finally
        {
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        ActivePoint = point;
        // Dinamik koordinat gösterimi için MainWindow'da kullanılabilir
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape)
        {
            Cancel();
        }
    }

    public void Draw(IRenderContext context)
    {
        // İmleç konumunda küçük bir '+' işareti çizilebilir
        if (ActivePoint.HasValue)
        {
            var p = ActivePoint.Value;
            double size = 100.0; // Dünya koordinatlarında boyut
            context.DrawLine(new Vector3D(p.X - size, p.Y, 0), new Vector3D(p.X + size, p.Y, 0), 0xFFFFFF00);
            context.DrawLine(new Vector3D(p.X, p.Y - size, 0), new Vector3D(p.X, p.Y + size, 0), 0xFFFFFF00);
        }
    }

    public void Cancel()
    {
        OnFeedback?.Invoke("İşlem iptal edildi.");
        OnCompleted?.Invoke();
    }
}
