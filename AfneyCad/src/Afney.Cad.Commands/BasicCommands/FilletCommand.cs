using System;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Algorithms;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: FILLET (Kavisli Birleştirme) Komutu
   NEDEN: AutoCAD'in en temel iki düzenleme komutundan biri (diğeri CHAMFER) kod tabanında hiç
          yoktu. Kullanıcı iki doğruyu sırayla tıklar; her doğru, tıklanan uca en yakın uç
          korunacak şekilde teğet noktasına kadar kısaltılır ve aralarına R yarıçaplı, her ikisine
          teğet bir yay (ArcEntity) eklenir.
   KAPSAM: SADECE iki ayrı LineEntity. LwPolyline segmentleri arası fillet, Pipe/Duct, Circle/Arc
           ile fillet KAPSAM DIŞI (bu oturumda bilinçli olarak ertelendi — bkz. FilletChamferMath).
*/
public class FilletCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly double _radius;
    private readonly double _hitTolerance;

    private LineEntity? _firstLine;
    private Vector3D _firstPick;

    public string CommandName => "FILLET";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public FilletCommand(CadDatabase database, TransactionManager transactionManager, double currentZoom, double radius)
    {
        _database = database;
        _transactionManager = transactionManager;
        _radius = radius;
        _hitTolerance = 10.0 / Math.Max(0.001, currentZoom);
    }

    public void Start()
    {
        if (_radius <= 0)
        {
            OnFeedback?.Invoke("FILLET: Yarıçap pozitif olmalı. Komut iptal.");
            OnCompleted?.Invoke();
            return;
        }
        OnFeedback?.Invoke($"FILLET (R={_radius:F2}): Birinci doğruyu seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var line = FindNearestLine(point);
        if (line == null)
        {
            OnFeedback?.Invoke("FILLET: Bu noktada bir doğru (Line) bulunamadı.");
            return;
        }

        if (_firstLine == null)
        {
            _firstLine = line;
            _firstPick = point;
            OnFeedback?.Invoke("FILLET: İkinci doğruyu seçin.");
            return;
        }

        if (line == _firstLine)
        {
            OnFeedback?.Invoke("FILLET: Lütfen BİRİNCİDEN FARKLI bir doğru seçin.");
            return;
        }

        ApplyFillet(_firstLine, _firstPick, line, point);
    }

    private void ApplyFillet(LineEntity a, Vector3D pickA, LineEntity b, Vector3D pickB)
    {
        bool ok = FilletChamferMath.TryComputeFillet(
            a.StartPoint, a.EndPoint, b.StartPoint, b.EndPoint,
            _radius, pickA, pickB, out var result, out var error);

        if (!ok)
        {
            OnFeedback?.Invoke($"FILLET: {error} Tekrar deneyin (ESC ile iptal).");
            _firstLine = null;
            return;
        }

        var composite = new CompositeOperation("Fillet Entities");
        composite.Add(new RemoveEntityOperation(_database, a));
        composite.Add(new RemoveEntityOperation(_database, b));

        var newA = new LineEntity(result.TrimmedAStart, result.TrimmedAEnd) { Color = a.Color, Layer = a.Layer, Linetype = a.Linetype };
        var newB = new LineEntity(result.TrimmedBStart, result.TrimmedBEnd) { Color = b.Color, Layer = b.Layer, Linetype = b.Linetype };
        var arc = new ArcEntity(result.ArcCenter, result.ArcRadius, result.ArcStartAngle, result.ArcEndAngle) { Color = a.Color, Layer = a.Layer };

        composite.Add(new AddEntityOperation(_database, newA));
        composite.Add(new AddEntityOperation(_database, newB));
        composite.Add(new AddEntityOperation(_database, arc));

        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("FILLET: İki doğru kavisle birleştirildi.");
        _firstLine = null;
        OnCompleted?.Invoke();
    }

    private LineEntity? FindNearestLine(Vector3D point)
    {
        LineEntity? target = null;
        double minDst = _hitTolerance;

        var lines = _database.GetAllEntities().OfType<LineEntity>().ToList();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            double d = lines[i].DistanceTo(point);
            if (d < minDst)
            {
                minDst = d;
                target = lines[i];
            }
        }
        return target;
    }

    public void OnPointerMoved(Vector3D point) { }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape || key == InputKey.Enter)
            Cancel();
    }

    public void Cancel()
    {
        _firstLine = null;
        OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context) { }
}
