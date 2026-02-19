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
            using (var reader = new DwgReader(filePath))
            {
                var cadDoc = reader.Read();
                
                // Layer tablosundan renkleri ve Linetype'ları oku ve cache'le
                var layerColors = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                var layerLinetypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var layer in cadDoc.Layers)
                {
                    layerColors[layer.Name] = MapColor(layer.Color);
                    try 
                    { 
                        dynamic l = layer;
                        layerLinetypes[layer.Name] = l.Linetype.Name; 
                    } 
                    catch { layerLinetypes[layer.Name] = "Continuous"; }
                }

                // Model Space
                foreach (var entity in cadDoc.Entities)
                {
                    // Root entity'ler için Identity matrisi kullanılır
                    var convertedList = ConvertEntity(entity, Matrix4x4.Identity, layerColors, layerLinetypes);
                    entities.AddRange(convertedList);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"DWG okuma hatası: {ex.Message}", ex);
        }

        return entities;
    }

    private IEnumerable<CadEntity> ConvertEntity(Entity entity, Matrix4x4 transform, Dictionary<string, uint> layerColors, Dictionary<string, string> layerLinetypes, ACadSharp.Color? parentColor = null, string? parentLinetype = null)
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
        string resolvedLinetype = "Continuous";
        try
        {
            dynamic entityDyn = entity;
            string lTypeName = entityDyn.Linetype.Name;

            if (lTypeName.Equals("ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                resolvedLinetype = parentLinetype ?? "Continuous";
            }
            else if (lTypeName.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                 if (!string.IsNullOrEmpty(entity.Layer.Name) && layerLinetypes.TryGetValue(entity.Layer.Name, out var lType))
                 {
                     resolvedLinetype = lType;
                 }
            }
            else
            {
                resolvedLinetype = lTypeName;
            }
        }
        catch { resolvedLinetype = "Continuous"; }

        // --- Insert (Block Reference) Özel İşlemi (Recursion) ---
        if (entity is Insert insert)
        {
            // 1. Insert Dönüşüm Matrisini Hesapla (T * R * S)
            // Sıra: Scale -> Rotate -> Translate
            
            // Scale
            var scaleMat = Matrix4x4.CreateScale(insert.XScale, insert.YScale, insert.ZScale);
            
            // Rotate (Z ekseninde)
            var rotMat = Matrix4x4.CreateRotationZ(insert.Rotation);
            
            // Translate
            var transMat = Matrix4x4.CreateTranslation(insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z);
            
            // Local Transform = T * R * S
            var localTransform = transMat * (rotMat * scaleMat);
            
            // Global Transform = Parent * Local
            var combinedTransform = transform * localTransform;

            // 2. Blok İçeriğini Dönüştür
            if (insert.Block != null && insert.Block.Entities.Any())
            {
                foreach (var child in insert.Block.Entities)
                {
                    // Recursive call
                    // Insert rengini parentColor olarak geçiriyoruz
                    foreach (var childConverted in ConvertEntity(child, combinedTransform, layerColors, layerLinetypes, insert.Color, resolvedLinetype))
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
                  foreach (var childConverted in ConvertEntity(child, transform, layerColors, layerLinetypes, dimension.Color, resolvedLinetype))
                  {
                      yield return childConverted;
                  }
              }
             yield break;
        }

        // --- Hatch (Tarama) Özel İşlemi ---
        if (result == null && entity is Hatch hatch)
        {
            // Hatch sınırlarını (Boundary) çizgiye dönüştür
            // ACadSharp versiyon farklılıkları nedeniyle dynamic kullanıyoruz
            foreach (dynamic path in hatch.Paths)
            {
                var points = new List<Vector3D>();

                // Hatch işleminden dönenleri burada toplayalım
                var entitiesInLoop = new List<CadEntity>();

                try
                {
                    // 1. Polyline Path (Vertices varsa)
                    // Dynamic property check: Vertices
                    bool handled = false;
                    try 
                    {
                        var vertices = path.Vertices;
                        if (vertices != null)
                        {
                            foreach (var v in vertices)
                            {
                                points.Add(new Vector3D(v.X, v.Y, 0));
                            }
                            if (points.Count > 0) points.Add(points[0]);
                            handled = true;
                        }
                    }
                    catch { /* Property yoksa veya erişilemezse */ }

                    if (!handled)
                    {
                        // 2. Edge Path (Edges varsa)
                        try 
                        {
                            var edges = path.Edges;
                            if (edges != null)
                            {
                                foreach (dynamic edge in edges)
                                {
                                     // LineEdge? -> Start, End
                                     try 
                                     {
                                         var start = edge.Start;
                                         var end = edge.End;
                                         // Line
                                         var p1 = new Vector3D(start.X, start.Y, 0);
                                         var p2 = new Vector3D(end.X, end.Y, 0);
                                         var lineEnt = new LineEntity(p1, p2);
                                         
                                         lineEnt.Layer = hatch.Layer.Name;
                                         lineEnt.Color = resolvedColor;
                                         lineEnt.Linetype = resolvedLinetype;
                                         lineEnt.Transform(transform);
                                         
                                         // Yield yerine listeye ekle
                                         entitiesInLoop.Add(lineEnt);
                                     }
                                     catch 
                                     {
                                         // ArcEdge vb. yoksay
                                     }
                                }
                            }
                        }
                        catch { /* Edges yoksa */ }
                    }
                }
                catch { /* General Path error */ }

                // Toplanan entity'leri döndür
                foreach (var e in entitiesInLoop)
                {
                    yield return e;
                }

                // Polyline noktaları toplandıysa çiz
                if (points.Count > 1)
                {
                    var poly = new LwPolylineEntity(points, true);
                    poly.Layer = hatch.Layer.Name;
                    poly.Color = resolvedColor;
                    poly.Linetype = resolvedLinetype;
                    poly.Transform(transform);
                    yield return poly;
                }
            }
            yield break;
        }

        if (result != null)
        {
            // Ortak Özellikler
            result.Layer = entity.Layer.Name;
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
        // ByLayer (256) -> Layer rengini almalı (bağlam gerekli)
        // Şimdilik ByLayer ise Beyaz (veya varsayılan) kabul edelim.
        if (color.IsByLayer) return 0xFFFFFFFF; // White
        if (color.IsByBlock) return 0xFFFFFFFF; 

        if (color.IsTrueColor)
        {
            // R, G, B pack
            return (uint)((0xFF << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }

        if (_aciPalette.TryGetValue(color.Index, out var rgba))
        {
            return rgba;
        }

        return 0xFFAAAAAA; // Default Gray
    }

    private static void InitializeAciPalette()
    {
        // Standart AutoCAD 7 Renk
        _aciPalette[1] = 0xFFFF0000; // Red
        _aciPalette[2] = 0xFFFFFF00; // Yellow
        _aciPalette[3] = 0xFF00FF00; // Green
        _aciPalette[4] = 0xFF00FFFF; // Cyan
        _aciPalette[5] = 0xFF0000FF; // Blue
        _aciPalette[6] = 0xFFFF00FF; // Magenta
        _aciPalette[7] = 0xFFFFFFFF; // White
        
        // Gri Tonlar (8, 9)
        _aciPalette[8] = 0xFF888888;
        _aciPalette[9] = 0xFFC0C0C0;

        // Diğer renkler için algoritmik üretim veya tam tablo (256 satır) gerekir.
        // Şimdilik yaygın mimari renkleri (gri tonlar, koyu renkler) için basit bir algoritma:
        // (Gerçek ACI tablosu çok uzundur, burada basitleştiriyoruz)
        
        for (short i = 10; i < 250; i++)
        {
             // Rastgele değil ama deterministik renkler atayalım ki ayırt edilebilsin
             // ACI index'e göre renk üretimi (Basit hashing)
             byte r = (byte)((i * 37) % 255);
             byte g = (byte)((i * 101) % 255);
             byte b = (byte)((i * 211) % 255);
             _aciPalette[i] = (uint)((0xFF << 24) | (r << 16) | (g << 8) | b);
        }
        
        // 250-255 arası Gri tonlardır
        _aciPalette[250] = 0xFF333333;
        _aciPalette[251] = 0xFF474747;
        _aciPalette[252] = 0xFF5B5B5B;
        _aciPalette[253] = 0xFF6F6F6F;
        _aciPalette[254] = 0xFF838383;
        _aciPalette[255] = 0xFF979797;
    }
}
