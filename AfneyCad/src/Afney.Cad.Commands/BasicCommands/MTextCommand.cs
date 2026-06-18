using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

public class MTextCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly Func<string?>      _promptText;

    private TextEntity? _ghost;

    public string    CommandName => "MTEXT";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public MTextCommand(CadDatabase db, TransactionManager tm, Func<string?> promptText)
    {
        _database   = db;
        _tm         = tm;
        _promptText = promptText;
    }

    public void Start() => OnFeedback?.Invoke("MTEXT: Metin yerleşim noktasını tıklayın.");

    public void OnPointerPressed(Vector3D point)
    {
        string? text = _promptText();
        if (string.IsNullOrWhiteSpace(text)) return;

        var entity = new TextEntity(text, point, 200)
        {
            Layer = _database.ActiveLayerName,
            Color = _database.GetLayer(_database.ActiveLayerName)?.Color ?? 0xFFE1E4EE
        };
        _tm.Submit(new AddEntityOperation(_database, entity));
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { }
    public void Draw(IRenderContext ctx) { }

    public void Cancel() { _ghost = null; }
}
