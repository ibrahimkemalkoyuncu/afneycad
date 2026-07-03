using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

public class PlaceDrainageOutletCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _tm;
    private readonly bool _isRainOutlet;

    public string    CommandName => "DRAINAGEOUTLET";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public PlaceDrainageOutletCommand(CadDatabase db, TransactionManager tm, bool isRainOutlet = false)
    {
        _database    = db;
        _tm          = tm;
        _isRainOutlet = isRainOutlet;
    }

    public void Start()
    {
        string type = _isRainOutlet ? "Yağmur Suyu Boşaltma" : "Rögar / Pis Su Boşaltma";
        OnFeedback?.Invoke($"BOŞALTMA NOKTASI ({type}): Boşaltma noktasını tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var outletType = _isRainOutlet
            ? DrainageOutletEntity.OutletType.RainDrain
            : DrainageOutletEntity.OutletType.SewerManhole;

        var outlet = new DrainageOutletEntity(point, outletType)
        {
            Layer = _isRainOutlet ? "MEK_YAGMUR" : "MEK_PIS_SU",
            Label = _isRainOutlet ? "YS-BOSALTMA" : "PS-ROGAR"
        };

        _tm.Submit(new AddEntityOperation(_database, outlet));

        string type = _isRainOutlet ? "yağmur suyu boşaltma" : "rögar bağlantı";
        OnFeedback?.Invoke($"BOŞALTMA NOKTASI: Bir {type} noktası yerleştirildi ({point.X:F0}, {point.Y:F0}).");
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { }
    public void Draw(IRenderContext ctx) { }
    public void Cancel() { }
}
