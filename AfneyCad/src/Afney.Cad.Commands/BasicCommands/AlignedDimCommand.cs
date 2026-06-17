using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

public class AlignedDimCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly double             _textHeight;

    private Vector3D?        _p1;
    private Vector3D?        _p2;
    private DimensionEntity? _ghost;

    public string    CommandName => "DIMALIGNED";
    public Vector3D? ActivePoint => _p1;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public AlignedDimCommand(CadDatabase db, TransactionManager tm, double textHeight = 250.0)
    {
        _database   = db;
        _tm         = tm;
        _textHeight = textHeight;
    }

    public void Start() => OnFeedback?.Invoke("DIMALIGNED: İlk ölçü noktasını seçin.");

    public void OnPointerPressed(Vector3D point)
    {
        if (_p1 == null)
        {
            _p1    = point;
            _ghost = new DimensionEntity(point, point, point, DimensionType.Aligned) { TextHeight = _textHeight };
            OnFeedback?.Invoke("DIMALIGNED: İkinci ölçü noktasını seçin.");
        }
        else if (_p2 == null)
        {
            _p2    = point;
            _ghost = new DimensionEntity(_p1.Value, point, point, DimensionType.Aligned) { TextHeight = _textHeight };
            OnFeedback?.Invoke("DIMALIGNED: Ölçü çizgisi konumunu belirtin.");
        }
        else
        {
            var dim = new DimensionEntity(_p1.Value, _p2.Value, point, DimensionType.Aligned)
            {
                Layer      = _database.ActiveLayerName,
                Color      = 0xFF00CCFF,
                TextHeight = _textHeight
            };
            _tm.Submit(new AddEntityOperation(_database, dim));
            Cancel();
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_ghost == null) return;
        if (_p2 == null)
            _ghost.SecondPoint = point;
        else
            _ghost.DimLinePoint = point;
    }

    public void OnKeyDown(InputKey key) { }

    public void Draw(IRenderContext ctx) => _ghost?.Draw(ctx);

    public void Cancel()
    {
        _ghost = null;
        _p1    = null;
        _p2    = null;
    }
}
