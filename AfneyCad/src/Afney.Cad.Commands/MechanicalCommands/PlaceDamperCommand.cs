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
   NE: Damper Yerleştirme Komutu (PlaceDamperCommand)
   NEDEN: Volume/Fire/Smoke/BackDraft damper kütüphanesi (DamperEntity) daha önce
          Presentation/Render katmanına hiç bağlanmamıştı. DamperEntity, ValveEntity ile
          BİREBİR AYNI port deseni taşır (2 port: Inlet/Outlet, kanal hattına seri bağlanır) —
          bu yüzden yerleştirme mantığı ValveLibraryDialog.Insert_Click'teki "en yakın hattı
          bul, hatta yasla, hattı böl, damperi araya yerleştir" desenini birebir izler; sadece
          PipeEntity yerine DuctEntity üzerinde çalışır.

   NASIL (Mühendislik Detayı):
   - SnapTol içinde en yakın DuctEntity segmenti aranır.
   - Bulunursa: damper kanal eksenine yaslanır (Rotation = kanal yönü), kanal damper gövde
     boyu (Size) kadar iki parçaya bölünür (segment başı→damper girişi, damper çıkışı→segment
     sonu), orta parça silinir.
   - Bulunamazsa damper tıklanan noktaya serbest (Rotation=0) yerleştirilir.
   - Fire/Smoke/FireSmoke tipleri DamperEntity ctor'unda otomatik FireRatingMin=90 alır (EN 1366-2).
*/
public class PlaceDamperCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private DamperType _damperType = DamperType.Volume;
    private double _diameter = 250.0;

    private const double SnapTol = 300.0; // mm

    public string CommandName => "PLACEDAMPER";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;
    public event Action<CadEntity>? OnEntityPlaced;

    public PlaceDamperCommand(CadDatabase database)
    {
        _database = database;
    }

    public void SetDamperType(DamperType type, double diameter = 250.0)
    {
        _damperType = type;
        _diameter = diameter;
    }

    public void Start()
    {
        OnFeedback?.Invoke($"DAMPER ({_damperType}): Kanal hattı üzerinde yerleşim noktası seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var damper = new DamperEntity(point, _damperType, _diameter)
        {
            Layer = "MEP_HAVALANDIRMA",
            SystemType = MechanicalSystemType.Ventilation
        };

        var ducts = _database.GetAllEntities().OfType<DuctEntity>().ToList();
        DuctEntity? nearest = null;
        double minDist = double.MaxValue;

        foreach (var duct in ducts)
        {
            double d = DistanceToSegment(point, duct.StartPoint, duct.EndPoint);
            if (d < minDist && d <= SnapTol) { minDist = d; nearest = duct; }
        }

        if (nearest != null)
        {
            var dir = nearest.EndPoint - nearest.StartPoint;
            double ductLen = dir.Length();

            if (ductLen > 1.0)
            {
                var unit = dir * (1.0 / ductLen);
                var w = point - nearest.StartPoint;
                double t = Math.Clamp(w.Dot(unit) / ductLen, 0.0, 1.0);

                damper.Position = nearest.StartPoint + dir * t;
                damper.Rotation = Math.Atan2(unit.Y, unit.X);
                damper.InnerDiameter = nearest.Shape == DuctShape.Circular ? nearest.DiameterMm : _diameter;

                double half = damper.Size / 2.0;
                double tIn = Math.Clamp(t - half / ductLen, 0.0, 1.0);
                double tOut = Math.Clamp(t + half / ductLen, 0.0, 1.0);

                if (tIn > 0.001)
                    _database.AddEntity(CloneDuct(nearest, nearest.StartPoint, nearest.StartPoint + dir * tIn));

                if (tOut < 0.999)
                    _database.AddEntity(CloneDuct(nearest, nearest.StartPoint + dir * tOut, nearest.EndPoint));

                _database.RemoveEntity(nearest.Id);
                OnFeedback?.Invoke($"{_damperType} damper kanal hattına yerleştirildi — hat bölündü.");
            }
            else
            {
                OnFeedback?.Invoke($"{_damperType} damper yerleştirildi (kanal çok kısa, bölünmedi).");
            }
        }
        else
        {
            OnFeedback?.Invoke($"{_damperType} damper serbest konuma yerleştirildi (yakın kanal bulunamadı).");
        }

        _database.AddEntity(damper);
        OnEntityPlaced?.Invoke(damper);
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

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static DuctEntity CloneDuct(DuctEntity src, Vector3D start, Vector3D end)
    {
        var clone = src.Shape == DuctShape.Circular
            ? new DuctEntity(start, end, src.DiameterMm)
            : new DuctEntity(start, end, src.WidthMm, src.HeightMm);

        clone.Type = src.Type;
        clone.InsulationMm = src.InsulationMm;
        clone.AirFlowM3h = src.AirFlowM3h;
        clone.VelocityMs = src.VelocityMs;
        clone.SystemType = src.SystemType;
        clone.Layer = src.Layer;
        clone.Color = src.Color;
        return clone;
    }

    private static double DistanceToSegment(Vector3D p, Vector3D s, Vector3D e)
    {
        var v = e - s;
        var w = p - s;
        double c2 = v.Dot(v);
        if (c2 <= 0) return p.DistanceTo(s);
        double b = Math.Clamp(w.Dot(v) / c2, 0.0, 1.0);
        return p.DistanceTo(s + v * b);
    }
}
