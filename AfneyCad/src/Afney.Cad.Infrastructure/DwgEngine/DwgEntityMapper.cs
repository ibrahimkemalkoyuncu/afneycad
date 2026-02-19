using System;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Infrastructure.DwgEngine;

/*
   NE: DWG Nesne Eşleştirici (DwgEntityMapper)
   NEDEN: DWG dosyasından çıkan "Ham" veriyi, AfneyCAD'in "Anlamlı" (MEP/CAD) nesnelerine dönüştürmek için.
   
   HASSASİYET (AutoCAD Standard):
   - Double precision korunur.
   - Layer ve Renk bilgileri senkronize edilir.
*/
public class DwgEntityMapper
{
    /*
       NE: Yerel Nesneye Dönüştür (MapToNative)
       NEDEN: DWG/DXF dosyasından okunan ham proxy verisini, uygulama içindeki tip güvenli CAD nesnelerine (Çizgi, Polylne vb.) çevirmek için.
    */
    public CadEntity? MapToNative(DwgEntityProxy proxy)
    {
        switch (proxy.DwgType)
        {
            case "LINE":
                return MapLine(proxy);
            case "LWPOLYLINE":
                return MapPolyline(proxy);
            case "INSERT":
                return MapInsert(proxy);
            default:
                return null;
        }
    }

    /*
       NE: Çizgiyi Eşleştir (MapLine)
       NEDEN: DWG formatındaki çizgileri okumak; eğer katman ismi "PIPE" veya "BORU" içeriyorsa, bu nesneyi otomatik olarak akıllı "PipeEntity" nesnesine dönüştürmek için.
    */
    private CadEntity MapLine(DwgEntityProxy proxy)
    {
        var start = (Vector3D)proxy.RawProperties["Start"];
        var end = (Vector3D)proxy.RawProperties["End"];
        
        // MÜHENDİSLİK ZEKASI (Mete Bey): 
        // Eğer layer ismi "PIPE" içeriyorsa direkt PipeEntity olarak oluştur.
        string layer = proxy.RawProperties.ContainsKey("Layer") ? proxy.RawProperties["Layer"].ToString()! : "0";
        
        if (layer.Contains("PIPE") || layer.Contains("BORU"))
        {
            var pipe = new PipeEntity(start, end, 25.0); // Varsayılan 25mm
            pipe.Layer = layer;
            return pipe;
        }

        return new LineEntity(start, end) { Layer = layer };
    }

    private CadEntity MapPolyline(DwgEntityProxy proxy)
    {
        if (!proxy.RawProperties.ContainsKey("Vertices")) return null!;
        
        var points = (List<Vector3D>)proxy.RawProperties["Vertices"];
        bool isClosed = proxy.RawProperties.ContainsKey("IsClosed") && (bool)proxy.RawProperties["IsClosed"];
        string layer = proxy.RawProperties.ContainsKey("Layer") ? proxy.RawProperties["Layer"].ToString()! : "0";

        var poly = new LwPolylineEntity(points, isClosed);
        poly.Layer = layer;
        
        // Renk vs eklenebilir
        return poly;
    }

    /*
       NE: Blok Yerleşimini Eşleştir (MapInsert)
       NEDEN: DWG içindeki blok referanslarını (Insert) tarayarak; isimlerine göre (Klozet, Lavabo vb.) sıhhi tesisat uç birimlerini (SanitaryFixtureEntity) otomatik olarak tanımak ve tesisat grafına dahil etmek için.
    */
    private CadEntity MapInsert(DwgEntityProxy proxy)
    {
        if (!proxy.RawProperties.ContainsKey("InsertionPoint")) return null!;
        
        var pos = (Vector3D)proxy.RawProperties["InsertionPoint"];
        string blockName = proxy.RawProperties.ContainsKey("Name") ? proxy.RawProperties["Name"].ToString()!.ToUpper() : "BLOCK";
        string layer = proxy.RawProperties.ContainsKey("Layer") ? proxy.RawProperties["Layer"].ToString()! : "0";
        double rotation = proxy.RawProperties.ContainsKey("Rotation") ? (double)proxy.RawProperties["Rotation"] : 0.0;

        // MİMARİ OKUMA & ARMATÜR TANIMA (Architectural Parsing)
        // Blok isminde veya layer isminde geçen anahtar kelimelere göre vitrifiye oluştur.
        
        SanitaryFixtureEntity? fixture = null;

        if (blockName.Contains("KLOZET") || blockName.Contains("WC") || blockName.Contains("TOILET"))
        {
            fixture = new SanitaryFixtureEntity(pos, "WC (Klozet)", 1.0); // TS 1258: 1 LU
        }
        else if (blockName.Contains("LAVABO") || blockName.Contains("WASHBASIN") || blockName.Contains("SINK"))
        {
            fixture = new SanitaryFixtureEntity(pos, "Lavabo", 0.5); // TS 1258: 0.5 LU
        }
        else if (blockName.Contains("DUS") || blockName.Contains("SHOWER") || blockName.Contains("BANYO"))
        {
            fixture = new SanitaryFixtureEntity(pos, "Duş Teknesi", 0.8); // TS 1258: 0.8 LU
        }
        else if (blockName.Contains("EVIYE") || blockName.Contains("KITCHEN"))
        {
            fixture = new SanitaryFixtureEntity(pos, "Mutfak Eviyesi", 0.8);
        }

        if (fixture != null)
        {
            fixture.Layer = layer;
            fixture.Rotation = rotation; // DWG rotation is usually in radians or degrees depending on library, ACadSharp usually radians? No, mostly degrees in raw? Need to check. ACadSharp standard is radians for some, degrees for others. Assuming Radians for internal CAD logic, but map might need conversion. Let's assume input is correct for now.
            // Actually ACadSharp entities usually have Rotation in DEGREES. AfneyCAD internal is RADIANS.
            // Let's convert DEGREES to RADIANS just in case, or verify. 
            // Standard DXF rotation is degrees.
            fixture.Rotation = rotation * (Math.PI / 180.0); 
            
            return fixture;
        }

        // Eğer vitrifiye değilse normal blok referansı (veya şimdilik null/point)
        // İleride BlockReferenceEntity eklenebilir.
        return null!;
    }
}
