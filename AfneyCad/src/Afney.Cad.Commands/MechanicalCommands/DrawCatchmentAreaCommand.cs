using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

// Polygon tıklama ile RainfallCatchmentEntity oluşturur.
// Enter ya da C tuşuyla kapatılır.
public class DrawCatchmentAreaCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _tm;

    private RainfallCatchmentEntity? _current;
    private Vector3D _cursor;

    public string    CommandName => "CATCHMENTAREA";
    public Vector3D? ActivePoint => _current?.Vertices.Count > 0 ? _current.Vertices[^1] : null;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public DrawCatchmentAreaCommand(CadDatabase db, TransactionManager tm)
    {
        _database = db;
        _tm = tm;
    }

    public void Start()
    {
        _current = new RainfallCatchmentEntity { Layer = "MEK_YAGMUR" };
        OnFeedback?.Invoke("YAĞMUR ALAN: İlk köşeyi tıklayın. Her tıklama bir köşe ekler. Enter veya C ile poligonu kapatın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        _current ??= new RainfallCatchmentEntity { Layer = "MEK_YAGMUR" };
        _current.AddVertex(point);
        OnFeedback?.Invoke($"YAĞMUR ALAN: {_current.Vertices.Count} köşe eklendi. Devam edin veya Enter ile kapatın.");
    }

    public void OnPointerMoved(Vector3D point) { _cursor = point; }

    public void OnKeyDown(InputKey key)
    {
        if ((key == InputKey.Enter || key == InputKey.Escape) && _current != null && _current.Vertices.Count >= 3)
        {
            _current.ClosePolygon();
            _tm.Submit(new AddEntityOperation(_database, _current));
            OnFeedback?.Invoke($"YAĞMUR ALAN: {_current.AreaM2:F1} m² poligon kaydedildi (C={_current.RunoffCoefficient:F1}).");
            _current = null;
            OnCompleted?.Invoke();
        }
        else if (key == InputKey.Escape)
        {
            _current = null;
            OnCompleted?.Invoke();
        }
    }

    public void Draw(IRenderContext ctx)
    {
        if (_current == null || _current.Vertices.Count == 0) return;
        const uint col = 0xFF0078FF;
        var verts = _current.Vertices;
        for (int i = 0; i < verts.Count - 1; i++)
            ctx.DrawLine(verts[i], verts[i + 1], col, 1.0);
        ctx.DrawLine(verts[^1], _cursor, col, 1.0); // önizleme kenarı
    }

    public void Cancel()
    {
        _current = null;
    }
}
