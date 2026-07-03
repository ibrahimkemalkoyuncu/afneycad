using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

// Polygon tıklama ile RainfallCatchmentEntity oluşturur.
// Enter ile poligon kapatılır; SurfaceTypeRequested event'i yüzey tipi seçimi için ateşlenir.
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

    /// <summary>
    /// Poligon kapandığında fırlatılır. Callback'e seçilen SurfaceType iletilir; callback null ise işlem iptal edilir.
    /// </summary>
    public event Action<RainfallCatchmentEntity, Action<RainfallCatchmentEntity.SurfaceType?>>? SurfaceTypeRequested;

    public DrawCatchmentAreaCommand(CadDatabase db, TransactionManager tm)
    {
        _database = db;
        _tm = tm;
    }

    public void Start()
    {
        _current = new RainfallCatchmentEntity { Layer = "MEK_YAGMUR" };
        OnFeedback?.Invoke("YAĞMUR ALAN: İlk köşeyi tıklayın. Her tıklama bir köşe ekler. Enter ile poligonu kapatın.");
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
        if (key == InputKey.Enter && _current != null && _current.Vertices.Count >= 3)
        {
            _current.ClosePolygon();
            var pending = _current;
            _current = null;

            if (SurfaceTypeRequested != null)
            {
                SurfaceTypeRequested.Invoke(pending, chosen =>
                {
                    if (chosen == null) return; // iptal
                    pending.Surface = chosen.Value;
                    _tm.Submit(new AddEntityOperation(_database, pending));
                    OnFeedback?.Invoke($"YAĞMUR ALAN: {pending.AreaM2:F1} m² kaydedildi (C={pending.RunoffCoefficient:F1}).");
                    OnCompleted?.Invoke();
                });
            }
            else
            {
                _tm.Submit(new AddEntityOperation(_database, pending));
                OnFeedback?.Invoke($"YAĞMUR ALAN: {pending.AreaM2:F1} m² kaydedildi (C={pending.RunoffCoefficient:F1}).");
                OnCompleted?.Invoke();
            }
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
