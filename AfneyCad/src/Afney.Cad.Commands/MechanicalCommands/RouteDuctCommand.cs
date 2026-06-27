using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

public class RouteDuctCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;

    private Vector3D?    _startPoint;
    private DuctEntity?  _ghost;
    private DuctShape    _shape  = DuctShape.Rectangular;
    private DuctType     _type   = DuctType.Supply;
    private double       _width  = 400;
    private double       _height = 300;
    private double       _diameter = 315;
    private int          _count;

    public string    CommandName => "DUCT";
    public Vector3D? ActivePoint => _startPoint;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public RouteDuctCommand(CadDatabase db, TransactionManager tm,
        DuctShape shape = DuctShape.Rectangular, DuctType type = DuctType.Supply,
        double width = 400, double height = 300, double diameter = 315)
    {
        _database = db;
        _tm       = tm;
        _shape    = shape;
        _type     = type;
        _width    = width;
        _height   = height;
        _diameter = diameter;
    }

    public void Start()
    {
        string shapeText = _shape == DuctShape.Circular ? $"D{_diameter}" : $"{_width}x{_height}";
        OnFeedback?.Invoke($"KANAL ({shapeText}): Baslangic noktasini tiklayin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        if (_startPoint == null)
        {
            _startPoint = point;
            _ghost = CreateDuct(point, point);
            OnFeedback?.Invoke("KANAL: Sonraki noktayi tiklayin (ESC ile bitirin).");
        }
        else
        {
            var duct = CreateDuct(_startPoint.Value, point);
            _tm.Submit(new AddEntityOperation(_database, duct));
            _count++;

            _startPoint = point;
            _ghost = CreateDuct(point, point);
            OnFeedback?.Invoke($"KANAL: {_count} segment eklendi. Sonraki noktayi tiklayin (ESC ile bitirin).");
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_ghost != null && _startPoint != null)
            _ghost.EndPoint = point;
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter || key == InputKey.Space)
        {
            OnFeedback?.Invoke($"KANAL: {_count} segment tamamlandi.");
            OnCompleted?.Invoke();
        }
    }

    public void Draw(IRenderContext ctx) => _ghost?.Draw(ctx);

    public void Cancel()
    {
        _ghost = null;
        _startPoint = null;
        if (_count > 0)
            OnFeedback?.Invoke($"KANAL: {_count} segment tamamlandi.");
    }

    private DuctEntity CreateDuct(Vector3D start, Vector3D end)
    {
        var duct = _shape == DuctShape.Circular
            ? new DuctEntity(start, end, _diameter)
            : new DuctEntity(start, end, _width, _height);
        duct.Type = _type;
        duct.Layer = "MEP_HAVALANDIRMA";
        duct.Color = _type switch
        {
            DuctType.Supply   => 0xFF2ECC71,
            DuctType.Return   => 0xFF3498DB,
            DuctType.Exhaust  => 0xFFE74C3C,
            DuctType.FreshAir => 0xFF00BCD4,
            DuctType.Smoke    => 0xFFFF9800,
            _                 => 0xFF2ECC71
        };
        return duct;
    }
}
