using System;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

using AfneyLine     = Afney.Cad.Domain.Entities.Basic.LineEntity;
using AfneyCircle   = Afney.Cad.Domain.Entities.Basic.CircleEntity;
using AfneyArc      = Afney.Cad.Domain.Entities.Basic.ArcEntity;
using AfneyText     = Afney.Cad.Domain.Entities.Basic.TextEntity;
using AfneyPolyline = Afney.Cad.Domain.Entities.Basic.LwPolylineEntity;

namespace Afney.Cad.Infrastructure.Export;

// CadDatabase → ACadSharp.CadDocument dönüşümü — DWG ve DXF writer'lar tarafından
// ortak kullanılır. Bir yerde düzeltilen entity-dönüşüm hatası (örn. boru genişliği)
// otomatik olarak hem DWG hem DXF çıktısına yansır — iki ayrı kod yolu senkronizasyon
// dışı kalmaz.
public static class AcadSharpDocumentBuilder
{
    public static CadDocument Build(CadDatabase database)
    {
        var doc = new CadDocument();

        /*
           MÜHENDİSLİK: $INSUNITS hiç ayarlanmıyordu — ACadSharp'ın CadDocument varsayılanına
           (Unitless) bırakılıyordu. AfneyCAD içi round-trip'i etkilemiyor (DwgImportService/
           DxfImportService Unitless için de unitScale=1.0 varsayıyor, ki bu mm-native
           koordinatlarımızla zaten doğru sonuç veriyor) ama GERÇEK AutoCAD/başka bir MEP
           yazılımına aktarılan dosyalarda (asıl DXF/DWG export amacı budur) birim meta verisi
           eksik kalıyordu. Uygulamanın evrensel iç birimi mm olduğundan bunu açıkça belirtmek
           (INSUNITS=4) hem doğru hem de bedelsiz.
        */
        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Millimeters;

        foreach (var layer in database.GetLayers())
        {
            if (doc.Layers.Contains(layer.Name)) continue;
            var acadLayer = new Layer(layer.Name)
            {
                Color = RgbToAcadColor(layer.Color),
                IsOn  = layer.IsVisible,
            };
            if (layer.IsFrozen)
                acadLayer.Flags |= LayerFlags.Frozen;
            doc.Layers.Add(acadLayer);
        }

        foreach (var entity in database.GetAllEntities())
        {
            var acEnt = ToAcad(entity);
            if (acEnt is null) continue;

            string ln = string.IsNullOrEmpty(entity.Layer) ? "0" : entity.Layer;
            if (!doc.Layers.Contains(ln)) doc.Layers.Add(new Layer(ln));
            acEnt.Layer = doc.Layers[ln];
            acEnt.Color = RgbToAcadColor(entity.Color);

            doc.ModelSpace.Entities.Add(acEnt);
        }

        return doc;
    }

    private static ACadSharp.Entities.Entity? ToAcad(CadEntity e) => e switch
    {
        AfneyLine      l    => new Line         { StartPoint = V(l.StartPoint),  EndPoint = V(l.EndPoint) },
        AfneyCircle    c    => new Circle       { Center     = V(c.Center),       Radius   = c.Radius },
        AfneyArc       a    => new Arc          { Center     = V(a.Center),       Radius   = a.Radius,
                                                  StartAngle = Deg2Rad(a.StartAngle),
                                                  EndAngle   = Deg2Rad(a.EndAngle) },
        AfneyText      t    => ToAcadText(t),
        AfneyPolyline  pl   => ToAcadPolyline(pl),
        PipeEntity     pipe => ToPipePolyline(pipe),
        _                   => null
    };

    private static ACadSharp.Entities.TextEntity ToAcadText(AfneyText t)
    {
        return new ACadSharp.Entities.TextEntity
        {
            Value       = t.Text,
            Height      = t.Height,
            Rotation    = Deg2Rad(t.Rotation),
            InsertPoint = V(t.Position),
        };
    }

    private static LwPolyline ToAcadPolyline(AfneyPolyline p)
    {
        var lw = new LwPolyline();
        foreach (var v in p.Vertices)
            lw.Vertices.Add(new LwPolyline.Vertex(V2(v)));
        lw.IsClosed = p.IsClosed;
        return lw;
    }

    private static LwPolyline ToPipePolyline(PipeEntity pipe)
    {
        // Boru, gerçek dış çapında (mm) çizilir — InnerDiameter zaten mm cinsinden.
        double width = pipe.InnerDiameter > 0 ? pipe.InnerDiameter : 1.0;
        var lw = new LwPolyline();
        lw.Vertices.Add(new LwPolyline.Vertex(V2(pipe.StartPoint)) { StartWidth = width, EndWidth = width });
        lw.Vertices.Add(new LwPolyline.Vertex(V2(pipe.EndPoint))   { StartWidth = width, EndWidth = width });
        return lw;
    }

    private static CSMath.XYZ V(Vector3D v) => new(v.X, v.Y, v.Z);
    private static CSMath.XY  V2(Vector3D v) => new(v.X, v.Y);
    private static double Deg2Rad(double deg) => deg * Math.PI / 180.0;

    private static Color RgbToAcadColor(uint argb)
    {
        byte r = (byte)((argb >> 16) & 0xFF);
        byte g = (byte)((argb >>  8) & 0xFF);
        byte b = (byte)(argb & 0xFF);
        return new Color(r, g, b);
    }
}
