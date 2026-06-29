using System;
using System.Collections.Generic;

namespace Afney.Cad.Render.Services;

// Lineweight render servisi — AutoCAD benzeri kalınlık tablosu ve zoom-bağımlı çizim
public static class LineweightRenderService
{
    // AutoCAD standart lineweight tablosu (mm → pixel dönüşümü)
    private static readonly Dictionary<string, double> LayerWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = 0.25,
        ["ARCH-WALL"] = 0.50, ["DUVAR"] = 0.50, ["A-WALL"] = 0.50,
        ["ARCH-COLUMN"] = 0.50, ["KOLON"] = 0.50,
        ["ARCH-DOOR"] = 0.25, ["KAPI"] = 0.25,
        ["ARCH-WINDOW"] = 0.25, ["PENCERE"] = 0.25,
        ["MEP_TEMIZ_SU"] = 0.35, ["MEP_SICAK_SU"] = 0.35,
        ["MEP_PIS_SU"] = 0.35, ["MEP_YANGIN"] = 0.50,
        ["MEP_GAZ"] = 0.35, ["MEP_HAVALANDIRMA"] = 0.35,
        ["MEP_FIXTURES"] = 0.25,
        ["DIM"] = 0.13, ["TEXT"] = 0.13,
        ["RISER_DIAGRAM"] = 0.25,
        ["XREF"] = 0.09, ["IFC_WALL"] = 0.35,
    };

    // Layer'a göre çizim kalınlığı (mm)
    public static double GetLineweightMm(string? layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return 0.25;
        if (LayerWeights.TryGetValue(layerName, out var w)) return w;
        foreach (var kvp in LayerWeights)
            if (layerName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)) return kvp.Value;
        return 0.25;
    }

    // Zoom seviyesine göre pixel kalınlığı hesapla
    public static float GetRenderThickness(string? layerName, double zoomScale, bool lineweightDisplay = true)
    {
        if (!lineweightDisplay) return 1.0f; // Hairline mode

        double mmWeight = GetLineweightMm(layerName);
        // 1mm ≈ 3.78 pixel @ 96 DPI, zoom ile ölçekle ama min 1px
        float px = (float)(mmWeight * 3.78 * Math.Min(zoomScale, 2.0));
        return Math.Max(px, 1.0f);
    }

    // Dimension native render parametreleri
    public static DimensionRenderParams GetDimensionParams(double textHeight, double zoomScale)
    {
        return new DimensionRenderParams
        {
            TextSizePx = (float)(textHeight * zoomScale * 0.001),
            ArrowSizePx = (float)(textHeight * 0.6 * zoomScale * 0.001),
            ExtLineLengthPx = (float)(textHeight * 0.3 * zoomScale * 0.001),
            DimLineThicknessPx = Math.Max(1.0f, (float)(0.18 * 3.78 * Math.Min(zoomScale, 2.0))),
            TextColor = 0xFFFFFFFF,
            DimLineColor = 0xFFAAAAAA,
        };
    }
}

public class DimensionRenderParams
{
    public float TextSizePx { get; set; }
    public float ArrowSizePx { get; set; }
    public float ExtLineLengthPx { get; set; }
    public float DimLineThicknessPx { get; set; }
    public uint TextColor { get; set; }
    public uint DimLineColor { get; set; }
}
