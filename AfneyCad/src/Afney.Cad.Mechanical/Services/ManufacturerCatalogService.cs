using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Üretici Ekipman Katalog Servisi (ManufacturerCatalogService)
   NEDEN: Mühendis tasarım sırasında gerçek üretici verilerine dayalı
          pompa, boru ve vana seçimi yapabilsin diye.
          FINE MEP'teki üretici kataloğu özelliğinin karşılığı.

   KAPSAM:
   - Pompa: Grundfos, Wilo — Q/H eğrisi noktaları, verimlilik, motor gücü
   - Boru: Valsir, Wavin, Geberit — DN, PN, malzeme, pürüzlülük
   - Vana: Honeywell, Danfoss — Kv değeri, basınç kaybı
*/
public class ManufacturerCatalogService
{
    // ── Enum Tanımları ───────────────────────────────────────────────────────────

    public enum PumpManufacturer { Grundfos, Wilo, Lowara, DAB, Ebara }
    public enum PipeManufacturer { Valsir, Wavin, Geberit, Uponor, Rehau }
    public enum ValveManufacturer { Honeywell, Danfoss, Oventrop, TA, IMI }
    public enum PipeMaterial { HDPE, PEX, PPR, Copper, GalvanizedSteel, StainlessSteel, CastIron }

    // ── Pompa Modeli ─────────────────────────────────────────────────────────────

    public class PumpModel
    {
        public string            ModelName    { get; set; } = "";
        public PumpManufacturer  Manufacturer { get; set; }
        public string            Series       { get; set; } = "";
        public double            MaxFlowM3h   { get; set; }   // Maksimum debi (m³/h)
        public double            MaxHeadM     { get; set; }   // Maksimum basma yüksekliği (m)
        public double            NomPowerKw   { get; set; }   // Nominal motor gücü (kW)
        public double            MaxEffPct    { get; set; }   // Maks. verim (%)
        public double            NomSpeedRPM  { get; set; }   // Nominal hız (RPM)
        public string            ConnectionDN { get; set; } = "";   // Bağlantı çapı
        public double            WeightKg     { get; set; }
        public string            Application  { get; set; } = "";   // Isıtma/Soğutma/SHW
        public List<(double Q, double H)> CurvePoints { get; set; } = [];

        // Q noktasında pompa basma yüksekliği interpolasyonu
        public double GetHeadAtFlow(double flowM3h)
        {
            if (CurvePoints.Count < 2) return MaxHeadM * Math.Max(0, 1 - flowM3h / MaxFlowM3h);
            var sorted = CurvePoints.OrderBy(p => p.Q).ToList();
            if (flowM3h <= sorted[0].Q)  return sorted[0].H;
            if (flowM3h >= sorted[^1].Q) return sorted[^1].H;
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (flowM3h >= sorted[i].Q && flowM3h <= sorted[i + 1].Q)
                {
                    double t = (flowM3h - sorted[i].Q) / (sorted[i + 1].Q - sorted[i].Q);
                    return sorted[i].H + t * (sorted[i + 1].H - sorted[i].H);
                }
            }
            return 0;
        }
    }

    // ── Boru Modeli ──────────────────────────────────────────────────────────────

    public class PipeModel
    {
        public string           ModelName    { get; set; } = "";
        public PipeManufacturer Manufacturer { get; set; }
        public PipeMaterial     Material     { get; set; }
        public int              DN           { get; set; }    // Nominal çap (mm)
        public double           OD           { get; set; }    // Dış çap (mm)
        public double           WallThickMm  { get; set; }    // Et kalınlığı (mm)
        public double           ID           => OD - 2 * WallThickMm;  // İç çap (mm)
        public double           PN           { get; set; }    // Basınç sınıfı (bar)
        public double           RoughnessM   { get; set; }    // Pürüzlülük (m)
        public double           TempMaxC     { get; set; }    // Max çalışma sıcaklığı (°C)
        public string           Standard     { get; set; } = "";   // TS EN / DIN
        public double           PricePerMtr  { get; set; }    // TL/m (referans)
        public string           Color        { get; set; } = "";   // Renk kodu
    }

    // ── Vana Modeli ──────────────────────────────────────────────────────────────

    public class ValveModel
    {
        public string           ModelName    { get; set; } = "";
        public ValveManufacturer Manufacturer { get; set; }
        public string           ValveType    { get; set; } = "";   // Küresel/Kelebek/Geri Vurma
        public int              DN           { get; set; }
        public double           Kv           { get; set; }    // Akış katsayısı (m³/h @ 1bar)
        public double           PN           { get; set; }    // Basınç sınıfı (bar)
        public double           TempMaxC     { get; set; }
        public string           BodyMaterial { get; set; } = "";
        public double           PressureDropKpa(double flowM3h) =>
            flowM3h > 0 && Kv > 0 ? 100.0 * Math.Pow(flowM3h / Kv, 2) : 0;
    }

    // ── Pompa Kataloğu ───────────────────────────────────────────────────────────

    public static readonly List<PumpModel> PumpCatalog =
    [
        new PumpModel
        {
            ModelName = "UP 15-14 B", Manufacturer = PumpManufacturer.Grundfos,
            Series = "UP", MaxFlowM3h = 0.7, MaxHeadM = 1.4, NomPowerKw = 0.045,
            MaxEffPct = 12, NomSpeedRPM = 2600, ConnectionDN = "G 1\"",
            WeightKg = 1.4, Application = "Konut Isıtma",
            CurvePoints = [(0, 1.4), (0.35, 1.0), (0.7, 0)]
        },
        new PumpModel
        {
            ModelName = "UP 20-15 N", Manufacturer = PumpManufacturer.Grundfos,
            Series = "UP", MaxFlowM3h = 1.4, MaxHeadM = 1.5, NomPowerKw = 0.068,
            MaxEffPct = 15, NomSpeedRPM = 2600, ConnectionDN = "G 1¼\"",
            WeightKg = 1.9, Application = "Konut / Küçük Ticari",
            CurvePoints = [(0, 1.5), (0.7, 1.1), (1.4, 0)]
        },
        new PumpModel
        {
            ModelName = "UPS 25-60 180", Manufacturer = PumpManufacturer.Grundfos,
            Series = "UPS", MaxFlowM3h = 3.5, MaxHeadM = 6.0, NomPowerKw = 0.220,
            MaxEffPct = 33, NomSpeedRPM = 2700, ConnectionDN = "DN 25",
            WeightKg = 4.5, Application = "Isıtma / SHW Sirkülasyon",
            CurvePoints = [(0, 6.0), (1.0, 5.2), (2.0, 4.0), (3.0, 2.4), (3.5, 0)]
        },
        new PumpModel
        {
            ModelName = "UPS 32-80 180", Manufacturer = PumpManufacturer.Grundfos,
            Series = "UPS", MaxFlowM3h = 7.0, MaxHeadM = 8.0, NomPowerKw = 0.550,
            MaxEffPct = 40, NomSpeedRPM = 2700, ConnectionDN = "DN 32",
            WeightKg = 7.5, Application = "Orta Ölçekli Isıtma",
            CurvePoints = [(0, 8.0), (2.0, 7.2), (4.0, 6.0), (6.0, 3.5), (7.0, 0)]
        },
        new PumpModel
        {
            ModelName = "TPE 40-120/2", Manufacturer = PumpManufacturer.Grundfos,
            Series = "TPE", MaxFlowM3h = 16, MaxHeadM = 12.0, NomPowerKw = 1.10,
            MaxEffPct = 55, NomSpeedRPM = 2900, ConnectionDN = "DN 40",
            WeightKg = 20, Application = "Ticari / Endüstriyel",
            CurvePoints = [(0, 12.0), (4, 11.0), (8, 9.5), (12, 7.0), (16, 0)]
        },
        // ── Wilo ──────────────────────────────────────────────────────────────────
        new PumpModel
        {
            ModelName = "Star-RS 25/4", Manufacturer = PumpManufacturer.Wilo,
            Series = "Star-RS", MaxFlowM3h = 2.0, MaxHeadM = 4.0, NomPowerKw = 0.065,
            MaxEffPct = 28, NomSpeedRPM = 2600, ConnectionDN = "G 1\"",
            WeightKg = 1.8, Application = "Konut Isıtma / SHW",
            CurvePoints = [(0, 4.0), (0.8, 3.2), (1.5, 2.0), (2.0, 0)]
        },
        new PumpModel
        {
            ModelName = "Star-RS 25/6", Manufacturer = PumpManufacturer.Wilo,
            Series = "Star-RS", MaxFlowM3h = 3.0, MaxHeadM = 6.0, NomPowerKw = 0.085,
            MaxEffPct = 32, NomSpeedRPM = 2700, ConnectionDN = "G 1\"",
            WeightKg = 2.0, Application = "Konut / Küçük Ticari",
            CurvePoints = [(0, 6.0), (1.0, 5.0), (2.0, 3.5), (3.0, 0)]
        },
        new PumpModel
        {
            ModelName = "Top-S 30/7", Manufacturer = PumpManufacturer.Wilo,
            Series = "Top-S", MaxFlowM3h = 5.0, MaxHeadM = 7.0, NomPowerKw = 0.250,
            MaxEffPct = 38, NomSpeedRPM = 2700, ConnectionDN = "DN 30",
            WeightKg = 5.2, Application = "Orta Ölçekli Isıtma / Soğutma",
            CurvePoints = [(0, 7.0), (1.5, 6.2), (3.0, 5.0), (4.5, 2.8), (5.0, 0)]
        },
        new PumpModel
        {
            ModelName = "Top-S 40/4", Manufacturer = PumpManufacturer.Wilo,
            Series = "Top-S", MaxFlowM3h = 8.0, MaxHeadM = 4.0, NomPowerKw = 0.250,
            MaxEffPct = 40, NomSpeedRPM = 2700, ConnectionDN = "DN 40",
            WeightKg = 6.5, Application = "Isıtma / Fan-Coil Sirkülasyon",
            CurvePoints = [(0, 4.0), (2.5, 3.5), (5.0, 2.5), (7.5, 1.0), (8.0, 0)]
        },
        new PumpModel
        {
            ModelName = "VeroLine IP-E 32/95-0.37", Manufacturer = PumpManufacturer.Wilo,
            Series = "VeroLine", MaxFlowM3h = 12, MaxHeadM = 9.5, NomPowerKw = 0.370,
            MaxEffPct = 58, NomSpeedRPM = 2800, ConnectionDN = "DN 32",
            WeightKg = 18, Application = "Ticari Isıtma / Soğutma",
            CurvePoints = [(0, 9.5), (3, 9.0), (6, 7.8), (9, 5.5), (12, 0)]
        },
    ];

    // ── Boru Kataloğu ────────────────────────────────────────────────────────────

    public static readonly List<PipeModel> PipeCatalog =
    [
        // ── Valsir Multistrat PPR ──────────────────────────────────────────────
        new() { ModelName="Multistrat PPR DN20", Manufacturer=PipeManufacturer.Valsir, Material=PipeMaterial.PPR,
                DN=20, OD=20, WallThickMm=1.9, PN=20, RoughnessM=0.000007, TempMaxC=95,
                Standard="TS EN ISO 15874", Color="#88BB88", PricePerMtr=12 },
        new() { ModelName="Multistrat PPR DN25", Manufacturer=PipeManufacturer.Valsir, Material=PipeMaterial.PPR,
                DN=25, OD=25, WallThickMm=2.3, PN=20, RoughnessM=0.000007, TempMaxC=95,
                Standard="TS EN ISO 15874", Color="#88BB88", PricePerMtr=18 },
        new() { ModelName="Multistrat PPR DN32", Manufacturer=PipeManufacturer.Valsir, Material=PipeMaterial.PPR,
                DN=32, OD=32, WallThickMm=2.9, PN=20, RoughnessM=0.000007, TempMaxC=95,
                Standard="TS EN ISO 15874", Color="#88BB88", PricePerMtr=26 },
        new() { ModelName="Multistrat PPR DN40", Manufacturer=PipeManufacturer.Valsir, Material=PipeMaterial.PPR,
                DN=40, OD=40, WallThickMm=3.7, PN=20, RoughnessM=0.000007, TempMaxC=95,
                Standard="TS EN ISO 15874", Color="#88BB88", PricePerMtr=38 },
        new() { ModelName="Multistrat PPR DN50", Manufacturer=PipeManufacturer.Valsir, Material=PipeMaterial.PPR,
                DN=50, OD=50, WallThickMm=4.6, PN=16, RoughnessM=0.000007, TempMaxC=95,
                Standard="TS EN ISO 15874", Color="#88BB88", PricePerMtr=55 },
        // ── Wavin PVC-U (Pis Su) ───────────────────────────────────────────────
        new() { ModelName="Wavin AS+ DN40 PVC", Manufacturer=PipeManufacturer.Wavin, Material=PipeMaterial.HDPE,
                DN=40, OD=40, WallThickMm=1.8, PN=4, RoughnessM=0.0000015, TempMaxC=60,
                Standard="TS EN 1329-1", Color="#BBBBBB", PricePerMtr=8 },
        new() { ModelName="Wavin AS+ DN50 PVC", Manufacturer=PipeManufacturer.Wavin, Material=PipeMaterial.HDPE,
                DN=50, OD=50, WallThickMm=1.8, PN=4, RoughnessM=0.0000015, TempMaxC=60,
                Standard="TS EN 1329-1", Color="#BBBBBB", PricePerMtr=11 },
        new() { ModelName="Wavin AS+ DN75 PVC", Manufacturer=PipeManufacturer.Wavin, Material=PipeMaterial.HDPE,
                DN=75, OD=75, WallThickMm=1.8, PN=4, RoughnessM=0.0000015, TempMaxC=60,
                Standard="TS EN 1329-1", Color="#BBBBBB", PricePerMtr=16 },
        new() { ModelName="Wavin AS+ DN100 PVC", Manufacturer=PipeManufacturer.Wavin, Material=PipeMaterial.HDPE,
                DN=100, OD=110, WallThickMm=2.2, PN=4, RoughnessM=0.0000015, TempMaxC=60,
                Standard="TS EN 1329-1", Color="#BBBBBB", PricePerMtr=22 },
        // ── Geberit PEX (Sıhhi) ───────────────────────────────────────────────
        new() { ModelName="Geberit PEXa DN15", Manufacturer=PipeManufacturer.Geberit, Material=PipeMaterial.PEX,
                DN=15, OD=16, WallThickMm=2.0, PN=10, RoughnessM=0.000007, TempMaxC=90,
                Standard="TS EN ISO 15875", Color="#4444AA", PricePerMtr=15 },
        new() { ModelName="Geberit PEXa DN20", Manufacturer=PipeManufacturer.Geberit, Material=PipeMaterial.PEX,
                DN=20, OD=20, WallThickMm=2.0, PN=10, RoughnessM=0.000007, TempMaxC=90,
                Standard="TS EN ISO 15875", Color="#4444AA", PricePerMtr=22 },
        new() { ModelName="Geberit PEXa DN25", Manufacturer=PipeManufacturer.Geberit, Material=PipeMaterial.PEX,
                DN=25, OD=25, WallThickMm=2.3, PN=10, RoughnessM=0.000007, TempMaxC=90,
                Standard="TS EN ISO 15875", Color="#4444AA", PricePerMtr=32 },
        new() { ModelName="Geberit PEXa DN32", Manufacturer=PipeManufacturer.Geberit, Material=PipeMaterial.PEX,
                DN=32, OD=32, WallThickMm=2.9, PN=10, RoughnessM=0.000007, TempMaxC=90,
                Standard="TS EN ISO 15875", Color="#4444AA", PricePerMtr=44 },
    ];

    // ── Vana Kataloğu ─────────────────────────────────────────────────────────────

    public static readonly List<ValveModel> ValveCatalog =
    [
        new() { ModelName="Honeywell V5832A DN15", Manufacturer=ValveManufacturer.Honeywell,
                ValveType="Küresel Vana", DN=15, Kv=3.6, PN=16, TempMaxC=120, BodyMaterial="Pirinç" },
        new() { ModelName="Honeywell V5832A DN20", Manufacturer=ValveManufacturer.Honeywell,
                ValveType="Küresel Vana", DN=20, Kv=6.3, PN=16, TempMaxC=120, BodyMaterial="Pirinç" },
        new() { ModelName="Honeywell V5832A DN25", Manufacturer=ValveManufacturer.Honeywell,
                ValveType="Küresel Vana", DN=25, Kv=10.0, PN=16, TempMaxC=120, BodyMaterial="Pirinç" },
        new() { ModelName="Danfoss MSV-BD DN20", Manufacturer=ValveManufacturer.Danfoss,
                ValveType="Dengeleme Vanası", DN=20, Kv=4.7, PN=16, TempMaxC=120, BodyMaterial="Pirinç" },
        new() { ModelName="Danfoss MSV-BD DN25", Manufacturer=ValveManufacturer.Danfoss,
                ValveType="Dengeleme Vanası", DN=25, Kv=9.5, PN=16, TempMaxC=120, BodyMaterial="Pirinç" },
        new() { ModelName="Danfoss MSV-BD DN32", Manufacturer=ValveManufacturer.Danfoss,
                ValveType="Dengeleme Vanası", DN=32, Kv=16.0, PN=16, TempMaxC=120, BodyMaterial="Pirinç" },
        new() { ModelName="Oventrop Cocon QTZ DN20", Manufacturer=ValveManufacturer.Oventrop,
                ValveType="Ayar Vanası", DN=20, Kv=5.2, PN=16, TempMaxC=120, BodyMaterial="Bronz" },
    ];

    // ── Sorgu Metotları ──────────────────────────────────────────────────────────

    public IEnumerable<PumpModel> FindPumps(double flowM3h, double headM,
                                             PumpManufacturer? manufacturer = null,
                                             string? application = null)
    {
        var q = PumpCatalog.AsEnumerable();
        if (manufacturer.HasValue) q = q.Where(p => p.Manufacturer == manufacturer.Value);
        if (application != null)   q = q.Where(p => p.Application.Contains(application, StringComparison.OrdinalIgnoreCase));
        return q
            .Where(p => p.MaxFlowM3h >= flowM3h && p.GetHeadAtFlow(flowM3h) >= headM)
            .OrderBy(p => p.NomPowerKw)
            .ThenBy(p => p.MaxFlowM3h - flowM3h);
    }

    public IEnumerable<PipeModel> FindPipes(int dn, PipeMaterial? material = null,
                                              PipeManufacturer? manufacturer = null)
    {
        var q = PipeCatalog.AsEnumerable();
        if (material != null)     q = q.Where(p => p.Material == material.Value);
        if (manufacturer != null) q = q.Where(p => p.Manufacturer == manufacturer.Value);
        return q.Where(p => p.DN == dn).OrderBy(p => p.PricePerMtr);
    }

    public IEnumerable<ValveModel> FindValves(int dn, string? valveType = null,
                                               ValveManufacturer? manufacturer = null)
    {
        var q = ValveCatalog.AsEnumerable();
        if (valveType != null)    q = q.Where(v => v.ValveType.Contains(valveType, StringComparison.OrdinalIgnoreCase));
        if (manufacturer != null) q = q.Where(v => v.Manufacturer == manufacturer.Value);
        return q.Where(v => v.DN == dn).OrderBy(v => v.Kv);
    }

    // En uygun pompa: sistem eğrisini (Q, H) listesiyle verilen çalışma noktasına en yakın model
    public PumpModel? BestPump(double flowM3h, double headM)
        => FindPumps(flowM3h, headM).FirstOrDefault();
}
