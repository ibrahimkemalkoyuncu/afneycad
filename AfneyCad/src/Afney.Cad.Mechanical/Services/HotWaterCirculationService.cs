using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Sıcak Su Resirkülasyon Servisi (HotWaterCirculationService)
   NEDEN: OtoNET / FINE SANI'deki "Sıcak Su Sirkülasyonu" modülünün AfneyCAD karşılığı.
          Sıcak su borusunun soğumaması için resirkülasyon devresi tasarımını (debi, çap,
          ısı kaybı, vana dengeleme) hesaplar.

   STANDART: TS EN 806-3, TS EN 1107-2
   FORMÜLLER:
     - Isı kaybı: Q_loss = U * π * D_out * L * ΔT   [W]
     - U = 1 / (1/h_i + δ_boru/(λ_boru) + δ_izolasyon/λ_izolasyon + 1/h_o)  [W/m²K]
     - Resirkülasyon debisi: Q_recirc = Q_loss / (ρ * cp * ΔT_recirc)  [m³/s]
     - Devre dengeleme: basınç kaybını eşitlemek için değişken dirençli vana (balancing valve)
*/
public class HotWaterCirculationService
{
    private const double RhoWater = 998.0;    // kg/m³ (60°C yakını)
    private const double CpWater  = 4182.0;   // J/kgK
    private const double HInner   = 1500.0;   // W/m²K (iç konveksiyon — basit)
    private const double HOuter   = 10.0;     // W/m²K (dış konveksiyon — hava)
    private const double LambdaSteel = 50.0;  // W/mK çelik boru
    private const double LambdaPVC    = 0.17; // W/mK PVC
    private const double LambdaInsul  = 0.04; // W/mK (köpük izolasyon — Armaflex)

    private readonly CadDatabase _database;

    public HotWaterCirculationService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Resirkülasyon Devre Tasarımı (DesignCirculationLoop)
       NEDEN: Tüm hat segmentlerinde ısı kaybı ve gerekli resirkülasyon debisini hesaplayarak
              her kolda dengeleme vanası ayarını vermek.

       PARAMETRE:
         segments      : Hat parçaları (uzunluk, çap, izolasyon kalınlığı)
         supplyTempC   : Besleme sıcaklığı (°C) — genellikle 60°C
         returnTempC   : Dönüş sıcaklığı (°C)   — genellikle 55°C
         ambientTempC  : Ortam sıcaklığı (°C)   — genellikle 20°C
    */
    public CirculationLoopResult DesignCirculationLoop(
        List<CirculationSegment> segments,
        double supplyTempC  = 60.0,
        double returnTempC  = 55.0,
        double ambientTempC = 20.0)
    {
        double deltaT       = (supplyTempC + returnTempC) / 2.0 - ambientTempC;
        double deltaTRecirc = supplyTempC - returnTempC;

        var result = new CirculationLoopResult
        {
            SupplyTempC  = supplyTempC,
            ReturnTempC  = returnTempC,
            AmbientTempC = ambientTempC
        };

        foreach (var seg in segments)
        {
            double uValue   = CalculateUValue(seg.PipeDiameterMm, seg.WallThicknessMm, seg.InsulationMm, seg.Material);
            double dOut     = (seg.PipeDiameterMm + 2 * seg.WallThicknessMm) / 1000.0; // m
            double qLossW   = uValue * Math.PI * dOut * seg.LengthM * deltaT;

            double qRecircM3s = deltaTRecirc > 0
                ? qLossW / (RhoWater * CpWater * deltaTRecirc)
                : 0;
            double qRecircLh  = qRecircM3s * 1000 * 3600; // lt/h

            // Resirkülasyon debisi → boru çap kontrolü (v < 0.5 m/s önerilir)
            double dReqMm = qRecircM3s > 0
                ? Math.Sqrt(4 * qRecircM3s / (Math.PI * 0.5)) * 1000
                : 0;
            double dn = GetStandardReturnDN(dReqMm);

            // Darcy-Weisbach basınç kaybı (basit Hazen-Williams yaklaşımı)
            double velocityMs = qRecircM3s > 0 && seg.PipeDiameterMm > 0
                ? qRecircM3s / (Math.PI * Math.Pow(seg.PipeDiameterMm / 1000.0 / 2, 2))
                : 0;
            double dpPaPerM = velocityMs > 0
                ? 0.5 * RhoWater * velocityMs * velocityMs * 0.02 / (seg.PipeDiameterMm / 1000.0)
                : 0;
            double dpTotal = dpPaPerM * seg.LengthM;

            result.Segments.Add(new CirculationSegmentResult
            {
                SegmentId         = seg.Id,
                Description       = seg.Description,
                LengthM           = seg.LengthM,
                PipeDiameterMm    = seg.PipeDiameterMm,
                InsulationMm      = seg.InsulationMm,
                HeatLossW         = qLossW,
                RecircFlowLh      = qRecircLh,
                ReturnPipeDN      = dn,
                PressureDropPa    = dpTotal,
                FlowVelocityMs    = velocityMs,
                IsVelocityOK      = velocityMs <= 0.5
            });

            result.TotalHeatLossW    += qLossW;
            result.TotalRecircFlowLh += qRecircLh;
        }

        // Devrenin kritik hattı (en yüksek basınç kayıplı)
        var critical = result.Segments.OrderByDescending(s => s.PressureDropPa).FirstOrDefault();
        if (critical != null)
            result.CriticalPathPressurePa = critical.PressureDropPa;

        // Resirkülasyon pompası seçimi
        result.RecommendedPumpFlow = result.TotalRecircFlowLh;
        result.RecommendedPumpHeadMSS = result.CriticalPathPressurePa / (RhoWater * 9.80665);

        // Balancing vana ayarları — her segment için kritik hattın % oranına göre
        foreach (var seg in result.Segments)
        {
            double targetPa   = result.CriticalPathPressurePa;
            double excessPa   = targetPa - seg.PressureDropPa;
            seg.ValveSettingPct = excessPa > 0 ? Math.Min(100, excessPa / targetPa * 100) : 0;
        }

        return result;
    }

    /*
       NE: Gerçek Database'den Boru Hatlarını Al ve Hesapla
       NEDEN: AutoRoute edilmiş sistem üzerinden otomatik resirkülasyon tasarımı.
    */
    public CirculationLoopResult DesignFromDatabase(
        double supplyTempC = 60.0, double returnTempC = 55.0, double ambientTempC = 20.0)
    {
        var hotPipes = _database.GetAllEntities()
            .OfType<PipeEntity>()
            .Where(p => p.SystemType == Afney.Cad.Mechanical.Enums.MechanicalSystemType.DomesticHotWater)
            .ToList();

        var segments = hotPipes.Select((pipe, i) => new CirculationSegment
        {
            Id              = pipe.Id.ToString(),
            Description     = $"Sıcak Su Hattı #{i + 1}",
            LengthM         = pipe.Length,
            PipeDiameterMm  = pipe.InnerDiameter,
            WallThicknessMm = 3.0,   // Varsayılan PPR/çelik et kalınlığı
            InsulationMm    = 25.0,  // Varsayılan izolasyon
            Material        = "Steel"
        }).ToList();

        return segments.Count > 0
            ? DesignCirculationLoop(segments, supplyTempC, returnTempC, ambientTempC)
            : new CirculationLoopResult { SupplyTempC = supplyTempC, ReturnTempC = returnTempC };
    }

    /*
       NE: HTML Raporu Oluştur
       NEDEN: Mühendis raporuna eklenecek resirkülasyon hesap föyü.
    */
    public string ExportToHtml(CirculationLoopResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<title>Sıcak Su Resirkülasyon Raporu — AfneyCAD</title>");
        sb.AppendLine("<style>body{font-family:Consolas,monospace;background:#1a1a2e;color:#eee;padding:20px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:16px}");
        sb.AppendLine("th{background:#005A9C;color:white;padding:6px 10px;text-align:left}");
        sb.AppendLine("td{padding:5px 10px;border-bottom:1px solid #444}");
        sb.AppendLine("tr:nth-child(even){background:#252540}.warn{color:#FFB347}.ok{color:#90EE90}</style></head><body>");
        sb.AppendLine("<h2>SICAK SU RESİRKÜLASYON RAPORU</h2>");
        sb.AppendLine($"<p>Besleme: {result.SupplyTempC}°C | Dönüş: {result.ReturnTempC}°C | Ortam: {result.AmbientTempC}°C</p>");
        sb.AppendLine("<table><tr><th>#</th><th>Hat</th><th>L (m)</th><th>DN (mm)</th><th>İzolasyon (mm)</th>");
        sb.AppendLine("<th>Isı Kaybı (W)</th><th>Resirkülasyon (lt/h)</th><th>Dönüş DN</th><th>ΔP (Pa)</th><th>Vana (%)</th></tr>");
        int row = 1;
        foreach (var s in result.Segments)
        {
            string velClass = s.IsVelocityOK ? "ok" : "warn";
            sb.AppendLine($"<tr><td>{row++}</td><td>{s.Description}</td><td>{s.LengthM:F1}</td>");
            sb.AppendLine($"<td>{s.PipeDiameterMm:F0}</td><td>{s.InsulationMm:F0}</td>");
            sb.AppendLine($"<td>{s.HeatLossW:F0}</td><td>{s.RecircFlowLh:F2}</td><td>DN {s.ReturnPipeDN:F0}</td>");
            sb.AppendLine($"<td class='{velClass}'>{s.PressureDropPa:F0}</td><td>{s.ValveSettingPct:F0}</td></tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine($"<p><b>Toplam Isı Kaybı:</b> {result.TotalHeatLossW:F0} W</p>");
        sb.AppendLine($"<p><b>Toplam Resirkülasyon Debisi:</b> {result.TotalRecircFlowLh:F1} lt/h</p>");
        sb.AppendLine($"<p><b>Pompa Önerisi:</b> Q = {result.RecommendedPumpFlow:F1} lt/h, Hm = {result.RecommendedPumpHeadMSS:F2} mSS</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // ── YARDIMCI METODLAR ────────────────────────────────────────────────────────

    private static double CalculateUValue(double diMm, double wallMm, double insulMm, string material)
    {
        double di = diMm / 1000.0;
        double wallM = wallMm / 1000.0;
        double insulM = insulMm / 1000.0;

        double lambda = material.Contains("PVC", StringComparison.OrdinalIgnoreCase)
            ? LambdaPVC : LambdaSteel;

        double r1 = di / 2;
        double r2 = r1 + wallM;
        double r3 = r2 + insulM;

        double rInner  = 1.0 / (HInner * 2 * Math.PI * r1);
        double rPipe   = Math.Log(r2 / r1) / (2 * Math.PI * lambda);
        double rInsul  = Math.Log(r3 / r2) / (2 * Math.PI * LambdaInsul);
        double rOuter  = 1.0 / (HOuter * 2 * Math.PI * r3);

        double rTotal  = rInner + rPipe + rInsul + rOuter;
        return rTotal > 0 ? 1.0 / rTotal : 0;
    }

    private static double GetStandardReturnDN(double requiredMm)
    {
        double[] standards = { 15, 20, 25, 32, 40, 50, 65, 80 };
        foreach (var dn in standards)
            if (dn >= requiredMm) return dn;
        return 80;
    }
}

// ── VERİ MODELLERİ ──────────────────────────────────────────────────────────────

public class CirculationSegment
{
    public string Id          { get; set; } = "";
    public string Description { get; set; } = "";
    public double LengthM         { get; set; }
    public double PipeDiameterMm  { get; set; }  // İç çap mm
    public double WallThicknessMm { get; set; } = 3.0;
    public double InsulationMm    { get; set; } = 25.0;
    public string Material        { get; set; } = "Steel";
}

public class CirculationSegmentResult
{
    public string SegmentId       { get; set; } = "";
    public string Description     { get; set; } = "";
    public double LengthM         { get; set; }
    public double PipeDiameterMm  { get; set; }
    public double InsulationMm    { get; set; }
    public double HeatLossW       { get; set; }
    public double RecircFlowLh    { get; set; }
    public double ReturnPipeDN    { get; set; }
    public double PressureDropPa  { get; set; }
    public double FlowVelocityMs  { get; set; }
    public bool   IsVelocityOK    { get; set; }
    public double ValveSettingPct { get; set; }  // Dengeleme vanası kısma yüzdesi
}

public class CirculationLoopResult
{
    public double SupplyTempC  { get; set; }
    public double ReturnTempC  { get; set; }
    public double AmbientTempC { get; set; }
    public double TotalHeatLossW    { get; set; }
    public double TotalRecircFlowLh { get; set; }
    public double CriticalPathPressurePa { get; set; }
    public double RecommendedPumpFlow    { get; set; }  // lt/h
    public double RecommendedPumpHeadMSS { get; set; }
    public List<CirculationSegmentResult> Segments { get; set; } = [];
}
