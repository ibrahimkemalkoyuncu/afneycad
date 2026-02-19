using System;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Database.Transactions.Operations; 
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

public class RotateCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<CadEntity> _entitiesToRotate;
    
    private Vector3D? _centerPoint;
    private double _currentAngle; // Relative or absolute?
    private Vector3D _currentMousePos;
    
    // Ghost
    private List<CadEntity> _ghosts = new();

    public string CommandName => "ROTATE";
    public Vector3D? ActivePoint => _centerPoint;
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public RotateCommand(CadDatabase database, TransactionManager transactionManager, IEnumerable<CadEntity> selection)
    {
        _database = database;
        _transactionManager = transactionManager;
        _entitiesToRotate = selection.ToList();
    }
    
    /*
       NE: Komutu BaÅŸlat (Start)
       NEDEN: DÃ¶necek nesne seÃ§imini kontrol etmek ve kullanÄ±cÄ±dan dÃ¶nme merkezini (Base Point) istemek iÃ§in.
    */
    public void Start()
    {
        if (_entitiesToRotate.Count == 0)
        {
            OnFeedback?.Invoke("ROTATE: Nesne seçilmedi.");
            OnCompleted?.Invoke();
            return;
        }
        OnFeedback?.Invoke("ROTATE: Dönme merkezini seçin.");
    }

    /*
       NE: TÄ±klama OlayÄ± (OnPointerPressed)
       NEDEN: Ä°lk tÄ±klamada dönme merkezini sabitlemek, ikinci tÄ±klamada ise güncel fare konumuna göre hesaplanan açıyı uygulayıp dönüşü tamamlamak için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_centerPoint == null)
        {
            _centerPoint = point;
            // Mouse açısından başlangıç açısı hesaplanabilir ama referans açısı 0 (X ekseni) kabul edelim.
            // Kullanıcı ikinci tıklamayı yaptığı yer açıyı belirleyecek.
            OnFeedback?.Invoke("ROTATE: Dönme açısını belirtin.");
            
            // Create initial ghosts
            _ghosts = _entitiesToRotate.Select(e => e.Clone()).ToList();
        }
        else
        {
            // Apply Rotation
            double angle = CalculateAngle(_centerPoint.Value, point);
            // Snap to 90 degrees? Maybe later with Shift key
            
            // Matrix: Translate to Origin -> Rotate -> Translate Back
            // M = T(c) * R * T(-c)
            var t1 = Matrix4x4.TranslationMatrix(-_centerPoint.Value.X, -_centerPoint.Value.Y, -_centerPoint.Value.Z);
            var r = Matrix4x4.RotationZ(angle); // Angle in radians
            var t2 = Matrix4x4.TranslationMatrix(_centerPoint.Value.X, _centerPoint.Value.Y, _centerPoint.Value.Z);
            
            var combined = t2 * r * t1; // Order: Apply T1 first (Rightmost) then R then T2 ? Check Matrix mult order. 
            // Usually Proj * View * Model * VLocal. Pre-multiplication vs Post.
            // My implementation of Matrix operator * :
            // res(i,j) = sum(a(i,k) * b(k,j)) -> Standard row-major or column-major math.
            // If v' = M * v, then v' = A * (B * v). So A * B.
            // So to apply T1 then R then T2: v' = T2 * (R * (T1 * v)). So T2 * R * T1. Correct.

            var composite = new CompositeOperation("Rotate Entities");

            // Undo Matrix
            var rInv = Matrix4x4.RotationZ(-_currentAngle);
            var combinedInv = t2 * rInv * t1;

            foreach (var ent in _entitiesToRotate)
            {
                composite.Add(new TransformEntityOperation(ent, combined, combinedInv, _database));
            }

            _transactionManager.Submit(composite);
            OnFeedback?.Invoke("ROTATE: Tamamlandı.");
            OnCompleted?.Invoke();
        }
    }

    /*
       NE: Fare Hareket OlayÄ± (OnPointerMoved)
       NEDEN: Dönme merkezi belli olduktan sonra, mouse hareketine göre nesnelerin yeni açıdaki konumlarını (Ghost) dinamik olarak göstermek için.
    */
    public void OnPointerMoved(Vector3D point)
    {
        _currentMousePos = point;

        if (_centerPoint != null)
        {
            double angle = CalculateAngle(_centerPoint.Value, point);
            _currentAngle = angle;
            
            // Update ghosts
            // Re-clone from source to avoid accumulation errors
            _ghosts = _entitiesToRotate.Select(e => e.Clone()).ToList();
            
            var t1 = Matrix4x4.TranslationMatrix(-_centerPoint.Value.X, -_centerPoint.Value.Y, -_centerPoint.Value.Z);
            var r = Matrix4x4.RotationZ(angle);
            var t2 = Matrix4x4.TranslationMatrix(_centerPoint.Value.X, _centerPoint.Value.Y, _centerPoint.Value.Z);
            var combined = t2 * r * t1;

            foreach(var g in _ghosts)
            {
                g.Transform(combined);
                // Override Color for ghost
                g.Color = 0xFFAAAAAA; // Grey
            }
        }
    }

    /*
       NE: Klavye GiriÅŸ OlayÄ± (OnKeyDown)
       NEDEN: ESC tuÅŸu ile dÃ¶ndÃ¼rme iÅŸlemini iptal etmek iÃ§in.
    */
    public void OnKeyDown(InputKey key) 
    {
        if(key == InputKey.Escape) Cancel();
    }

    /*
       NE: YardÄ±mcÄ± Ã‡izim (Draw)
       NEDEN: DÃ¶nme merkezini, mouse'a uzanan referans hattini ve nesnelerin dÃ¶nen hayalet Ã¶nizlemelerini gÃ¶stermek iÃ§in.
    */
    public void Draw(IRenderContext context)
    {
        if (_centerPoint != null)
        {
            // Center Marker
            context.DrawCircle(_centerPoint.Value, 5 * context.PixelSize, 0xFFFF0000, 1 * context.PixelSize);
            
            // Visual line
            context.DrawLine(_centerPoint.Value, _currentMousePos, 0xFF888888, 1.0 * context.PixelSize, isDashed:true); // Mouse pos track needed

            // Ghosts
            foreach(var g in _ghosts) g.Draw(context);
        }
    }

    public void Cancel()
    {
        _centerPoint = null;
        OnCompleted?.Invoke();
    }

    /*
       NE: AÃ§Ä± Hesapla (CalculateAngle)
       NEDEN: Merkez noktasÄ± ile gÃ¼ncel mouse konumu arasÄ±ndaki aÃ§Ä±yÄ± (Atan2) radyan cinsinden saptamak iÃ§in.
    */
    private double CalculateAngle(Vector3D center, Vector3D current)
    {
        return Math.Atan2(current.Y - center.Y, current.X - center.X);
    }
}
