using System.Text;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

public class SelectionBomResult
{
    public int TotalCount { get; set; }
    public int PipeCount { get; set; }
    public double PipeLengthM { get; set; }
    public int DuctCount { get; set; }
    public double DuctLengthM { get; set; }
    public int FixtureCount { get; set; }
    public int LineCount { get; set; }
    public double LineLengthM { get; set; }
    public double TotalAreaM2 { get; set; }
    public double EstimatedCost { get; set; }

    public string Summary =>
        $"Secim: {TotalCount} nesne | " +
        (PipeCount > 0 ? $"Boru: {PipeCount} ({PipeLengthM:F1}m) | " : "") +
        (DuctCount > 0 ? $"Kanal: {DuctCount} ({DuctLengthM:F1}m) | " : "") +
        (FixtureCount > 0 ? $"Cihaz: {FixtureCount} | " : "") +
        (LineCount > 0 ? $"Cizgi: {LineCount} ({LineLengthM:F1}m) | " : "") +
        $"Maliyet: {EstimatedCost:N0} TRY";
}

public class SelectionBomService
{
    public SelectionBomResult Calculate(IEnumerable<CadEntity> selectedEntities)
    {
        var result = new SelectionBomResult();
        var costSvc = new RealTimeCostService();

        foreach (var ent in selectedEntities)
        {
            result.TotalCount++;

            switch (ent)
            {
                case PipeEntity pipe:
                    result.PipeCount++;
                    double pipeLen = pipe.GetLength() / 1000.0;
                    result.PipeLengthM += pipeLen;
                    result.EstimatedCost += costSvc.CalculateSinglePipeCost(
                        pipe.GetLength(), Enums.PipeMaterial.PPRC_PN20, pipe.InnerDiameter);
                    break;

                case DuctEntity duct:
                    result.DuctCount++;
                    result.DuctLengthM += duct.GetLength() / 1000.0;
                    result.EstimatedCost += (duct.GetLength() / 1000.0) * 95.0;
                    break;

                case SanitaryFixtureEntity fixture:
                    result.FixtureCount++;
                    result.EstimatedCost += 850;
                    break;

                case LineEntity line:
                    result.LineCount++;
                    result.LineLengthM += line.GetLength() / 1000.0;
                    break;
            }
        }

        return result;
    }

    public string ExportToHtml(SelectionBomResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<title>Secim Metraj</title>");
        sb.AppendLine("<style>body{font-family:'Segoe UI',sans-serif;margin:30px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:12px 0}");
        sb.AppendLine("th,td{border:1px solid #CCC;padding:8px;text-align:left}");
        sb.AppendLine("th{background:#E8F0FE}</style></head><body>");
        sb.AppendLine("<h1>Secim Bazli Metraj</h1>");
        sb.AppendLine($"<p>Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
        sb.AppendLine("<table><tr><th>Kalem</th><th>Adet</th><th>Uzunluk/Miktar</th><th>Birim</th></tr>");

        if (result.PipeCount > 0)
            sb.AppendLine($"<tr><td>Boru</td><td>{result.PipeCount}</td><td>{result.PipeLengthM:F2}</td><td>m</td></tr>");
        if (result.DuctCount > 0)
            sb.AppendLine($"<tr><td>Kanal</td><td>{result.DuctCount}</td><td>{result.DuctLengthM:F2}</td><td>m</td></tr>");
        if (result.FixtureCount > 0)
            sb.AppendLine($"<tr><td>Cihaz</td><td>{result.FixtureCount}</td><td>-</td><td>adet</td></tr>");
        if (result.LineCount > 0)
            sb.AppendLine($"<tr><td>Cizgi</td><td>{result.LineCount}</td><td>{result.LineLengthM:F2}</td><td>m</td></tr>");

        sb.AppendLine($"<tr style='font-weight:bold'><td>TOPLAM</td><td>{result.TotalCount}</td><td colspan='2'>Maliyet: {result.EstimatedCost:N0} TRY</td></tr>");
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }
}
