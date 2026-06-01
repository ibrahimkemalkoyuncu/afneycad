using System;
using System.Collections.Generic;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Boru İçi Gürültü Tahmin Servisi (PipeNoiseService)
   NEDEN: TS EN 14366 / DIN 4109 kapsamında boru hattındaki akış sesini (dB)
          tahmin edip yüksek gürültülü segmentleri tespit etmek için.

   MODEL (EN 14366 Ek D — Basitleştirilmiş):
   L_w = K_base + 10·log(Q²/D) + ΔL_velocity + ΔL_fitting

   - K_base ≈ 5 dB (plastik) / 10 dB (çelik) — boru malzeme sabiti
   - Q: debi (m³/s), D: iç çap (m)
   - ΔL_velocity: hız < 1 m/s → −5 dB, > 2 m/s → +8 dB
   - ΔL_fitting: T-parça/vana +3 dB, dirsek +1 dB

   SINIFLAR (DIN 4109):
   Sınıf I: ≤ 30 dB (çok sessiz — hastane, studio)
   Sınıf II: ≤ 35 dB (sessiz — konut)
   Sınıf III: ≤ 40 dB (normal — ofis, ticari)
   > 40 dB: ⚠ Önlem gerekli
*/
public class PipeNoiseService
{
    private readonly CadDatabase _database;
    public double MaterialFactor { get; set; } = 5.0;  // Plastik=5, Çelik=10

    public PipeNoiseService(CadDatabase database) { _database = database; }

    public class NoiseResult
    {
        public Guid   PipeId         { get; set; }
        public string PipeInfo       { get; set; } = "";
        public double VelocityMs     { get; set; }
        public double FlowM3s        { get; set; }
        public double NoiseLevelDb   { get; set; }
        public string NoiseClass     { get; set; } = "";
        public bool   HasWarning     { get; set; }
        public string Note           { get; set; } = "";
    }

    public class NoiseAnalysisResult
    {
        public List<NoiseResult> Pipes   { get; set; } = [];
        public double MaxNoiseDb         { get; set; }
        public int    WarningCount       { get; set; }
        public string Summary            { get; set; } = "";
    }

    public NoiseAnalysisResult Analyze()
    {
        var result = new NoiseAnalysisResult();
        var pipes  = _database.GetAllEntities().OfType<PipeEntity>();

        foreach (var pipe in pipes)
        {
            if (pipe.InnerDiameter <= 0 || pipe.FlowRate <= 0) continue;

            double D   = pipe.InnerDiameter / 1000.0;   // m
            double Q   = pipe.FlowRate / 3600.0;         // m³/s
            double v   = Q / (Math.PI * D * D / 4.0);   // m/s

            if (Q <= 0 || D <= 0) continue;

            // Temel gürültü seviyesi
            double Lw = MaterialFactor + 10 * Math.Log10(Q * Q / D);

            // Hız düzeltmesi
            double dLv = v < 1.0 ? -5.0 : (v > 2.0 ? +8.0 : (v - 1.0) * 8.0);
            Lw += dLv;

            // Fitting katkısı (tahmin)
            Lw += 1.5; // ortalama fitting payı

            Lw = Math.Max(Lw, 10); // minimum fiziksel sınır

            string noiseClass = Lw <= 30 ? "Sınıf I (≤30 dB)" :
                                Lw <= 35 ? "Sınıf II (≤35 dB)" :
                                Lw <= 40 ? "Sınıf III (≤40 dB)" : "⚠ > 40 dB";

            bool warn = Lw > 35;
            string note = warn
                ? (v > 2.0 ? "Hız azaltılmalı (DN artır)" : "Ses yalıtımı veya esnek bağlantı gerekli")
                : "";

            result.Pipes.Add(new NoiseResult
            {
                PipeId       = pipe.Id,
                PipeInfo     = $"DN{pipe.InnerDiameter:F0} — {pipe.SystemType}",
                VelocityMs   = Math.Round(v, 2),
                FlowM3s      = Math.Round(Q, 5),
                NoiseLevelDb = Math.Round(Lw, 1),
                NoiseClass   = noiseClass,
                HasWarning   = warn,
                Note         = note
            });
        }

        if (result.Pipes.Count > 0)
        {
            result.MaxNoiseDb  = result.Pipes.Max(p => p.NoiseLevelDb);
            result.WarningCount = result.Pipes.Count(p => p.HasWarning);
            result.Summary     = $"Analiz edilen boru: {result.Pipes.Count} | Maks. gürültü: {result.MaxNoiseDb:F1} dB | Uyarı: {result.WarningCount} segment";
        }
        else
        {
            result.Summary = "Hesaplı debi/çap verisi olan boru bulunamadı — önce hidrolik hesap yapın.";
        }

        return result;
    }
}
