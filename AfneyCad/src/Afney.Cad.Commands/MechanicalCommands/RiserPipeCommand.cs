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

    // Komut satırından gelen son sayısal giriş (metre cinsinden kot)
    private string _inputBuffer = "";

    public void Start()
    {
        _step = 0;
        _bottomZ = 0;
        _topZ    = 3.0; // varsayılan kat yüksekliği 3 m
        _inputBuffer = "";
        OnFeedback?.Invoke($"KOLON BORU ({_systemType}): Kolon XY konumunu tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        if (_step == 0)
        {
            _xyPosition = point;
            _step = 1;
            OnFeedback?.Invoke("KOLON BORU: Taban kotunu girin (m, varsayılan 0) → ENTER. Veya bir sonraki tıklama için tekrar tıklayın.");
        }
        else if (_step == 2)
        {
            // Tıklama ile üst kot belirleme: Z koordinatından al
            _topZ = point.Z != 0 ? point.Z : _topZ;
            CreateRiserPipe();
        }
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter)
        {
            if (_step == 1)
            {
                if (double.TryParse(_inputBuffer, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double bot))
                    _bottomZ = bot;
                _inputBuffer = "";
                _step = 2;
                OnFeedback?.Invoke($"KOLON BORU: Taban kotu = {_bottomZ:F1} m. Şimdi üst kotunu girin (m) → ENTER.");
            }
            else if (_step == 2)
            {
                if (double.TryParse(_inputBuffer, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double top))
                    _topZ = top;
                _inputBuffer = "";
                CreateRiserPipe();
            }
        }
    }

    // Komut satırı karakterlerini biriktir (CadViewport "TextInput" olayından beslenir)
    public void OnTextInput(char c)
    {
        if (char.IsDigit(c) || c == '.' || c == ',')
            _inputBuffer += c == ',' ? '.' : c;
        else if (c == '\b' && _inputBuffer.Length > 0)
            _inputBuffer = _inputBuffer[..^1];
    }

    private void CreateRiserPipe()
    {
        if (_xyPosition == null) return;

        var startPt = new Vector3D(_xyPosition.Value.X, _xyPosition.Value.Y, _bottomZ * 1000);
        var endPt   = new Vector3D(_xyPosition.Value.X, _xyPosition.Value.Y, _topZ   * 1000);

        var pipe = new PipeEntity(startPt, endPt, 50) { SystemType = _systemType };
        pipe.Layer = GetLayerForSystem(_systemType);
        pipe.ApplySystemColor();

        _tm.Submit(new AddEntityOperation(_database, pipe));

        OnFeedback?.Invoke($"KOLON BORU: {_systemType} kolon oluşturuldu ({_bottomZ:F1}m → {_topZ:F1}m).");
        _xyPosition = null;
        _step = 0;
        OnCompleted?.Invoke();
    }

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

    public void OnPointerMoved(Vector3D point) { }
    public void Draw(IRenderContext ctx) { }
    public void Cancel() { _xyPosition = null; _step = 0; }
}
