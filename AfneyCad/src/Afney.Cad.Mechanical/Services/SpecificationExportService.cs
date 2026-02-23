using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Teknik Şartname PDF Çıktı Servisi (SpecificationExportService)
   NEDEN: FINE SANI, detaylı mühendislik teknik şartname dokümanı üretir.
          Bu servis, boru malzemesi, birleşim yöntemi, yalıtım, test prosedürü gibi
          teknik bilgileri düzenli bir doküman olarak dışa aktarır.
   
   REFERANS: TMMOB Tesisat Mühendisleri Odası şartname formatı
*/
public class SpecificationExportService
{
    private readonly CadDatabase _database;

    public SpecificationExportService(CadDatabase database) { _database = database; }

    public class SpecificationDocument
    {
        public string ProjectName { get; set; } = "";
        public string Engineer { get; set; } = "";
        public DateTime Date { get; set; } = DateTime.Now;
        public List<SpecSection> Sections { get; set; } = new();
    }

    public class SpecSection
    {
        public string Number { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }

    /*
       NE: Otomatik teknik şartname dokümanı üretimi
       NEDEN: Projede kullanılan malzeme ve sistemlere göre özelleştirilmiş şartname
    */
    public SpecificationDocument GenerateSpecification(string projectName)
    {
        var doc = new SpecificationDocument
        {
            ProjectName = projectName,
            Engineer = Environment.UserName,
            Date = DateTime.Now
        };

        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        var materials = pipes.Select(p => p.PipeMaterialType).Distinct().ToList();

        // 1. Genel Hükümler
        doc.Sections.Add(new SpecSection
        {
            Number = "1",
            Title = "GENEL HÜKÜMLER",
            Content =
                "1.1. Bu şartname, sıhhi tesisat işlerinin projeye uygun olarak yapılmasını kapsar.\n" +
                "1.2. Tüm malzeme ve işçilik, yürürlükteki TS, EN ve DIN standartlarına uygun olacaktır.\n" +
                "1.3. İşin yapımında TS 1258, TS EN 806, DIN 1988 ve ilgili yönetmelikler esas alınacaktır.\n" +
                "1.4. Yüklenici, işe başlamadan önce projeyi ve şartnameyi detaylı inceleyecektir."
        });

        // 2. Malzeme Şartları
        var materialContent = new StringBuilder();
        materialContent.AppendLine("2.1. Kullanılacak boru malzemeleri aşağıda belirtilmiştir:");
        foreach (var mat in materials)
        {
            string matSpec = mat switch
            {
                Enums.PipeMaterial.PPRC_PN20 =>
                    "PP-R (Polipropilen Random Kopolimer) PN20 borular kullanılacaktır.\n" +
                    "   - Standart: TS EN ISO 15874\n" +
                    "   - Çalışma basıncı: 20 bar (20°C), 10 bar (70°C)\n" +
                    "   - Birleşim yöntemi: Polifüzyon (soket) kaynağı\n" +
                    "   - Kaynak sıcaklığı: 260°C ± 10°C",
                Enums.PipeMaterial.PPRC_PN25 =>
                    "PP-R (Polipropilen Random Kopolimer) PN25 borular kullanılacaktır.\n" +
                    "   - Standart: TS EN ISO 15874\n" +
                    "   - Çalışma basıncı: 25 bar (20°C), 12 bar (70°C)\n" +
                    "   - Birleşim yöntemi: Polifüzyon (soket) kaynağı",
                Enums.PipeMaterial.PVC_SN4 =>
                    "PVC-U (Sert Polivinil Klorür) SN4 borular kullanılacaktır.\n" +
                    "   - Standart: TS EN 1401-1\n" +
                    "   - Halka sertliği: SN4 (4 kN/m²)\n" +
                    "   - Birleşim yöntemi: Contalı muf",
                Enums.PipeMaterial.PEX_b =>
                    "PEX-b (Çapraz Bağlı Polietilen) borular kullanılacaktır.\n" +
                    "   - Standart: TS EN ISO 15875\n" +
                    "   - Birleşim yöntemi: Press-fitting veya sıkıştırma",
                Enums.PipeMaterial.Steel_Galvanized =>
                    "Galvaniz çelik borular kullanılacaktır.\n" +
                    "   - Standart: TS EN 10255\n" +
                    "   - Galvaniz kaplama: TS EN ISO 1461\n" +
                    "   - Birleşim yöntemi: Dişli bağlantı veya kaynak",
                _ =>
                    $"{mat} malzeme kullanılacaktır."
            };
            materialContent.AppendLine($"   {mat}: {matSpec}");
        }
        doc.Sections.Add(new SpecSection { Number = "2", Title = "MALZEME ŞARTLARI", Content = materialContent.ToString() });

        // 3. İşçilik ve Montaj
        doc.Sections.Add(new SpecSection
        {
            Number = "3",
            Title = "İŞÇİLİK VE MONTAJ",
            Content =
                "3.1. Borular, projesinde gösterilen güzergâh ve kotlarda döşenecektir.\n" +
                "3.2. Askı ve destek elemanları, TS 1258'e uygun aralıklarla yerleştirilecektir:\n" +
                "   - DN20-25: 1.5 m aralıkla\n   - DN32-50: 2.0 m aralıkla\n   - DN65-100: 2.5 m aralıkla\n" +
                "3.3. Tüm bağlantı noktaları sızdırmazlık testine tabi tutulacaktır.\n" +
                "3.4. Duvar ve döşeme geçişlerinde manşon kullanılacaktır."
        });

        // 4. Test ve Kabul
        doc.Sections.Add(new SpecSection
        {
            Number = "4",
            Title = "TEST VE KABUL",
            Content =
                "4.1. Temiz su tesisatı 10 bar basınçla 2 saat süre ile basınç testine tabi tutulacaktır.\n" +
                "4.2. Pis su tesisatı duman veya su ile sızdırmazlık testinden geçirilecektir.\n" +
                "4.3. Test sonuçları tutanakla belgelenecektir.\n" +
                "4.4. Referans: TS EN 806-4 / DIN 1988-200"
        });

        // 5. Yalıtım
        doc.Sections.Add(new SpecSection
        {
            Number = "5",
            Title = "YALITIM",
            Content =
                "5.1. Soğuk su borularına terleme önleyici yalıtım uygulanacaktır.\n" +
                "5.2. Sıcak su borularına ısı yalıtımı uygulanacaktır:\n" +
                "   - DN20-25: 13 mm kalınlık\n   - DN32-50: 19 mm kalınlık\n   - DN65+: 25 mm kalınlık\n" +
                "5.3. Yalıtım malzemesi: Elastomerik kauçuk (TS EN 14304) veya cam yünü\n" +
                "5.4. Dış mekân borularında UV dayanımlı kaplama kullanılacaktır."
        });

        return doc;
    }

    /*
       NE: HTML formatında şartname çıktısı
       NEDEN: Yazdırılabilir ve tarayıcıda görüntülenebilir
    */
    public string ExportToHtml(SpecificationDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Teknik Şartname — {doc.ProjectName}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Times New Roman', serif; margin: 40px; color: #333; line-height: 1.6; }");
        sb.AppendLine("h1 { text-align: center; color: #1a1a1a; border-bottom: 2px solid #333; }");
        sb.AppendLine("h2 { color: #005A9C; margin-top: 25px; }");
        sb.AppendLine(".header { text-align: center; margin-bottom: 30px; }");
        sb.AppendLine("pre { white-space: pre-wrap; font-family: inherit; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine($"<h1>TEKNİK ŞARTNAME</h1>");
        sb.AppendLine($"<p><strong>Proje:</strong> {doc.ProjectName}</p>");
        sb.AppendLine($"<p><strong>Hazırlayan:</strong> {doc.Engineer} | <strong>Tarih:</strong> {doc.Date:dd.MM.yyyy}</p>");
        sb.AppendLine("</div>");

        foreach (var s in doc.Sections)
        {
            sb.AppendLine($"<h2>{s.Number}. {s.Title}</h2>");
            sb.AppendLine($"<pre>{s.Content}</pre>");
        }

        sb.AppendLine("<hr><p style=\"color:#888;\">Bu şartname AfneyCAD Mechanical yazılımı ile otomatik oluşturulmuştur.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
