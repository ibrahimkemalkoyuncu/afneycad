using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Commands.MechanicalCommands;

public class ConnectFixtureCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;

    private SanitaryFixtureEntity? _selectedFixture;
    private PipeEntity?            _selectedPipe;
    private int                    _connectCount;

    public string    CommandName => "CONNECT";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public ConnectFixtureCommand(CadDatabase db, TransactionManager tm)
    {
        _database = db;
        _tm       = tm;
    }

    public void Start() => OnFeedback?.Invoke("BAGLA: Bağlanacak cihazı tıklayın.");

    public void OnPointerPressed(Vector3D point)
    {
        if (_selectedFixture == null)
        {
            var fixture = FindNearestFixture(point, 500);
            if (fixture != null)
            {
                _selectedFixture = fixture;
                OnFeedback?.Invoke($"BAGLA: {fixture.FixtureType} seçildi. Şimdi bağlanacak boruyu tıklayın.");
            }
            else
            {
                OnFeedback?.Invoke("BAGLA: Cihaz bulunamadı. Daha yakın tıklayın.");
            }
        }
        else
        {
            var pipe = FindNearestPipe(point, 500);
            if (pipe != null)
            {
                CreateConnection(_selectedFixture, pipe, point);
                _connectCount++;
                _selectedFixture = null;
                OnFeedback?.Invoke($"BAGLA: Bağlantı yapıldı ({_connectCount} adet). Sonraki cihazı seçin (ESC ile bitirin).");
            }
            else
            {
                OnFeedback?.Invoke("BAGLA: Boru bulunamadı. Daha yakın tıklayın.");
            }
        }
    }

    private void CreateConnection(SanitaryFixtureEntity fixture, PipeEntity pipe, Vector3D clickPoint)
    {
        var fixtureCenter = fixture.GetBoundingBox().Center;
        var closestOnPipe = GetClosestPointOnPipe(pipe, fixtureCenter);

        var branchPipe = new PipeEntity(fixtureCenter, closestOnPipe, Math.Min(pipe.InnerDiameter, 20))
        {
            SystemType = pipe.SystemType,
            Layer = pipe.Layer,
            Color = pipe.Color
        };
        _tm.Submit(new AddEntityOperation(_database, branchPipe));
    }

    private Vector3D GetClosestPointOnPipe(PipeEntity pipe, Vector3D point)
    {
        var a = pipe.StartPoint;
        var b = pipe.EndPoint;
        var ab = b - a;
        double len2 = ab.X * ab.X + ab.Y * ab.Y + ab.Z * ab.Z;
        if (len2 < 1e-9) return a;
        double t = ((point.X - a.X) * ab.X + (point.Y - a.Y) * ab.Y + (point.Z - a.Z) * ab.Z) / len2;
        t = Math.Clamp(t, 0, 1);
        return new Vector3D(a.X + t * ab.X, a.Y + t * ab.Y, a.Z + t * ab.Z);
    }

    private SanitaryFixtureEntity? FindNearestFixture(Vector3D point, double maxDist)
    {
        SanitaryFixtureEntity? best = null;
        double bestDist = maxDist;
        foreach (var e in _database.GetAllEntities())
        {
            if (e is SanitaryFixtureEntity f)
            {
                double d = f.DistanceTo(point);
                if (d < bestDist) { bestDist = d; best = f; }
            }
        }
        return best;
    }

    private PipeEntity? FindNearestPipe(Vector3D point, double maxDist)
    {
        PipeEntity? best = null;
        double bestDist = maxDist;
        foreach (var e in _database.GetAllEntities())
        {
            if (e is PipeEntity p)
            {
                double d = p.DistanceTo(point);
                if (d < bestDist) { bestDist = d; best = p; }
            }
        }
        return best;
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { }

    public void Draw(IRenderContext ctx)
    {
        if (_selectedFixture != null)
        {
            var bb = _selectedFixture.GetBoundingBox();
            ctx.DrawRectangle(bb.Min, bb.Max, 0xFF00FF00, 0);
        }
    }

    public void Cancel()
    {
        _selectedFixture = null;
        if (_connectCount > 0)
            OnFeedback?.Invoke($"BAGLA: {_connectCount} bağlantı tamamlandı.");
    }
}
