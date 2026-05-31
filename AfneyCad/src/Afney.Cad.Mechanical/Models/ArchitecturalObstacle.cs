using System;
using System.Collections.Generic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Models;

/*
    NE: Mimari Engel (ArchitecturalObstacle)
    NEDEN: Mekanik tesisat borularının geçemeyeceği veya vitrifiyelerin yerleşemeyeceği alanları tanımlamak için.

    BIM-LITE (Session #29): U-değeri, malzeme katmanları, yangın sınıfı, ses yalıtımı eklendi.
    ISO 6946 / TS EN 12207 / TS EN 13501-1 referans alınmıştır.
*/
public enum ObstacleType
{
    Wall,
    Door,
    Window,
    Column,
    Furniture,
    Slab,       // Döşeme
    Roof        // Çatı
}

// ── Malzeme Katmanı (ISO 6946) ────────────────────────────────────────────────
public class BimMaterialLayer
{
    public string MaterialName    { get; set; } = "";
    public double ThicknessMm     { get; set; }       // mm
    public double ThermalConductivity { get; set; }   // λ (W/mK)
    public double ThermalResistance   => ThicknessMm > 0 && ThermalConductivity > 0
        ? (ThicknessMm / 1000.0) / ThermalConductivity : 0; // R = d/λ (m²K/W)
}

// ── Yangın Sınıfı (TS EN 13501-1) ────────────────────────────────────────────
public enum FireRatingClass { A1, A2, B, C, D, E, F, NotRated }

public class ArchitecturalObstacle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceEntityId { get; set; }
    public ObstacleType Type { get; set; }
    public List<Vector3D> Boundary { get; set; } = new();
    public string OriginalLayer { get; set; } = string.Empty;
    public double Height { get; set; } = 3000.0;

    // ── BIM Özellikleri ──────────────────────────────────────────────────────

    public string Name           { get; set; } = "";
    public string Description    { get; set; } = "";

    // Malzeme katmanları (dıştan içe) — ISO 6946
    public List<BimMaterialLayer> MaterialLayers { get; set; } = new();

    // U-değeri (W/m²K) — ISO 6946 hesabı veya manuel override
    public double? UValueOverride { get; set; }
    public double UValue => UValueOverride ?? CalculateUValue();

    // Ses yalıtımı (dB) — TS EN ISO 717-1
    public double SoundReductionIndexDb { get; set; }

    // Yangın sınıfı — TS EN 13501-1
    public FireRatingClass FireRating { get; set; } = FireRatingClass.NotRated;
    public int FireResistanceMinutes  { get; set; }   // REI değeri (dakika): 30, 60, 90, 120

    // Nem geçirgenliği (g/m²h) — TS EN ISO 10211
    public double VapourPermeability  { get; set; }

    // ── Hesaplar ──────────────────────────────────────────────────────────────

    // ISO 6946: U = 1 / (Rsi + Σ(d/λ) + Rse)
    public double CalculateUValue()
    {
        if (MaterialLayers.Count == 0) return 0;
        const double Rsi = 0.13; // İç yüzey ısı geçiş direnci (m²K/W)
        const double Rse = 0.04; // Dış yüzey ısı geçiş direnci (m²K/W)
        double rTotal = Rsi + Rse + MaterialLayers.Sum(l => l.ThermalResistance);
        return rTotal > 0 ? Math.Round(1.0 / rTotal, 3) : 0;
    }

    // Toplam duvar kalınlığı (mm)
    public double TotalThicknessMm => MaterialLayers.Sum(l => l.ThicknessMm);

    public string FireRatingLabel => FireResistanceMinutes > 0
        ? $"REI {FireResistanceMinutes} ({FireRating})"
        : FireRating.ToString();

    // ── BoundingBox ───────────────────────────────────────────────────────────

    public CadBoundingBox GetBoundingBox()
    {
        if (Boundary == null || Boundary.Count == 0) return CadBoundingBox.Empty;

        double minX = Boundary.Min(p => p.X), maxX = Boundary.Max(p => p.X);
        double minY = Boundary.Min(p => p.Y), maxY = Boundary.Max(p => p.Y);
        double minZ = Boundary.Min(p => p.Z), maxZ = Boundary.Max(p => p.Z) + Height;

        return new CadBoundingBox(new Vector3D(minX, minY, minZ), new Vector3D(maxX, maxY, maxZ));
    }
}
