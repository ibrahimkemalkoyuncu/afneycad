using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.BasicCommands;

public class ContinueDimCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly DimensionStyle     _style;
    private readonly double             _dimLineY;

    private Vector3D?        _lastPoint;
    private DimensionEntity? _ghost;
    private int              _count;

    public string    CommandName => "DIMCONTINUE";
    public Vector3D? ActivePoint => _lastPoint;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public ContinueDimCommand(CadDatabase db, TransactionManager tm, Vector3D startPoint, double dimLineY, DimensionStyle? style = null)
    {
        _database  = db;
        _tm        = tm;
        _lastPoint = startPoint;
        _dimLineY  = dimLineY;
        _style     = style ?? new DimensionStyle();
    }

    public void Start() => OnFeedback?.Invoke("DIMCONTINUE: Sonraki noktayı seçin (ESC ile bitirin).");

    public void OnPointerPressed(Vector3D point)
    {
        if (_lastPoint == null) return;

        var dimLinePoint = new Vector3D(point.X, _dimLineY, 0);
        var dim = new DimensionEntity(_lastPoint.Value, point, dimLinePoint, DimensionType.Linear)
        {
            Layer = _database.ActiveLayerName
        };
        DimensionStyleApplier.Apply(dim, _style);
        _tm.Submit(new AddEntityOperation(_database, dim));
        _count++;

        _lastPoint = point;
        _ghost = new DimensionEntity(point, point, new Vector3D(point.X, _dimLineY, 0), DimensionType.Linear);
        DimensionStyleApplier.Apply(_ghost, _style);
        OnFeedback?.Invoke($"DIMCONTINUE: {_count} ölçü eklendi. Sonraki noktayı seçin (ESC ile bitirin).");
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_ghost == null || _lastPoint == null) return;
        _ghost.SecondPoint  = point;
        _ghost.DimLinePoint = new Vector3D(point.X, _dimLineY, 0);
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
