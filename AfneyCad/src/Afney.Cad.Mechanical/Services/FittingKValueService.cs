using Afney.Cad.Mechanical.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

public class FittingKValueService
{
    // TS EN 806-3 / DIN 1988-300 / Crane TP 410 referans K-değerleri
    // K = zeta (yerel kayıp katsayısı), hf_local = K * v² / 2g
    private static readonly Dictionary<FittingType, KValueEntry> _kValues = new()
    {
        // Dirsekler
        [FittingType.Elbow90] = new(1.5, 0.9, "90° Dirsek (standart)"),
        [FittingType.Elbow90LongRadius] = new(0.6, 0.4, "90° Dirsek (uzun radius)"),
        [FittingType.Elbow45] = new(0.4, 0.3, "45° Dirsek"),
        [FittingType.ElbowReturn180] = new(2.2, 1.5, "180° Dönüş"),

        // Te parçaları
        [FittingType.TeeStraightRun] = new(0.5, 0.3, "Te — Düz geçiş"),
        [FittingType.TeeBranch] = new(1.8, 1.2, "Te — Branşman"),
        [FittingType.TeeConverging] = new(1.0, 0.7, "Te — Birleşim"),
        [FittingType.TeeDiverging] = new(1.5, 1.0, "Te — Ayrılma"),

        // Vanalar
        [FittingType.GateValveOpen] = new(0.2, 0.1, "Sürgülü Vana (tam açık)"),
        [FittingType.GateValveHalfOpen] = new(5.6, 4.0, "Sürgülü Vana (yarım açık)"),
        [FittingType.GlobeValveOpen] = new(10.0, 6.0, "Globvana (tam açık)"),
        [FittingType.BallValveOpen] = new(0.05, 0.03, "Küresel Vana (tam açık)"),
        [FittingType.ButterflyValveOpen] = new(0.3, 0.2, "Kelebek Vana (tam açık)"),
        [FittingType.CheckValveSwing] = new(2.5, 1.5, "Çekvalf (salınım)"),
        [FittingType.CheckValveSpring] = new(5.0, 3.0, "Çekvalf (yaylı)"),

        // Redüksiyonlar
        [FittingType.ReducerContraction] = new(0.5, 0.3, "Daralma"),
        [FittingType.ReducerExpansion] = new(1.0, 0.6, "Genişleme"),

        // Giriş/Çıkış
        [FittingType.EntrySharpEdge] = new(0.5, 0.5, "Keskin kenarlı giriş"),
        [FittingType.EntryBellmouth] = new(0.05, 0.05, "Huni ağızlı giriş"),
        [FittingType.Exit] = new(1.0, 1.0, "Çıkış"),

        // Armatürler (Cihaz bağlantı kaybı)
        [FittingType.WCConnection] = new(3.0, 2.0, "Klozet bağlantısı"),
        [FittingType.LavatoryConnection] = new(2.0, 1.5, "Lavabo bağlantısı"),
        [FittingType.ShowerConnection] = new(2.5, 1.8, "Duş bağlantısı"),
        [FittingType.BathtubConnection] = new(3.0, 2.0, "Küvet bağlantısı"),

        // Sayaç / Filtre
        [FittingType.WaterMeter] = new(7.0, 5.0, "Su sayacı"),
        [FittingType.YStrainer] = new(3.0, 2.0, "Y filtre"),
    };

    public static double GetKValue(FittingType type, double diameterMm)
    {
        if (!_kValues.TryGetValue(type, out var entry)) return 1.0;
        // DN≤50 için büyük K, DN>50 için küçük K (lineer interpolasyon)
        if (diameterMm <= 25) return entry.KSmallDN;
        if (diameterMm >= 100) return entry.KLargeDN;
        double t = (diameterMm - 25.0) / 75.0;
        return entry.KSmallDN + t * (entry.KLargeDN - entry.KSmallDN);
    }

    public static double CalculateLocalLoss(FittingType type, double diameterMm, double velocityMs)
    {
        double k = GetKValue(type, diameterMm);
        return k * Math.Pow(velocityMs, 2) / (2.0 * 9.81); // mSS
    }

    public static double CalculateTotalLocalLoss(IEnumerable<FittingType> fittings, double diameterMm, double velocityMs)
    {
        return fittings.Sum(f => CalculateLocalLoss(f, diameterMm, velocityMs));
    }

    public static IReadOnlyDictionary<FittingType, KValueEntry> GetAllEntries() => _kValues;

    public record KValueEntry(double KSmallDN, double KLargeDN, string Description);
}

public enum FittingType
{
    Elbow90,
    Elbow90LongRadius,
    Elbow45,
    ElbowReturn180,
    TeeStraightRun,
    TeeBranch,
    TeeConverging,
    TeeDiverging,
    GateValveOpen,
    GateValveHalfOpen,
    GlobeValveOpen,
    BallValveOpen,
    ButterflyValveOpen,
    CheckValveSwing,
    CheckValveSpring,
    ReducerContraction,
    ReducerExpansion,
    EntrySharpEdge,
    EntryBellmouth,
    Exit,
    WCConnection,
    LavatoryConnection,
    ShowerConnection,
    BathtubConnection,
    WaterMeter,
    YStrainer
}
