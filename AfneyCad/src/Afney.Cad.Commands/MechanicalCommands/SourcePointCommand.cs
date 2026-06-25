using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

public class SourcePointCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly MechanicalSystemType _systemType;

    public string    CommandName => "SOURCEPOINT";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public SourcePointCommand(CadDatabase db, TransactionManager tm, MechanicalSystemType systemType = MechanicalSystemType.DomesticColdWater)
    {
        _database   = db;
        _tm         = tm;
        _systemType = systemType;
    }

    public void Start()
    {
        string name = _systemType == MechanicalSystemType.DomesticColdWater ? "Soğuk Su" : "Sıcak Su";
        OnFeedback?.Invoke($"BAŞLANGIÇ NOKTASI ({name}): Başlangıç noktasını tıklayın (boru ucuna snap yapın).");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var loadNode = new MechanicalLoadNode(point, 1.0)
        {
            SystemType = _systemType,
            Layer = _systemType == MechanicalSystemType.DomesticColdWater ? "MEP_TEMIZ_SU" : "MEP_SICAK_SU",
            Color = _systemType == MechanicalSystemType.DomesticColdWater ? 0xFF00DDFF : 0xFFFF6666
        };
        _tm.Submit(new AddEntityOperation(_database, loadNode));

        string name = _systemType == MechanicalSystemType.DomesticColdWater ? "Soğuk Su" : "Sıcak Su";
        OnFeedback?.Invoke($"BAŞLANGIÇ NOKTASI: {name} başlangıç noktası yerleştirildi ({point.X:F2}, {point.Y:F2}).");
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { }
    public void Draw(IRenderContext ctx) { }
    public void Cancel() { }
}
