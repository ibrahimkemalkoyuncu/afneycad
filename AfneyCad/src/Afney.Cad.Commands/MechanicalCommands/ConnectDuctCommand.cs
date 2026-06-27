using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

public class ConnectDuctCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;

    private DuctEntity? _firstDuct;
    private int         _connectCount;

    public string    CommandName => "DUCTCONNECT";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public ConnectDuctCommand(CadDatabase db, TransactionManager tm)
    {
        _database = db;
        _tm       = tm;
    }

    public void Start() => OnFeedback?.Invoke("KANAL BAGLA: Birinci kanali tiklayin.");

    public void OnPointerPressed(Vector3D point)
    {
        var duct = FindNearestDuct(point, 500);
        if (duct == null)
        {
            OnFeedback?.Invoke("KANAL BAGLA: Kanal bulunamadi. Daha yakin tiklayin.");
            return;
        }

        if (_firstDuct == null)
        {
            _firstDuct = duct;
            OnFeedback?.Invoke($"KANAL BAGLA: {duct.GetSizeText()} secildi. Ikinci kanali tiklayin.");
        }
        else
        {
            CreateConnection(_firstDuct, duct);
            _connectCount++;
            _firstDuct = null;
            OnFeedback?.Invoke($"KANAL BAGLA: {_connectCount} baglanti yapildi. Sonraki kanali secin (ESC ile bitirin).");
        }
    }

    private void CreateConnection(DuctEntity d1, DuctEntity d2)
    {
        double distStartStart = d1.StartPoint.DistanceTo(d2.StartPoint);
        double distStartEnd   = d1.StartPoint.DistanceTo(d2.EndPoint);
        double distEndStart   = d1.EndPoint.DistanceTo(d2.StartPoint);
        double distEndEnd     = d1.EndPoint.DistanceTo(d2.EndPoint);

        double minDist = Math.Min(Math.Min(distStartStart, distStartEnd), Math.Min(distEndStart, distEndEnd));

        Vector3D p1, p2;
        if (minDist == distEndStart) { p1 = d1.EndPoint; p2 = d2.StartPoint; }
        else if (minDist == distEndEnd) { p1 = d1.EndPoint; p2 = d2.EndPoint; }
        else if (minDist == distStartEnd) { p1 = d1.StartPoint; p2 = d2.EndPoint; }
        else { p1 = d1.StartPoint; p2 = d2.StartPoint; }

        if (p1.DistanceTo(p2) < 1.0) return;

        var connector = d1.Shape == DuctShape.Circular
            ? new DuctEntity(p1, p2, d1.DiameterMm)
            : new DuctEntity(p1, p2, Math.Min(d1.WidthMm, d2.WidthMm), Math.Min(d1.HeightMm, d2.HeightMm));
        connector.Type = d1.Type;
        connector.Layer = d1.Layer;
        connector.Color = d1.Color;
        _tm.Submit(new AddEntityOperation(_database, connector));
    }

    private DuctEntity? FindNearestDuct(Vector3D point, double maxDist)
    {
        DuctEntity? best = null;
        double bestDist = maxDist;
        foreach (var e in _database.GetAllEntities())
        {
            if (e is DuctEntity d)
            {
                double dist = d.DistanceTo(point);
                if (dist < bestDist) { bestDist = dist; best = d; }
            }
        }
        return best;
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { }

    public void Draw(IRenderContext ctx)
    {
        if (_firstDuct != null)
        {
            var bb = _firstDuct.GetBoundingBox();
            ctx.DrawRectangle(bb.Min, bb.Max, 0xFF00FF00, 0);
        }
    }

    public void Cancel()
    {
        _firstDuct = null;
        if (_connectCount > 0)
            OnFeedback?.Invoke($"KANAL BAGLA: {_connectCount} baglanti tamamlandi.");
    }
}
