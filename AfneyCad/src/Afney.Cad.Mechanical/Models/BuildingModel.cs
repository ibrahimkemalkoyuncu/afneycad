using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Models;

/*
   NE: BIM-Lite Bina Modeli (BuildingModel)
   NEDEN: CAD nesnelerini "Anlamsız Çizgi" olmaktan çıkarıp, BIM standartlarında (IFC-lite) hiyerarşik bir veriye dönüştürmek için.
   
   HİYERARŞİ: 
   Project -> Building -> Levels -> Rooms -> Systems -> Entities
*/
public class BuildingModel
{
    public string ProjectName { get; set; } = "YENI_PROJE";
    public List<MepLevel> Levels { get; } = new();
    public List<MechanicalSystem> Systems { get; } = new();
    
    // Proje bazlı tüm riser'lar (kolonlar)
    public List<RiserGroup> Risers { get; } = new();

    public BuildingModel()
    {
        InitializeDefaultSystems();
    }

    private void InitializeDefaultSystems()
    {
        Systems.Add(new MechanicalSystem(MechanicalSystemType.DomesticColdWater, "Temiz Su (Soğuk)"));
        Systems.Add(new MechanicalSystem(MechanicalSystemType.DomesticHotWater, "Temiz Su (Sıcak)"));
        Systems.Add(new MechanicalSystem(MechanicalSystemType.WasteWater, "Pis Su"));
    }

    public MepLevel? GetLevelAt(double z)
    {
        return Levels.OrderBy(l => Math.Abs(l.Elevation - z)).FirstOrDefault();
    }
}

public class MechanicalSystem
{
    public MechanicalSystemType Type { get; }
    public string Name { get; }
    public List<MechanicalEntity> Entities { get; } = new();

    public MechanicalSystem(MechanicalSystemType type, string name)
    {
        Type = type;
        Name = name;
    }
}

public class RiserGroup
{
    public string Id { get; set; } = "";
    public MechanicalSystemType SystemType { get; set; }
    public List<PipeEntity> Segments { get; } = new();
    public double TotalFlow => Segments.FirstOrDefault()?.FlowRate ?? 0;
}
