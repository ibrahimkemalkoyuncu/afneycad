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
   NEDEN: Kullanıcının çizgilerin veya boruların bir ucuna tıklayarak onu ilk kesen diğer nesneye kadar uzatmasını sağlamak için.
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
            if (ent is not LineEntity && ent is not PipeEntity) continue;
            
            double d = ent.DistanceTo(point);
            if (d < minDst)
            {
                minDst = d;
                targetEntity = ent;
            }
        }

        if (targetEntity == null) return;

        Vector3D tA, tB;
        if (targetEntity is LineEntity tl) { tA = tl.StartPoint; tB = tl.EndPoint; }
        else if (targetEntity is PipeEntity tp) { tA = tp.StartPoint; tB = tp.EndPoint; }
        else return;

        // Tıklanan nokta hangi uca daha yakın?
        double distToStart = point.DistanceTo(tA);
        double distToEnd = point.DistanceTo(tB);
        bool extendStart = distToStart < distToEnd;

        // Uzatma yönü (Target Line Segment)
        Vector3D rayDir = extendStart ? new Vector3D(tA.X - tB.X, tA.Y - tB.Y, 0) : new Vector3D(tB.X - tA.X, tB.Y - tA.Y, 0);
        Vector3D rayOrigin = extendStart ? tA : tB;

        double minT = double.MaxValue;
        Vector3D? bestIntersection = null;

        foreach (var ent in allEntities)
        {
            if (ent == targetEntity) continue;

            Vector3D oA, oB;
            if (ent is LineEntity l) { oA = l.StartPoint; oB = l.EndPoint; }
            else if (ent is PipeEntity p) { oA = p.StartPoint; oB = p.EndPoint; }
            else continue;

            double dx1 = rayDir.X; double dy1 = rayDir.Y;
            double x1 = rayOrigin.X; double y1 = rayOrigin.Y;

            double dx3 = oB.X - oA.X; double dy3 = oB.Y - oA.Y;
            double x3 = oA.X; double y3 = oA.Y;

            double det = dx1 * dy3 - dy1 * dx3;
            if (Math.Abs(det) < 1e-9) continue; // Paralel

            double t = ((x3 - x1) * dy3 - (y3 - y1) * dx3) / det;
            double u = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / det;

            if (t > 0.0001 && t < minT && u >= 0 && u <= 1)
            {
                minT = t;
                bestIntersection = new Vector3D(x1 + t * dx1, y1 + t * dy1, 0);
            }
        }

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

            composite.Add(new AddEntityOperation(_database, clone));
            _transactionManager.Submit(composite);

            OnFeedback?.Invoke("EXTEND: Obje uzatıldı. Devam edebilirsiniz.");
        }
        else
        {
            OnFeedback?.Invoke("EXTEND: Uzatma doğrultusunda bir kesişim bulunamadı.");
        }
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
