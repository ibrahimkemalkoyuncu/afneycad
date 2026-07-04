using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Otomatik Boru Boyutlandırma Servisi (AutoSizingService)
   NEDEN: TS EN 806-3 / TS 1258 normlarına göre projedeki tüm boruları
          Fixture Unit yöntemiyle tek komutla otomatik boyutlandırmak için.

   YÖNTEM:
     1. Her boru için LoadUnits (FU) → tasarım debisi (l/s) → Q (m³/h)
     2. Q + sistem tipine göre max hız limitiyle iç çap:  D = sqrt(4Q/(π·v))
     3. Standart DN serisine yuvarla (DN15..DN300)
     4. Klozet yükü taşıyan borular → min DN100 (TS 1258 şartı)
     5. Sonuç: güncellenen boru sayısı, değişen DN'ler, uyarılar
*/
public class AutoSizingService
{
    // ── TS EN 806-3 Hız Limitleri ─────────────────────────────────────────────
    private static readonly Dictionary<MechanicalSystemType, double> _maxVelocity = new()
    {
        [MechanicalSystemType.DomesticColdWater] = 2.0,   // m/s — TS EN 806-3 §7
        [MechanicalSystemType.DomesticHotWater]  = 1.5,   // m/s — sıcak su: daha düşük aşınma için
        [MechanicalSystemType.FireProtection]    = 2.5,   // m/s — NFPA / TS 9811
        [MechanicalSystemType.Gas]               = 5.0,   // m/s — TS 7363 (gaz, düşük basınç)
        [MechanicalSystemType.WasteWater]        = 0.7,   // m/s — minimum doluluk hızı
        [MechanicalSystemType.RainWater]         = 0.7,   // m/s — TS EN 12056-3
        [MechanicalSystemType.Ventilation]       = 5.0,   // m/s — hava kanalı
    };

    private const double DefaultMaxVelocity = 2.0; // m/s

    // ── Standart DN Serisi (iç çap mm cinsinden) ──────────────────────────────
    private static readonly double[] StandardDiametersMm =
        [12, 16, 20, 25, 32, 40, 50, 63, 75, 90, 100, 125, 150, 200, 250, 300];

    // ── Sonuç Modeli ─────────────────────────────────────────────────────────
    public class SizingResult
    {
        public int TotalPipes         { get; set; }
        public int ResizedPipes       { get; set; }
        public int UnchangedPipes     { get; set; }
        public int WCMinimumApplied   { get; set; }
        public List<SizingChange> Changes { get; set; } = [];
        public List<string> Warnings  { get; set; } = [];
        public string Summary         { get; set; } = "";
    }

    public class SizingChange
    {
        public string PipeId       { get; set; } = "";
        public string SystemType   { get; set; } = "";
        public double OldDiameterMm { get; set; }
        public double NewDiameterMm { get; set; }
        public double FlowM3h      { get; set; }
        public double VelocityMs   { get; set; }
        public double LoadUnits    { get; set; }
        public string Reason       { get; set; } = "";
    }

    // ── Ana API ──────────────────────────────────────────────────────────────

    public SizingResult SizeAll(CadDatabase database)
    {
        var pipes = database.GetAllEntities().OfType<PipeEntity>().ToList();
        var result = new SizingResult { TotalPipes = pipes.Count };

        foreach (var pipe in pipes)
        {
            double oldDia = pipe.InnerDiameter; // mm

            double flowLs  = FuToDesignFlow(pipe.LoadUnits, pipe.SystemType);
            double flowM3h = flowLs * 3.6;
            double vMax    = GetMaxVelocity(pipe.SystemType);
            double requiredDiaMm = DiameterFromFlow(flowLs, vMax);

            // WC minimum: DN 100 per TS 1258
            if (pipe.IsCarryingWCLoad && requiredDiaMm < 100)
            {
                requiredDiaMm = 100;
                result.WCMinimumApplied++;
            }

            double newDia = RoundUpToStandardDN(requiredDiaMm);

            // Sanity: never reduce below existing if manually set larger
            // (respect user overrides when pipe has no FU assigned)
            if (pipe.LoadUnits <= 0)
            {
                result.UnchangedPipes++;
                result.Warnings.Add($"Boru {pipe.Id}: Yükleme birimi (FU) = 0 — boyutlandırma atlandı.");
                continue;
            }

            double actualVelocity = flowLs > 0 && newDia > 0
                ? flowLs / (Math.PI * Math.Pow(newDia / 1000.0 / 2, 2)) / 1000.0
                : 0;

            pipe.InnerDiameter = newDia; // mm
            pipe.FlowRate      = flowM3h;
            pipe.Velocity      = actualVelocity;

            if (Math.Abs(newDia - oldDia) > 0.5)
            {
                result.ResizedPipes++;
                result.Changes.Add(new SizingChange
                {
                    PipeId         = pipe.Id.ToString(),
                    SystemType     = pipe.SystemType.ToString(),
                    OldDiameterMm  = oldDia,
                    NewDiameterMm  = newDia,
                    FlowM3h        = flowM3h,
                    VelocityMs     = actualVelocity,
                    LoadUnits      = pipe.LoadUnits,
                    Reason         = BuildReason(pipe, flowLs, vMax, requiredDiaMm, newDia),
                });
            }
            else
            {
                result.UnchangedPipes++;
            }

            if (actualVelocity > vMax * 1.1)
                result.Warnings.Add($"Boru {pipe.Id} DN{newDia:F0}: hız {actualVelocity:F2} m/s > limit {vMax:F1} m/s");
        }

        result.Summary = $"{result.TotalPipes} boru kontrol edildi — " +
                         $"{result.ResizedPipes} yeniden boyutlandırıldı, " +
                         $"{result.UnchangedPipes} değişmedi, " +
                         $"{result.WCMinimumApplied} boruda DN100 min. uygulandı.";

        return result;
    }

    // ── FU → Debi Dönüşümü (TS EN 806-3 / Walther Formülü) ──────────────────

    public static double FuToDesignFlow(double fu, MechanicalSystemType system)
    {
        if (fu <= 0) return 0;

        // Atık su / yağmur suyu: min 0.5 l/s per FU (Manning bazlı)
        if (system == MechanicalSystemType.WasteWater ||
            system == MechanicalSystemType.RainWater)
        {
            return Math.Max(0.3, 0.5 * Math.Sqrt(fu));
        }

        // Basınçlı sistemler: TS EN 806-3 §6.2 — Walther formülü yaklaşımı
        // Q [l/s] = 0.682 * sqrt(FU)  (Walther, Türkiye pratiğine göre uyarlanmış)
        // Düşük FU için lineer bölge (< 5 FU)
        return fu < 5
            ? 0.15 + 0.10 * fu
            : 0.682 * Math.Sqrt(fu);
    }

    // ── Akış → Çap (Debi + Hız → iç çap) ────────────────────────────────────

    private static double DiameterFromFlow(double flowLs, double maxVelocityMs)
    {
        if (flowLs <= 0 || maxVelocityMs <= 0) return 15;
        double flowM3s = flowLs / 1000.0;
        double diaMeter = Math.Sqrt(4.0 * flowM3s / (Math.PI * maxVelocityMs));
        return diaMeter * 1000.0; // m → mm
    }

    private static double RoundUpToStandardDN(double diaMm)
    {
        foreach (double dn in StandardDiametersMm)
            if (dn >= diaMm) return dn;
        return StandardDiametersMm[^1]; // max
    }

    private static double GetMaxVelocity(MechanicalSystemType system)
        => _maxVelocity.TryGetValue(system, out double v) ? v : DefaultMaxVelocity;

    private static string BuildReason(PipeEntity pipe, double flowLs, double vMax, double reqDia, double finalDia)
    {
        string base_ = $"FU={pipe.LoadUnits:F1} → Q={flowLs:F3} l/s, v≤{vMax:F1} m/s → req DN{reqDia:F0} → DN{finalDia:F0}";
        if (pipe.IsCarryingWCLoad && reqDia < 100) return base_ + " [WC min DN100]";
        return base_;
    }
}
