using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Afney.Cad.Mechanical.Services;

// SVG grafik rapor servisi — basınç kaybı, debi, enerji dağılımı grafikleri
public static class SvgChartService
{
    public static string BarChart(string title, List<(string Label, double Value)> data, string unit = "", string color = "#00DDFF")
    {
        if (!data.Any()) return "";
        double maxVal = data.Max(d => d.Value);
        if (maxVal <= 0) maxVal = 1;

        int w = 600, h = 400, margin = 60, barGap = 5;
        double barWidth = (w - 2.0 * margin) / data.Count - barGap;

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' style='background:#1a1a2e'>");
        sb.AppendLine($"<text x='{w / 2}' y='25' fill='white' text-anchor='middle' font-size='14'>{title}</text>");

        for (int i = 0; i < data.Count; i++)
        {
            double x = margin + i * (barWidth + barGap);
            double barH = (data[i].Value / maxVal) * (h - 2 * margin);
            double y = h - margin - barH;

            sb.AppendLine($"<rect x='{F(x)}' y='{F(y)}' width='{F(barWidth)}' height='{F(barH)}' fill='{color}' opacity='0.8'/>");
            sb.AppendLine($"<text x='{F(x + barWidth / 2)}' y='{F(y - 5)}' fill='white' text-anchor='middle' font-size='10'>{data[i].Value:F1}{unit}</text>");
            sb.AppendLine($"<text x='{F(x + barWidth / 2)}' y='{h - margin + 15}' fill='#888' text-anchor='middle' font-size='9' transform='rotate(-45,{F(x + barWidth / 2)},{h - margin + 15})'>{data[i].Label}</text>");
        }

        // Axes
        sb.AppendLine($"<line x1='{margin}' y1='{h - margin}' x2='{w - margin}' y2='{h - margin}' stroke='#444'/>");
        sb.AppendLine($"<line x1='{margin}' y1='{margin}' x2='{margin}' y2='{h - margin}' stroke='#444'/>");

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    public static string PieChart(string title, List<(string Label, double Value, string Color)> data)
    {
        if (!data.Any()) return "";
        double total = data.Sum(d => d.Value);
        if (total <= 0) return "";

        int size = 300, cx = size / 2, cy = size / 2, r = 100;
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{size + 200}' height='{size}' style='background:#1a1a2e'>");
        sb.AppendLine($"<text x='{cx}' y='20' fill='white' text-anchor='middle' font-size='14'>{title}</text>");

        double startAngle = 0;
        int legendY = 40;
        foreach (var (label, value, color) in data)
        {
            double sweep = value / total * 360;
            double endAngle = startAngle + sweep;

            double x1 = cx + r * Math.Cos(startAngle * Math.PI / 180);
            double y1 = cy + r * Math.Sin(startAngle * Math.PI / 180);
            double x2 = cx + r * Math.Cos(endAngle * Math.PI / 180);
            double y2 = cy + r * Math.Sin(endAngle * Math.PI / 180);
            int largeArc = sweep > 180 ? 1 : 0;

            sb.AppendLine($"<path d='M{cx},{cy} L{F(x1)},{F(y1)} A{r},{r} 0 {largeArc},1 {F(x2)},{F(y2)} Z' fill='{color}' opacity='0.85'/>");

            // Legend
            sb.AppendLine($"<rect x='{size + 10}' y='{legendY}' width='12' height='12' fill='{color}'/>");
            sb.AppendLine($"<text x='{size + 28}' y='{legendY + 10}' fill='white' font-size='11'>{label} ({value / total * 100:F0}%)</text>");
            legendY += 20;

            startAngle = endAngle;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    public static string LineChart(string title, List<(double X, double Y)> data, string xLabel = "", string yLabel = "", string color = "#FF6600")
    {
        if (data.Count < 2) return "";

        int w = 600, h = 350, margin = 60;
        double minX = data.Min(d => d.X), maxX = data.Max(d => d.X);
        double minY = data.Min(d => d.Y), maxY = data.Max(d => d.Y);
        if (maxX <= minX) maxX = minX + 1;
        if (maxY <= minY) maxY = minY + 1;

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' style='background:#1a1a2e'>");
        sb.AppendLine($"<text x='{w / 2}' y='20' fill='white' text-anchor='middle' font-size='14'>{title}</text>");

        // Grid
        for (int i = 0; i <= 5; i++)
        {
            double y = margin + i * (h - 2.0 * margin) / 5;
            double val = maxY - i * (maxY - minY) / 5;
            sb.AppendLine($"<line x1='{margin}' y1='{F(y)}' x2='{w - margin}' y2='{F(y)}' stroke='#333' stroke-dasharray='2'/>");
            sb.AppendLine($"<text x='{margin - 5}' y='{F(y + 4)}' fill='#888' text-anchor='end' font-size='10'>{val:F1}</text>");
        }

        // Data line
        var points = data.Select(d =>
        {
            double x = margin + (d.X - minX) / (maxX - minX) * (w - 2 * margin);
            double y = h - margin - (d.Y - minY) / (maxY - minY) * (h - 2 * margin);
            return $"{F(x)},{F(y)}";
        });
        sb.AppendLine($"<polyline points='{string.Join(" ", points)}' fill='none' stroke='{color}' stroke-width='2'/>");

        // Axes
        sb.AppendLine($"<line x1='{margin}' y1='{h - margin}' x2='{w - margin}' y2='{h - margin}' stroke='#666'/>");
        sb.AppendLine($"<line x1='{margin}' y1='{margin}' x2='{margin}' y2='{h - margin}' stroke='#666'/>");
        sb.AppendLine($"<text x='{w / 2}' y='{h - 10}' fill='#888' text-anchor='middle' font-size='11'>{xLabel}</text>");
        sb.AppendLine($"<text x='15' y='{h / 2}' fill='#888' text-anchor='middle' font-size='11' transform='rotate(-90,15,{h / 2})'>{yLabel}</text>");

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
}
