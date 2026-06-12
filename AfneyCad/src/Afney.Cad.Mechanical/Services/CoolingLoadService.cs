using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Soğutma Yük Hesap Servisi (CoolingLoadService)
   NEDEN: ASHRAE / TS EN 12831-3 kapsamında bina soğutma yükü, klima/VRF kapasitesi
          ve bölge bazlı soğutma tasarımını yapmak için.

   HESAP ZİNCİRİ:
   1. Her bölge için iletim kazancı = Σ(U × A × CLTD)
   2. Güneş kazancı = SHGC × A_pencere × I_solar × yön katsayısı
   3. İç yükler = kişi (duyulur+gizil) + aydınlatma + ekipman
   4. Havalandırma soğutma yükü (duyulur+gizil)
   5. Toplam duyulur + gizil yük → soğutma kapasitesi (kW / TR)
*/
public class CoolingLoadService
{
    // ── Yön Tanımı ────────────────────────────────────────────────────────────────

    public enum Orientation { Kuzey, KuzeyDogu, Dogu, GuneyDogu, Guney, GuneyBati, Bati, KuzeyBati, Cati }

    // ── Bölge Tanımı ─────────────────────────────────────────────────────────────

    public class Zone
    {
        public string Name             { get; set; } = "";
        public string ZoneType         { get; set; } = "Ofis";
        public double FloorAreaM2      { get; set; }
        public double HeightM          { get; set; } = 3.0;
        public double ExternalWallM2   { get; set; }
        public double WindowM2         { get; set; }
        public double RoofM2           { get; set; }
        public Orientation WallFacing  { get; set; } = Orientation.Guney;

        // Zarf U-değerleri (W/m²K)
        public double UWall            { get; set; } = 0.6;
        public double UWindow          { get; set; } = 2.4;
        public double URoof            { get; set; } = 0.4;

        // Güneş kontrol verisi
        public double SHGC             { get; set; } = 0.6;   // Güneş ısı kazanç katsayısı
        public bool   HasShading       { get; set; } = false;

        // İç yük girdileri
        public int    OccupantCount    { get; set; } = 2;
        public string ActivityLevel    { get; set; } = "Ofis Çalışması";
        public double LightingWperm2   { get; set; } = 12.0;  // W/m²
        public double EquipmentWperm2  { get; set; } = 15.0;  // W/m²

        // Havalandırma
        public double AirChangesPerHour { get; set; } = 1.0;
    }

    // ── Bölge Hesap Sonucu ───────────────────────────────────────────────────────

    public class ZoneCoolResult
    {
        public Zone   Zone                  { get; set; } = null!;
        public double TransmissionGainW     { get; set; }
        public double SolarGainW            { get; set; }
        public double InternalSensibleW     { get; set; }
        public double InternalLatentW       { get; set; }
        public double VentilationSensibleW  { get; set; }
        public double VentilationLatentW    { get; set; }
        public double TotalSensibleW        { get; set; }
        public double TotalLatentW          { get; set; }
        public double TotalCoolingW         { get; set; }
        public double TotalCoolingKw        => TotalCoolingW / 1000.0;
        public double TotalCoolingTR        => TotalCoolingW / 3517.0;  // 1 TR = 3.517 kW
        public string RecommendedUnit       { get; set; } = "";
    }

    // ── Sistem Sonucu ────────────────────────────────────────────────────────────

    public class CoolingResult
    {
        public List<ZoneCoolResult> Zones   { get; set; } = [];
        public double TotalCoolingKw        { get; set; }
        public double TotalCoolingTR        { get; set; }
        public double ChillerCapacityKw     { get; set; }  // +%15 güvenlik
        public string RecommendedChiller    { get; set; } = "";
        public double TotalSensibleKw       { get; set; }
        public double TotalLatentKw         { get; set; }
        public double SensibleHeatRatio     { get; set; }  // SHR
        public int    WarningCount          { get; set; }
        public List<string> Warnings        { get; set; } = [];
        public string Summary               { get; set; } = "";
    }

    // ── Parametreler ──────────────────────────────────────────────────────────────

    public double OutdoorSummerTempC   { get; set; } = 34.0;   // Dış yaz tasarım sıcaklığı
    public double OutdoorWetBulbC      { get; set; } = 24.0;   // Yaş termometre
    public double IndoorTempC          { get; set; } = 24.0;   // İç tasarım sıcaklığı
    public double IndoorRH             { get; set; } = 50.0;   // % bağıl nem
    public double SafetyFactor         { get; set; } = 1.15;   // %15 güvenlik
    public int    PeakHour             { get; set; } = 15;     // Pik saat (15:00)

    // ── Ana Hesap ─────────────────────────────────────────────────────────────────

    public CoolingResult Calculate(List<Zone> zones)
    {
        var result = new CoolingResult();
        double totalSensibleW = 0;
        double totalLatentW   = 0;

        double ΔT = OutdoorSummerTempC - IndoorTempC;  // duyulur ΔT

        foreach (var zone in zones)
        {
            // 1. İletim kazancı (CLTD kaba yaklaşım: ΔT + 4°C güneş radyasyonu etkisi)
            double cltdWall = ΔT + (zone.WallFacing is Orientation.Guney or Orientation.GuneyBati or Orientation.GuneyDogu ? 5 : 3);
            double cltdRoof = ΔT + 10;  // Çatı: ek CLTD
            double qTrans =
                zone.ExternalWallM2 * zone.UWall   * cltdWall +
                zone.WindowM2       * zone.UWindow  * ΔT +
                zone.RoofM2         * zone.URoof    * cltdRoof;

            // 2. Güneş kazancı (SHGC × Alan × Solar irradiance × yön × gölge)
            double solarIntensity = SolarIntensityWpm2(zone.WallFacing, PeakHour);
            double shadingFactor  = zone.HasShading ? 0.5 : 1.0;
            double qSolar = zone.SHGC * zone.WindowM2 * solarIntensity * shadingFactor;

            // 3. İç yükler
            var (sensPerPerson, latPerPerson) = OccupantLoads(zone.ActivityLevel);
            double qIntSensible =
                zone.OccupantCount    * sensPerPerson +
                zone.FloorAreaM2      * zone.LightingWperm2    * 0.85 +  // 15% ışık enerjisi ısıya dönüşmez
                zone.FloorAreaM2      * zone.EquipmentWperm2   * 0.90;
            double qIntLatent = zone.OccupantCount * latPerPerson;

            // 4. Havalandırma yükü
            double volumeM3 = zone.FloorAreaM2 * zone.HeightM;
            double qVentSens = 0.34 * zone.AirChangesPerHour * volumeM3 * ΔT;
            double qVentLat  = VentilationLatentLoad(zone.AirChangesPerHour, volumeM3,
                                                      OutdoorWetBulbC, IndoorRH);

            // 5. Toplam
            double totalSensZone = (qTrans + qSolar + qIntSensible + qVentSens) * SafetyFactor;
            double totalLatZone  = (qIntLatent + qVentLat) * SafetyFactor;
            double totalZone     = totalSensZone + totalLatZone;

            if (totalZone < 500)
                result.Warnings.Add($"{zone.Name}: soğutma yükü çok düşük ({totalZone:F0} W) — girişleri kontrol edin.");

            var zoneResult = new ZoneCoolResult
            {
                Zone                 = zone,
                TransmissionGainW    = Math.Round(qTrans,       1),
                SolarGainW           = Math.Round(qSolar,       1),
                InternalSensibleW    = Math.Round(qIntSensible, 1),
                InternalLatentW      = Math.Round(qIntLatent,   1),
                VentilationSensibleW = Math.Round(qVentSens,    1),
                VentilationLatentW   = Math.Round(qVentLat,     1),
                TotalSensibleW       = Math.Round(totalSensZone, 1),
                TotalLatentW         = Math.Round(totalLatZone,  1),
                TotalCoolingW        = Math.Round(totalZone,     1),
                RecommendedUnit      = SelectCoolingUnit(totalZone / 1000.0)
            };

            result.Zones.Add(zoneResult);
            totalSensibleW += totalSensZone;
            totalLatentW   += totalLatZone;
        }

        double totalKw    = (totalSensibleW + totalLatentW) / 1000.0;
        double totalTR    = totalKw / 3.517;
        double chillerKw  = totalKw * SafetyFactor;

        result.TotalCoolingKw    = Math.Round(totalKw,    2);
        result.TotalCoolingTR    = Math.Round(totalTR,    2);
        result.ChillerCapacityKw = Math.Round(chillerKw,  1);
        result.TotalSensibleKw   = Math.Round(totalSensibleW / 1000.0, 2);
        result.TotalLatentKw     = Math.Round(totalLatentW   / 1000.0, 2);
        result.SensibleHeatRatio = totalKw > 0 ? Math.Round(totalSensibleW / 1000.0 / totalKw, 3) : 0;
        result.RecommendedChiller = SelectChiller(chillerKw);
        result.WarningCount      = result.Warnings.Count;

        result.Summary =
            $"Toplam soğutma yükü: {totalKw:F2} kW ({totalTR:F1} TR) | " +
            $"Chiller kapasitesi: {chillerKw:F1} kW | " +
            $"SHR: {result.SensibleHeatRatio:F2} | " +
            $"Bölge sayısı: {zones.Count}";

        return result;
    }

    // ── Güneş Yoğunluğu (W/m²) — Türkiye yaz pik değerleri ─────────────────────
    // ASHRAE HOF 2021 Tablo verilerinden Türkiye enlemi (~39°N) için

    private static double SolarIntensityWpm2(Orientation orientation, int hour)
    {
        return orientation switch
        {
            Orientation.Guney      => hour is >= 10 and <= 14 ? 620 : 380,
            Orientation.GuneyDogu  => hour is >= 8  and <= 12 ? 680 : 320,
            Orientation.GuneyBati  => hour is >= 12 and <= 16 ? 680 : 320,
            Orientation.Dogu       => hour is >= 7  and <= 11 ? 700 : 200,
            Orientation.Bati       => hour is >= 13 and <= 17 ? 700 : 200,
            Orientation.KuzeyDogu  => hour is >= 7  and <= 9  ? 450 : 150,
            Orientation.KuzeyBati  => hour is >= 16 and <= 18 ? 450 : 150,
            Orientation.Kuzey      => 150,
            Orientation.Cati       => 800,
            _                       => 400
        };
    }

    // ── Kişi Isı Yükü (W/kişi) — ASHRAE Tablo 1, 24°C iç sıcaklık ─────────────

    private static (double sensible, double latent) OccupantLoads(string activity) => activity switch
    {
        "Oturma / Dinlenme"  => (75,  35),
        "Ofis Çalışması"     => (75,  55),
        "Hafif Yürüyüş"      => (90,  90),
        "Mağaza / Alışveriş" => (75,  55),
        "Restoran"           => (80,  80),
        "Dans / Egzersiz"    => (100, 200),
        "Fabrika (Hafif)"    => (110, 185),
        "Fabrika (Ağır)"     => (130, 315),
        _                     => (75,  55)
    };

    // ── Havalandırma Gizil Yük (nem kazancı) ────────────────────────────────────
    // Q_lat = 0.68 × n × V × Δw   (Δw: özgül nem farkı g/kg)

    private static double VentilationLatentLoad(double ach, double volumeM3,
                                                  double outdoorWB, double indoorRH)
    {
        // Kaba nem modeli: yaş termometre → özgül nem yaklaşımı
        double wOutdoor = Math.Max(0, (outdoorWB - 14.0) * 1.1);   // g/kg yaklaşım
        double wIndoor  = indoorRH / 100.0 * 11.0;                  // ~24°C için doyma ~18 g/kg
        double Δw       = Math.Max(0, wOutdoor - wIndoor);
        return 0.68 * ach * volumeM3 * Δw;
    }

    // ── Soğutma Ünitesi Seçimi ───────────────────────────────────────────────────

    private static string SelectCoolingUnit(double kw)
    {
        if (kw <= 2.5)  return "Monosplit 9.000 BTU (2.6 kW)";
        if (kw <= 3.5)  return "Monosplit 12.000 BTU (3.5 kW)";
        if (kw <= 5.0)  return "Monosplit 18.000 BTU (5.3 kW)";
        if (kw <= 7.0)  return "Monosplit 24.000 BTU (7.0 kW)";
        if (kw <= 10.5) return "Monosplit 36.000 BTU (10.5 kW)";
        if (kw <= 14.0) return "Kaset Tipi 48.000 BTU (14.0 kW)";
        if (kw <= 20.0) return "VRF İç Ünite 20 kW";
        if (kw <= 30.0) return "VRF/Chiller ünite (28–32 kW)";
        return $"Merkezi Chiller / VRF dış ünite ({kw:F1} kW)";
    }

    // ── Chiller Seçimi ───────────────────────────────────────────────────────────

    private static string SelectChiller(double kw)
    {
        double tr = kw / 3.517;
        if (kw <= 25)   return $"VRF Dış Ünite ~{kw:F0} kW ({tr:F1} TR)";
        if (kw <= 50)   return $"Hava Soğutmalı Chiller ~{kw:F0} kW ({tr:F1} TR)";
        if (kw <= 100)  return $"Hava Soğutmalı Chiller ~{kw:F0} kW ({tr:F1} TR)";
        if (kw <= 250)  return $"Su Soğutmalı Chiller ~{kw:F0} kW ({tr:F1} TR)";
        if (kw <= 500)  return $"Santrifüj Chiller ~{kw:F0} kW ({tr:F1} TR)";
        return $"Büyük ölçekli Santrifüj Chiller ~{(int)Math.Ceiling(kw / 50) * 50} kW ({tr:F1} TR)";
    }

    // ── Şehir Yaz Tasarım Sıcaklıkları (TS EN 12831-3 / ASHRAE HOF) ─────────────

    public static readonly Dictionary<string, (double DB, double WB)> CitySummerTemps = new()
    {
        ["İstanbul"]     = (32, 24), ["Ankara"]       = (34, 20), ["İzmir"]        = (37, 24),
        ["Bursa"]        = (33, 23), ["Antalya"]      = (36, 25), ["Adana"]        = (37, 26),
        ["Konya"]        = (33, 19), ["Kayseri"]      = (31, 18), ["Trabzon"]      = (29, 24),
        ["Samsun"]       = (30, 25), ["Erzurum"]      = (27, 16), ["Diyarbakır"]   = (40, 22),
        ["Eskişehir"]    = (33, 20), ["Gaziantep"]    = (38, 22), ["Mersin"]       = (38, 27),
        ["Kocaeli"]      = (32, 24), ["Denizli"]      = (36, 22), ["Malatya"]      = (35, 21),
    };

    // ── Bölge Tipi Varsayılanları ────────────────────────────────────────────────

    public static readonly Dictionary<string, (double Lighting, double Equipment, double ACH, string Activity)> ZoneTypeDefaults = new()
    {
        ["Ofis"]            = (12, 20, 1.0, "Ofis Çalışması"),
        ["Toplantı Salonu"] = (15, 10, 2.0, "Ofis Çalışması"),
        ["Oturma Odası"]    = (8,  5,  0.5, "Oturma / Dinlenme"),
        ["Yatak Odası"]     = (6,  3,  0.5, "Oturma / Dinlenme"),
        ["Restoran"]        = (20, 30, 3.0, "Restoran"),
        ["Mağaza"]          = (30, 10, 2.0, "Mağaza / Alışveriş"),
        ["Hastane Odası"]   = (10, 20, 2.0, "Ofis Çalışması"),
        ["Otel Odası"]      = (8,  8,  0.5, "Oturma / Dinlenme"),
        ["Spor Salonu"]     = (20, 5,  4.0, "Dans / Egzersiz"),
        ["Fabrika"]         = (15, 50, 3.0, "Fabrika (Hafif)"),
    };
}
