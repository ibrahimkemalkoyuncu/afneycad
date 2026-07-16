using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.BasicCommands;

public class AngularDimCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly DimensionStyle     _style;

    private Vector3D?        _vertex;
    private Vector3D?        _p1;
    private DimensionEntity? _ghost;

    public string    CommandName => "DIMANGULAR";
    public Vector3D? ActivePoint => _vertex;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public AngularDimCommand(CadDatabase db, TransactionManager tm, DimensionStyle? style = null)
    {
        _database = db;
        _tm       = tm;
        _style    = style ?? new DimensionStyle();
    }

    public void Start() => OnFeedback?.Invoke("DIMANGULAR: Köşe noktasını (vertex) seçin.");

    public void OnPointerPressed(Vector3D point)
    {
        if (_vertex == null)
        {
            _vertex = point;
            _ghost = new DimensionEntity(point, point, point, DimensionType.Angular) { AngularVertex = point };
            DimensionStyleApplier.Apply(_ghost, _style);
            OnFeedback?.Invoke("DIMANGULAR: Birinci kol noktasını seçin.");
        }
        else if (_p1 == null)
        {
            _p1 = point;
            _ghost!.FirstPoint = point;
            OnFeedback?.Invoke("DIMANGULAR: İkinci kol noktasını seçin.");
        }
        else
        {
            var dim = new DimensionEntity(_p1.Value, point, point, DimensionType.Angular)
            {
                Layer         = _database.ActiveLayerName,
                AngularVertex = _vertex.Value
            };
            DimensionStyleApplier.Apply(dim, _style);
            _tm.Submit(new AddEntityOperation(_database, dim));
            Cancel();
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_ghost == null) return;
        if (_p1 == null)
            _ghost.FirstPoint = point;
        else
            _ghost.SecondPoint = point;
    }

    public void OnKeyDown(InputKey key) { }
    public void Draw(IRenderContext ctx) => _ghost?.Draw(ctx);

    public void Cancel()
    {
        _ghost  = null;
        _vertex = null;
        _p1     = null;
    }
}
