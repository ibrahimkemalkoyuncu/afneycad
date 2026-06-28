using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

public class PasteCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly List<CadEntity>    _entities;
    private readonly Vector3D           _basePoint;
    private Vector3D                    _cursor;

    public string    CommandName => "PASTE";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public PasteCommand(CadDatabase db, TransactionManager tm, List<CadEntity> entities, Vector3D basePoint)
    {
        _database  = db;
        _tm        = tm;
        _entities  = entities;
        _basePoint = basePoint;
        _cursor    = basePoint;
    }

    public void Start() => OnFeedback?.Invoke($"YAPISTIR: {_entities.Count} nesne — yerlestirme noktasini tiklayin.");

    public void OnPointerPressed(Vector3D point)
    {
        var delta = point - _basePoint;
        foreach (var ent in _entities)
        {
            var clone = ent.Clone();
            clone.Move(delta);
            _tm.Submit(new AddEntityOperation(_database, clone));
        }
        OnFeedback?.Invoke($"YAPISTIR: {_entities.Count} nesne yerlestirildi.");
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point)
    {
        _cursor = point;
    }

    public void OnKeyDown(InputKey key) { }

    public void Draw(IRenderContext ctx)
    {
        var delta = _cursor - _basePoint;
        uint ghostColor = 0x6000CCFF;

        foreach (var ent in _entities)
        {
            var bb = ent.GetBoundingBox();
            var min = new Vector3D(bb.Min.X + delta.X, bb.Min.Y + delta.Y, 0);
            var max = new Vector3D(bb.Max.X + delta.X, bb.Max.Y + delta.Y, 0);
            ctx.DrawRectangle(min, max, ghostColor, 0);
        }
    }

    public void Cancel() { }
}
