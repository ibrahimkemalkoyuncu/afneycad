using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Database.Transactions.Operations; 
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Offset (Öteleme) Komutu
   NEDEN: Seçilen objeleri eksene dik doğrultuda fare ile belirlenen yöne (ve mesafeye) öteleyerek paralel kopyalarını üretmek için.
*/
public class OffsetCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly List<CadEntity> _entitiesToOffset;
    
    // Ghost drawing
    private List<CadEntity>? _ghostEntities;
    private Vector3D? _currentMousePos;

    public string CommandName => "OFFSET";
    public Vector3D? ActivePoint => null; // Offset için base point çizim esnasında dinamiktir
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public OffsetCommand(CadDatabase database, TransactionManager transactionManager, IEnumerable<CadEntity> selection)
    {
        _database = database;
        _transactionManager = transactionManager;
        var enumerable = selection as CadEntity[] ?? selection.ToArray();
        // Desteklenen objeler: Line, Pipe, Duct, Circle, Arc, LwPolyline
        _entitiesToOffset = enumerable.Where(e =>
            e is LineEntity || e is PipeEntity || e is DuctEntity || e is CircleEntity || e is ArcEntity || e is LwPolylineEntity).ToList();

        if (_entitiesToOffset.Count != enumerable.Length)
        {
             Serilog.Log.Warning("OFFSET: Bazı nesneler offset işlemini desteklemiyor ve listeden çıkarıldı.");
        }
    }
    
    public void Start()
    {
        if (_entitiesToOffset.Count == 0)
        {
            OnFeedback?.Invoke("OFFSET: Öncelikle ötelenecek Line veya Pipe nesnelerini seçin. Komut iptal.");
            OnCompleted?.Invoke();
            return;
        }

        OnFeedback?.Invoke("OFFSET: Öteleme yönü ve mesafesini belirlemek için hedef noktaya tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        UpdateGhosts(point);
        
        if (_ghostEntities != null && _ghostEntities.Count > 0)
        {
            var composite = new CompositeOperation("Offset Entities");
            foreach (var ent in _ghostEntities)
            {
                // Clone yapılmış olanları temiz (gri) renginden orijinal rengine çevirelim
                ent.Color = _entitiesToOffset.FirstOrDefault(o => o.Layer == ent.Layer)?.Color ?? 0xFFFFFFFF;
                composite.Add(new AddEntityOperation(_database, ent));
            }

            _transactionManager.Submit(composite);
            
            OnFeedback?.Invoke($"OFFSET: {_ghostEntities.Count} nesne ötelendi.");
        }
        else
        {
            OnFeedback?.Invoke("OFFSET: Ötelenen nesne hesaplanamadı.");
        }
        
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point)
    {
         _currentMousePos = point;
         UpdateGhosts(point);
    }

    private void UpdateGhosts(Vector3D targetPoint)
    {
         _ghostEntities = new List<CadEntity>();
         
         foreach (var ent in _entitiesToOffset)
         {
             var clone = ent.Clone();
             clone.Color = 0xFFAAAAAA; // Ghost rengi (Gri)
             
             if (ent is LineEntity line)
             {
                 var v = CalculateOffsetVector(line.StartPoint, line.EndPoint, targetPoint);
                 clone.Move(v);
             }
             else if (ent is PipeEntity pipe)
             {
                 var v = CalculateOffsetVector(pipe.StartPoint, pipe.EndPoint, targetPoint);
                 clone.Move(v);
             }
             else if (ent is DuctEntity duct)
             {
                 var v = CalculateOffsetVector(duct.StartPoint, duct.EndPoint, targetPoint);
                 clone.Move(v);
             }
             else if (ent is LwPolylineEntity poly)
             {
                 var offsetPoly = OffsetPolyline(poly, targetPoint);
                 if (offsetPoly == null) continue;
                 offsetPoly.Color = 0xFFAAAAAA;
                 _ghostEntities.Add(offsetPoly);
                 continue;
             }
             else if (ent is CircleEntity circle)
             {
                 // Çember offset: yeni yarıçap = merkezden hedef noktaya olan mesafe.
                 // Dışarı tıklarsa büyür, içeri tıklarsa küçülür — Line offset'teki "hedefe doğru ötele" mantığıyla tutarlı.
                 _ghostEntities.Add(new CircleEntity(circle.Center, targetPoint.DistanceTo(circle.Center)) { Color = 0xFFAAAAAA, Layer = circle.Layer });
                 continue;
             }
             else if (ent is ArcEntity arc)
             {
                 _ghostEntities.Add(new ArcEntity(arc.Center, targetPoint.DistanceTo(arc.Center), arc.StartAngle, arc.EndAngle) { Color = 0xFFAAAAAA, Layer = arc.Layer });
                 continue;
             }
             else
             {
                 continue; // Şimdilik desteklemeyenleri silme ama offsetlemesin de
             }

             _ghostEntities.Add(clone);
         }
    }

    /*
       NE: Polyline Offset (OffsetPolyline)
       NEDEN: OFFSET önceden LwPolyline'ı hiç desteklemiyordu — AutoCAD OFFSET'in en temel
              kullanım senaryolarından biri (polyline offset, ör. duvar/kanal hattı ötelemesi)
              eksikti. Her segment kendi dik normali boyunca aynı mesafede ötelenir (mesafe/taraf
              en yakın segmentten türetilir), ardışık ötelenmiş segmentler birbirine kesiştirilerek
              (sonsuz çizgi kesişimi) köşe noktaları elde edilir — klasik "miter join" yaklaşımı.
       KAPSAM DIŞI: Yansız (reflex) köşelerde kendini kesen sonuçların budanması (self-intersection
              trimming) — AutoCAD da karmaşık şekillerde bunun için ayrı bir algoritma kullanır.
    */
    private LwPolylineEntity? OffsetPolyline(LwPolylineEntity poly, Vector3D targetPoint)
    {
        var verts = poly.Vertices;
        if (verts.Count < 2) return null;

        int segCount = poly.IsClosed ? verts.Count : verts.Count - 1;
        if (segCount < 1) return null;

        // 1. En yakın segmenti bul — offset mesafesi ve tarafı (yön) buradan türetilir.
        double bestDist = double.MaxValue;
        int nearestSeg = 0;
        Vector3D nearestOffsetVec = default;
        for (int i = 0; i < segCount; i++)
        {
            var a = verts[i];
            var b = verts[(i + 1) % verts.Count];
            var offsetVec = CalculateOffsetVector(a, b, targetPoint);
            double d = offsetVec.Length();
            if (d < bestDist) { bestDist = d; nearestSeg = i; nearestOffsetVec = offsetVec; }
        }
        if (bestDist < 1e-6) return null;
        double distance = bestDist;

        var refA = verts[nearestSeg];
        var refB = verts[(nearestSeg + 1) % verts.Count];
        var refDir = refB - refA;
        double refLen = refDir.Length();
        if (refLen < 1e-9) return null;
        var refUnit = refDir / refLen;
        var refLeftNormal = new Vector3D(-refUnit.Y, refUnit.X, 0);
        bool useLeft = (nearestOffsetVec.X * refLeftNormal.X + nearestOffsetVec.Y * refLeftNormal.Y) > 0;

        // 2. Her segmentin ötelenmiş uç noktalarını hesapla (aynı mesafe, tutarlı taraf).
        var offsetSegStarts = new Vector3D[segCount];
        var offsetSegEnds = new Vector3D[segCount];
        for (int i = 0; i < segCount; i++)
        {
            var a = verts[i];
            var b = verts[(i + 1) % verts.Count];
            var dir = b - a;
            double len = dir.Length();
            if (len < 1e-9) { offsetSegStarts[i] = a; offsetSegEnds[i] = b; continue; }
            var unit = dir / len;
            var normal = new Vector3D(-unit.Y, unit.X, 0);
            if (!useLeft) normal = new Vector3D(-normal.X, -normal.Y, 0);
            offsetSegStarts[i] = a + normal * distance;
            offsetSegEnds[i] = b + normal * distance;
        }

        // 3. Yeni köşe noktalarını, ardışık ötelenmiş segmentleri kesiştirerek bul.
        var newVerts = new List<Vector3D>();
        int vertCount = poly.IsClosed ? segCount : segCount + 1;
        for (int i = 0; i < vertCount; i++)
        {
            if (!poly.IsClosed && i == 0) { newVerts.Add(offsetSegStarts[0]); continue; }
            if (!poly.IsClosed && i == segCount) { newVerts.Add(offsetSegEnds[segCount - 1]); continue; }

            int prevSeg = poly.IsClosed ? (i - 1 + segCount) % segCount : i - 1;
            int currSeg = poly.IsClosed ? i % segCount : i;

            if (LineLineIntersect(offsetSegStarts[prevSeg], offsetSegEnds[prevSeg], offsetSegStarts[currSeg], offsetSegEnds[currSeg], out var corner))
                newVerts.Add(corner);
            else
                newVerts.Add(offsetSegEnds[prevSeg]); // paralel segmentler — kesişim yok
        }

        return new LwPolylineEntity(newVerts, poly.IsClosed) { Layer = poly.Layer };
    }

    /// <summary>İki SONSUZ çizginin (segment değil) kesişimini bulur — köşe birleştirme (miter join) için gereklidir.</summary>
    private static bool LineLineIntersect(Vector3D p1, Vector3D p2, Vector3D p3, Vector3D p4, out Vector3D result)
    {
        double d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
        double d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;
        double denom = d1x * d2y - d1y * d2x;
        if (Math.Abs(denom) < 1e-9) { result = default; return false; }

        double t = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / denom;
        result = new Vector3D(p1.X + t * d1x, p1.Y + t * d1y, 0);
        return true;
    }

    /*
       NE: Öteleme (Offset) Vektörü Hesaplayıcı
       NEDEN: Verilen A-B hattına olan en yakın dik izdüşümü bulup, o noktadan farenin mevcut yerine (P) uzanan "vektör"ü offset taşıma vektörü olarak kullanmak için.
    */
    private Vector3D CalculateOffsetVector(Vector3D A, Vector3D B, Vector3D P)
    {
        // 1. Hat vektörü (AB)
        double dx = B.X - A.X;
        double dy = B.Y - A.Y;
        
        // 2. Noktadan A'ya vektör (AP)
        double apx = P.X - A.X;
        double apy = P.Y - A.Y;

        double ab_length_sq = dx * dx + dy * dy;
        
        if (ab_length_sq < 0.000001) 
        {
           // Çizgi çok kısaysa farenin noktasına direkt ötele (Fallback)
           return new Vector3D(P.X - A.X, P.Y - A.Y, 0); 
        }

        // 3. İzdüşüm katsayısı (AP dot AB) / |AB|^2
        double t = (apx * dx + apy * dy) / ab_length_sq;
        
        // 4. Hat üzerindeki izdüşüm noktası (Projected) - Hattın dışına taşsa da doğru hattı uzatarak izdüşüm alırız.
        double projX = A.X + t * dx;
        double projY = A.Y + t * dy;

        // 5. Offset taşıma vektörü: P'den İzdüşüme olan dik uzaklık. Move() komutu P -> Projected mi yoksa Projected -> P mi?
        // Çizgiyi P tarafına o kadar mesafede ötelemek istiyoruz, yani Yön vektörü (P - Projected) olur.
        return new Vector3D(P.X - projX, P.Y - projY, 0);
    }

    public void OnKeyDown(InputKey key) 
    {
        if(key == InputKey.Escape)
            Cancel();
    }

    public void Draw(IRenderContext context)
    {
         if (_currentMousePos.HasValue && _ghostEntities != null && _entitiesToOffset.Count > 0)
         {
             // Bir adet yardımcı dik çizgi çizebiliriz (Rubber band). İlk nesnenin merkezinden P'ye.
             var first = _entitiesToOffset.FirstOrDefault();
             if (first != null)
             {
                  Vector3D basePt = new Vector3D(0,0,0);
                  if (first is LineEntity l) basePt = CalculateProjection(l.StartPoint, l.EndPoint, _currentMousePos.Value);
                  else if (first is PipeEntity p) basePt = CalculateProjection(p.StartPoint, p.EndPoint, _currentMousePos.Value);
                  
                  context.DrawLine(basePt, _currentMousePos.Value, 0xFF888888, 1.0 * context.PixelSize, isDashed: true);
             }

             // Ghosts
             foreach(var ghost in _ghostEntities) ghost.Draw(context);
         }
    }
    
    private Vector3D CalculateProjection(Vector3D A, Vector3D B, Vector3D P)
    {
        double dx = B.X - A.X;
        double dy = B.Y - A.Y;
        double apx = P.X - A.X;
        double apy = P.Y - A.Y;
        double ab_length_sq = dx * dx + dy * dy;
        if (ab_length_sq < 0.000001) return A;
        double t = (apx * dx + apy * dy) / ab_length_sq;
        return new Vector3D(A.X + t * dx, A.Y + t * dy, 0);
    }

    public void Cancel()
    {
        _ghostEntities = null;
        OnCompleted?.Invoke();
    }
}
