using System;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/// <summary>
/// Kullanıcının bir nesne (Entity) seçmesini bekleyen komut.
/// </summary>
public class PickEntityCommand : ICadCommand
{
    private readonly CadDatabase _database;
    public string CommandName => "PICK_ENTITY";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;
    public event Action<CadEntity>? OnEntityPicked;

    public PickEntityCommand(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Komutu BaÅŸlat (Start)
       NEDEN: KullanÄ±cÄ±yÄ± nesne seÃ§meye yÃ¶nlendiren mesajÄ± gÃ¶stermek iÃ§in.
    */
    public void Start()
    {
        OnFeedback?.Invoke("Lütfen bir nesne seçin...");
    }

    /*
       NE: TÄ±klama OlayÄ± (OnPointerPressed)
       NEDEN: TÄ±klanan noktadaki nesneyi (Entity) veritabanÄ±ndan sorgulayıp seÃ§im işlemini tamamlamak iÃ§in.
    */
    public void OnPointerPressed(Vector3D point)
    {
        // 5 birimlik minik bir seçim kutusu oluştur (Tolerance)
        double tolerance = 5.0; 
        var range = new CadBoundingBox(
            new Vector3D(point.X - tolerance, point.Y - tolerance, point.Z - 100),
            new Vector3D(point.X + tolerance, point.Y + tolerance, point.Z + 100)
        );

        var found = _database.QueryEntities(range).FirstOrDefault();
        if (found != null)
        {
            OnEntityPicked?.Invoke(found);
            OnCompleted?.Invoke();
        }
        else
        {
            OnFeedback?.Invoke("Nesne bulunamadı, lütfen tekrar deneyin.");
        }
    }

    /*
       NE: Fare Hareket OlayÄ± (OnPointerMoved)
       NEDEN: SeÃ§im sÄ±rasÄ±nda herhangi bir dinamik Ã¶nizleme gerekirse kullanmak iÃ§in.
    */
    public void OnPointerMoved(Vector3D point) { }
    /*
       NE: Klavye GiriÅŸ OlayÄ± (OnKeyDown)
       NEDEN: ESC tuÅŸu ile nesne seÃ§imini iptal etmek iÃ§in.
    */
    public void OnKeyDown(InputKey key) 
    {
        if (key == InputKey.Escape)
        {
            OnCompleted?.Invoke();
        }
    }

    /*
       NE: YardÄ±mcÄ± Ã‡izim (Draw)
       NEDEN: SeÃ§im imlecini veya seçim penceresini render döngüsünde göstermek için.
    */
    public void Draw(IRenderContext context) { }
    /*
       NE: Komutu Ä°ptal Et (Cancel)
       NEDEN: Nesne seÃ§imini sonlandÄ±rmak iÃ§in.
    */
    public void Cancel() 
    {
        OnCompleted?.Invoke();
    }
}
