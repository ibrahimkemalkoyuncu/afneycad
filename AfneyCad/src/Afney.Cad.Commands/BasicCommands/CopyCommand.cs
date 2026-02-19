using System;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Database.Transactions.Operations; 
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
NE:
Kopyalama Komutu (Copy).
*/
public class CopyCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<CadEntity> _entitiesToCopy;
    
    private Vector3D? _basePoint;
    private Vector3D _currentMousePos;

    // Ghost drawing
    private List<CadEntity>? _ghostEntities;

    public string CommandName => "COPY";
    public Vector3D? ActivePoint => _basePoint;
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public CopyCommand(CadDatabase database, TransactionManager transactionManager, IEnumerable<CadEntity> selection)
    {
        _database = database;
        _transactionManager = transactionManager;
        _entitiesToCopy = selection.ToList();
    }
    
    /*
       NE: Komutu BaÅŸlat (Start)
       NEDEN: Kopyalanacak nesne olup olmadÄ±ÄŸÄ±nÄ± kontrol etmek ve kullanÄ±cÄ±dan baz noktasÄ±nÄ± istemek iÃ§in.
    */
    public void Start()
    {
        if (_entitiesToCopy.Count == 0)
        {
            OnFeedback?.Invoke("COPY: Seçili nesne yok. Komut iptal.");
            OnCompleted?.Invoke();
            return;
        }

        OnFeedback?.Invoke("COPY: Baz noktasını belirtin.");
    }

    /*
       NE: TÄ±klama OlayÄ± (OnPointerPressed)
       NEDEN: Ä°lk tÄ±klamada kopyalama iÃ§in referans baz noktasÄ±nÄ± sabitlemek, ikinci tÄ±klamada ise hedef noktayÄ± belirleyip kopyalarÄ± oluÅŸturmak iÃ§in.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_basePoint == null)
        {
            _basePoint = point;
            OnFeedback?.Invoke("COPY: Hedef noktayı belirtin.");
            
            // Generate ghost entities for preview
             _ghostEntities = _entitiesToCopy.Select(e => { var c = e.Clone(); c.Color = 0xFFAAAAAA; return c; }).ToList();
        }
        else
        {
            // İkinci nokta -> Kopyala
            var delta = new Vector3D(point.X - _basePoint.Value.X, point.Y - _basePoint.Value.Y, 0);
            
            var composite = new CompositeOperation("Copy Entities");
            foreach (var ent in _entitiesToCopy)
            {
                var clone = ent.Clone();
                clone.Move(delta);
                // Assign new ID is automatic in Clone? Yes (via Constructor usually) logic.
                // But my Clone implementation manually copied properties. 
                // Let's check LineEntity.Clone() -> new LineEntity(...) calls constructor -> new Guid().
                // So ID is unique. Good.
                
                composite.Add(new AddEntityOperation(_database, clone));
            }

            _transactionManager.Submit(composite);
            
            OnFeedback?.Invoke("COPY: Tamamlandı.");
            OnCompleted?.Invoke();
        }
    }

    /*
       NE: Fare Hareket OlayÄ± (OnPointerMoved)
       NEDEN: Baz noktasÄ± belirlendikten sonra kopyalanacak nesnelerin mouse ile birlikte hareket eden hayalet Ã¶nizlemelerini (Ghost) gÃ¼ncellemek iÃ§in.
    */
    public void OnPointerMoved(Vector3D point)
    {
         _currentMousePos = point;
         
         if (_basePoint != null && _ghostEntities != null)
         {
             var delta = new Vector3D(point.X - _basePoint.Value.X, point.Y - _basePoint.Value.Y, 0);
             
             // Efficient Ghost Update: Re-clone to avoid drift
             _ghostEntities = _entitiesToCopy.Select(e => { var c = e.Clone(); c.Color = 0xFFAAAAAA; return c; }).ToList();
             foreach(var g in _ghostEntities) g.Move(delta);
         }
    }

    /*
       NE: Klavye GiriÅŸ OlayÄ± (OnKeyDown)
       NEDEN: ESC tuÅŸu ile kopyalama iÅŸlemini herhangi bir aÅŸamada iptal etmek iÃ§in.
    */
    public void OnKeyDown(InputKey key) 
    {
        if(key == InputKey.Escape)
            Cancel();
    }

    /*
       NE: YardÄ±mcÄ± Ã‡izim (Draw)
       NEDEN: Kopyalama sÄ±rasÄ±nda baz noktadan mouse'a uzanan kesikli hat (Rubber band) ve nesne kopyalarÄ±nÄ±n Ã¶nizlemelerini render döngüsünde göstermek için.
    */
    public void Draw(IRenderContext context)
    {
        if (_basePoint != null)
        {
             // Draw displacement vector (Rubber band)
             context.DrawLine(_basePoint.Value, _currentMousePos, 0xFF888888, 1.0 * context.PixelSize, isDashed: true);

             // Draw Ghosts
             if (_ghostEntities != null)
             {
                 foreach(var ghost in _ghostEntities) ghost.Draw(context);
             }
        }
    }

    /*
       NE: Komutu Ä°ptal Et (Cancel)
       NEDEN: Komutu sonlandÄ±rmak ve geÃ§ici hayalet nesneleri bellekten temizlemek iÃ§in.
    */
    public void Cancel()
    {
        _basePoint = null;
        _ghostEntities = null;
        OnCompleted?.Invoke();
    }
}
