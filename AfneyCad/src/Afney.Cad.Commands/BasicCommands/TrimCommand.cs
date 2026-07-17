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
   NE: Hızlı Buda (Quick Trim) Komutu
   NEDEN: Kullanıcının çizgilerin, boruların, çemberlerin ve yayların kesişim noktaları
          arasında kalan kısmına tıklayarak o kısmı silebilmesi için.
   NOT: Line/Pipe/Duct/Circle/Arc/LwPolyline destekleniyor. Polyline'da sadece tıklanan segment
        kendi kesişimlerine göre budanır (kendi kendini kesme kapsam dışı).
*/
public class TrimCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly double _hitTolerance;

    public string CommandName => "TRIM";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public TrimCommand(CadDatabase database, TransactionManager transactionManager, double currentZoom)
    {
        _database = database;
        _transactionManager = transactionManager;
        _hitTolerance = 10.0 / Math.Max(0.001, currentZoom);
    }

    public void Start()
    {
        OnFeedback?.Invoke("TRIM (Buda): Budanacak kısmı seçin (Hızlı Buda aktiftir. Kapatmak için ESC veya Sağ Tık).");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var targetEntity = FindNearestSupportedEntity(point);
        if (targetEntity == null) return;

        switch (targetEntity)
        {
            case LineEntity or PipeEntity or DuctEntity:
                TrimLinear(targetEntity, point);
                break;
            case CircleEntity circle:
                TrimCircle(circle, point);
                break;
            case ArcEntity arc:
                TrimArc(arc, point);
                break;
            case LwPolylineEntity poly:
                TrimPolyline(poly, point);
                break;
        }
    }

    private CadEntity? FindNearestSupportedEntity(Vector3D point)
    {
        CadEntity? targetEntity = null;
        double minDst = _hitTolerance;

        var allEntities = _database.GetAllEntities().ToList();

        // Tersten tarama (üsttekini önce bulmak için)
        for (int i = allEntities.Count - 1; i >= 0; i--)
        {
            var ent = allEntities[i];

            if (ent is not LineEntity && ent is not PipeEntity && ent is not DuctEntity && ent is not CircleEntity && ent is not ArcEntity && ent is not LwPolylineEntity) continue;

            double d = ent.DistanceTo(point);
            if (d < minDst)
            {
                minDst = d;
                targetEntity = ent;
            }
        }

        return targetEntity;
    }

    // ── LINE / PIPE (Doğrusal — t ∈ [0,1] parametrizasyonu) ─────────────────

    private void TrimLinear(CadEntity targetEntity, Vector3D point)
    {
        Vector3D tA, tB;
        if (targetEntity is LineEntity tl) { tA = tl.StartPoint; tB = tl.EndPoint; }
        else if (targetEntity is PipeEntity tp) { tA = tp.StartPoint; tB = tp.EndPoint; }
        else if (targetEntity is DuctEntity td) { tA = td.StartPoint; tB = td.EndPoint; }
        else return;

        double clickT = GetTParameter(tA, tB, point);

        List<double> intersections = new List<double> { 0.0, 1.0 };

        foreach (var ent in _database.GetAllEntities())
        {
            if (ent == targetEntity) continue;

            Vector3D oA, oB;
            if (ent is LineEntity l) { oA = l.StartPoint; oB = l.EndPoint; }
            else if (ent is PipeEntity p) { oA = p.StartPoint; oB = p.EndPoint; }
            else if (ent is DuctEntity d) { oA = d.StartPoint; oB = d.EndPoint; }
            else if (ent is CircleEntity c)
            {
                foreach (var ip in GeomUtils.GetIntersectionsLineCircle(tA, tB, c.Center, c.Radius))
                {
                    double t = GetTParameter(tA, tB, ip);
                    if (t > 0.0001 && t < 0.9999) intersections.Add(t);
                }
                continue;
            }
            else if (ent is ArcEntity a)
            {
                foreach (var ip in GeomUtils.GetIntersectionsLineCircle(tA, tB, a.Center, a.Radius))
                {
                    if (!IsAngleWithinArc(GeomUtils.AngleOf(a.Center, ip), a.StartAngle, a.EndAngle)) continue;
                    double t = GetTParameter(tA, tB, ip);
                    if (t > 0.0001 && t < 0.9999) intersections.Add(t);
                }
                continue;
            }
            else continue;

            if (GeomUtils.DoSegmentsIntersect(tA, tB, oA, oB, out Vector3D lineIp))
            {
                double t = GetTParameter(tA, tB, lineIp);
                if (t > 0.0001 && t < 0.9999) intersections.Add(t);
            }
        }

        intersections.Sort();

        double tStart = 0.0, tEnd = 1.0;
        for (int i = 0; i < intersections.Count - 1; i++)
        {
            if (clickT >= intersections[i] && clickT <= intersections[i + 1])
            {
                tStart = intersections[i];
                tEnd = intersections[i + 1];
                break;
            }
        }

        var composite = new CompositeOperation("Trim Entity");
        composite.Add(new RemoveEntityOperation(_database, targetEntity));

        if (tStart > 0.0001)
        {
            Vector3D p1 = tA;
            Vector3D p2 = new Vector3D(tA.X + tStart * (tB.X - tA.X), tA.Y + tStart * (tB.Y - tA.Y), 0);
            composite.Add(new AddEntityOperation(_database, CloneWithNewPoints(targetEntity, p1, p2)));
        }

        if (tEnd < 0.9999)
        {
            Vector3D p1 = new Vector3D(tA.X + tEnd * (tB.X - tA.X), tA.Y + tEnd * (tB.Y - tA.Y), 0);
            Vector3D p2 = tB;
            composite.Add(new AddEntityOperation(_database, CloneWithNewPoints(targetEntity, p1, p2)));
        }

        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("TRIM: Obje budandı. Devam edebilirsiniz.");
    }

    // ── CIRCLE (Kapalı döngü — açısal, 0..2π sarmalı) ───────────────────────

    private void TrimCircle(CircleEntity circle, Vector3D point)
    {
        double clickAngle = GeomUtils.AngleOf(circle.Center, point);
        var angles = new List<double>();

        foreach (var ent in _database.GetAllEntities())
        {
            if (ent == circle) continue;

            IEnumerable<Vector3D> pts = ent switch
            {
                LineEntity l => GeomUtils.GetIntersectionsLineCircle(l.StartPoint, l.EndPoint, circle.Center, circle.Radius),
                PipeEntity p => GeomUtils.GetIntersectionsLineCircle(p.StartPoint, p.EndPoint, circle.Center, circle.Radius),
                DuctEntity d => GeomUtils.GetIntersectionsLineCircle(d.StartPoint, d.EndPoint, circle.Center, circle.Radius),
                CircleEntity c2 => GeomUtils.GetIntersectionsCircleCircle(circle.Center, circle.Radius, c2.Center, c2.Radius),
                ArcEntity a2 => GeomUtils.GetIntersectionsCircleCircle(circle.Center, circle.Radius, a2.Center, a2.Radius)
                    .Where(ip => IsAngleWithinArc(GeomUtils.AngleOf(a2.Center, ip), a2.StartAngle, a2.EndAngle)),
                _ => Enumerable.Empty<Vector3D>()
            };

            foreach (var p in pts) angles.Add(GeomUtils.AngleOf(circle.Center, p));
        }

        if (angles.Count < 2)
        {
            OnFeedback?.Invoke("TRIM: Bu çemberi budamak için en az 2 kesişim noktası gerekli.");
            return;
        }

        angles = angles.Select(GeomUtils.NormalizeAngle).Distinct().OrderBy(a => a).ToList();

        // Tıklanan açıyı içeren komşu kesişim çiftini (segStart, segEnd) bul (dairesel sarmalı).
        double segStart = angles[^1], segEnd = angles[0] + 2 * Math.PI;
        for (int i = 0; i < angles.Count; i++)
        {
            double a1 = angles[i];
            double a2 = i + 1 < angles.Count ? angles[i + 1] : angles[0] + 2 * Math.PI;
            double ca = clickAngle < a1 ? clickAngle + 2 * Math.PI : clickAngle;
            if (ca >= a1 && ca <= a2)
            {
                segStart = a1;
                segEnd = a2;
                break;
            }
        }

        // Kalan yay: tıklanan segmentin DIŞINDA kalan (uzun) taraf — segEnd'den segStart'a kadar.
        var newArc = new ArcEntity(circle.Center, circle.Radius, GeomUtils.NormalizeAngle(segEnd), GeomUtils.NormalizeAngle(segStart))
            { Color = circle.Color, Layer = circle.Layer };

        var composite = new CompositeOperation("Trim Circle");
        composite.Add(new RemoveEntityOperation(_database, circle));
        composite.Add(new AddEntityOperation(_database, newArc));
        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("TRIM: Çember budanarak yaya dönüştürüldü.");
    }

    // ── ARC (Sınırlı yay — StartAngle..EndAngle sarmalını "unwrap" ederek Line ile aynı mantık) ──

    private void TrimArc(ArcEntity arc, Vector3D point)
    {
        double sweep = ArcSweep(arc.StartAngle, arc.EndAngle);
        double arcEndUnwrapped = arc.StartAngle + sweep;

        double clickAngle = GeomUtils.AngleOf(arc.Center, point);
        double clickU = UnwrapToArc(clickAngle, arc.StartAngle);
        if (clickU > arcEndUnwrapped + 1e-6) return; // Tıklama bu yayın üzerinde değil

        var cuts = new List<double> { arc.StartAngle, arcEndUnwrapped };

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
                    .Where(ip => IsAngleWithinArc(GeomUtils.AngleOf(a2.Center, ip), a2.StartAngle, a2.EndAngle)),
                _ => Enumerable.Empty<Vector3D>()
            };

            foreach (var p in pts)
            {
                double u = UnwrapToArc(GeomUtils.AngleOf(arc.Center, p), arc.StartAngle);
                if (u > 0.0001 && u < arcEndUnwrapped - 0.0001) cuts.Add(u);
            }
        }

        cuts = cuts.Distinct().OrderBy(x => x).ToList();

        double segStart = arc.StartAngle, segEnd = arcEndUnwrapped;
        for (int i = 0; i < cuts.Count - 1; i++)
        {
            if (clickU >= cuts[i] && clickU <= cuts[i + 1])
            {
                segStart = cuts[i];
                segEnd = cuts[i + 1];
                break;
            }
        }

        var composite = new CompositeOperation("Trim Arc");
        composite.Add(new RemoveEntityOperation(_database, arc));

        if (segStart - arc.StartAngle > 0.0001)
            composite.Add(new AddEntityOperation(_database, new ArcEntity(arc.Center, arc.Radius, GeomUtils.NormalizeAngle(arc.StartAngle), GeomUtils.NormalizeAngle(segStart)) { Color = arc.Color, Layer = arc.Layer }));

        if (arcEndUnwrapped - segEnd > 0.0001)
            composite.Add(new AddEntityOperation(_database, new ArcEntity(arc.Center, arc.Radius, GeomUtils.NormalizeAngle(segEnd), GeomUtils.NormalizeAngle(arcEndUnwrapped)) { Color = arc.Color, Layer = arc.Layer }));

        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("TRIM: Yay budandı.");
    }

    // ── LWPOLYLINE (Tıklanan segment, Line ile aynı t∈[0,1] mantığıyla budanır) ──

    /*
       NE: Polyline Buda (TrimPolyline)
       NEDEN: Önceden LwPolyline hiç desteklenmiyordu. Tıklanan TEK segment, o segmentin
              kendi doğrusu üzerindeki kesişimlere göre budanır (diğer segmentlere göre değil —
              kendi kendini kesme kapsam dışı, gerçek CAD yazılımlarında da nadiren aranan bir
              senaryo). Sonuç: açık polyline'da segment ORTADAYSA çizgi İKİYE bölünür (iki ayrı
              LwPolyline); UÇTAYSA tek parça kısalır. Kapalı polyline'da tek segment budandığında
              halka açılır ve TEK bir açık polyline'a dönüşür.
    */
    private void TrimPolyline(LwPolylineEntity poly, Vector3D point)
    {
        var verts = poly.Vertices;
        int n = verts.Count;
        int segCount = poly.IsClosed ? n : n - 1;
        if (segCount < 1) return;

        // 1. En yakın segmenti bul.
        int segIdx = 0;
        double minDst = double.MaxValue;
        for (int i = 0; i < segCount; i++)
        {
            double d = PointToSegmentDistance(point, verts[i], verts[(i + 1) % n]);
            if (d < minDst) { minDst = d; segIdx = i; }
        }

        Vector3D sA = verts[segIdx], sB = verts[(segIdx + 1) % n];
        double clickT = GetTParameter(sA, sB, point);

        // 2. Bu segmentin diğer TÜM nesnelerle (kendi polyline'ı hariç) kesişimlerini topla.
        var intersections = new List<double> { 0.0, 1.0 };
        foreach (var ent in _database.GetAllEntities())
        {
            if (ent == poly) continue;

            Vector3D oA, oB;
            if (ent is LineEntity l) { oA = l.StartPoint; oB = l.EndPoint; }
            else if (ent is PipeEntity p) { oA = p.StartPoint; oB = p.EndPoint; }
            else if (ent is DuctEntity d) { oA = d.StartPoint; oB = d.EndPoint; }
            else if (ent is CircleEntity c)
            {
                foreach (var ip in GeomUtils.GetIntersectionsLineCircle(sA, sB, c.Center, c.Radius))
                {
                    double t = GetTParameter(sA, sB, ip);
                    if (t > 0.0001 && t < 0.9999) intersections.Add(t);
                }
                continue;
            }
            else if (ent is ArcEntity a)
            {
                foreach (var ip in GeomUtils.GetIntersectionsLineCircle(sA, sB, a.Center, a.Radius))
                {
                    if (!IsAngleWithinArc(GeomUtils.AngleOf(a.Center, ip), a.StartAngle, a.EndAngle)) continue;
                    double t = GetTParameter(sA, sB, ip);
                    if (t > 0.0001 && t < 0.9999) intersections.Add(t);
                }
                continue;
            }
            else continue;

            if (GeomUtils.DoSegmentsIntersect(sA, sB, oA, oB, out Vector3D lineIp))
            {
                double t = GetTParameter(sA, sB, lineIp);
                if (t > 0.0001 && t < 0.9999) intersections.Add(t);
            }
        }

        intersections.Sort();

        double tStart = 0.0, tEnd = 1.0;
        for (int i = 0; i < intersections.Count - 1; i++)
        {
            if (clickT >= intersections[i] && clickT <= intersections[i + 1])
            {
                tStart = intersections[i];
                tEnd = intersections[i + 1];
                break;
            }
        }

        Vector3D? cutStart = tStart > 0.0001 ? sA + (sB - sA) * tStart : null;
        Vector3D? cutEnd = tEnd < 0.9999 ? sA + (sB - sA) * tEnd : null;

        var composite = new CompositeOperation("Trim Polyline");
        composite.Add(new RemoveEntityOperation(_database, poly));

        if (poly.IsClosed)
        {
            // Halka tek noktadan açılır: (segIdx+1)'den başlayarak n adım ilerleyip segIdx'e ulaş.
            var chain = new List<Vector3D>();
            if (cutEnd.HasValue) chain.Add(cutEnd.Value);
            for (int step = 0; step < n; step++)
            {
                int idx = (segIdx + 1 + step) % n;
                chain.Add(verts[idx]);
                if (idx == segIdx) break;
            }
            if (cutStart.HasValue) chain.Add(cutStart.Value);

            if (chain.Count >= 2)
                composite.Add(new AddEntityOperation(_database, new LwPolylineEntity(chain, isClosed: false) { Color = poly.Color, Layer = poly.Layer }));
        }
        else
        {
            var prefix = new List<Vector3D>(verts.Take(segIdx + 1));
            if (cutStart.HasValue) prefix.Add(cutStart.Value);
            if (prefix.Count >= 2)
                composite.Add(new AddEntityOperation(_database, new LwPolylineEntity(prefix, isClosed: false) { Color = poly.Color, Layer = poly.Layer }));

            var suffix = new List<Vector3D>();
            if (cutEnd.HasValue) suffix.Add(cutEnd.Value);
            suffix.AddRange(verts.Skip(segIdx + 1));
            if (suffix.Count >= 2)
                composite.Add(new AddEntityOperation(_database, new LwPolylineEntity(suffix, isClosed: false) { Color = poly.Color, Layer = poly.Layer }));
        }

        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("TRIM: Polyline budandı.");
    }

    private static double PointToSegmentDistance(Vector3D p, Vector3D a, Vector3D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-9) return p.DistanceTo(a);
        double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
        return p.DistanceTo(new Vector3D(a.X + t * dx, a.Y + t * dy, 0));
    }

    // ── Ortak yardımcılar ────────────────────────────────────────────────────

    internal static double ArcSweep(double startAngle, double endAngle)
        => endAngle > startAngle ? endAngle - startAngle : (2 * Math.PI - startAngle) + endAngle;

    /// <summary>Bir açıyı, verilen yayın başlangıcından itibaren "sarmalı çözülmüş" (unwrapped) hale getirir.</summary>
    internal static double UnwrapToArc(double angle, double arcStartAngle)
    {
        double a = angle;
        while (a < arcStartAngle - 1e-9) a += 2 * Math.PI;
        return a;
    }

    internal static bool IsAngleWithinArc(double angle, double startAngle, double endAngle)
    {
        double sweep = ArcSweep(startAngle, endAngle);
        double u = UnwrapToArc(angle, startAngle);
        return u >= startAngle - 1e-6 && u <= startAngle + sweep + 1e-6;
    }

    private double GetTParameter(Vector3D A, Vector3D B, Vector3D P)
    {
        double l2 = Math.Pow(B.X - A.X, 2) + Math.Pow(B.Y - A.Y, 2);
        if (l2 < 1e-9) return 0;
        double t = ((P.X - A.X) * (B.X - A.X) + (P.Y - A.Y) * (B.Y - A.Y)) / l2;
        return Math.Max(0.0, Math.Min(1.0, t));
    }

    private CadEntity CloneWithNewPoints(CadEntity source, Vector3D p1, Vector3D p2)
    {
        var clone = source.Clone();
        if (clone is LineEntity l)
        {
            l.StartPoint = p1;
            l.EndPoint = p2;
        }
        else if (clone is PipeEntity p)
        {
            p.StartPoint = p1;
            p.EndPoint = p2;
        }
        else if (clone is DuctEntity d)
        {
            d.StartPoint = p1;
            d.EndPoint = p2;
        }
        return clone;
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
