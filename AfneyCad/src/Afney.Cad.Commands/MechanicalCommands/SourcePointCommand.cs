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
    private readonly CadDatabase _database;
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
        OnFeedback?.Invoke($"BAŞLANGIÇ NOKTASI ({SystemLabel(_systemType)}): Başlangıç noktasını tıklayın (boru ucuna snap yapın).");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var loadNode = new MechanicalLoadNode(point, 1.0)
        {
            SystemType = _systemType,
            Layer      = GetLayerForSystem(_systemType),
            Color      = GetColorForSystem(_systemType)
        };
        _tm.Submit(new AddEntityOperation(_database, loadNode));

        OnFeedback?.Invoke($"BAŞLANGIÇ NOKTASI: {SystemLabel(_systemType)} giriş noktası yerleştirildi ({point.X:F0}, {point.Y:F0}).");
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { }
    public void Draw(IRenderContext ctx) { }
    public void Cancel() { }

    private static string SystemLabel(MechanicalSystemType t) => t switch
    {
        MechanicalSystemType.DomesticColdWater => "Soğuk Su",
        MechanicalSystemType.DomesticHotWater  => "Sıcak Su",
        MechanicalSystemType.WasteWater        => "Pis Su",
        MechanicalSystemType.RainWater         => "Yağmur Suyu",
        MechanicalSystemType.FireProtection    => "Yangın",
        MechanicalSystemType.Gas               => "Gaz",
        _                                      => t.ToString()
    };

    private static string GetLayerForSystem(MechanicalSystemType t) => t switch
    {
        MechanicalSystemType.DomesticColdWater => "MEK_TEMIZ_SU",
        MechanicalSystemType.DomesticHotWater  => "MEK_SICAK_SU",
        MechanicalSystemType.WasteWater        => "MEK_PIS_SU",
        MechanicalSystemType.RainWater         => "MEK_YAGMUR",
        MechanicalSystemType.FireProtection    => "MEK_YANGIN",
        MechanicalSystemType.Gas               => "MEK_GAZ",
        _                                      => "MEK_GENEL"
    };

    private static uint GetColorForSystem(MechanicalSystemType t) => t switch
    {
        MechanicalSystemType.DomesticColdWater => 0xFF0077CC,
        MechanicalSystemType.DomesticHotWater  => 0xFFCC2200,
        MechanicalSystemType.WasteWater        => 0xFF886633,
        MechanicalSystemType.RainWater         => 0xFF00BBDD,
        MechanicalSystemType.FireProtection    => 0xFFFF0000,
        MechanicalSystemType.Gas               => 0xFFFFCC00,
        _                                      => 0xFFCCCCCC
    };
}
