using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.IO;
using ACadSharp.IO;
using ACadSharp.Entities;
using ACadSharp.Tables;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities; // PipeEntity vb. için

namespace Afney.Cad.Infrastructure.Import;

/*
    NE: Profesyonel DWG İçe Aktarma Servisi
    NEDEN: AutoCAD (.dwg) dosyalarını renk, katman ve blok sadakatiyle okumak için.
    
    ÖZELLİKLER:
    - ACadSharp kütüphanesi üzerine kurulu.
    - 256 Renk (ACI) Desteği.
    - Blok (Insert) desteği (Patlatarak okuma - Explode).
    - Polilayn, Yay (Arc), Çember ve Çizgi desteği.
    - Metin (Text/MText) desteği.
*/
public class DwgImportService
{
    // ACI (AutoCAD Color Index) -> RGB Mapping Table (Standart 256 renk)
    // Performans için static cache
    private static readonly Dictionary<short, uint> _aciPalette = new();
    
    // Yüksek Performanslı Reflection Ön Belleği (14 saniyelik Hatch gecikmesini 0.1 saniyeye indirir)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Reflection.PropertyInfo?> _propCache = new();

    private static System.Reflection.PropertyInfo? GetCachedProperty(Type type, string propName)
    {
        string key = $"{type.FullName}::{propName}";
        return _propCache.GetOrAdd(key, _ => type.GetProperty(propName));
    }

    static DwgImportService()
    {
        InitializeAciPalette();
    }

    public List<CadEntity> ImportDwg(string filePath)
    {
        var entities = new List<CadEntity>();

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DWG dosyası bulunamadı: {filePath}");

        try
        {
            Serilog.Log.Information("[DWG] DwgReader nesnesi oluşturuluyor... (Dosya: {filePath})", filePath);
            using (var reader = new DwgReader(filePath))
            {
                Serilog.Log.Information("[DWG] reader.Read() metodu çağrılıyor. (Bu işlem uzun sürebilir veya ACadSharp kütüphanesi çökerse log burada kesilir).");
                var cadDoc = reader.Read();
                
                Serilog.Log.Information("[DWG] DWG dokümanı başarıyla okundu. Table katmanları çıkarılıyor...");

                // Layer tablosundan renkleri ve Linetype'ları oku ve cache'le
                var layerColors = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                var layerLinetypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var layer in cadDoc.Layers)
                {
                    layerColors[layer.Name] = MapColor(layer.Color);
                    if (layer.LineType != null)
                    {
                        layerLinetypes[layer.Name] = layer.LineType.Name ?? "Continuous";
                    }
                    else
                    {
                        layerLinetypes[layer.Name] = "Continuous";
                    }
                }

                Serilog.Log.Information("[DWG] Layer verisi çekildi. Toplam {count} model objesi dönüştürülüyor...", cadDoc.Entities.Count);
                
                // Model Space (Multi-Threaded)
                int convertedCount = 0;
                var concurrentEntities = new System.Collections.Concurrent.ConcurrentBag<CadEntity>();

                System.Threading.Tasks.Parallel.ForEach(cadDoc.Entities, entity =>
                {
                    // Root entity'ler için Identity matrisi kullanılır
                    var convertedList = ConvertEntity(entity, Matrix4x4.Identity, layerColors, layerLinetypes);
                    foreach (var c in convertedList)
                    {
                        concurrentEntities.Add(c);
                    }
                    
                    int currentCount = System.Threading.Interlocked.Increment(ref convertedCount);
                    if (currentCount % 10000 == 0)
                    {
                        Serilog.Log.Information("[DWG] Dönüştürülen ana obje sayısı: {Count}...", currentCount);
                    }
                });
                
                entities.AddRange(concurrentEntities);
                
                Serilog.Log.Information("[DWG] Dönüştürme tamamlandı. Toplam {count} sonuç objesi oluşturuldu.", entities.Count);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"DWG okuma hatası: {ex.Message}", ex);
        }

        return entities;
    }

    private IEnumerable<CadEntity> ConvertEntity(Entity entity, Matrix4x4 transform, Dictionary<string, uint> layerColors, Dictionary<string, string> layerLinetypes, ACadSharp.Color? parentColor = null, string? parentLinetype = null, int depth = 0, HashSet<string>? visitedBlocks = null)
    {
        // Renk Çözümleme (ByBlock ise parent rengi al)
        uint resolvedColor = 0xFFFFFFFF; // Default

        if (entity.Color.IsByBlock && parentColor.HasValue)
        {
             // Parent rengi (Blok rengi) kullanılır. Note: Parent rengi zaten resolved gelmeli veya ACadSharp.Color gelmeli?
             // ConvertEntity signature'da parentColor ACadSharp.Color tipinde. 
             // Eğer parentColor da ByLayer ise... recursion içinde çözülmüş olmalıydı ama burada ham Color taşıyoruz.
             // Basitleştirme: Blok rengi ByLayer ise, blok reference'ın Layer'ına bakılır.
             
             if (parentColor.Value.IsByLayer)
             {
                 // Blok reference'ın layer'ı neresiyse oradan almalıydık ama burada o bilgi yok.
                 // Insert entity işlenirken rengi çözüp pass etsek daha iyi olurdu.
                 // Şimdilik MapColor ile devam, ama ByLayer ise beyaz dönecek (eski logic).
                 // Geliştirelim:
                 resolvedColor = MapColor(parentColor.Value);
             }
             else
             {
                 resolvedColor = MapColor(parentColor.Value);
             }
        }
        else if (entity.Color.IsByLayer)
        {
             // Layer tablosundan bak
             if (!string.IsNullOrEmpty(entity.Layer.Name) && layerColors.TryGetValue(entity.Layer.Name, out var lColor))
             {
                 resolvedColor = lColor;
             }
             else
             {
                 resolvedColor = 0xFFFFFFFF; // Layer bulunamazsa Beyaz
             }
        }
        else
        {
             resolvedColor = MapColor(entity.Color);
        }

        // Linetype Çözümleme
        // Linetype Çözümleme (Reflection iptal edildi - Extreme Fast Track)
        string resolvedLinetype = "Continuous";
        if (entity.LineType != null)
        {
            string lTypeName = entity.LineType.Name;
            if (lTypeName.Equals("ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                resolvedLinetype = parentLinetype ?? "Continuous";
            }
            else if (lTypeName.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                 if (!string.IsNullOrEmpty(entity.Layer?.Name) && layerLinetypes.TryGetValue(entity.Layer.Name, out var lType))
                 {
                     resolvedLinetype = lType;
                 }
            }
            else
            {
                resolvedLinetype = lTypeName;
            }
        }

        // --- Insert (Block Reference) Özel İşlemi (Recursion) ---
        if (entity is Insert insert)
        {
            if (depth > 50) yield break; // Recursive stack protection
            
            var blocks = visitedBlocks ?? new HashSet<string>();
            string blockName = insert.Block?.Name ?? "UNKNOWN";
            if (blocks.Contains(blockName)) yield break; // Cyclic reference prevention
            
            blocks.Add(blockName);
            // BasePoint Translation
            var basePointTrans = Matrix4x4.Identity;
            if (insert.Block != null && insert.Block.BlockEntity != null)
            {
                var bp = insert.Block.BlockEntity.BasePoint;
                basePointTrans = Matrix4x4.CreateTranslation(-bp.X, -bp.Y, -bp.Z);
            }

            // Scale
            var scaleMat = Matrix4x4.CreateScale(insert.XScale, insert.YScale, insert.ZScale);
            
            // Rotate (Z ekseninde)
            var rotMat = Matrix4x4.CreateRotationZ(insert.Rotation);
            
            // Translate
            var transMat = Matrix4x4.CreateTranslation(insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z);
            
            // Local Transform = Translate * Rotate * Scale * (-BasePoint)
            var localTransform = transMat * rotMat * scaleMat * basePointTrans;
            
            // Global Transform = Parent * Local
            var combinedTransform = transform * localTransform;

            // 2. Blok İçeriğini Dönüştür
            if (insert.Block != null && insert.Block.Entities.Any())
            {
                var newVisited = new HashSet<string>(blocks);
                foreach (var child in insert.Block.Entities)
                {
                    // Recursive call
                    // Insert rengini parentColor olarak geçiriyoruz
                    foreach (var childConverted in ConvertEntity(child, combinedTransform, layerColors, layerLinetypes, insert.Color, resolvedLinetype, depth + 1, newVisited))
                    {
                        yield return childConverted;
                    }
                }
            }
            yield break; // Insert'in kendisini bir "nokta" olarak döndürmüyoruz artık.
        }

        // --- Temel Geometri Dönüşümleri ---
        CadEntity? result = entity switch
        {
            Line l => MapLine(l),
            Arc a => MapArc(a),
            Circle c => MapCircle(c),
            LwPolyline pl => MapLwPolyline(pl),
            MText mt => MapMText(mt), 
            ACadSharp.Entities.TextEntity t => MapText(t),
            Dimension dim => null, 
            Hatch h => null, // Özel işlem (aşağıda)
            _ => null
        };
        
        // --- Dimension (Ölçülendirme) Özel İşlemi ---
        // Dimension bir 'Insert' değildir ama 'Block' özelliği taşır ve görünümü oradadır.
        if (result == null && entity is Dimension dimension && dimension.Block != null)
        {
             // Dimension bloğunu (Anonymous Block) işle
             // Dimension'ın konumu genelde 0,0 dır çünkü blok içindeki koordinatlar mutlaktır veya insertion point'e göredir.
             // Ancak ACadSharp'ta Dimension entity'nin kendisi transform içermeyebilir, geometrisi bloktadır.
             // Biz yine de Identity ile gönderelim veya Dimension özelliklerine bakalım.
             
             // NOT: Dimension blokları genelde InsertPoint gerektirmez, içindeki koordinatlar doğrudur.
             // Ancak transform gerekebilir. Basitlik adına Identity geçiyoruz.
             
              foreach (var child in dimension.Block.Entities)
              {
                  foreach (var childConverted in ConvertEntity(child, transform, layerColors, layerLinetypes, dimension.Color, resolvedLinetype, depth + 1, visitedBlocks))
                  {
                      yield return childConverted;
                  }
              }
             yield break;
        }

         // --- Hatch (Tarama) Özel İşlemi (Strong-Typed - Extreme Fast Track) ---
        if (result == null && entity is Hatch hatch)
        {
            foreach (var path in hatch.Paths)
            {
                if (path == null) continue;

                // Edge Path: Doğrudan Edges koleksiyonunu kullan
                if (path.Edges != null && path.Edges.Count > 0)
                {
                    foreach (var edge in path.Edges)
                    {
                        if (edge == null) continue;
                        
                        // Line edge
                        if (edge is ACadSharp.Entities.Hatch.BoundaryPath.Line lineEdge)
                        {
                            var p1 = new Vector3D(lineEdge.Start.X, lineEdge.Start.Y, 0);
                            var p2 = new Vector3D(lineEdge.End.X, lineEdge.End.Y, 0);
                            var lineEnt = new LineEntity(p1, p2)
                            {
                                Layer = hatch.Layer?.Name ?? "0",
                                Color = resolvedColor,
                                Linetype = resolvedLinetype
                            };
                            lineEnt.Transform(transform);
                            yield return lineEnt;
                        }
                        else if (edge is ACadSharp.Entities.Hatch.BoundaryPath.Arc arcEdge)
                        {
                            // Arc edge: tessellate
                            int segments = 16;
                            double startAngle = arcEdge.StartAngle;
                            double endAngle = arcEdge.EndAngle;
                            if (arcEdge.CounterClockWise && endAngle < startAngle) endAngle += 2 * Math.PI;
                            if (!arcEdge.CounterClockWise && endAngle > startAngle) endAngle -= 2 * Math.PI;
                            double step = (endAngle - startAngle) / segments;
                            var arcPoints = new List<Vector3D>();
                            for (int i = 0; i <= segments; i++)
                            {
                                double angle = startAngle + step * i;
                                double x = arcEdge.Center.X + arcEdge.Radius * Math.Cos(angle);
                                double y = arcEdge.Center.Y + arcEdge.Radius * Math.Sin(angle);
                                arcPoints.Add(new Vector3D(x, y, 0));
                            }
                            if (arcPoints.Count > 1)
                            {
                                var poly = new LwPolylineEntity(arcPoints, false)
                                {
                                    Layer = hatch.Layer?.Name ?? "0",
                                    Color = resolvedColor,
                                    Linetype = resolvedLinetype
                                };
                                poly.Transform(transform);
                                yield return poly;
                            }
                        }
                        // Diğer edge tipleri (Ellipse, Spline) sonra eklenebilir
                    }
                }
                
                // Polyline Path (Entities listesinden)
                if (path.Entities != null && path.Entities.Count > 0)
                {
                    foreach (var child in path.Entities)
                    {
                        foreach (var childConverted in ConvertEntity(child, transform, layerColors, layerLinetypes, hatch.Color, resolvedLinetype, depth + 1, visitedBlocks))
                        {
                            yield return childConverted;
                        }
                    }
                }
            }
            yield break;
        }

        if (result != null)
        {
            // Ortak Özellikler
            result.Layer = entity.Layer?.Name ?? "0";
            result.Color = resolvedColor;
            result.Linetype = resolvedLinetype;

            // Matris Uygula (Transform)
            result.Transform(transform);
            
            yield return result;
        }
    }

    private CadEntity MapLine(Line l)
    {
        return new LineEntity(
            new Vector3D(l.StartPoint.X, l.StartPoint.Y, l.StartPoint.Z),
            new Vector3D(l.EndPoint.X, l.EndPoint.Y, l.EndPoint.Z)
        );
    }

    private CadEntity MapCircle(Circle c)
    {
        return new CircleEntity(
            new Vector3D(c.Center.X, c.Center.Y, c.Center.Z),
            c.Radius
        );
    }
    
    private CadEntity MapArc(Arc a)
    {
        // Arc Tessellation
        var segmentCount = 16; 
        var points = new List<Vector3D>();
        
        double start = a.StartAngle;
        double end = a.EndAngle;
        if (end < start) end += 2 * Math.PI;
        
        double step = (end - start) / segmentCount;
        
        for (int i = 0; i <= segmentCount; i++)
        {
            double angle = start + (step * i);
            double x = a.Center.X + a.Radius * Math.Cos(angle);
            double y = a.Center.Y + a.Radius * Math.Sin(angle);
            points.Add(new Vector3D(x, y, a.Center.Z));
        }

        return new LwPolylineEntity(points, false);
    }

    private CadEntity MapLwPolyline(LwPolyline pl)
    {
        var points = pl.Vertices.Select(v => new Vector3D(v.Location.X, v.Location.Y, 0)).ToList();
        return new LwPolylineEntity(points, pl.IsClosed);
    }
    
    private CadEntity? MapMText(MText mt)
    {
        // MText -> TextEntity
        string cleanValue = CleanMText(mt.Value ?? "");
        
        return new Afney.Cad.Domain.Entities.Basic.TextEntity(
            cleanValue,
            new Vector3D(mt.InsertPoint.X, mt.InsertPoint.Y, mt.InsertPoint.Z),
            mt.Height,
            mt.Rotation
        );
    }
    
    private string CleanMText(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // 0. \U+xxxx AutoCAD Unicode Escape Çözümleme (EN ÖNCELİKLİ)
        // Türkçe karakterler DWG içinde \U+015E (Ş), \U+011E (Ğ) vb. olarak saklanabilir.
        // Regex: \U+ ile başlayan, 4 hex basamaklı dizileri Unicode char'a çevir.
        value = Regex.Replace(value, @"\\U\+([0-9A-Fa-f]{4})", m =>
        {
            int codePoint = Convert.ToInt32(m.Groups[1].Value, 16);
            return ((char)codePoint).ToString();
        });

        // 1. Yeni Satır (\P) -> Environment.NewLine
        value = value.Replace("\\P", Environment.NewLine);
        
        // 2. Format Gruplarını ve Komutları Temizle
        // Örnek: \pxsm0.75;{\fTimes New Roman|b0|i0|c0|p18;C51;025}
        
        // a. Grup parantezlerini kaldır {}
        // İçeriği tut, sadece süslü parantezleri sil. (Nested yapılar için basit replace yetmez ama çoğu MText düzdür)
        // Basit yaklaşım: Regex ile temizlemeden önce veya sonra.
        
        // b. Format komutları: \[herhangi bir karakter][parametreler];
        // \fTimes New Roman|b0|i0|c0|p18; -> Font değişimi
        // \C1; -> Renk
        // \A1; -> Alignment
        // \H10; -> Height
        // \pxsm0.75; -> Paragraph
        
        // Genel Regex: Ters eğik çizgi ile başla, opsiyonel komut harfleri, opsiyonel parametreler, noktalı virgül ile bitir.
        // Veya sadece süslü parantez içindeki format komutları.
        
        // Adım Adım Regex Temizliği:
        
        // 1. { ve } karakterlerini kaldır (Grup)
        // Not: Bazen { sembolü metin olarak kullanılır, o zaman \{ şeklinde kaçışlıdır.
        // Bizim regex'imiz sadece format parantezlerini hedeflemeli ama basitlik adına hepsini kaldıralım mı?
        // Hayır, önce komutları temizleyelim.
        
        // Komut Temizliği: \[Harf][Değerler];  Örn: \fArial; \H2; 
        // Ancak \pxsm... gibi uzun komutlar da var.
        
        // Regex 1: Noktalı virgül ile biten komutlar (En yaygın)
        // \\[a-zA-Z0-9.\-|]+;
        value = Regex.Replace(value, @"\\[a-zA-Z0-9.\-|]+;", "");
        
        // Regex 2: Süslü parantezler ve blok başlangıçları
        // { ve }
        value = value.Replace("{", "").Replace("}", "");
        
        // Regex 3: Tekil kaçış karakterleri (Örn: \L, \O - Underline/Overline start)
        value = Regex.Replace(value, @"\\[LOloKk]", "");
        
        // Regex 4: Boşluk kaçışı (\~)
        value = value.Replace("\\~", " ");
        
        // Regex 5: Kalan ters eğik çizgiler (Eğer metin değilse)
        // Çift ters eğik çizgiyi (\\) tek'e indir (?) - Hayır, dosya yolu olabilir.
        
        return value.Trim();
    }
    
    private CadEntity MapText(ACadSharp.Entities.TextEntity t)
    {
        return new Afney.Cad.Domain.Entities.Basic.TextEntity(
            t.Value ?? "",
            new Vector3D(t.InsertPoint.X, t.InsertPoint.Y, t.InsertPoint.Z),
            t.Height,
            t.Rotation
        );
    }

    
    // MapInsertAsPoint kaldırıldı çünkü artık ConvertEntity içinde işleniyor.

    // --- Renk Yönetimi (ACI -> RGBA) ---
    public static uint MapColor(ACadSharp.Color color)
    {
        if (color.IsTrueColor)
        {
            // R, G, B pack
            return (uint)((0xFF << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }

        if (_aciPalette.TryGetValue(color.Index, out var rgba))
        {
            return rgba;
        }

        return 0xFFFFFFFF; // Default/Fallback White if index is 256 (ByLayer) or missing
    }

    private static void InitializeAciPalette()
    {
        // Standart AutoCAD 9 Temel Renk
        _aciPalette[1] = 0xFFFF0000; // Red
        _aciPalette[2] = 0xFFFFFF00; // Yellow
        _aciPalette[3] = 0xFF00FF00; // Green
        _aciPalette[4] = 0xFF00FFFF; // Cyan
        _aciPalette[5] = 0xFF0000FF; // Blue
        _aciPalette[6] = 0xFFFF00FF; // Magenta
        _aciPalette[7] = 0xFFFFFFFF; // White/Black
        _aciPalette[8] = 0xFF808080; // Dark Gray
        _aciPalette[9] = 0xFFC0C0C0; // Light Gray

        // AutoCAD ACI 10-249 (24 Renk Tonu * 10 Varyasyon)
        // Çift indeksler: Tam doygunluk (%100 S)
        // Tek indeksler: Yarım doygunluk (%50 S)
        // Parlaklık seviyeleri L = { 0.5, 0.65, 0.8, 0.9, 0.95 }
        for (int hueIdx = 0; hueIdx < 24; hueIdx++)
        {
            double h = hueIdx * 15.0; // 0 ile 345 derece arası
            int baseIndex = 10 + (hueIdx * 10);
            
            for (int v = 0; v < 5; v++)
            {
                double l = 0.5 - (v * 0.1); // En parlaktan en koyuya doğru
                if (l < 0.1) l = 0.1;
                
                _aciPalette[(short)(baseIndex + v * 2)]     = HslToRgb(h, 1.0, l); // Even: %100 Saturation
                _aciPalette[(short)(baseIndex + v * 2 + 1)] = HslToRgb(h, 0.3, l); // Odd: %30 Saturation
            }
        }

        // 250-255 (Gri Tonlar)
        _aciPalette[250] = 0xFF333333; // Çok Koyu Gri
        _aciPalette[251] = 0xFF555555;
        _aciPalette[252] = 0xFF777777;
        _aciPalette[253] = 0xFF999999;
        _aciPalette[254] = 0xFFBBBBBB;
        _aciPalette[255] = 0xFFDDDDDD; // Çok Açık Gri
    }

    private static uint HslToRgb(double h, double s, double l)
    {
        byte r, g, b;
        if (s == 0)
        {
            r = g = b = (byte)(l * 255);
        }
        else
        {
            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;
            r = HueToRgb(p, q, h / 360.0 + 1.0 / 3.0);
            g = HueToRgb(p, q, h / 360.0);
            b = HueToRgb(p, q, h / 360.0 - 1.0 / 3.0);
        }
        return (uint)((0xFF << 24) | (r << 16) | (g << 8) | b);
    }

    private static byte HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1.0;
        if (t > 1) t -= 1.0;
        if (t < 1.0 / 6.0) return (byte)((p + (q - p) * 6.0 * t) * 255.0);
        if (t < 1.0 / 2.0) return (byte)(q * 255.0);
        if (t < 2.0 / 3.0) return (byte)((p + (q - p) * (2.0 / 3.0 - t) * 6.0) * 255.0);
        return (byte)(p * 255.0);
    }
}
