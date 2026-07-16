using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.BasicCommands;

public class RadiusDimCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly DimensionStyle     _style;

    private Vector3D?        _center;
    private DimensionEntity? _ghost;

    public string    CommandName => "DIMRADIUS";
    public Vector3D? ActivePoint => _center;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public RadiusDimCommand(CadDatabase db, TransactionManager tm, DimensionStyle? style = null)
    {
        _database = db;
        _tm       = tm;
        _style    = style ?? new DimensionStyle();
    }

    public void Start() => OnFeedback?.Invoke("DIMRADIUS: Merkez noktasını seçin.");

    public void OnPointerPressed(Vector3D point)
    {
        if (_center == null)
        {
            _center = point;
            _ghost  = new DimensionEntity(point, point, point, DimensionType.Radius);
            DimensionStyleApplier.Apply(_ghost, _style);
            OnFeedback?.Invoke("DIMRADIUS: Çevre noktasını seçin (yarıçap ucu).");
        }
        else
        {
            var dim = new DimensionEntity(_center.Value, point, point, DimensionType.Radius)
            {
                Layer = _database.ActiveLayerName
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
        _ghost.SecondPoint  = point;
        _ghost.DimLinePoint = point;
    }

    public void OnKeyDown(InputKey key) { }

    public void Draw(IRenderContext ctx) => _ghost?.Draw(ctx);

    public void Cancel()
    {
        _ghost  = null;
        _center = null;
    }
}
