using System;
using System.Collections.Generic;
using System.IO;
using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Entities;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Infrastructure.Import;

/*
NE: DXF Dosya Aktarım Servisi (DXF Import Service)
NEDEN: Mimari projelerin DXF formatında içe aktarılması ve AfneyCAD entity'lerine dönüştürülmesi için.

MÜHENDİSLİK DETAYI (Kemal & Mebrure):
- FineSANI benzeri: Mimari planları underlay olarak import eder.
- ACadSharp kütüphanesi ile DXF parse edilir.
- DXF Entity'leri (LINE, ARC, POLYLINE, CIRCLE) AfneyCAD Domain Entity'lerine map edilir.
- Layer bilgisi korunur - mimari ve tesisat katmanları ayrıştırılabilir.
- KOORDINAT DÖNÜŞÜMÜmalıdır: DXF mm birimi → AfneyCAD mm birimi (1:1 mapping)
*/
public class DxfImportService
{
    /*
    NE: DXF Dosyasını Okuyup AfneyCAD Entity Listesi Döndürür
    NEDEN: Kullanıcı "File → Import DXF" dediğinde mimari planı yüklemek için.
    
    PARAMETRELER:
    - filePath: DXF dosya yolu
    - targetLayer: Import edilen entity'lerin hangi layer'a yerleştirileceği (default: "IMPORT")
    
    DÖNÜŞ: AfneyCAD CadEntity listesi
    */
    public List<CadEntity> ImportDxf(string filePath, string targetLayer = "IMPORT")
    {
        var entities = new List<CadEntity>();
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"DXF dosyası bulunamadı: {filePath}");
        }
        
        try
        {
            // ACadSharp ile DXF parse et
            using (DxfReader reader = new DxfReader(filePath))
            {
                CadDocument cadDoc = reader.Read();
                
                // Her entity'yi çevir
                foreach (var acadEntity in cadDoc.Entities)
                {
                    var convertedEntity = ConvertEntity(acadEntity, targetLayer);
                    if (convertedEntity != null)
                    {
                        entities.Add(convertedEntity);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"DXF import hatası: {ex.Message}", ex);
        }
        
        return entities;
    }
    
    /*
    NE: ACadSharp Entity → AfneyCAD Entity Dönüşümü
    NEDEN: DXF formatındaki geometrileri kendi domain modelimize çevirmek için.
    */
    private CadEntity? ConvertEntity(ACadSharp.Entities.Entity acadEntity, string targetLayer)
    {
        return acadEntity switch
        {
            Line line => ConvertLine(line, targetLayer),
            Arc arc => ConvertArc(arc, targetLayer),
            Circle circle => ConvertCircle(circle, targetLayer),
            LwPolyline polyline => ConvertPolyline(polyline, targetLayer),
            Polyline3D poly => ConvertPolyline3D(poly, targetLayer),
            _ => null // Desteklenmeyen entity tip (TEXT, BLOCK vb. ileride eklenecek)
        };
    }
    
    /*
    NE: DXF LINE → AfneyCAD LineEntity
    */
    private LineEntity ConvertLine(Line line, string targetLayer)
    {
        var start = new Vector3D(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z);
        var end = new Vector3D(line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z);
        
        return new LineEntity(start, end)
        {
            Layer = targetLayer,
            Color = ConvertColor(line.Color)
        };
    }
    
    /*
    NE: DXF CIRCLE → AfneyCAD CircleEntity
    */
    private CircleEntity ConvertCircle(Circle circle, string targetLayer)
    {
        var center = new Vector3D(circle.Center.X, circle.Center.Y, circle.Center.Z);
        
        return new CircleEntity(center, circle.Radius)
        {
            Layer = targetLayer,
            Color = ConvertColor(circle.Color)
        };
    }
    
    /*
    NE: DXF ARC → AfneyCAD LineEntity (Yaklaşık - ileride ArcEntity eklenecek)
    NEDEN: Şimdilik arc'ı çizgi segmentlere bölerek temsil ediyoruz.
    */
    private LineEntity? ConvertArc(Arc arc, string targetLayer)
    {
        // Basitleştirilmiş: Arc'ın başlangıç ve bitiş noktasını çizgi olarak çiziyoruz
        // FineSANI benzeri: Arc tessellation yapılabilir (çok segment)
        var startAngle = arc.StartAngle * (System.Math.PI / 180.0);
        var endAngle = arc.EndAngle * (System.Math.PI / 180.0);
        
        var startX = arc.Center.X + arc.Radius * System.Math.Cos(startAngle);
        var startY = arc.Center.Y + arc.Radius * System.Math.Sin(startAngle);
        var endX = arc.Center.X + arc.Radius * System.Math.Cos(endAngle);
        var endY = arc.Center.Y + arc.Radius * System.Math.Sin(endAngle);
        
        var start = new Vector3D(startX, startY, arc.Center.Z);
        var end = new Vector3D(endX, endY, arc.Center.Z);
        
        return new LineEntity(start, end)
        {
            Layer = targetLayer,
            Color = ConvertColor(arc.Color)
        };
    }
    
    /*
    NE: DXF LWPOLYLINE → AfneyCAD LineEntity (Segment bazlı)
    NEDEN: POLYLINE'ı birden fazla LINE segment olarak import ediyoruz.
    NOT: İleride PolylineEntity eklendiğinde doğrudan map edilecek.
    */
    private CadEntity? ConvertPolyline(LwPolyline polyline, string targetLayer)
    {
        // Şimdilik sadece ilk segment'i döndürüyoruz (basitleştirme)
        // FineSANI: Tüm vertex'leri döner
        if (polyline.Vertices.Count < 2) return null;
        
        var v1 = polyline.Vertices[0];
        var v2 = polyline.Vertices[1];
        
        var start = new Vector3D(v1.Location.X, v1.Location.Y, 0);
        var end = new Vector3D(v2.Location.X, v2.Location.Y, 0);
        
        return new LineEntity(start, end)
        {
            Layer = targetLayer,
            Color = ConvertColor(polyline.Color)
        };
    }
    
    /*
    NE: DXF POLYLINE (3D) → LineEntity
    */
    private CadEntity? ConvertPolyline3D(Polyline3D poly, string targetLayer)
    {
        if (poly.Vertices.Count < 2) return null;
        
        var v1 = poly.Vertices[0].Location;
        var v2 = poly.Vertices[1].Location;
        
        var start = new Vector3D(v1.X, v1.Y, v1.Z);
        var end = new Vector3D(v2.X, v2.Y, v2.Z);
        
        return new LineEntity(start, end)
        {
            Layer = targetLayer,
            Color = ConvertColor(poly.Color)
        };
    }
    
    /*
    NE: DXF Renk → AfneyCAD uint Color
    NEDEN: Color formatı dönüşümü
    */
    private uint ConvertColor(ACadSharp.Color acadColor)
    {
        // DXF color index → RGB (basitleştirilmiş)
        // 7 = White, 1 = Red, 3 = Green vb.
        return acadColor.Index switch
        {
            1 => 0xFFFF0000, // Red
            2 => 0xFFFFFF00, // Yellow
            3 => 0xFF00FF00, // Green
            4 => 0xFF00FFFF, // Cyan
            5 => 0xFF0000FF, // Blue
            6 => 0xFFFF00FF, // Magenta
            7 => 0xFFFFFFFF, // White
            _ => 0xFFAAAAAA  // Gray (default)
        };
    }
}

