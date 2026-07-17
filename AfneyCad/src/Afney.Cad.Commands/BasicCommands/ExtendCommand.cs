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
using Afney.Cad.Geometry.Algorithms;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Hızlı Uzat (Quick Extend) Komutu
   NEDEN: Kullanıcının çizgilerin/boruların bir ucuna veya bir yayın açık kenarına tıklayarak
          onu ilk kesen diğer nesneye kadar uzatmasını sağlamak için.
   NOT: Önceden sadece Line/Pipe (hem hedef hem sınır olarak) destekleniyordu. Artık Circle/Arc
        da sınır (boundary) olarak kullanılabiliyor; ArcEntity'nin kendisi de kendi çemberi
        üzerinde sweep'ini genişleterek uzatılabiliyor.
*/
public class ExtendCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly double _hitTolerance;

    public string CommandName => "EXTEND";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public ExtendCommand(CadDatabase database, TransactionManager transactionManager, double currentZoom)
    {
        _database = database;
        _transactionManager = transactionManager;
        _hitTolerance = 10.0 / Math.Max(0.001, currentZoom);
    }

    public void Start()
    {
        OnFeedback?.Invoke("EXTEND (Uzat): Uzatılacak nesnenin ucuna tıklayın (Hızlı Uzat aktiftir. Kapatmak için ESC veya Sağ Tık).");
    }

    public void OnPointerPressed(Vector3D point)
    {
        CadEntity? targetEntity = null;
        double minDst = _hitTolerance;

        var allEntities = _database.GetAllEntities().ToList();

        for (int i = allEntities.Count - 1; i >= 0; i--)
        {
            var ent = allEntities[i];
            if (ent is not LineEntity && ent is not PipeEntity && ent is not DuctEntity && ent is not ArcEntity && ent is not LwPolylineEntity) continue;

            double d = ent.DistanceTo(point);
            if (d < minDst)
            {
                minDst = d;
                targetEntity = ent;
            }
        }

        if (targetEntity == null) return;

        if (targetEntity is ArcEntity arc)
        {
            ExtendArc(arc, point);
            return;
        }

        if (targetEntity is LwPolylineEntity poly)
        {
            ExtendPolyline(poly, point, allEntities);
            return;
        }

        ExtendLinear(targetEntity, point, allEntities);
    }

    /*
       NE: Polyline Uzat (ExtendPolyline)
       NEDEN: Önceden LwPolyline hiç desteklenmiyordu. Tıklanan uca en yakın UÇ segment
              (ilk veya son) kendi doğrultusunda ilk kesişime kadar uzatılır — ortadaki
              segmentler değişmez. Kapalı polyline'ın açık ucu olmadığı için uzatılamaz.
    */
    private void ExtendPolyline(LwPolylineEntity poly, Vector3D point, List<CadEntity> allEntities)
    {
        if (poly.IsClosed)
        {
            OnFeedback?.Invoke("EXTEND: Kapalı polyline uzatılamaz.");
            return;
        }

        var verts = poly.Vertices;
        int n = verts.Count;
        if (n < 2) return;

        bool extendStart = point.DistanceTo(verts[0]) < point.DistanceTo(verts[n - 1]);

        Vector3D rayOrigin = extendStart ? verts[0] : verts[n - 1];
        Vector3D neighbor = extendStart ? verts[1] : verts[n - 2];
        Vector3D rayDir = new Vector3D(rayOrigin.X - neighbor.X, rayOrigin.Y - neighbor.Y, 0);

        var bestIntersection = FindNearestBoundaryAlongRay(rayOrigin, rayDir, poly, allEntities);
        if (!bestIntersection.HasValue)
        {
            OnFeedback?.Invoke("EXTEND: Uzatma doğrultusunda bir kesişim bulunamadı.");
            return;
        }

        var newVerts = new List<Vector3D>(verts);
        if (extendStart) newVerts[0] = bestIntersection.Value;
        else newVerts[n - 1] = bestIntersection.Value;

        var composite = new CompositeOperation("Extend Polyline");
        composite.Add(new RemoveEntityOperation(_database, poly));
        composite.Add(new AddEntityOperation(_database, new LwPolylineEntity(newVerts, poly.IsClosed) { Color = poly.Color, Layer = poly.Layer }));
        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("EXTEND: Polyline uzatıldı.");
    }

    /*
       NE: Işın Boyunca En Yakın Sınırı Bul (FindNearestBoundaryAlongRay)
       NEDEN: ExtendLinear ve ExtendPolyline aynı sınır-arama algoritmasını (Line/Pipe/Duct/
              Circle/Arc sınırlarıyla ışın kesişimi) kullanıyor — tekrar yazmak yerine ortak
              yardımcı metoda çıkarıldı.
    */
    private Vector3D? FindNearestBoundaryAlongRay(Vector3D rayOrigin, Vector3D rayDir, CadEntity excludeEntity, List<CadEntity> allEntities)
    {
        double minT = double.MaxValue;
        Vector3D? bestIntersection = null;

        foreach (var ent in allEntities)
        {
            if (ent == excludeEntity) continue;

            if (ent is CircleEntity c)
            {
                foreach (var (rt, rp) in GeomUtils.GetIntersectionsRayCircle(rayOrigin, rayDir, c.Center, c.Radius))
                    if (rt < minT) { minT = rt; bestIntersection = rp; }
                continue;
            }
            if (ent is ArcEntity a)
            {
                foreach (var (rt, rp) in GeomUtils.GetIntersectionsRayCircle(rayOrigin, rayDir, a.Center, a.Radius))
                    if (rt < minT && TrimCommand.IsAngleWithinArc(GeomUtils.AngleOf(a.Center, rp), a.StartAngle, a.EndAngle)) { minT = rt; bestIntersection = rp; }
                continue;
            }

            Vector3D oA, oB;
            if (ent is LineEntity l) { oA = l.StartPoint; oB = l.EndPoint; }
            else if (ent is PipeEntity p2) { oA = p2.StartPoint; oB = p2.EndPoint; }
            else if (ent is DuctEntity d2) { oA = d2.StartPoint; oB = d2.EndPoint; }
            else continue;

            double dx1 = rayDir.X, dy1 = rayDir.Y;
            double x1 = rayOrigin.X, y1 = rayOrigin.Y;
            double dx3 = oB.X - oA.X, dy3 = oB.Y - oA.Y;
            double x3 = oA.X, y3 = oA.Y;

            double det = dx1 * dy3 - dy1 * dx3;
            if (Math.Abs(det) < 1e-9) continue;

            double t = ((x3 - x1) * dy3 - (y3 - y1) * dx3) / det;
            double u = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / det;

            if (t > 0.0001 && t < minT && u >= 0 && u <= 1)
            {
                minT = t;
                bestIntersection = new Vector3D(x1 + t * dx1, y1 + t * dy1, 0);
            }
        }

        return bestIntersection;
    }

    private void ExtendLinear(CadEntity targetEntity, Vector3D point, List<CadEntity> allEntities)
    {
        Vector3D tA, tB;
        if (targetEntity is LineEntity tl) { tA = tl.StartPoint; tB = tl.EndPoint; }
        else if (targetEntity is PipeEntity tp) { tA = tp.StartPoint; tB = tp.EndPoint; }
        else if (targetEntity is DuctEntity td) { tA = td.StartPoint; tB = td.EndPoint; }
        else return;

        double distToStart = point.DistanceTo(tA);
        double distToEnd = point.DistanceTo(tB);
        bool extendStart = distToStart < distToEnd;

        Vector3D rayDir = extendStart ? new Vector3D(tA.X - tB.X, tA.Y - tB.Y, 0) : new Vector3D(tB.X - tA.X, tB.Y - tA.Y, 0);
        Vector3D rayOrigin = extendStart ? tA : tB;

        var bestIntersection = FindNearestBoundaryAlongRay(rayOrigin, rayDir, targetEntity, allEntities);

        if (bestIntersection.HasValue)
        {
            var composite = new CompositeOperation("Extend Entity");
            composite.Add(new RemoveEntityOperation(_database, targetEntity));

            var clone = targetEntity.Clone();
            if (clone is LineEntity lcl)
            {
                if (extendStart) lcl.StartPoint = bestIntersection.Value;
                else lcl.EndPoint = bestIntersection.Value;
            }
            else if (clone is PipeEntity pcl)
            {
                if (extendStart) pcl.StartPoint = bestIntersection.Value;
                else pcl.EndPoint = bestIntersection.Value;
            }
            else if (clone is DuctEntity dcl)
            {
                if (extendStart) dcl.StartPoint = bestIntersection.Value;
                else dcl.EndPoint = bestIntersection.Value;
            }

            composite.Add(new AddEntityOperation(_database, clone));
            _transactionManager.Submit(composite);

            OnFeedback?.Invoke("EXTEND: Obje uzatıldı. Devam edebilirsiniz.");
        }
        else
        {
            OnFeedback?.Invoke("EXTEND: Uzatma doğrultusunda bir kesişim bulunamadı.");
        }
    }

    // ── ARC — kendi çemberi üzerinde sweep'ini genişletir (düz çizgi değil, aynı yarıçapta) ──

    private void ExtendArc(ArcEntity arc, Vector3D point)
    {
        double sweep = TrimCommand.ArcSweep(arc.StartAngle, arc.EndAngle);
        double startEndpointAngle = arc.StartAngle;
        double endEndpointAngle = arc.StartAngle + sweep;

        var startPt = new Vector3D(arc.Center.X + Math.Cos(arc.StartAngle) * arc.Radius, arc.Center.Y + Math.Sin(arc.StartAngle) * arc.Radius, 0);
        var endPt = new Vector3D(arc.Center.X + Math.Cos(arc.EndAngle) * arc.Radius, arc.Center.Y + Math.Sin(arc.EndAngle) * arc.Radius, 0);

        bool extendAtStart = point.DistanceTo(startPt) < point.DistanceTo(endPt);

        double bestDelta = double.MaxValue;
        double? bestNewAngleUnwrapped = null;

        foreach (var ent in _database.GetAllEntities())
        {
            if (ent == arc) continue;

            IEnumerable<Vector3D> pts = ent switch
            {
                LineEntity l => GeomUtils.GetIntersectionsLineCircle(l.StartPoint, l.EndPoint, arc.Center, arc.Radius),
                PipeEntity p => GeomUtils.GetIntersectionsLineCircle(p.StartPoint, p.EndPoint, arc.Center, arc.Radius),
                DuctEntity d => GeomUtils.GetIntersectionsLineCircle(d.StartPoint, d.EndPoint, arc.Center, arc.Radius),
                CircleEntity c2 => GeomUtils.GetIntersectionsCircleCircle(arc.Center, arc.Radius, c2.Center, c2.Radius),
                ArcEntity a2 => GeomUtils.GetIntersectionsCircleCircle(arc.Center, arc.Radius, a2.Center, a2.Radius)
                    .Where(ip => TrimCommand.IsAngleWithinArc(GeomUtils.AngleOf(a2.Center, ip), a2.StartAngle, a2.EndAngle)),
                _ => Enumerable.Empty<Vector3D>()
            };

            foreach (var p in pts)
            {
                double rawAngle = GeomUtils.AngleOf(arc.Center, p);

                if (extendAtStart)
                {
                    // startEndpointAngle'dan GERİYE doğru en yakın kesişimi ara (unwrap ederek negatif yönde)
                    double u = rawAngle;
                    while (u > startEndpointAngle + 1e-9) u -= 2 * Math.PI;
                    double delta = startEndpointAngle - u;
                    if (delta > 1e-4 && delta < bestDelta) { bestDelta = delta; bestNewAngleUnwrapped = u; }
                }
                else
                {
                    double u = TrimCommand.UnwrapToArc(rawAngle, endEndpointAngle);
                    double delta = u - endEndpointAngle;
                    if (delta > 1e-4 && delta < bestDelta) { bestDelta = delta; bestNewAngleUnwrapped = u; }
                }
            }
        }

        if (!bestNewAngleUnwrapped.HasValue)
        {
            OnFeedback?.Invoke("EXTEND: Uzatma doğrultusunda bir kesişim bulunamadı.");
            return;
        }

        double newStart = extendAtStart ? GeomUtils.NormalizeAngle(bestNewAngleUnwrapped.Value) : arc.StartAngle;
        double newEnd = extendAtStart ? arc.EndAngle : GeomUtils.NormalizeAngle(bestNewAngleUnwrapped.Value);

        var composite = new CompositeOperation("Extend Arc");
        composite.Add(new RemoveEntityOperation(_database, arc));
        composite.Add(new AddEntityOperation(_database, new ArcEntity(arc.Center, arc.Radius, newStart, newEnd) { Color = arc.Color, Layer = arc.Layer }));
        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("EXTEND: Yay uzatıldı.");
    }

    public void OnPointerMoved(Vector3D point) { }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape || key == InputKey.Enter)
            Cancel();
    }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context) { }
}
