using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;

namespace Afney.Cad.Application.Services
{
    public class MahalData
    {
        public string? kat { get; set; }
        public string? daire { get; set; }
        public string? mahal { get; set; }
        public string? tip { get; set; }
        public double alan { get; set; }
        public double[] center { get; set; } = new double[2];
    }

    public class MahalExportService
    {
        private readonly CadDatabase _database;

        public MahalExportService(CadDatabase database)
        {
            _database = database;
        }

        public string ExportMahalDataToJson(string outputFilePath)
        {
            // 1. Veritabanındaki tüm yazı (Text) objelerini topla
            var texts = _database.GetAllEntities()
                                 .OfType<TextEntity>()
                                 .ToList();

            if (!texts.Any())
                return "Hata: Tasarımda hiçbir yazı (Text) bulunamadı.";

            var mahaller = ProcessTexts(texts);

            // 2. JSON olarak kaydet
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(mahaller, options);
            
            File.WriteAllText(outputFilePath, jsonString);

            return $"Başarılı: {mahaller.Count} adet mahal tespit edildi ve Mahal.txt dosyasına yazıldı.";
        }

        private List<MahalData> ProcessTexts(List<TextEntity> texts)
        {
            var detectedMahaller = new List<MahalData>();

            // Anahtar kelime tabanlı tip tespit sözlüğü
            var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SALON", "LivingRoom" },
                { "MUTFAK", "Kitchen" },
                { "BANYO", "Bathroom" },
                { "YATAK", "Bedroom" },
                { "GARAJ", "Garage" },
                { "DEPO", "Storage" },
                { "SU DEPOSU", "WaterTank" },
                { "PARK", "Parking" },
                { "HOL", "Hall" },
                { "KORİDOR", "Corridor" },
                { "BALKON", "Balcony" },
                { "WC", "WC" },
                { "EBEVEYN", "MasterBedroom" }
            };

            // Gruplama için: Yazılar genelde aynı odada birbirine yakındır. (Örn: "SALON" ve "28.5 m2" alt alta yazılır)
            // Ya da aynı MText içindedir. ("SALON\n28.5 m2")

            var processedTexts = new HashSet<TextEntity>();

            // Kat ve Daire isimlerini bulmak için genel bir geçiş (Örn: "ZB", "ZEMİN KAT", "DAİRE 1" büyük yazılar olabilir)
            // Alan (m2) içeren yazıları bul
            var areaRegex = new Regex(@"(\d+[.,]\d+|\d+)\s*(m2|m²)", RegexOptions.IgnoreCase);

            foreach (var txt in texts)
            {
                if (processedTexts.Contains(txt)) continue;

                string content = txt.Text.Trim();
                
                // MText içinde newline varsa ("SALON\n28.5 m2" gibi)
                if (content.Contains("\n") || content.Contains("\r"))
                {
                    double area = 0;
                    string name = "BİLMEYEN MAHAL";
                    
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var match = areaRegex.Match(line);
                        if (match.Success)
                        {
                            double.TryParse(match.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out area);
                        }
                        else if (!line.Contains("m2") && !line.Contains("m²"))
                        {
                            name = line.Trim(); // İlk alanı olmayan satırı isim kabul et
                        }
                    }

                    if (area > 0 || typeMap.Keys.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        var mahal = CreateMahalData(name, area, txt, typeMap);
                        detectedMahaller.Add(mahal);
                        processedTexts.Add(txt);
                    }
                }
                else
                {
                    // Alan (m2) belirten text tek satırsa (Sadece "28.5 m2" yazıyorsa) 
                    // Yada oda ismi tek satırsa ("SALON") ve alanı alttaki/üstteki başka text'tense
                    
                    var match = areaRegex.Match(content);
                    if (match.Success)
                    {
                        // Bu alan bilgisi, yakınındaki text mahal ismini ifade ediyordur
                        double.TryParse(match.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double area);
                        
                        // En yakın (isim) text'i bul:
                        var closestNameText = texts.Where(t => t != txt && !areaRegex.IsMatch(t.Text) && !t.Text.Contains("KAT") && !t.Text.Contains("DAİRE"))
                                                   .OrderBy(t => t.Position.DistanceTo(txt.Position))
                                                   .FirstOrDefault();

                        string name = "BİLİNMEYEN";
                        if (closestNameText != null && closestNameText.Position.DistanceTo(txt.Position) < 200) // Yakınlık toleransı
                        {
                            name = closestNameText.Text.Trim();
                            processedTexts.Add(closestNameText);
                        }
                        
                        detectedMahaller.Add(CreateMahalData(name, area, txt, typeMap));
                        processedTexts.Add(txt);
                    }
                    else if (typeMap.Keys.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Acaba sadece oda ismi mi var alanı girilmemiş mi? (Veya area regex yukarıda kaldı)
                        // Bunu area bulamasa da listeye ekle
                        var mahal = CreateMahalData(content, 0, txt, typeMap);
                        
                        // Alanı bulmak için etrafına bak
                        var closestAreaText = texts.Where(t => t != txt && areaRegex.IsMatch(t.Text))
                                                   .OrderBy(t => t.Position.DistanceTo(txt.Position))
                                                   .FirstOrDefault();
                                                   
                        if (closestAreaText != null && closestAreaText.Position.DistanceTo(txt.Position) < 200)
                        {
                            var areaMatch = areaRegex.Match(closestAreaText.Text);
                            if (areaMatch.Success && double.TryParse(areaMatch.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double a))
                            {
                                mahal.alan = a;
                                processedTexts.Add(closestAreaText);
                            }
                        }

                        if (!processedTexts.Contains(txt))
                        {
                            detectedMahaller.Add(mahal);
                            processedTexts.Add(txt);
                        }
                    }
                }
            }

            // Kat ve Daire Mantığı (Basitçe En Yakın BÜYÜK veya Özel İsimli text'i arama)
            AssignKatAndDaire(detectedMahaller, texts);

            return detectedMahaller;
        }

        private MahalData CreateMahalData(string name, double area, TextEntity txt, Dictionary<string, string> typeMap)
        {
            name = name.Replace("\\P", "").Trim(); // MText kalıntıları
            string type = "Room";
            foreach (var kvp in typeMap)
            {
                if (name.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    type = kvp.Value;
                    break;
                }
            }

            return new MahalData
            {
                mahal = name,
                alan = area,
                tip = type,
                center = new double[] { txt.Position.X, txt.Position.Y }
                // Kat ve Daire sonradan atanacak
            };
        }

        private void AssignKatAndDaire(List<MahalData> mahaller, List<TextEntity> allTexts)
        {
            // Örnek proje verilerine dayanarak: 
            // "BODRUM KAT", "ZEMİN KAT", "DAİRE 1", "DAİRE 2" vb. textleri bul
            var katTexts = allTexts.Where(t => t.Text.Contains("KAT", StringComparison.OrdinalIgnoreCase)).ToList();
            var daireTexts = allTexts.Where(t => t.Text.Contains("DAİRE", StringComparison.OrdinalIgnoreCase) || t.Text.Contains("DAIRE", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var mahal in mahaller)
            {
                // Mahal'e en yakın Daire Text'i
                var nearestDaire = daireTexts.OrderBy(t => Math.Sqrt(Math.Pow(t.Position.X - mahal.center[0], 2) + Math.Pow(t.Position.Y - mahal.center[1], 2))).FirstOrDefault();
                if (nearestDaire != null)
                {
                    mahal.daire = nearestDaire.Text.Trim();
                }

                // Mahal'e en yakın Kat Text'i
                var nearestKat = katTexts.OrderBy(t => Math.Sqrt(Math.Pow(t.Position.X - mahal.center[0], 2) + Math.Pow(t.Position.Y - mahal.center[1], 2))).FirstOrDefault();
                if (nearestKat != null)
                {
                    // "BODRUM KAT PLAN" içinden sadece "BODRUM" kelimesini ayıklamak
                    var split = nearestKat.Text.ToUpper().Split(new[] { " KAT" }, StringSplitOptions.RemoveEmptyEntries);
                    mahal.kat = split[0].Trim();
                }
                
                // layer bazlı ekstra mantık da eklenebilir. Örn: Eğer Layer adı "BODRUM_PLAN" ise kat="BODRUM" vs.
            }
        }
    }
}
