using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Doğalgaz Boru Boyutlandırma Servisi (GasCalcSheetService)
   NEDEN: TS EN 1775 ve TS 7363 kapsamında bina içi doğalgaz tesisat hesabı.

   HESAP YÖNTEMLERİ:
     - Düşük Basınç (< 50 mbar): Weymouth basit formülü
         ΔP = λ · (L/D) · (v²/2) · ρ
         ya da pratik form: Q = k · D^2.71 · √(ΔP/(L·ρ))
     - Boru çapı tayini: Hız ≤ 8 m/s (TS EN 1775 §7.2)
     - Minimum basınç: Cihaz öncesi ≥ 17 mbar (düşük basınç)

   STANDARTLAR:
     - TS EN 1775:2007 (Gaz tedarik — Bina içi boru tesisatı)
     - TS 7363 (Doğalgaz iç tesisat)
*/
public class GasCalcSheetService
{
    // ── Giriş Parametreleri ────────────────────────────────────────────────────
    public class CalcOptions
    {
        public double SupplyPressureMbar  { get; set; } = 21.0;  // Sayaç çıkış basıncı (mbar)
        public double MinDevicePressure   { get; set; } = 17.0;  // Cihaz öncesi min. basınç (mbar)
        public double MaxVelocityMs       { get; set; } = 8.0;   // Maks. hız (m/s)
        public double GasDensity          { get; set; } = 0.72;  // Doğalgaz yoğunluğu (kg/m³) — CH4 ağırlıklı
        public double CalorificValue      { get; set; } = 34.02; // Alt ısıl değer (MJ/m³) — Doğalgaz
        public string PipeMaterial        { get; set; } = "Çelik"; // Çelik / Bakır / PE
        public double RoughnessKMm        { get; set; } = 0.046; // Pürüzlülük k (mm) — çelik
    }

    // ── Segment/Cihaz Tanımı ──────────────────────────────────────────────────
    public class GasDevice
    {
        public string Name           { get; set; } = "";
        public double NominalPowerKw { get; set; }       // Nominal güç (kW)
        public double LoadFactor     { get; set; } = 1.0; // Eş zamanlılık (0-1)
        public double FlowM3h        => NominalPowerKw * LoadFactor * 3.6 / CalorificValueRef;
        private const double CalorificValueRef = 34.02; // MJ/m³
    }

    // ── Hesap Satırı ──────────────────────────────────────────────────────────
    public class CalcRow
    {
        public int    RowNo          { get; set; }
        public string SegmentName    { get; set; } = "";
        public double LengthM        { get; set; }
        public double EquivLengthM   { get; set; }  // L + yerel kayıplar (L_ekv)
        public double FlowM3h        { get; set; }  // Debi (m³/h)
        public double FlowM3s        => FlowM3h / 3600.0;
        public double DiameterMm     { get; set; }  // Seçilen iç çap (mm)
        public double VelocityMs     { get; set; }  // Hız (m/s)
        public double PressureDropMbar { get; set; } // Basınç düşümü (mbar)
        public double RemainingPressureMbar { get; set; } // Kalan basınç
        public bool   IsOk           { get; set; }
        public string Warnings       { get; set; } = "";
    }

    // ── Sonuç ─────────────────────────────────────────────────────────────────
    public class CalcSheetResult
    {
        public List<CalcRow>  Rows             { get; set; } = [];
        public CalcOptions    Options          { get; set; } = new();
        public List<GasDevice> Devices         { get; set; } = [];
        public double         TotalFlowM3h     { get; set; }
        public double         TotalPressureDrop { get; set; }
        public double         TotalLengthM     { get; set; }
        public int            WarningCount     { get; set; }
        public string         Summary          { get; set; } = "";
        public List<string>   Notes            { get; set; } = [];
    }

    // ── Standart Çap Serisi (mm iç çap) ──────────────────────────────────────
    private static readonly double[] StandardDiameters = [12, 16, 20, 25, 32, 40, 50, 65, 80, 100, 125, 150];

    // ── Ana Hesap ─────────────────────────────────────────────────────────────
    public CalcSheetResult Calculate(List<GasDevice> devices, List<(string name, double lengthM, double[] deviceIndices)> segments, CalcOptions opts)
    {
        var result = new CalcSheetResult { Options = opts, Devices = devices };

        if (devices.Count == 0)
        {
            result.Notes.Add("Hesap yapılabilmesi için en az bir cihaz tanımlanmalıdır.");
            result.Summary = "Cihaz tanımlı değil";
            return result;
        }

        double remainingP = opts.SupplyPressureMbar;
        int rowNo = 1;

        foreach (var (segName, lengthM, deviceIdxArr) in segments)
        {
            // Segment debisi: ilgili cihazların toplamı
            double qM3h = deviceIdxArr
                .Where(i => i >= 0 && i < devices.Count)
                .Sum(i => devices[(int)i].FlowM3h);

            if (qM3h <= 0) qM3h = devices.Sum(d => d.FlowM3h); // fallback: tüm cihazlar

            // Eşdeğer uzunluk: %20 yerel kayıp eklentisi (fitting)
            double lEkv = lengthM * 1.20;

            var (dn, v, dp) = SizePipe(qM3h, lEkv, opts);

            remainingP -= dp;

            var row = new CalcRow
            {
                RowNo                  = rowNo++,
                SegmentName            = segName,
                LengthM                = Math.Round(lengthM, 2),
                EquivLengthM           = Math.Round(lEkv, 2),
                FlowM3h                = Math.Round(qM3h, 3),
                DiameterMm             = dn,
                VelocityMs             = Math.Round(v, 2),
                PressureDropMbar       = Math.Round(dp, 3),
                RemainingPressureMbar  = Math.Round(remainingP, 2),
                IsOk                   = v <= opts.MaxVelocityMs && remainingP >= opts.MinDevicePressure,
            };

            var warns = new List<string>();
            if (v > opts.MaxVelocityMs)
                warns.Add($"Hız {v:F2} m/s > {opts.MaxVelocityMs} m/s (TS EN 1775)");
            if (remainingP < opts.MinDevicePressure)
                warns.Add($"Kalan basınç {remainingP:F2} mbar < {opts.MinDevicePressure} mbar");
            row.Warnings = string.Join("; ", warns);
            result.Rows.Add(row);
        }

        result.TotalFlowM3h      = Math.Round(devices.Sum(d => d.FlowM3h), 3);
        result.TotalPressureDrop = Math.Round(result.Rows.Sum(r => r.PressureDropMbar), 3);
        result.TotalLengthM      = Math.Round(result.Rows.Sum(r => r.LengthM), 2);
        result.WarningCount      = result.Rows.Count(r => !r.IsOk);

        result.Summary = $"Q_toplam = {result.TotalFlowM3h:F3} m³/h, " +
                         $"ΔP_toplam = {result.TotalPressureDrop:F3} mbar, " +
                         $"Kalan = {result.Rows.LastOrDefault()?.RemainingPressureMbar:F2} mbar" +
                         (result.WarningCount > 0 ? $" — ⚠ {result.WarningCount} uyarı" : " — ✓ Tamam");

        result.Notes.Add($"Besleme basıncı: {opts.SupplyPressureMbar} mbar");
        result.Notes.Add($"Min. cihaz basıncı: {opts.MinDevicePressure} mbar");
        result.Notes.Add($"Maks. hız: {opts.MaxVelocityMs} m/s (TS EN 1775 §7.2)");
        result.Notes.Add($"Gaz: ρ = {opts.GasDensity} kg/m³, Hv = {opts.CalorificValue} MJ/m³");
        result.Notes.Add("Standart: TS EN 1775:2007, TS 7363");

        return result;
    }

    // ── Weymouth Boru Boyutlandırma ───────────────────────────────────────────
    // Düşük basınç bölgesi (<50 mbar) — Darcy-Weisbach pratik formu
    private static (double dn, double v, double dpMbar) SizePipe(double qM3h, double lEkvM, CalcOptions opts)
    {
        double qM3s = qM3h / 3600.0;

        foreach (double dn in StandardDiameters)
        {
            double d  = dn / 1000.0;  // m
            double A  = Math.PI * d * d / 4.0;
            double v  = qM3s / A;

            if (v > opts.MaxVelocityMs * 1.5) continue; // çok küçük çap, atla

            // Basınç düşümü: Weymouth pratik (düşük basınç)
            // ΔP (Pa) = λ·(L/D)·(ρ·v²/2)
            // λ ≈ 0.02 (pürüzlü boru, hidrolik olarak gelişmiş akış)
            double lambda = 0.02 + 0.0005 / (dn / 1000.0); // basit yaklaşım
            double dpPa   = lambda * (lEkvM / d) * (opts.GasDensity * v * v / 2.0);
            double dpMbar = dpPa / 100.0;  // Pa → mbar (1 mbar = 100 Pa)

            if (v <= opts.MaxVelocityMs)
                return (dn, v, dpMbar);
        }

        // En büyük çap ile git
        double lastDn = StandardDiameters[^1];
        double lastD  = lastDn / 1000.0;
        double lastA  = Math.PI * lastD * lastD / 4.0;
        double lastV  = qM3s / lastA;
        double lastLambda = 0.02;
        double lastDpPa   = lastLambda * (lEkvM / lastD) * (opts.GasDensity * lastV * lastV / 2.0);
        return (lastDn, lastV, lastDpPa / 100.0);
    }

    // ── Veritabanından otomatik hesap (PipeEntity.SystemType == Gas) ──────────
    public CalcSheetResult CalculateFromDatabase(CadDatabase database, CalcOptions opts)
    {
        var gasPipes = database.GetAllEntities()
            .OfType<PipeEntity>()
            .Where(p => p.SystemType == MechanicalSystemType.Gas)
            .ToList();

        if (gasPipes.Count == 0)
        {
            var empty = new CalcSheetResult { Options = opts };
            empty.Notes.Add("Çizimde gas (MEP_GAZ) katmanlı boru bulunamadı.");
            empty.Summary = "Veri Yok";
            return empty;
        }

        // Her boruyu ayrı segment olarak değerlendir
        var devices = new List<GasDevice> { new GasDevice { Name = "Toplam", NominalPowerKw = 20.0 } };
        var segments = gasPipes.Select((p, i) =>
            ($"Segment {i + 1}  Ø{p.InnerDiameter:F0}", p.Length / 1000.0, new double[] { 0 })
        ).ToList();

        return Calculate(devices, segments, opts);
    }
}
