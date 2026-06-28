using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

public static class AdvancedHydraulicsService
{
    // ═══ 1. İteratif Colebrook-White (Newton-Raphson, 10 iterasyon) ═══
    // Swamee-Jain yaklaşımı %1-2 hata içerir. Bu metod tam çözüm sağlar.
    public static double ColebrookWhiteFriction(double Re, double roughnessMm, double diameterMm)
    {
        if (Re < 2300) return 64.0 / Re; // Laminer

        double eps = roughnessMm / 1000.0;
        double D = diameterMm / 1000.0;
        double relRoughness = eps / D;

        // Swamee-Jain başlangıç tahmini
        double f = 0.25 / Math.Pow(Math.Log10(relRoughness / 3.7 + 5.74 / Math.Pow(Re, 0.9)), 2);

        // Newton-Raphson iterasyonu (Colebrook-White denklemi: 1/√f = -2·log10(ε/3.7D + 2.51/(Re·√f)))
        for (int i = 0; i < 10; i++)
        {
            double sqrtF = Math.Sqrt(f);
            double lhs = 1.0 / sqrtF;
            double arg = relRoughness / 3.7 + 2.51 / (Re * sqrtF);
            double rhs = -2.0 * Math.Log10(arg);
            double residual = lhs - rhs;

            // df/d(1/√f) türevi
            double dArg = -2.51 / (Re * sqrtF * sqrtF * sqrtF) * (-0.5);
            double dRhs = -2.0 / (arg * Math.Log(10)) * dArg;
            double jacobian = 1.0 - dRhs;

            if (Math.Abs(jacobian) < 1e-15) break;
            double correction = residual / jacobian;
            double newInvSqrtF = lhs - correction;
            if (newInvSqrtF <= 0) break;

            f = 1.0 / (newInvSqrtF * newInvSqrtF);

            if (Math.Abs(residual) < 1e-8) break;
        }

        return Math.Max(f, 0.001);
    }

    // ═══ 2. Kısmi Doluluk Hesabı (Manning — h/D eğrileri) ═══
    // TS EN 12056-2: Pis su borularında doluluk oranı h/D ≤ 0.7 olmalıdır
    // Camp formülü: Q/Q_full ve v/v_full oranları

    public static PartialFlowResult CalculatePartialFlow(double actualFlowLs, double pipeDiameterMm, double slopePercent, double manningN = 0.013)
    {
        double D = pipeDiameterMm / 1000.0;
        double S = slopePercent > 0 ? slopePercent / 100.0 : 0.01;
        double A_full = Math.PI * D * D / 4.0;
        double R_full = D / 4.0;

        // Tam doluluk kapasitesi (Manning)
        double v_full = (1.0 / manningN) * Math.Pow(R_full, 2.0 / 3.0) * Math.Sqrt(S);
        double Q_full = v_full * A_full * 1000.0; // m³/s → l/s

        double qRatio = actualFlowLs / Q_full;
        if (qRatio <= 0) return new PartialFlowResult { FillingRatio = 0, ActualVelocity = 0 };
        if (qRatio >= 1) return new PartialFlowResult { FillingRatio = 1.0, ActualVelocity = v_full, IsOverCapacity = true };

        // Camp formülü ile h/D iterasyonu
        // Q/Q_full ≈ f(h/D) — iteratif çözüm (bisection)
        double hdLow = 0.01, hdHigh = 0.99;
        double hd = 0.5;

        for (int i = 0; i < 50; i++)
        {
            hd = (hdLow + hdHigh) / 2.0;
            double qRatioCalc = PartialFlowRatio(hd);
            if (Math.Abs(qRatioCalc - qRatio) < 1e-6) break;
            if (qRatioCalc < qRatio) hdLow = hd;
            else hdHigh = hd;
        }

        double vRatio = PartialVelocityRatio(hd);

        return new PartialFlowResult
        {
            FillingRatio = hd,
            FillingPercent = hd * 100.0,
            ActualVelocity = v_full * vRatio,
            FullCapacityLs = Q_full,
            FullVelocity = v_full,
            IsOverCapacity = hd > 0.7,
            SelfCleansingOk = v_full * vRatio >= 0.7 // min 0.7 m/s temizleme hızı
        };
    }

    // Q/Q_full = f(h/D) — Camp-Shields yaklaşımı (dairesel kesit)
    private static double PartialFlowRatio(double hd)
    {
        double theta = 2.0 * Math.Acos(1.0 - 2.0 * hd);
        double areaRatio = (theta - Math.Sin(theta)) / (2.0 * Math.PI);
        double wetPerimRatio = theta / (2.0 * Math.PI);
        if (wetPerimRatio <= 0) return 0;
        double rRatio = areaRatio / wetPerimRatio;
        return areaRatio * Math.Pow(rRatio, 2.0 / 3.0);
    }

    private static double PartialVelocityRatio(double hd)
    {
        double theta = 2.0 * Math.Acos(1.0 - 2.0 * hd);
        double areaRatio = (theta - Math.Sin(theta)) / (2.0 * Math.PI);
        double wetPerimRatio = theta / (2.0 * Math.PI);
        if (wetPerimRatio <= 0 || areaRatio <= 0) return 0;
        double rRatio = areaRatio / wetPerimRatio;
        return Math.Pow(rRatio, 2.0 / 3.0);
    }

    // ═══ 3. Geometriden Fitting Otomatik Tespiti ═══
    public static List<FittingType> DetectFittingsFromGeometry(IEnumerable<PipeEntity> pipes)
    {
        var fittings = new List<FittingType>();
        var pipeList = pipes.ToList();

        for (int i = 0; i < pipeList.Count; i++)
        {
            var pipe = pipeList[i];
            for (int j = i + 1; j < pipeList.Count; j++)
            {
                var other = pipeList[j];
                if (!AreConnected(pipe, other)) continue;

                double angle = CalculateAngle(pipe, other);

                if (angle >= 85 && angle <= 95)
                    fittings.Add(FittingType.Elbow90);
                else if (angle >= 40 && angle <= 50)
                    fittings.Add(FittingType.Elbow45);
                else if (angle >= 170 && angle <= 180)
                    fittings.Add(FittingType.ElbowReturn180);

                // Te tespiti (3+ boru aynı noktada birleşiyorsa)
                var connPoint = GetConnectionPoint(pipe, other);
                if (connPoint.HasValue)
                {
                    int connCount = pipeList.Count(p => p != pipe && p != other && IsConnectedAt(p, connPoint.Value));
                    if (connCount >= 1)
                        fittings.Add(FittingType.TeeBranch);
                }
            }

            // Giriş/çıkış tespiti (boru ucu hiçbir boruya bağlı değilse)
            bool startConnected = pipeList.Any(p => p != pipe && AreConnected(pipe, p, true));
            bool endConnected = pipeList.Any(p => p != pipe && AreConnected(pipe, p, false));
            if (!startConnected) fittings.Add(FittingType.EntrySharpEdge);
            if (!endConnected) fittings.Add(FittingType.Exit);
        }

        return fittings;
    }

    private static bool AreConnected(PipeEntity a, PipeEntity b, bool? aStartOnly = null)
    {
        double tol = 50; // mm
        if (aStartOnly == true)
            return (a.StartPoint - b.StartPoint).Length() < tol || (a.StartPoint - b.EndPoint).Length() < tol;
        if (aStartOnly == false)
            return (a.EndPoint - b.StartPoint).Length() < tol || (a.EndPoint - b.EndPoint).Length() < tol;
        return (a.EndPoint - b.StartPoint).Length() < tol || (a.EndPoint - b.EndPoint).Length() < tol ||
               (a.StartPoint - b.StartPoint).Length() < tol || (a.StartPoint - b.EndPoint).Length() < tol;
    }

    private static bool IsConnectedAt(PipeEntity p, Afney.Cad.Geometry.Primitives.Vector3D point)
    {
        double tol = 50;
        return (p.StartPoint - point).Length() < tol || (p.EndPoint - point).Length() < tol;
    }

    private static Afney.Cad.Geometry.Primitives.Vector3D? GetConnectionPoint(PipeEntity a, PipeEntity b)
    {
        double tol = 50;
        if ((a.EndPoint - b.StartPoint).Length() < tol) return a.EndPoint;
        if ((a.EndPoint - b.EndPoint).Length() < tol) return a.EndPoint;
        if ((a.StartPoint - b.StartPoint).Length() < tol) return a.StartPoint;
        if ((a.StartPoint - b.EndPoint).Length() < tol) return a.StartPoint;
        return null;
    }

    private static double CalculateAngle(PipeEntity a, PipeEntity b)
    {
        var dirA = (a.EndPoint - a.StartPoint).Normalize();
        var dirB = (b.EndPoint - b.StartPoint).Normalize();
        double dot = dirA.X * dirB.X + dirA.Y * dirB.Y + dirA.Z * dirB.Z;
        dot = Math.Clamp(dot, -1, 1);
        return Math.Acos(Math.Abs(dot)) * 180.0 / Math.PI;
    }

    // ═══ 4. Çoklu Kritik Hat Analizi ═══
    public static List<CriticalPathResult> AnalyzeAllCriticalPaths(
        Afney.Cad.Database.Core.CadDatabase database,
        PressureDropService pressureService)
    {
        var pipes = database.GetAllEntities().OfType<PipeEntity>().ToList();
        var results = new List<CriticalPathResult>();

        // Tüm terminal noktalarını (boru ucu başka boruya bağlı olmayan) bul
        var terminals = new List<PipeEntity>();
        foreach (var pipe in pipes)
        {
            bool endIsTerminal = !pipes.Any(p => p != pipe &&
                ((p.StartPoint - pipe.EndPoint).Length() < 50 || (p.EndPoint - pipe.EndPoint).Length() < 50));
            if (endIsTerminal && pipe.FlowRate > 0)
                terminals.Add(pipe);
        }

        // Her sink (riser/giriş) noktası için kritik hatları hesapla
        var risers = pipes.Where(p => Math.Abs((p.EndPoint - p.StartPoint).Normalize().Z) > 0.8).ToList();

        foreach (var riser in risers.Take(5)) // Max 5 riser analizi
        {
            try
            {
                var report = pressureService.GenerateReport(riser.Id);
                if (report != null && report.Segments.Any())
                {
                    results.Add(new CriticalPathResult
                    {
                        RiserId = riser.Id,
                        RiserLabel = $"R-{riser.Id.ToString()[..6]}",
                        TotalPressureDrop = report.TotalLinearLoss,
                        StaticHead = report.StaticHead,
                        RequiredPressure = report.TotalPressureRequired,
                        MaxVelocity = report.MaxVelocity,
                        SegmentCount = report.Segments.Count,
                        DisadvantagedFixture = report.DisadvantagedFixture
                    });
                }
            }
            catch { /* Skip problematic risers */ }
        }

        return results.OrderByDescending(r => r.RequiredPressure).ToList();
    }

    // ═══ 5. Water Hammer (Su Çekici) Kontrolü — Joukowski ═══
    // ΔP = ρ · c · ΔV (bar)
    // c = ses hızı boruda ≈ 1200-1400 m/s (çelik), 300-400 m/s (plastik)
    public static WaterHammerResult CalculateWaterHammer(PipeEntity pipe, double closureTimeSec = 0.0)
    {
        double v = pipe.GetVelocity();
        if (v <= 0) return new WaterHammerResult();

        double D_m = pipe.InnerDiameter / 1000.0;
        double rho = WaterPropertiesService.GetDensity(pipe.Temperature);

        // Ses hızı — malzemeye bağlı
        double c = pipe.PipeMaterialType switch
        {
            PipeMaterial.Steel_Galvanized => 1350,
            PipeMaterial.PPRC_PN20 or PipeMaterial.PPRC_PN25 => 350,
            PipeMaterial.PVC_SN4 => 400,
            PipeMaterial.PEX_b => 300,
            PipeMaterial.Silent_PP => 380,
            _ => 400
        };

        double pipeLength = pipe.GetLength() / 1000.0;

        // Kritik kapanma süresi: T_cr = 2L/c
        double criticalTime = 2.0 * pipeLength / c;

        double deltaP; // bar
        if (closureTimeSec <= 0 || closureTimeSec <= criticalTime)
        {
            // Ani kapanma (tam Joukowski)
            deltaP = rho * c * v / 100000.0; // Pa → bar
        }
        else
        {
            // Yavaş kapanma (azaltılmış basınç)
            deltaP = rho * c * v / 100000.0 * (criticalTime / closureTimeSec);
        }

        return new WaterHammerResult
        {
            PressureSurgebar = deltaP,
            PressureSurgeMSS = deltaP * 10.197, // bar → mSS
            WaveSpeedMs = c,
            CriticalClosureTimeSec = criticalTime,
            IsDangerous = deltaP > 5.0, // 5 bar üzeri tehlikeli
            Recommendation = deltaP > 5.0
                ? $"Su çekici basıncı {deltaP:F1} bar — yavaş kapanan vana veya su çekici önleyici kullanın (T_min = {criticalTime:F3} s)"
                : $"Su çekici basıncı {deltaP:F1} bar — güvenli aralıkta"
        };
    }
}

// ═══ TS EN 806-2 Cihaz Yük Birimi (FU) Standart Tablosu ═══
public static class FixtureUnitTable
{
    // Kaynak: TS EN 806-2:2005 Tablo 1, DIN 1988-300 Tablo 2
    private static readonly Dictionary<string, FixtureUnitEntry> _table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lavabo"] = new(0.5, 0.07, 0.07, 32, "DN15", "TS EN 806-2 Tablo 1"),
        ["Washbasin"] = new(0.5, 0.07, 0.07, 32, "DN15", "TS EN 806-2 Tablo 1"),
        ["WC (Rezervuar)"] = new(2.5, 0.1, 0.0, 100, "DN15", "TS EN 806-2 Tablo 1"),
        ["WC (Flushvalve)"] = new(6.0, 1.5, 0.0, 100, "DN25", "TS EN 806-2 Tablo 1"),
        ["Klozet"] = new(2.5, 0.1, 0.0, 100, "DN15", "TS EN 806-2 Tablo 1"),
        ["Duş"] = new(1.0, 0.15, 0.15, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["Shower"] = new(1.0, 0.15, 0.15, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["Küvet"] = new(1.5, 0.3, 0.3, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["Bathtub"] = new(1.5, 0.3, 0.3, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["Eviye (Mutfak)"] = new(1.0, 0.07, 0.07, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["KitchenSink"] = new(1.0, 0.07, 0.07, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["Sink"] = new(1.0, 0.07, 0.07, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["Çamaşır Makinesi"] = new(1.0, 0.15, 0.15, 50, "DN15", "DIN 1988-300 §5.3"),
        ["WashingMachine"] = new(1.0, 0.15, 0.15, 50, "DN15", "DIN 1988-300 §5.3"),
        ["Bulaşık Makinesi"] = new(1.0, 0.15, 0.15, 50, "DN15", "DIN 1988-300 §5.3"),
        ["Dishwasher"] = new(1.0, 0.15, 0.15, 50, "DN15", "DIN 1988-300 §5.3"),
        ["Pisuvar"] = new(0.5, 0.3, 0.0, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["Urinal"] = new(0.5, 0.3, 0.0, 50, "DN15", "TS EN 806-2 Tablo 1"),
        ["Bide"] = new(0.5, 0.07, 0.07, 32, "DN15", "TS EN 806-2 Tablo 1"),
        ["Bidet"] = new(0.5, 0.07, 0.07, 32, "DN15", "TS EN 806-2 Tablo 1"),
        ["Yer Süzgeci"] = new(0.0, 0.0, 0.0, 50, "-", "Pasif gider"),
        ["FloorDrain"] = new(0.0, 0.0, 0.0, 50, "-", "Pasif gider"),
        ["Endüstriyel Evye"] = new(2.0, 0.3, 0.3, 75, "DN20", "DIN 1988-300 §5.3"),
        ["Lab Evye"] = new(1.0, 0.15, 0.15, 50, "DN15", "DIN 1988-300 §5.3"),
        ["Cerrahi Lavabo"] = new(1.5, 0.2, 0.2, 50, "DN20", "DIN 1988-300 §5.3"),
        ["Şofben (Ani)"] = new(3.0, 0.2, 0.2, 50, "DN20", "DIN 1988-300 §5.3"),
        ["Yangın Dolabı"] = new(0.0, 1.0, 0.0, 50, "DN25", "TS 9311"),
    };

    public static FixtureUnitEntry? GetEntry(string fixtureType)
    {
        if (string.IsNullOrEmpty(fixtureType)) return null;

        if (_table.TryGetValue(fixtureType, out var entry)) return entry;

        // Fuzzy match
        foreach (var kvp in _table)
        {
            if (fixtureType.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return null;
    }

    public static IReadOnlyDictionary<string, FixtureUnitEntry> GetAll() => _table;
}

public record FixtureUnitEntry(
    double LoadUnits,        // DU / FU (Yük Birimi)
    double ColdFlowLs,       // Soğuk su debisi (l/s)
    double HotFlowLs,        // Sıcak su debisi (l/s)
    double MinWasteDN,        // Minimum atık su çapı (mm)
    string MinSupplyDN,       // Minimum besleme çapı
    string StandardRef        // Referans standart
);

public class PartialFlowResult
{
    public double FillingRatio { get; set; }
    public double FillingPercent { get; set; }
    public double ActualVelocity { get; set; }
    public double FullCapacityLs { get; set; }
    public double FullVelocity { get; set; }
    public bool IsOverCapacity { get; set; }
    public bool SelfCleansingOk { get; set; }
}

public class CriticalPathResult
{
    public Guid RiserId { get; set; }
    public string RiserLabel { get; set; } = "";
    public double TotalPressureDrop { get; set; }
    public double StaticHead { get; set; }
    public double RequiredPressure { get; set; }
    public double MaxVelocity { get; set; }
    public int SegmentCount { get; set; }
    public string DisadvantagedFixture { get; set; } = "";
}

public class WaterHammerResult
{
    public double PressureSurgebar { get; set; }
    public double PressureSurgeMSS { get; set; }
    public double WaveSpeedMs { get; set; }
    public double CriticalClosureTimeSec { get; set; }
    public bool IsDangerous { get; set; }
    public string Recommendation { get; set; } = "";
}
