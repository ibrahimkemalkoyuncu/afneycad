using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Dikey Şema (Kolon/Riser Diagram) DWG Çıktı Servisi
   NEDEN: FINE SANI hesaplama sonuçlarına göre kolon şemasını DWG paftasına çizim verileri olarak üretir.
   
   MÜHENDİSLİK DETAYI:
   - Her kat seviyesinde yatay branş çizgisi
   - Vitrifiye sembolleri branş uçlarında
   - DN etiketleri, kat isimleri
   - Çift kolon: Soğuk Su (mavi) + Sıcak Su (kırmızı) ayrı
*/
public class RiserDiagramExportService
{
    private readonly CadDatabase _database;

    public RiserDiagramExportService(CadDatabase database) { _database = database; }

    public class RiserDiagramData
    {
        public List<RiserFloor> Floors { get; set; } = new();
        public double TotalHeight { get; set; }
        public string SystemName { get; set; } = "";
        public List<DiagramLine> Lines { get; set; } = new();
        public List<DiagramText> Labels { get; set; } = new();
        public List<DiagramSymbol> Symbols { get; set; } = new();
    }

    public class RiserFloor
    {
        public string Name { get; set; } = "";
        public double Elevation { get; set; }
        public double BranchDN { get; set; }
        public int FixtureCount { get; set; }
    }

    public class DiagramLine
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public uint Color { get; set; }
        public double LineWeight { get; set; } = 1.0;
    }

    public class DiagramText
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Text { get; set; } = "";
        public double Height { get; set; } = 2.5;
    }

    public class DiagramSymbol
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Type { get; set; } = "";
    }

    /*
       NE: Kolon şeması çizim verisi üret
       NEDEN: DWG'ye çizilebilecek formatta, kat bazlı kolon şeması oluşturur
    */
    public RiserDiagramData GenerateRiserDiagram(List<RiserFloor> floors, MechanicalSystemType system = MechanicalSystemType.DomesticColdWater)
    {
        var data = new RiserDiagramData
        {
            Floors = floors,
            SystemName = system.ToString()
        };

        // Çizim parametreleri (mm cinsinden)
        double floorSpacing = 100.0;    // Katlar arası çizim mesafesi
        double riserX = 200.0;          // Kolon X konumu
        double branchLength = 150.0;    // Yatay branş uzunluğu
        uint riserColor = system == MechanicalSystemType.DomesticColdWater ? 0xFF0066FF : 0xFFFF3300;

        for (int i = 0; i < floors.Count; i++)
        {
            double y = i * floorSpacing;

            // Kat seviye çizgisi (yatay kesik çizgi)
            data.Lines.Add(new DiagramLine { X1 = 0, Y1 = y, X2 = riserX + branchLength + 100, Y2 = y, Color = 0xFF888888, LineWeight = 0.5 });

            // Kat isim etiketi
            data.Labels.Add(new DiagramText { X = 5, Y = y + 5, Text = $"{floors[i].Name} (+{floors[i].Elevation:F0})", Height = 3.0 });

            // Yatay branş
            data.Lines.Add(new DiagramLine { X1 = riserX, Y1 = y, X2 = riserX + branchLength, Y2 = y, Color = riserColor, LineWeight = 1.5 });

            // DN etiketi
            data.Labels.Add(new DiagramText { X = riserX + branchLength / 2, Y = y + 3, Text = $"DN{floors[i].BranchDN:F0}" });

            // Vitrifiye sembolleri
            for (int j = 0; j < floors[i].FixtureCount && j < 5; j++)
            {
                double symX = riserX + branchLength + 20 + j * 25;
                data.Symbols.Add(new DiagramSymbol { X = symX, Y = y, Type = "Fixture" });
            }

            // Dikey kolon (kat → üst kat)
            if (i < floors.Count - 1)
            {
                double nextY = (i + 1) * floorSpacing;
                data.Lines.Add(new DiagramLine { X1 = riserX, Y1 = y, X2 = riserX, Y2 = nextY, Color = riserColor, LineWeight = 2.0 });
            }
        }

        data.TotalHeight = (floors.Count - 1) * floorSpacing;
        return data;
    }

    /*
       NE: SVG formatında kolon şeması çıktısı
       NEDEN: Vektörel grafik olarak görüntülenebilir ve yazdırılabilir
    */
    public string ExportToSvg(RiserDiagramData diagram)
    {
        var sb = new StringBuilder();
        double width = 600;
        double height = diagram.TotalHeight + 100;

        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\">");
        sb.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#1E1E2E\"/>");

        foreach (var line in diagram.Lines)
        {
            string color = $"#{(line.Color & 0x00FFFFFF):X6}";
            sb.AppendLine($"<line x1=\"{line.X1}\" y1=\"{height - line.Y1}\" x2=\"{line.X2}\" y2=\"{height - line.Y2}\" stroke=\"{color}\" stroke-width=\"{line.LineWeight}\"/>");
        }

        foreach (var label in diagram.Labels)
        {
            sb.AppendLine($"<text x=\"{label.X}\" y=\"{height - label.Y}\" fill=\"white\" font-size=\"{label.Height * 3}\">{label.Text}</text>");
        }

        foreach (var sym in diagram.Symbols)
        {
            sb.AppendLine($"<circle cx=\"{sym.X}\" cy=\"{height - sym.Y}\" r=\"5\" fill=\"#00FF88\" stroke=\"white\" stroke-width=\"1\"/>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }
}
