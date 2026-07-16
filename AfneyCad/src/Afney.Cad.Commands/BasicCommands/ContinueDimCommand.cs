using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Zincir Ölçü Komutu (ContinueDimCommand)
   NEDEN: AutoCAD'in DIMCONTINUE komutu gibi, önceki ölçünün bitiş noktasından başlayarak
          aynı ölçü çizgisi hizasında ardışık ölçüler eklemek için.

   ÖNCEDEN: Yalnızca yatay (Y sabit) zincirleme destekleniyordu — ilk ölçü dikey
   (IsHorizontal=false) olduğunda zincir yanlış hizada (Y yerine X kullanılması gereken
   yerde Y kullanarak) çiziliyordu. Artık başlangıç ölçüsünün yönü (_isHorizontal) alınıp
   zincir gerçekten o yönde (yatay → Y sabit, dikey → X sabit) devam ediyor.
*/
public class ContinueDimCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly DimensionStyle     _style;
    private readonly double             _dimLineCoord;
    private readonly bool               _isHorizontal;

    private Vector3D?        _lastPoint;
    private DimensionEntity? _ghost;
    private int              _count;

    public string    CommandName => "DIMCONTINUE";
    public Vector3D? ActivePoint => _lastPoint;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public ContinueDimCommand(CadDatabase db, TransactionManager tm, Vector3D startPoint, double dimLineCoord,
        DimensionStyle? style = null, bool isHorizontal = true)
    {
        _database     = db;
        _tm           = tm;
        _lastPoint    = startPoint;
        _dimLineCoord = dimLineCoord;
        _isHorizontal = isHorizontal;
        _style        = style ?? new DimensionStyle();
    }

    public void Start() => OnFeedback?.Invoke("DIMCONTINUE: Sonraki noktayı seçin (ESC ile bitirin).");

    private Vector3D BuildDimLinePoint(Vector3D point) => _isHorizontal
        ? new Vector3D(point.X, _dimLineCoord, 0)
        : new Vector3D(_dimLineCoord, point.Y, 0);

    public void OnPointerPressed(Vector3D point)
    {
        if (_lastPoint == null) return;

        var dim = new DimensionEntity(_lastPoint.Value, point, BuildDimLinePoint(point), DimensionType.Linear)
        {
            Layer = _database.ActiveLayerName
        };
        DimensionStyleApplier.Apply(dim, _style);
        _tm.Submit(new AddEntityOperation(_database, dim));
        _count++;

        _lastPoint = point;
        _ghost = new DimensionEntity(point, point, BuildDimLinePoint(point), DimensionType.Linear);
        DimensionStyleApplier.Apply(_ghost, _style);
        OnFeedback?.Invoke($"DIMCONTINUE: {_count} ölçü eklendi. Sonraki noktayı seçin (ESC ile bitirin).");
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_ghost == null || _lastPoint == null) return;
        _ghost.SecondPoint  = point;
        _ghost.DimLinePoint = BuildDimLinePoint(point);
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter || key == InputKey.Space)
        {
            OnFeedback?.Invoke($"DIMCONTINUE tamamlandı: {_count} ölçü eklendi.");
            OnCompleted?.Invoke();
        }
    }

    public void Draw(IRenderContext ctx) => _ghost?.Draw(ctx);

    public void Cancel()
    {
        _ghost     = null;
        _lastPoint = null;
    }
}
