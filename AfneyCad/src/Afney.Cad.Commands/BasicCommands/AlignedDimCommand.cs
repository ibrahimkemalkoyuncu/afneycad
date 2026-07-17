using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.BasicCommands;

public class AlignedDimCommand : ICadCommand, IDimensionOverridable
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly DimensionStyle     _style;

    private Vector3D?        _p1;
    private Vector3D?        _p2;
    private DimensionEntity? _ghost;
    private string?          _overrideText;

    public string    CommandName => "DIMALIGNED";
    public Vector3D? ActivePoint => _p1;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public AlignedDimCommand(CadDatabase db, TransactionManager tm, DimensionStyle? style = null)
    {
        _database = db;
        _tm       = tm;
        _style    = style ?? new DimensionStyle();
    }

    public void Start() => OnFeedback?.Invoke("DIMALIGNED: İlk ölçü noktasını seçin.");

    public void SetTextOverride(string? text)
    {
        _overrideText = text;
        if (_ghost != null) _ghost.OverrideText = text;
        OnFeedback?.Invoke(string.IsNullOrEmpty(text)
            ? "DIMALIGNED: Ölçü geçersiz kılma temizlendi."
            : $"DIMALIGNED: Ölçü metni '{text}' olarak sabitlendi.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        if (_p1 == null)
        {
            _p1    = point;
            _ghost = new DimensionEntity(point, point, point, DimensionType.Aligned);
            DimensionStyleApplier.Apply(_ghost, _style);
            _ghost.OverrideText = _overrideText;
            OnFeedback?.Invoke("DIMALIGNED: İkinci ölçü noktasını seçin.");
        }
        else if (_p2 == null)
        {
            _p2    = point;
            _ghost = new DimensionEntity(_p1.Value, point, point, DimensionType.Aligned);
            DimensionStyleApplier.Apply(_ghost, _style);
            _ghost.OverrideText = _overrideText;
            OnFeedback?.Invoke("DIMALIGNED: Ölçü çizgisi konumunu belirtin.");
        }
        else
        {
            var dim = new DimensionEntity(_p1.Value, _p2.Value, point, DimensionType.Aligned)
            {
                Layer = _database.ActiveLayerName
            };
            DimensionStyleApplier.Apply(dim, _style);
            dim.OverrideText = _overrideText;
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
        _ghost        = null;
        _p1           = null;
        _p2           = null;
        _overrideText = null;
    }
}
