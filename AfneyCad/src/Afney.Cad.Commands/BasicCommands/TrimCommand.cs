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
   NEDEN: Kullanıcının çizgilerin veya boruların kesişim noktaları arasında kalan kısmına tıklayarak o kısmı silebilmesi için.
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
        // 1. Tıklanan noktaya en yakın nesneyi bul
        CadEntity? targetEntity = null;
        double minDst = _hitTolerance;

        var allEntities = _database.GetAllEntities().ToList();
        
        // Tersten tarama (üsttekini önce bulmak için)
        for (int i = allEntities.Count - 1; i >= 0; i--)
        {
            var ent = allEntities[i];
            
            // Sadece Line ve Pipe destekleniyor şimdilik
            if (ent is not LineEntity && ent is not PipeEntity) continue;
            
            double d = ent.DistanceTo(point);
            if (d < minDst)
            {
                minDst = d;
                targetEntity = ent;
            }
        }

        if (targetEntity == null) return; // Boşa tıklandı

        Vector3D tA, tB;
        if (targetEntity is LineEntity tl) { tA = tl.StartPoint; tB = tl.EndPoint; }
        else if (targetEntity is PipeEntity tp) { tA = tp.StartPoint; tB = tp.EndPoint; }
        else return;

        // Tıklanan noktanın target üzerindeki izdüşümü (t parametresi 0 ile 1 arası)
        double clickT = GetTParameter(tA, tB, point);

        // 2. Kesişimleri bul
        List<double> intersections = new List<double> { 0.0, 1.0 }; // Start ve End noktaları T=0, T=1

        foreach (var ent in allEntities)
        {
            if (ent == targetEntity) continue;

            Vector3D oA, oB;
            if (ent is LineEntity l) { oA = l.StartPoint; oB = l.EndPoint; }
            else if (ent is PipeEntity p) { oA = p.StartPoint; oB = p.EndPoint; }
            else continue;

            if (GeomUtils.DoSegmentsIntersect(tA, tB, oA, oB, out Vector3D ip))
            {
                double t = GetTParameter(tA, tB, ip);
                if (t > 0.0001 && t < 0.9999) // Tam uçlarda kesişenleri parçalamaya gerek yok
                    intersections.Add(t);
            }
        }

        intersections.Sort();

        // 3. Tıklanan noktanın düştüğü aralığı bul
        double tStart = 0.0;
        double tEnd = 1.0;

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
            // Create first part
            Vector3D p1 = tA;
            Vector3D p2 = new Vector3D(tA.X + tStart * (tB.X - tA.X), tA.Y + tStart * (tB.Y - tA.Y), 0);
            CadEntity part1 = CloneWithNewPoints(targetEntity, p1, p2);
            composite.Add(new AddEntityOperation(_database, part1));
        }

        if (tEnd < 0.9999)
        {
            // Create second part
            Vector3D p1 = new Vector3D(tA.X + tEnd * (tB.X - tA.X), tA.Y + tEnd * (tB.Y - tA.Y), 0);
            Vector3D p2 = tB;
            CadEntity part2 = CloneWithNewPoints(targetEntity, p1, p2);
            composite.Add(new AddEntityOperation(_database, part2));
        }

        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("TRIM: Obje budandı. Devam edebilirsiniz.");
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
