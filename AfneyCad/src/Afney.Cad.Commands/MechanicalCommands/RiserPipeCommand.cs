using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

public class RiserPipeCommand : ICadCommand
{
    private readonly CadDatabase        _database;
    private readonly TransactionManager _tm;
    private readonly MechanicalSystemType _systemType;

    private Vector3D? _xyPosition;
    private double    _bottomZ;
    private double    _topZ;
    private int       _step;

    public string    CommandName => "RISERPIPE";
    public Vector3D? ActivePoint => _xyPosition;

    public event Action<string>? OnFeedback;
    public event Action?         OnCompleted;

    public RiserPipeCommand(CadDatabase db, TransactionManager tm, MechanicalSystemType systemType = MechanicalSystemType.DomesticColdWater)
    {
        _database   = db;
        _tm         = tm;
        _systemType = systemType;
    }

    public void Start()
    {
        _step = 0;
        OnFeedback?.Invoke($"KOLON BORU ({_systemType}): Kolon borusunun XY konumunu tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        if (_step == 0)
        {
            _xyPosition = point;
            _step = 1;
            OnFeedback?.Invoke("KOLON BORU: Taban yüksekliğini girin (varsayılan: 0). ENTER ile onaylayın.");
            _bottomZ = 0;
            _step = 2;
            OnFeedback?.Invoke("KOLON BORU: Son kat yüksekliğini tıklayarak belirtin veya komut satırına metre girin.");
        }
        else if (_step == 2)
        {
            _topZ = 6.0;
            CreateRiserPipe();
        }
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter && _step == 2)
        {
            _topZ = 6.0;
            CreateRiserPipe();
        }
    }

    private void CreateRiserPipe()
    {
        if (_xyPosition == null) return;

        var startPt = new Vector3D(_xyPosition.Value.X, _xyPosition.Value.Y, _bottomZ);
        var endPt   = new Vector3D(_xyPosition.Value.X, _xyPosition.Value.Y, _topZ);

        var pipe = new PipeEntity(startPt, endPt, 32)
        {
            SystemType    = _systemType,
            Layer         = _systemType == MechanicalSystemType.DomesticColdWater ? "MEP_TEMIZ_SU" : "MEP_SICAK_SU",
            Color         = _systemType == MechanicalSystemType.DomesticColdWater ? 0xFF0088FF : 0xFFFF4444
        };
        _tm.Submit(new AddEntityOperation(_database, pipe));

        OnFeedback?.Invoke($"KOLON BORU: {_systemType} kolon borusu oluşturuldu (Z: {_bottomZ} → {_topZ} m).");
        _xyPosition = null;
        _step = 0;
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }
    public void Draw(IRenderContext ctx) { }
    public void Cancel() { _xyPosition = null; _step = 0; }
}
