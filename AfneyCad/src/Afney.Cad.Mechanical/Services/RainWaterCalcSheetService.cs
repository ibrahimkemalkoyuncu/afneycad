using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Yağmur Suyu Hesap Föyü Servisi (RainWaterCalcSheetService)
   NEDEN: TS EN 12056-3 ve TS 821 kapsamında çatı/teras yağmur suyu miktarını
          ve drenaj borularını hesaplar.

   HESAP YÖNTEMLERİ:
     - Akış Yükü : Q = r · C · A / 10000  (l/s)
         r = yağış yoğunluğu (l/s·m²) — Türkiye ortalama: 0.030
         C = yüzey katsayısı (FlatRoof=1.0, GreenRoof=0.5, GravelRoof=0.7)
         A = alan (m²)
     - Manning ile boru boyutlandırma (tam-dolu esas)
*/
public class RainWaterCalcSheetService
{
    // ── Giriş Seçenekleri ─────────────────────────────────────────────────────
    public class CalcOptions
    {
        public double RainfallIntensity { get; set; } = 0.030; // l/s·m² (Türkiye ort.)
        public string Location          { get; set; } = "İstanbul";
        public string PipeMaterial      { get; set; } = "PVC";
        public double ManningN          { get; set; } = 0.011;
        public double DefaultSlopePct   { get; set; } = 1.0;   // % eğim
        public double MaxFillRatio      { get; set; } = 0.70;  // doluluk üst sınırı
        public bool   IncludeGutter     { get; set; } = true;  // oluk hesabı
    }

    // ── Hesap Satırı ──────────────────────────────────────────────────────────
    public class CalcRow
    {
        public int    RowNo          { get; set; }
        public string AreaName       { get; set; } = "";
        public string SurfaceType    { get; set; } = "";
        public double AreaM2         { get; set; }
        public double RunoffC        { get; set; }
        public double RainfallR      { get; set; }   // l/s·m²
        public double DesignFlowLs   { get; set; }   // Q (l/s)
        public double DiameterMm     { get; set; }   // Seçilen DN (mm)
        public double SlopePct       { get; set; }
        public double VelocityMs     { get; set; }
        public double FillRatio      { get; set; }
        public double CapacityLs     { get; set; }   // Tam dolu kapasite
        public bool   IsOk           { get; set; }
        public string Warnings       { get; set; } = "";
    }

    // ── Sonuç ─────────────────────────────────────────────────────────────────
    public class CalcSheetResult
    {
        public List<CalcRow> Rows          { get; set; } = [];
        public CalcOptions   Options       { get; set; } = new();
        public int           TotalAreas    { get; set; }
        public double        TotalAreaM2   { get; set; }
        public double        TotalFlowLs   { get; set; }
        public int           WarningCount  { get; set; }
        public string        Summary       { get; set; } = "";
        public List<string>  Notes         { get; set; } = [];
    }

    // ── Standart Çap Serisi (mm) ──────────────────────────────────────────────
    private static readonly double[] StandardDiameters = [50, 75, 90, 110, 125, 160, 200, 250, 315];

    // ── Ana Hesap Metodu ──────────────────────────────────────────────────────
    public CalcSheetResult Calculate(CadDatabase database, CalcOptions opts)
    {
        var result = new CalcSheetResult { Options = opts };

        var catchments = database.GetAllEntities()
            .OfType<RainfallCatchmentEntity>()
            .ToList();

        // Çizimde tanımlı alan yoksa manuel alan listesi oluştur
        if (catchments.Count == 0)
        {
            result.Notes.Add("Çizimde yağmur düşme alanı tanımlı değil. Lütfen 'Yağmur Düşme Alanı' komutu ile alan çizin.");
            result.Summary = "Veri Yok";
            return result;
        }

        int rowNo = 1;
        foreach (var ca in catchments)
        {
            double q = opts.RainfallIntensity * ca.RunoffCoefficient * ca.AreaM2;
            var (dn, v, fill, cap) = SizePipe(q, opts);

            var row = new CalcRow
            {
                RowNo        = rowNo++,
                AreaName     = ca.AreaName,
                SurfaceType  = ca.Surface.ToString(),
                AreaM2       = Math.Round(ca.AreaM2, 1),
                RunoffC      = ca.RunoffCoefficient,
                RainfallR    = opts.RainfallIntensity,
                DesignFlowLs = Math.Round(q, 3),
                DiameterMm   = dn,
                SlopePct     = opts.DefaultSlopePct,
                VelocityMs   = Math.Round(v, 2),
                FillRatio    = Math.Round(fill, 2),
                CapacityLs   = Math.Round(cap, 3),
                IsOk         = fill <= opts.MaxFillRatio && v >= 0.6,
            };

            var warns = new List<string>();
            if (fill > opts.MaxFillRatio)
                warns.Add($"Doluluk {fill:P0} > {opts.MaxFillRatio:P0}");
            if (v < 0.6)
                warns.Add($"Hız {v:F2} m/s < 0.6 (kendinden temizlenme)");
            row.Warnings = string.Join("; ", warns);
            result.Rows.Add(row);
        }

        result.TotalAreas   = catchments.Count;
        result.TotalAreaM2  = Math.Round(catchments.Sum(c => c.AreaM2), 1);
        result.TotalFlowLs  = Math.Round(result.Rows.Sum(r => r.DesignFlowLs), 3);
        result.WarningCount = result.Rows.Count(r => !r.IsOk);

        result.Summary = $"Toplam {result.TotalAreas} alan, {result.TotalAreaM2} m², " +
                         $"Q_toplam = {result.TotalFlowLs:F3} l/s" +
                         (result.WarningCount > 0 ? $" — ⚠ {result.WarningCount} uyarı" : " — ✓ Tamam");

        result.Notes.Add($"Yağış yoğunluğu: r = {opts.RainfallIntensity} l/s·m² ({opts.Location})");
        result.Notes.Add("Standart: TS EN 12056-3 (Çatı Drenajı)");
        result.Notes.Add("Boru malzemesi: " + opts.PipeMaterial + $"  Manning n = {opts.ManningN}");

        return result;
    }

    // ── Manning ile Boru Boyutlandırma ────────────────────────────────────────
    private static (double dn, double v, double fill, double fullCap) SizePipe(
        double qLs, CalcOptions opts)
    {
        double qM3s = qLs / 1000.0;
        double slope = opts.DefaultSlopePct / 100.0;

        foreach (double dn in StandardDiameters)
        {
            double d = dn / 1000.0;              // m
            double area = Math.PI * d * d / 4.0; // m²
            double rh   = d / 4.0;               // hidroflik yarıçap (tam dolu)

            // Manning tam dolu kapasite
            double qFull = (1.0 / opts.ManningN) * area * Math.Pow(rh, 2.0/3.0) * Math.Pow(slope, 0.5);

            if (qFull <= 0) continue;

            double fill = qM3s / qFull;
            if (fill > opts.MaxFillRatio) continue;

            // Hız (tam dolu için)
            double vFull = qFull / area;
            // Kısmi doluda hız düzeltmesi ≈ 0.85 (basit yaklaşım)
            double v = vFull * Math.Pow(fill, 0.25);

            return (dn, v, fill, qFull * 1000);
        }

        // En büyük çap bile yetmiyorsa son çapı ver, uyarı flag'leniyor
        double lastDn = StandardDiameters[^1];
        double lastD  = lastDn / 1000.0;
        double lastA  = Math.PI * lastD * lastD / 4.0;
        double lastQ  = (1.0 / opts.ManningN) * lastA * Math.Pow(lastD/4, 2.0/3.0) * Math.Pow(slope, 0.5);
        return (lastDn, lastQ / lastA, qM3s / lastQ, lastQ * 1000);
    }
}
