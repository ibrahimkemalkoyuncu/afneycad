using System;
using System.Collections.Generic;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Mechanical.Entities;

using AfneyLine     = Afney.Cad.Domain.Entities.Basic.LineEntity;
using AfneyCircle   = Afney.Cad.Domain.Entities.Basic.CircleEntity;
using AfneyArc      = Afney.Cad.Domain.Entities.Basic.ArcEntity;
using AfneyText     = Afney.Cad.Domain.Entities.Basic.TextEntity;
using AfneyPolyline = Afney.Cad.Domain.Entities.Basic.LwPolylineEntity;
using AfneySpline   = Afney.Cad.Domain.Entities.Basic.SplineEntity;
using AfneyHatch    = Afney.Cad.Domain.Entities.Basic.HatchEntity;
using AfneySolid    = Afney.Cad.Domain.Entities.Basic.SolidEntity;
using AfneyDimension = Afney.Cad.Domain.Entities.Annotation.DimensionEntity;

namespace Afney.Cad.Infrastructure.Export;

// CadDatabase → ACadSharp.CadDocument dönüşümü — DWG ve DXF writer'lar tarafından
// ortak kullanılır. Bir yerde düzeltilen entity-dönüşüm hatası (örn. boru genişliği)
// otomatik olarak hem DWG hem DXF çıktısına yansır — iki ayrı kod yolu senkronizasyon
// dışı kalmaz.
public static class AcadSharpDocumentBuilder
{
    public static CadDocument Build(CadDatabase database) => Build(database, out _);

    /*
       NE: CadDatabase → ACadSharp.CadDocument (Atlanan Entity Raporuyla)
       NEDEN: ToAcad() switch'i yalnızca 6 entity tipini (Line/Circle/Arc/Text/Polyline/Pipe)
              destekliyor — Spline/Hatch/Dimension/Solid/çoğu MEP fitting'i (Elbow/Tee/Duct/
              Wall/Room vb.) eşleşmeyip `null` dönüyor ve `continue` ile HİÇBİR UYARI OLMADAN
              atlanıyordu. Kullanıcı DWG/DXF'i gerçek AutoCAD'de açtığında nesnelerin neden
              eksik olduğunu asla öğrenemiyordu. Artık atlanan her entity tipi ve adedi
              toplanıp çağırana (bkz. DwgExportService) döndürülüyor; UI katmanı bunu
              kullanıcıya gösterebilir. Dönüştürme mantığının kendisi DEĞİŞMEDİ — sadece
              görünürlük eklendi.
    */
    public static CadDocument Build(CadDatabase database, out IReadOnlyList<string> skippedEntitySummary)
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

        var skippedCounts = new Dictionary<string, int>();

        foreach (var entity in database.GetAllEntities())
        {
            // MÜHENDİSLİK: ToAcad artık bir liste döndürüyor — SolidEntity gibi tipler tek bir
            // ACadSharp nesnesine karşılık gelmiyor (bir B-Rep, üçgenlenip HER üçgen ayrı bir
            // 3DFACE olarak yazılmalı). Tek Entity? dönen eski tasarımda bu tür entity'ler
            // temsil edilemiyordu.
            var acEntities = ToAcad(entity);
            if (acEntities.Count == 0)
            {
                string typeName = entity.GetType().Name;
                skippedCounts[typeName] = skippedCounts.TryGetValue(typeName, out int n) ? n + 1 : 1;
                continue;
            }

            string ln = string.IsNullOrEmpty(entity.Layer) ? "0" : entity.Layer;
            if (!doc.Layers.Contains(ln)) doc.Layers.Add(new Layer(ln));

            foreach (var acEnt in acEntities)
            {
                acEnt.Layer = doc.Layers[ln];
                acEnt.Color = RgbToAcadColor(entity.Color);
                doc.ModelSpace.Entities.Add(acEnt);
            }
        }

        skippedEntitySummary = skippedCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key} ({kv.Value} adet)")
            .ToList();

        return doc;
    }

    private static List<ACadSharp.Entities.Entity> ToAcad(CadEntity e)
    {
        ACadSharp.Entities.Entity? single = e switch
        {
            AfneyLine      l    => new Line   { StartPoint = V(l.StartPoint), EndPoint = V(l.EndPoint) },
            AfneyCircle    c    => new Circle { Center     = V(c.Center),     Radius   = c.Radius },
            AfneyArc       a    => new Arc    { Center     = V(a.Center),     Radius   = a.Radius,
                                                StartAngle = Deg2Rad(a.StartAngle),
                                                EndAngle   = Deg2Rad(a.EndAngle) },
            AfneyText      t    => ToAcadText(t),
            AfneyPolyline  pl   => ToAcadPolyline(pl),
            PipeEntity     pipe => ToPipePolyline(pipe),
            DuctEntity     duct => ToDuctPolyline(duct),
            AfneySpline    sp   => ToAcadSpline(sp),
            AfneyHatch     h    => ToHatchBoundaryPolyline(h),
            _                   => null
        };

        if (single != null) return new List<ACadSharp.Entities.Entity> { single };
        if (e is AfneySolid solid) return ToAcadFaces(solid);

        // MÜHENDİSLİK (TB-02): DimensionEntity/ElbowEntity/TeeEntity/WallEntity/RoomEntity
        // önceden switch'te eşleşmiyordu — `null` dönüp sessizce atlanıyordu. Her biri birden
        // fazla ACadSharp nesnesine karşılık geldiğinden (ölçü = çizgiler+metin, dirsek/T-parçası
        // = birkaç çizgi) `single` yerine doğrudan liste döndürüyorlar.
        if (e is AfneyDimension dim) return ToAcadDimensionParts(dim);
        if (e is ElbowEntity elbow) return ToAcadElbowLines(elbow);
        if (e is TeeEntity tee) return ToAcadTeeLines(tee);
        if (e is WallEntity wall) return new List<ACadSharp.Entities.Entity> { ToAcadWallPolyline(wall) };
        if (e is RoomEntity room) return new List<ACadSharp.Entities.Entity> { ToAcadPolyline(room.Boundary) };

        return new List<ACadSharp.Entities.Entity>();
    }

    /*
       NE: Ölçü → Temel Bileşenler (ToAcadDimensionParts)
       NEDEN: Gerçek bir AutoCAD DIMENSION nesnesi kurmak bir DimensionStyle (DIMSTYLE
              tablosu) referansı gerektirir — ACadSharp'ta bu kurulum, sadece dışa aktarım
              için orantısız bir karmaşıklık. `DimensionEntity.ExplodeToBasicEntities()`
              (EXPLODE komutunun da kullandığı, zaten test edilmiş metot) ölçüyü çizim
              mantığıyla AYNI geometriyi kullanarak uzatma çizgileri + ölçü çizgisi + metne
              ayırıyor — bu temel parçalar (Line/Text) zaten `ToAcad`'ın desteklediği
              tiplerdir, tekrar `ToAcad`'a verilip birleştiriliyor. Sonuç: AutoCAD'de
              düzenlenebilir bir DIMENSION nesnesi DEĞİL ama GÖRÜNÜR (önceden tamamen
              kayboluyordu) ve geometrik olarak doğru bir ölçü çizimi.
    */
    private static List<ACadSharp.Entities.Entity> ToAcadDimensionParts(AfneyDimension dim)
    {
        var result = new List<ACadSharp.Entities.Entity>();
        foreach (var part in dim.ExplodeToBasicEntities())
            result.AddRange(ToAcad(part));
        return result;
    }

    /*
       NE: Dirsek → İki Çizgi (ToAcadElbowLines)
       NEDEN: `ElbowEntity.Draw`'daki AYNI basit görselleştirme (gelen + giden boru
              doğrultusu, merkezden iki çizgi) — gerçek bir dirsek gövdesi/süpürülmüş
              yüzey modeli DEĞİL, ama önceden TAMAMEN kaybolan bir elemanın en azından
              plan görünümünde yerini ve yönünü doğru gösteriyor.
    */
    private static List<ACadSharp.Entities.Entity> ToAcadElbowLines(ElbowEntity elbow)
    {
        var p1 = elbow.Center - elbow.IncomingVector * elbow.Radius;
        var p2 = elbow.Center + elbow.OutgoingVector * elbow.Radius;
        return new List<ACadSharp.Entities.Entity>
        {
            new Line { StartPoint = V(p1), EndPoint = V(elbow.Center) },
            new Line { StartPoint = V(elbow.Center), EndPoint = V(p2) }
        };
    }

    /*
       NE: T-Parçası → İki Çizgi (ToAcadTeeLines)
       NEDEN: `TeeEntity.Draw` ile AYNI basitleştirme — ana hat + dal hattı iki çizgi
              olarak yazılır (gerçek T-parçası gövdesi değil, plan görünümü için yeterli).
    */
    private static List<ACadSharp.Entities.Entity> ToAcadTeeLines(TeeEntity tee)
    {
        double length = tee.MainDiameter * 2.5;
        var p1 = tee.Center - tee.MainDirection * (length / 2);
        var p2 = tee.Center + tee.MainDirection * (length / 2);
        var pBranch = tee.Center + tee.BranchDirection * (length / 2);
        return new List<ACadSharp.Entities.Entity>
        {
            new Line { StartPoint = V(p1), EndPoint = V(p2) },
            new Line { StartPoint = V(tee.Center), EndPoint = V(pBranch) }
        };
    }

    /*
       NE: Mimari Duvar → Kapalı Polyline (ToAcadWallPolyline)
       NEDEN: `WallEntity.Draw` ile AYNI geometri — merkez hattı, ThicknessMm genişliğinde
              bir dikdörtgene (4 köşe) açılır. `ToPipePolyline`'ın (genişlikli iki-nokta
              polyline) AKSİNE burada gerçek bir KAPALI dikdörtgen üretiliyor çünkü duvar
              görsel olarak dolgu değil kontur çizgileriyle (iç/dış yüz) temsil ediliyor.
    */
    private static LwPolyline ToAcadWallPolyline(WallEntity wall)
    {
        var dir = wall.EndPoint - wall.StartPoint;
        double len = dir.Length();
        var lw = new LwPolyline { IsClosed = true };
        if (len < 1e-9)
        {
            lw.Vertices.Add(new LwPolyline.Vertex(V2(wall.StartPoint)));
            return lw;
        }

        var norm = new Vector3D(-dir.Y / len, dir.X / len, 0);
        double halfT = wall.ThicknessMm / 2.0;

        var p1 = new Vector3D(wall.StartPoint.X + norm.X * halfT, wall.StartPoint.Y + norm.Y * halfT, 0);
        var p2 = new Vector3D(wall.StartPoint.X - norm.X * halfT, wall.StartPoint.Y - norm.Y * halfT, 0);
        var p3 = new Vector3D(wall.EndPoint.X - norm.X * halfT, wall.EndPoint.Y - norm.Y * halfT, 0);
        var p4 = new Vector3D(wall.EndPoint.X + norm.X * halfT, wall.EndPoint.Y + norm.Y * halfT, 0);

        lw.Vertices.Add(new LwPolyline.Vertex(V2(p1)));
        lw.Vertices.Add(new LwPolyline.Vertex(V2(p2)));
        lw.Vertices.Add(new LwPolyline.Vertex(V2(p3)));
        lw.Vertices.Add(new LwPolyline.Vertex(V2(p4)));
        return lw;
    }

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

    /*
       NE: Kanal → Genişlikli Polyline (ToDuctPolyline)
       NEDEN: Önceden DuctEntity (kanal — en yaygın HVAC nesnelerinden biri) hiç desteklenmiyordu,
              dışa aktarımda tamamen kayboluyordu. Pipe ile aynı yaklaşım: merkez hattı, kanal
              genişliğinde (WidthMm) kalın bir polyline olarak yazılır — dairesel kanal için
              DiameterMm kullanılır.
    */
    private static LwPolyline ToDuctPolyline(DuctEntity duct)
    {
        double width = duct.Shape == DuctShape.Circular
            ? (duct.DiameterMm > 0 ? duct.DiameterMm : 1.0)
            : (duct.WidthMm > 0 ? duct.WidthMm : 1.0);
        var lw = new LwPolyline();
        lw.Vertices.Add(new LwPolyline.Vertex(V2(duct.StartPoint)) { StartWidth = width, EndWidth = width });
        lw.Vertices.Add(new LwPolyline.Vertex(V2(duct.EndPoint))   { StartWidth = width, EndWidth = width });
        return lw;
    }

    /*
       NE: Spline → ACadSharp Spline (ToAcadSpline)
       NEDEN: Önceden SplineEntity hiç desteklenmiyordu. DXF'in hand-rolled metin yazıcısı
              (DxfWriterService) spline'ı kontrol noktaları arasında düz ÇİZGİLERLE yaklaşık
              çiziyor (gerçek SPLINE entity'si DXF ASCII'de elle üretmek karmaşık) — ama ACadSharp
              nesne modelinde gerçek bir Spline sınıfı var ve AfneyCAD'in SplineEntity'si zaten
              aynı NURBS verilerini (ControlPoints/Knots/Weights/Degree) tutuyor, bu yüzden DWG
              export'ta YAKLAŞIK değil TAM bir spline yazılabiliyor.
    */
    private static Spline ToAcadSpline(AfneySpline sp)
    {
        var spline = new Spline { Degree = sp.Degree };
        foreach (var p in sp.ControlPoints) spline.ControlPoints.Add(V(p));
        foreach (var k in sp.Knots) spline.Knots.Add(k);
        if (sp.Weights.Count == sp.ControlPoints.Count)
            foreach (var w in sp.Weights) spline.Weights.Add(w);
        return spline;
    }

    /*
       NE: Hatch → Sınır Polyline (ToHatchBoundaryPolyline)
       NEDEN: Önceden HatchEntity hiç desteklenmiyordu. Gerçek bir ACadSharp Hatch nesnesi
              (dolgu deseni/gradyan dahil) kurmak BoundaryPath/HatchPattern API'sinin karmaşık
              kurulumunu gerektirir; DxfWriterService de aynı nedenle sadece sınırı (dolgusuz)
              çiziyor. Tutarlılık için aynı yaklaşım: en azından sınır kapalı bir polyline
              olarak korunuyor — hiç yazılmamaktan iyi, dolgu deseni kapsamı dışı (dokümante).
    */
    private static LwPolyline ToHatchBoundaryPolyline(AfneyHatch h)
    {
        var lw = new LwPolyline { IsClosed = true };
        foreach (var v in h.BoundaryVertices)
            lw.Vertices.Add(new LwPolyline.Vertex(V2(v)));
        return lw;
    }

    /*
       NE: B-Rep Solid → 3DFACE Üçgenleri (ToAcadFaces)
       NEDEN: Önceden SolidEntity (CSG Boolean sonuçları dahil TÜM 3D katı cisimler) hiç
              desteklenmiyordu — dışa aktarılan DWG'de bina/ekipman geometrisi tamamen
              kayboluyordu. DxfWriterService.WriteSolid ile AYNI teknik: BRepTessellator
              zaten var olan B-Rep'i üçgenlere böler, her üçgen ayrı bir Face3D (3DFACE)
              olarak yazılır (4. köşe 3.'nün tekrarı — DXF/DWG'nin dejenere-quad kuralı).
    */
    private static List<ACadSharp.Entities.Entity> ToAcadFaces(AfneySolid solid)
    {
        var result = new List<ACadSharp.Entities.Entity>();
        var (verts, faces) = BRepTessellator.Tessellate(solid.Solid);
        if (verts.Count < 3 || faces.Count == 0) return result;

        foreach (var (a, b, c) in faces)
        {
            var p1 = V(verts[a]); var p2 = V(verts[b]); var p3 = V(verts[c]);
            result.Add(new Face3D
            {
                FirstCorner  = p1,
                SecondCorner = p2,
                ThirdCorner  = p3,
                FourthCorner = p3
            });
        }
        return result;
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
