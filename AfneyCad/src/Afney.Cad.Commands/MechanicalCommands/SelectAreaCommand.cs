using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

public class SelectAreaCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly List<Vector3D>     _points = new();

    public string    CommandName => "AREA";
    public Vector3D? ActivePoint => _points.Count > 0 ? _points[^1] : null;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public SelectAreaCommand(CadDatabase db, TransactionManager tm)
    {
        _database = db;
        _tm       = tm;
    }

    public void Start() => OnFeedback?.Invoke("ALAN: Alanin koselerini tiklayin (min 3 nokta). ENTER ile kapatin.");

    public void OnPointerPressed(Vector3D point)
    {
        _points.Add(point);

        if (_points.Count >= 3)
        {
            double area = CalculatePolygonArea(_points);
            OnFeedback?.Invoke($"ALAN: {_points.Count} nokta — {FormatArea(area)}. ENTER ile onayla, devam icin tikla.");
        }
        else
        {
            OnFeedback?.Invoke($"ALAN: {_points.Count} nokta secildi. En az 3 nokta gerekli.");
        }
    }

    public void OnPointerMoved(Vector3D point) { }

    public void OnKeyDown(InputKey key)
    {
        if ((key == InputKey.Enter || key == InputKey.Space) && _points.Count >= 3)
        {
            double area = CalculatePolygonArea(_points);

            var room = new RoomEntity(new List<Vector3D>(_points), "Mahal");
            room.Layer = "MAHAL";
            room.Color = 0x4000BFFF;
            _tm.Submit(new AddEntityOperation(_database, room));

            OnFeedback?.Invoke($"ALAN TAMAMLANDI: {FormatArea(area)} — Mahal olusturuldu.");
            _points.Clear();
            OnCompleted?.Invoke();
        }
    }

    public void Draw(IRenderContext ctx)
    {
        if (_points.Count < 2) return;

        for (int i = 0; i < _points.Count - 1; i++)
            ctx.DrawLine(_points[i], _points[i + 1], 0xFF00FFFF, 0, "Dashed", true);

        if (_points.Count >= 3)
        {
            ctx.DrawLine(_points[^1], _points[0], 0xFF00FFFF, 0, "Dashed", true);
            ctx.DrawFilledPolygon(_points, 0x00BFFF, 30);

            double area = CalculatePolygonArea(_points);
            var center = GetCentroid(_points);
            ctx.DrawText(FormatArea(area), center, 0, 200, 0xFF00FFFF);
        }
    }

    public void Cancel() => _points.Clear();

    private static double CalculatePolygonArea(List<Vector3D> pts)
    {
        double area = 0;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += pts[i].X * pts[j].Y;
            area -= pts[j].X * pts[i].Y;
        }
        return Math.Abs(area) / 2.0;
    }

    private static Vector3D GetCentroid(List<Vector3D> pts)
    {
        double cx = pts.Average(p => p.X);
        double cy = pts.Average(p => p.Y);
        return new Vector3D(cx, cy, 0);
    }

    private static string FormatArea(double area)
    {
        if (area >= 1_000_000)
            return $"{area / 1_000_000.0:F2} m²";
        return $"{area:F0} mm² ({area / 1_000_000.0:F2} m²)";
    }
}
