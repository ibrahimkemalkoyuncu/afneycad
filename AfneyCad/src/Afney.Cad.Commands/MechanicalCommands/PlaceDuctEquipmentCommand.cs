using System;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   Esnek bağlantı / filtre / bobin / CAV / VAV kutusunu kanal hattı üzerine yerleştirir.
   Mantık PlaceDamperCommand ile aynı: en yakın DuctEntity'yi bul, hatta yasla, hattı gövde boyu
   kadar iki parçaya böl, ekipmanı araya koy; hepsi tek CompositeOperation (tek Ctrl+Z).
*/
public class PlaceDuctEquipmentCommand : ICadCommand
{
    private const double SnapTol = 300.0; // mm

    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly DuctEquipmentEntity _prototype;

    public string CommandName => "PLACEDUCTEQUIPMENT";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;
    public event Action<CadEntity>? OnEntityPlaced;

    public PlaceDuctEquipmentCommand(CadDatabase database, TransactionManager transactionManager, DuctEquipmentEntity prototype)
    {
        _database = database;
        _transactionManager = transactionManager;
        _prototype = prototype;
    }

    public void Start()
    {
        OnFeedback?.Invoke($"KANAL EKİPMANI ({DuctEquipmentEntity.ShortLabel(_prototype.EquipmentType)}): kanal hattı üzerinde yerleşim noktası seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var equipment = (DuctEquipmentEntity)_prototype.Clone();
        equipment.Position = point;

        DuctEntity? nearest = null;
        double minDist = double.MaxValue;
        foreach (var duct in _database.GetAllEntities().OfType<DuctEntity>())
        {
            double d = DistanceToSegment(point, duct.StartPoint, duct.EndPoint);
            if (d < minDist && d <= SnapTol) { minDist = d; nearest = duct; }
        }

        var composite = new CompositeOperation($"{DuctEquipmentEntity.ShortLabel(equipment.EquipmentType)} Yerleştir");

        if (nearest != null && nearest.GetLength() > 1.0)
        {
            var dir = nearest.EndPoint - nearest.StartPoint;
            double ductLen = dir.Length();
            var unit = dir * (1.0 / ductLen);
            double t = Math.Clamp((point - nearest.StartPoint).Dot(unit) / ductLen, 0.0, 1.0);

            equipment.Position = nearest.StartPoint + dir * t;
            equipment.Rotation = Math.Atan2(unit.Y, unit.X);
            equipment.InnerDiameter = nearest.Shape == DuctShape.Circular ? nearest.DiameterMm : equipment.InnerDiameter;

            double half = equipment.Size / 2.0;
            double tIn = Math.Clamp(t - half / ductLen, 0.0, 1.0);
            double tOut = Math.Clamp(t + half / ductLen, 0.0, 1.0);

            if (tIn > 0.001)
                composite.Add(new AddEntityOperation(_database, CloneDuct(nearest, nearest.StartPoint, nearest.StartPoint + dir * tIn)));
            if (tOut < 0.999)
                composite.Add(new AddEntityOperation(_database, CloneDuct(nearest, nearest.StartPoint + dir * tOut, nearest.EndPoint)));

            composite.Add(new RemoveEntityOperation(_database, nearest));
            OnFeedback?.Invoke("Ekipman kanal hattına yerleştirildi — hat bölündü.");
        }
        else
        {
            OnFeedback?.Invoke("Ekipman serbest konuma yerleştirildi (yakın kanal bulunamadı).");
        }

        composite.Add(new AddEntityOperation(_database, equipment));
        _transactionManager.Submit(composite);

        OnEntityPlaced?.Invoke(equipment);
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context) { }

    public void Cancel() { }

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
        double c2 = v.Dot(v);
        if (c2 <= 0) return p.DistanceTo(s);
        double b = Math.Clamp((p - s).Dot(v) / c2, 0.0, 1.0);
        return p.DistanceTo(s + v * b);
    }
}
