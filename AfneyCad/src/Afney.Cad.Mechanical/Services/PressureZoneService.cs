using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Basınç Bölgesi Yönetim Servisi (PressureZoneService)
   NEDEN: Yüksek binalarda (>30m) boru sistemindeki statik basınç, alt katlarda izin verilen
          maksimum işletme basıncını aşar. Basınç kırma vanaları (PRV) ile bina,
          her katın max 400-600 kPa (4-6 bar) basınca maruz kalacağı bölgelere ayrılır.

   STANDART: TS EN 806-3 §5.3, DIN 1988-300, BS 6700
   BASINÇ LİMİTLERİ:
     - Max statik: 500 kPa (5 bar)   — TS EN 806
     - Max dinamik: 600 kPa (6 bar)  — işletme
     - PRV çıkış: 300 kPa (3 bar)    — önerilen set noktası

   HESAP:
     P_statik = ρ × g × h   (kPa)     h = yükseklik (m), ρ=1000 kg/m³
     Bölge sınırı = her 30m'de bir PRV gerekir (P_statik > 500 kPa)
*/
public class PressureZoneService
{
    public record ZoneDesignInput(
        double TotalBuildingHeightM,
        double GroundSupplyPressureKPa,
        int    NumberOfFloors,
        double FloorHeightM = 3.0,
        double MaxZonePressureKPa = 500.0,
        double PrvSetPointKPa = 300.0);

    public class PressureZone
    {
        public int    ZoneNumber          { get; set; }
        public int    StartFloor          { get; set; }
        public int    EndFloor            { get; set; }
        public double ZoneBottomHeightM   { get; set; }  // zemin kotundan
        public double ZoneTopHeightM      { get; set; }
        public double StaticPressureBottomKPa { get; set; }
        public double StaticPressureTopKPa    { get; set; }
        public bool   RequiresPRV         { get; set; }
        public double PrvInputPressureKPa { get; set; }
        public double PrvOutputPressureKPa { get; set; }
        public double ZoneHeightM         => ZoneTopHeightM - ZoneBottomHeightM;
        public int    FloorCount          => EndFloor - StartFloor + 1;
    }

    public class PressureZoneDesignResult
    {
        public List<PressureZone> Zones              { get; set; } = [];
        public int    TotalZones                     { get; set; }
        public int    PrvCount                       { get; set; }
        public double MaxStaticPressureKPa           { get; set; }
        public bool   BoosterPumpRequired            { get; set; }  // Üst zonlar için
        public double BoosterPumpHeadMSS             { get; set; }
        public List<string> Notes                    { get; set; } = [];
        public string Standard                       { get; set; } = "";
    }

    /*
       NE: Basınç Bölgelerini Hesapla
       NEDEN: Binanın yüksekliğine ve şebeke basıncına göre kaç PRV bölgesi gerektiğini belirlemek.
    */
    public PressureZoneDesignResult Design(ZoneDesignInput input)
    {
        var result = new PressureZoneDesignResult
        {
            Standard = "TS EN 806-3 / DIN 1988-300"
        };

        const double G     = 9.80665;
        const double Rho   = 1000.0;
        double maxPressKPa = input.MaxZonePressureKPa;

        // Zemin şebeke basıncı yeterli değilse üst katlar için güçlendirme gerekir
        double maxHeightFromSupply = (input.GroundSupplyPressureKPa / (Rho * G)) * 1000;
        result.BoosterPumpRequired = maxHeightFromSupply < input.TotalBuildingHeightM;
        if (result.BoosterPumpRequired)
        {
            double deficit = input.TotalBuildingHeightM - maxHeightFromSupply;
            result.BoosterPumpHeadMSS = Math.Ceiling(deficit) + 10; // +10 mSS güvenlik
            result.Notes.Add($"⚠ Şebeke basıncı ({input.GroundSupplyPressureKPa} kPa) tüm binayı besleyemiyor. " +
                             $"Güçlendirme pompası gerekli: Hm ≥ {result.BoosterPumpHeadMSS:F0} mSS.");
        }

        // Kaç bölge gerektiğini hesapla
        double maxZoneHeightM = (maxPressKPa / (Rho * G / 1000.0));
        int totalFloors = input.NumberOfFloors;
        double totalHeightM = input.TotalBuildingHeightM;

        int zoneNum = 1;
        int currentFloor = 1;
        double currentHeight = 0;
        double prevPressureKPa = input.GroundSupplyPressureKPa;

        while (currentFloor <= totalFloors)
        {
            double zoneTopHeight  = Math.Min(currentHeight + maxZoneHeightM, totalHeightM);
            int    zoneEndFloor   = Math.Min(
                (int)Math.Floor(zoneTopHeight / input.FloorHeightM),
                totalFloors);

            double staticBottom   = Rho * G * currentHeight / 1000.0; // kPa
            double staticTop      = Rho * G * zoneTopHeight  / 1000.0; // kPa
            double pressureAtBottom = prevPressureKPa - staticBottom;

            var zone = new PressureZone
            {
                ZoneNumber              = zoneNum,
                StartFloor              = currentFloor,
                EndFloor                = zoneEndFloor > 0 ? zoneEndFloor : totalFloors,
                ZoneBottomHeightM       = currentHeight,
                ZoneTopHeightM          = zoneTopHeight,
                StaticPressureBottomKPa = Math.Max(0, pressureAtBottom),
                StaticPressureTopKPa    = Math.Max(0, pressureAtBottom - (Rho * G * (zoneTopHeight - currentHeight) / 1000.0)),
                RequiresPRV             = zoneNum > 1,
                PrvInputPressureKPa     = zoneNum > 1 ? prevPressureKPa : 0,
                PrvOutputPressureKPa    = zoneNum > 1 ? input.PrvSetPointKPa : 0
            };

            result.Zones.Add(zone);
            zoneNum++;
            currentFloor = zone.EndFloor + 1;
            currentHeight = zoneTopHeight;
            prevPressureKPa = input.PrvSetPointKPa;

            if (currentFloor > totalFloors) break;
        }

        result.TotalZones = result.Zones.Count;
        result.PrvCount   = result.Zones.Count(z => z.RequiresPRV);
        result.MaxStaticPressureKPa = result.Zones.Max(z => z.StaticPressureBottomKPa);

        if (result.MaxStaticPressureKPa > 600)
            result.Notes.Add($"⚠ Maks. statik basınç ({result.MaxStaticPressureKPa:F0} kPa) > 600 kPa — ek PRV bölgesi düşünün.");

        result.Notes.Add($"Toplam {result.TotalZones} basınç bölgesi, {result.PrvCount} adet PRV vanası gerekli.");
        result.Notes.Add($"PRV set noktası: {input.PrvSetPointKPa} kPa ({input.PrvSetPointKPa / 100:F1} bar)");

        return result;
    }

    /*
       NE: Bölge Özet Raporu (HTML)
       NEDEN: Mühendislik raporuna eklenecek basınç bölgesi şeması.
    */
    public string ExportToHtml(PressureZoneDesignResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<title>Basınç Bölgesi Raporu — AfneyCAD</title>");
        sb.AppendLine("<style>body{font-family:Consolas,monospace;background:#1a1a2e;color:#eee;padding:20px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:16px}");
        sb.AppendLine("th{background:#005A9C;color:white;padding:6px 10px;text-align:left}");
        sb.AppendLine("td{padding:5px 10px;border-bottom:1px solid #444}");
        sb.AppendLine("tr:nth-child(even){background:#252540}.prv{color:#FFB347}</style></head><body>");
        sb.AppendLine("<h2>BASINÇ BÖLGESİ TASARIM RAPORU</h2>");
        sb.AppendLine($"<p>Standart: {result.Standard}</p>");
        sb.AppendLine("<table><tr><th>Bölge</th><th>Katlar</th><th>Yükseklik (m)</th>");
        sb.AppendLine("<th>P_alt (kPa)</th><th>P_üst (kPa)</th><th>PRV</th><th>PRV Giriş</th><th>PRV Çıkış</th></tr>");

        foreach (var z in result.Zones)
        {
            string prv = z.RequiresPRV ? $"<span class='prv'>✓ PRV</span>" : "—";
            sb.AppendLine($"<tr><td>Bölge {z.ZoneNumber}</td>");
            sb.AppendLine($"<td>K{z.StartFloor}–K{z.EndFloor} ({z.FloorCount} kat)</td>");
            sb.AppendLine($"<td>{z.ZoneBottomHeightM:F1} – {z.ZoneTopHeightM:F1}</td>");
            sb.AppendLine($"<td>{z.StaticPressureBottomKPa:F0}</td><td>{z.StaticPressureTopKPa:F0}</td>");
            sb.AppendLine($"<td>{prv}</td>");
            sb.AppendLine($"<td>{(z.RequiresPRV ? $"{z.PrvInputPressureKPa:F0} kPa" : "—")}</td>");
            sb.AppendLine($"<td>{(z.RequiresPRV ? $"{z.PrvOutputPressureKPa:F0} kPa" : "—")}</td></tr>");
        }

        sb.AppendLine("</table>");
        foreach (var note in result.Notes)
            sb.AppendLine($"<p>{note}</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
