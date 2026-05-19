using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Vana Kütüphanesi Servisi (ValveLibraryService)
   NEDEN: Projedeki vana tiplerini (Küresel, Sürgülü, Çek Valf vb.)
          merkezi bir katalogdan yönetmek ve UI'a sunmak için.
*/
public class ValveLibraryService
{
    public class ValveDefinition
    {
        public string Id { get; set; } = "";
        public string NameTR { get; set; } = "";
        public string NameEN { get; set; } = "";
        public ValveType Type { get; set; }
        public double NominalDiameter { get; set; } // DN (mm)
        public double LossCoefficient { get; set; } // Zeta katsayısı (Yerel kayıp için)
        public string Standard { get; set; } = "TS EN 1074";
    }

    private readonly List<ValveDefinition> _catalog = new();
    private readonly string _catalogFilePath;

    public ValveLibraryService()
    {
        string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Catalogs");
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        _catalogFilePath = System.IO.Path.Combine(dir, "ValveLibrary.json");
        LoadCatalog();
    }

    private void LoadCatalog()
    {
        if (System.IO.File.Exists(_catalogFilePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(_catalogFilePath);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<List<ValveDefinition>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    _catalog.Clear();
                    _catalog.AddRange(loaded);
                    return;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Vana kataloğu okuma hatası.");
            }
        }

        LoadDefaults();
        SaveCatalog();
    }

    private void SaveCatalog()
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(_catalog, options);
            System.IO.File.WriteAllText(_catalogFilePath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Vana kataloğu yazma hatası.");
        }
    }

    private void LoadDefaults()
    {
        _catalog.Clear();
        _catalog.AddRange(new List<ValveDefinition>
        {
            new() { Id = "BV-15", NameTR = "Küresel Vana DN15", NameEN = "Ball Valve DN15", Type = ValveType.BallValve, NominalDiameter = 15, LossCoefficient = 0.5 },
            new() { Id = "BV-25", NameTR = "Küresel Vana DN25", NameEN = "Ball Valve DN25", Type = ValveType.BallValve, NominalDiameter = 25, LossCoefficient = 0.5 },
            new() { Id = "BV-50", NameTR = "Küresel Vana DN50", NameEN = "Ball Valve DN50", Type = ValveType.BallValve, NominalDiameter = 50, LossCoefficient = 0.5 },
            new() { Id = "GV-100", NameTR = "Sürgülü Vana DN100", NameEN = "Gate Valve DN100", Type = ValveType.GateValve, NominalDiameter = 100, LossCoefficient = 0.2 },
            new() { Id = "CV-25", NameTR = "Çek Valf DN25", NameEN = "Check Valve DN25", Type = ValveType.CheckValve, NominalDiameter = 25, LossCoefficient = 2.5 },
            new() { Id = "CV-50", NameTR = "Çek Valf DN50", NameEN = "Check Valve DN50", Type = ValveType.CheckValve, NominalDiameter = 50, LossCoefficient = 2.0 },
            new() { Id = "PRV-40", NameTR = "Basınç Düşürücü DN40", NameEN = "Pressure Reducing Valve DN40", Type = ValveType.PRV, NominalDiameter = 40, LossCoefficient = 3.0 },
            new() { Id = "FLT-32", NameTR = "Pislik Tutucu DN32", NameEN = "Sediment Filter DN32", Type = ValveType.Filter, NominalDiameter = 32, LossCoefficient = 1.5 }
        });
    }

    public List<ValveDefinition> GetAll() => _catalog.ToList();

    public ValveEntity CreateEntity(string valveId, Vector3D position)
    {
        var def = _catalog.FirstOrDefault(v => v.Id == valveId);
        if (def == null) throw new ArgumentException($"Vana ID '{valveId}' bulunamadı.");

        var entity = new ValveEntity(position, def.Type, def.NominalDiameter)
        {
            Color = 0xFF00FF00,
            SystemType = MechanicalSystemType.DomesticColdWater // Varsayılan
        };
        
        return entity;
    }
}
