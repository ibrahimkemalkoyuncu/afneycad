using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;

namespace Afney.Cad.Infrastructure.DwgEngine;

/*
   NE: DWG İç CAD Modeli (DwgDocument)
   NEDEN: AutoCAD'in dosya formatına özel tüm verileri (Handles, XRefs, Blocks) içeren, bağımsız bir bellek içi temsil sunmak için.
*/
public class DwgDocument
{
    public DwgHeader Header { get; set; } = new();
    public List<DwgLayer> Layers { get; } = new();
    public List<DwgBlockTableRecord> Blocks { get; } = new();
    public List<DwgEntityProxy> Entities { get; } = new();
    
    // AutoCAD Handle Yönetimi (0x01, 0x02...)
    public Dictionary<long, object> ObjectMap { get; } = new();

    public void AddObject(long handle, object obj)
    {
        ObjectMap[handle] = obj;
    }
}

public class DwgHeader
{
    public string Version { get; set; } = "AC1032"; // R2018
    public double DrawingScale { get; set; } = 1.0;
    public string Units { get; set; } = "Millimeters";
}

public class DwgLayer
{
    public string Name { get; set; } = "0";
    public string Color { get; set; } = "#FFFFFF";
    public bool IsLocked { get; set; }
}

public class DwgBlockTableRecord
{
    public string Name { get; set; } = "";
    public List<DwgEntityProxy> Entities { get; } = new();
}

/*
   NE: DWG Nesne Temsili (DwgEntityProxy)
   NEDEN: Binary veriden çözülen özellikleri ve AutoCAD "Handle" bilgilerini tutan geçici nesne.
*/
public class DwgEntityProxy
{
    public long Handle { get; set; }
    public string DwgType { get; set; } = "";
    public Dictionary<string, object> RawProperties { get; } = new();
    public CadEntity? NativeEntity { get; set; }
}
