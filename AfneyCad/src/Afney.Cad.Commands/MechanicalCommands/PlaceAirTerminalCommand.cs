using System;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   NE: Hava Terminali Yerleştirme Komutu (PlaceAirTerminalCommand)
   NEDEN: Difüzör/menfez/panjur kütüphanesi (AirTerminalEntity) daha önce Presentation/Render
          katmanına hiç bağlanmamıştı — Mechanical katmanında iyi test edilmiş olsa da çizime
          yerleştirilemiyordu. Bu komut, PlaceFixtureCommand (SanitaryFixtureEntity) ile aynı
          "tek nokta tıkla → yerleştir" desenini izler; AirTerminalEntity tek portlu (Neck) bir
          uç birim olduğundan (ValveEntity/DamperEntity gibi seri iki portlu değil) kanalı
          bölmez — en yakın DuctEntity ucuna (varsa) yaslanır, yoksa serbest konuma yerleşir.

   NASIL (Mühendislik Detayı):
   - SnapTol içinde en yakın kanal UCU (Start/End) bulunursa: terminal o uca oturtulur, yönü
     kanalın ekseni doğrultusunda (kanaldan dışarı bakacak şekilde) ayarlanır.
   - Kanal ucu bulunamazsa, terminal tıklanan noktaya serbest (Rotation=0) yerleştirilir
     (ör. duvar/tavan panjuru — mimari algılama sonrası ayrıca hizalanabilir).
*/
public class PlaceAirTerminalCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private AirTerminalType _terminalType = AirTerminalType.SupplyDiffuser;
    private double _airFlowM3h = 100.0;
    private double _neckDiameter = 200.0;

    private const double SnapTol = 300.0; // mm — kanal ucuna yaslanma toleransı

    public string CommandName => "PLACEAIRTERMINAL";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;
    public event Action<CadEntity>? OnEntityPlaced;

    public PlaceAirTerminalCommand(CadDatabase database)
    {
        _database = database;
    }

    public void SetTerminalType(AirTerminalType type, double airFlowM3h, double neckDiameter = 200.0)
    {
        _terminalType = type;
        _airFlowM3h = airFlowM3h;
        _neckDiameter = neckDiameter;
    }

    public void Start()
    {
        OnFeedback?.Invoke($"HAVA TERMİNALİ ({_terminalType}): Yerleşim noktası seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var terminal = new AirTerminalEntity(point, _terminalType, _airFlowM3h)
        {
            InnerDiameter = _neckDiameter,
            Layer = "MEP_HAVALANDIRMA",
            SystemType = MechanicalSystemType.Ventilation
        };

        var ducts = _database.GetAllEntities().OfType<DuctEntity>().ToList();
        DuctEntity? nearestDuct = null;
        bool nearestIsStart = true;
        double minDist = double.MaxValue;

        foreach (var duct in ducts)
        {
            double dStart = point.DistanceTo(duct.StartPoint);
            double dEnd = point.DistanceTo(duct.EndPoint);
            if (dStart < minDist && dStart <= SnapTol) { minDist = dStart; nearestDuct = duct; nearestIsStart = true; }
            if (dEnd < minDist && dEnd <= SnapTol) { minDist = dEnd; nearestDuct = duct; nearestIsStart = false; }
        }

        if (nearestDuct != null)
        {
            var anchor = nearestIsStart ? nearestDuct.StartPoint : nearestDuct.EndPoint;
            var other = nearestIsStart ? nearestDuct.EndPoint : nearestDuct.StartPoint;
            var outward = anchor - other; // kanaldan dışarı doğru
            if (outward.Length() > 0.001)
            {
                outward = outward * (1.0 / outward.Length());
                terminal.Rotation = Math.Atan2(outward.Y, outward.X);
            }
            terminal.Position = anchor;
            terminal.InnerDiameter = nearestDuct.Shape == DuctShape.Circular
                ? nearestDuct.DiameterMm
                : _neckDiameter;

            OnFeedback?.Invoke($"{_terminalType} kanal ucuna yerleştirildi.");
        }
        else
        {
            OnFeedback?.Invoke($"{_terminalType} serbest konuma yerleştirildi (yakın kanal ucu bulunamadı).");
        }

        _database.AddEntity(terminal);
        OnEntityPlaced?.Invoke(terminal);
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape)
            OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context) { }

    public void Cancel() { }
}
