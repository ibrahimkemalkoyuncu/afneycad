using System;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/// <summary>
/// Kullanıcının çizim alanında bir nokta (koordinat) seçmesini bekleyen jenerik komut.
/// PickEntityCommand'dan farklı olarak nesne aramaz, direkt tıklanan koordinatı döndürür.
/// </summary>
public class PickPointCommand : ICadCommand
{
    public string CommandName => "PICK_POINT";
    public Vector3D? ActivePoint { get; private set; }

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;
    
    // Kullanıcı noktayı seçtiğinde tetiklenir — koordinatı parametre olarak verir
    public event Action<Vector3D>? OnPointPicked;

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Kullanıcıya hangi noktayı seçmesi gerektiğini söyleyen mesajı göstermek için.
    */
    public void Start()
    {
        OnFeedback?.Invoke("Bir nokta tıklayın...");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: Tıklanan koordinatı doğrudan callback ile üste bildirmek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        ActivePoint = point;
        OnPointPicked?.Invoke(point);
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point)
    {
        // Dinamik önizleme için temel altyapı — şimdilik boş
    }

    /*
       NE: Klavye Girişi (OnKeyDown)
       NEDEN: ESC ile seçimi iptal etmek için.
    */
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape)
        {
            OnCompleted?.Invoke();
        }
    }

    public void Draw(IRenderContext context) { }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}
