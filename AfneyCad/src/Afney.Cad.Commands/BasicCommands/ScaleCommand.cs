using System;
using System.Linq;
using System.Collections.Generic;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Database.Transactions.Operations; 
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

public class ScaleCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<CadEntity> _entitiesToScale;
    
    private Vector3D? _basePoint;
    private double _currentScale = 1.0;
    
    // Ghost
    private List<CadEntity> _ghosts = new();

    public string CommandName => "SCALE";
    public Vector3D? ActivePoint => _basePoint;
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public ScaleCommand(CadDatabase database, TransactionManager transactionManager, IEnumerable<CadEntity> selection)
    {
        _database = database;
        _transactionManager = transactionManager;
        _entitiesToScale = selection.ToList();
    }
    
    /*
       NE: Komutu BaÅŸlat (Start)
       NEDEN: Ã–lÃ§eklendirilecek nesneleri kontrol etmek ve kullanÄ±cÄ±dan sabit baz noktasÄ±nÄ± istemek iÃ§in.
    */
    public void Start()
    {
        if (_entitiesToScale.Count == 0)
        {
            OnFeedback?.Invoke("SCALE: Nesne seçilmedi.");
            OnCompleted?.Invoke();
            return;
        }
        OnFeedback?.Invoke("SCALE: Baz noktasını belirtin.");
    }

    /*
       NE: TÄ±klama OlayÄ± (OnPointerPressed)
       NEDEN: Ä°lk tÄ±klamada Ã¶lÃ§eklendirme iÃ§in sabit baz noktasÄ±nÄ± belirlemek, ikinci tÄ±klamada ise baz noktaya olan mesafeye gÃ¶re Ã¶lÃ§ek faktÃ¶rÃ¼nÃ¼ uygulayÄ±p iÅŸlemi tamamlamak iÃ§in.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_basePoint == null)
        {
            _basePoint = point;
            OnFeedback?.Invoke("SCALE: Ölçek faktörünü belirtin (İkinci nokta mesafesi).");
            
            // Create initial ghosts
            _ghosts = _entitiesToScale.Select(e => e.Clone()).ToList();
        }
        else
        {
            // Apply Scale
            double scale = CalculateScale(_basePoint.Value, point);
            if (scale <= 0.0001) scale = 0.0001; // Prevent zero scale

            var composite = new CompositeOperation("Scale Entities");
            
            var t1 = Matrix4x4.TranslationMatrix(-_basePoint.Value.X, -_basePoint.Value.Y, -_basePoint.Value.Z);
            var s = Matrix4x4.Scaling(scale);
            var t2 = Matrix4x4.TranslationMatrix(_basePoint.Value.X, _basePoint.Value.Y, _basePoint.Value.Z);
            
            var combined = t2 * s * t1; 

            foreach (var ent in _entitiesToScale)
            {
                // We reuse the TransformEntityOperation I created earlier (assuming it exists now)
                // Note: I need to ensure TransformEntityOperation handles Undo correctly (via Memento or Inverse).
                // I used the Memento approach in previous turn or Inverse?
                // I tried to replace it with Inverse but tool failed because file existed. 
                // Then I successfully replaced it with Inverse version.
                
                // Construct Inverse Matrix for Undo
                // Inv(T2 * S * T1) = Inv(T1) * Inv(S) * Inv(T2)
                // = T(base) * S(1/s) * T(-base)
                // Which is exactly the same logic with 1/scale.
                
                var invStart = Matrix4x4.TranslationMatrix(-_basePoint.Value.X, -_basePoint.Value.Y, -_basePoint.Value.Z);
                var invScale = Matrix4x4.Scaling(1.0 / scale);
                var invEnd = Matrix4x4.TranslationMatrix(_basePoint.Value.X, _basePoint.Value.Y, _basePoint.Value.Z);
                var inverse = invEnd * invScale * invStart;

                composite.Add(new TransformEntityOperation(ent, combined, inverse));
            }

            _transactionManager.Submit(composite);
            OnFeedback?.Invoke($"SCALE: Tamamlandı (Faktör: {scale:F2}).");
            OnCompleted?.Invoke();
        }
    }

    /*
       NE: Fare Hareket OlayÄ± (OnPointerMoved)
       NEDEN: Baz noktasÄ± seÃ§ildikten sonra, farenin uzaklÄ±ÄŸÄ±na gÃ¶re nesnelerin dinamik olarak ne kadar bÃ¼yÃ¼yÃ¼p kÃ¼Ã§Ã¼leceÄŸini (Ghost) gÃ¶stermek iÃ§in.
    */
    public void OnPointerMoved(Vector3D point)
    {
        if (_basePoint != null)
        {
            double scale = CalculateScale(_basePoint.Value, point);
            _currentScale = scale;
            
            // Update ghosts
            _ghosts = _entitiesToScale.Select(e => e.Clone()).ToList();
            
            var t1 = Matrix4x4.TranslationMatrix(-_basePoint.Value.X, -_basePoint.Value.Y, -_basePoint.Value.Z);
            var s = Matrix4x4.Scaling(scale);
            var t2 = Matrix4x4.TranslationMatrix(_basePoint.Value.X, _basePoint.Value.Y, _basePoint.Value.Z);
            var combined = t2 * s * t1;

            foreach(var g in _ghosts)
            {
                g.Transform(combined);
                g.Color = 0xFFAAAAAA;
            }
        }
    }

    /*
       NE: Klavye GiriÅŸ OlayÄ± (OnKeyDown)
       NEDEN: ESC tuÅŸu ile Ã¶lÃ§eklendirme komutunu iptal etmek iÃ§in.
    */
    public void OnKeyDown(InputKey key) 
    {
        if(key == InputKey.Escape) Cancel();
    }

    /*
       NE: YardÄ±mcÄ± Ã‡izim (Draw)
       NEDEN: Baz noktasÄ±nÄ± ve nesnelerin Ã¶nbellekteki gÃ¼ncel Ã¶lÃ§ekli hayalet Ã¶nizlemelerini render döngüsünde göstermek için.
    */
    public void Draw(IRenderContext context)
    {
        if (_basePoint != null)
        {
            // Center Marker
            context.DrawCircle(_basePoint.Value, 5 * context.PixelSize, 0xFF0000FF, 1 * context.PixelSize);
            
            // Ghosts
            foreach(var g in _ghosts) g.Draw(context);
        }
    }

    public void Cancel()
    {
        _basePoint = null;
        OnCompleted?.Invoke();
    }

    /*
       NE: Ã–lÃ§ek FaktÃ¶rÃ¼ Hesapla (CalculateScale)
       NEDEN: Baz noktasÄ± ile mouse imleci arasÄ±ndaki mesafeyi bularak, nesnelerin kaÃ§ kat bÃ¼yÃ¼tÃ¼leceÄŸini saptamak iÃ§in.
    */
    private double CalculateScale(Vector3D basePt, Vector3D current)
    {
        // Simple logic: Scale = Distance from base.
        // If distance is 0, scale is epsilon.
        double dist = Math.Sqrt(Math.Pow(current.X - basePt.X, 2) + Math.Pow(current.Y - basePt.Y, 2));
        
        // This is a bit aggressive (zoom 1 unit = scale 1).
        // Maybe we want a Reference Ratio?
        // But for "Visual Scaling", this is standard behavior in simple tools.
        // User clicks Base, moves mouse to 2.0 units away -> Scale 2.0.
        
        // Alternative: User defined Ref Distance. 
        // We assume Ref Distance is 1.0.
        
        return Math.Max(0.1, dist); // Minimum scale 0.1 to avoid inversion/zero.
    }
}
