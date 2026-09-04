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
   NE: CHAMFER (Pah Kırma) Komutu
   NEDEN: AutoCAD'in en temel iki düzenleme komutundan biri (diğeri FILLET) kod tabanında hiç
          yoktu. Kullanıcı iki doğruyu sırayla tıklar; her doğru, tıklanan uca en yakın uç
          korunacak şekilde kesişim noktasından dist1/dist2 mesafesindeki noktaya kadar kısaltılır
          ve aralarına düz bir pah çizgisi (LineEntity) eklenir.
   KAPSAM: SADECE iki ayrı LineEntity. LwPolyline segmentleri arası chamfer, Pipe/Duct, Circle/Arc
           ile chamfer KAPSAM DIŞI (bu oturumda bilinçli olarak ertelendi — bkz. FilletChamferMath).
*/
public class ChamferCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly double _dist1;
    private readonly double _dist2;
    private readonly double _hitTolerance;

    private LineEntity? _firstLine;
    private Vector3D _firstPick;

    public string CommandName => "CHAMFER";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public ChamferCommand(CadDatabase database, TransactionManager transactionManager, double currentZoom, double dist1, double dist2)
    {
        _database = database;
        _transactionManager = transactionManager;
        _dist1 = dist1;
        _dist2 = dist2;
        _hitTolerance = 10.0 / Math.Max(0.001, currentZoom);
    }

    public void Start()
    {
        if (_dist1 <= 0 || _dist2 <= 0)
        {
            OnFeedback?.Invoke("CHAMFER: Mesafeler pozitif olmalı. Komut iptal.");
            OnCompleted?.Invoke();
            return;
        }
        OnFeedback?.Invoke($"CHAMFER (D1={_dist1:F2}, D2={_dist2:F2}): Birinci doğruyu seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var line = FindNearestLine(point);
        if (line == null)
        {
            OnFeedback?.Invoke("CHAMFER: Bu noktada bir doğru (Line) bulunamadı.");
            return;
        }

        if (_firstLine == null)
        {
            _firstLine = line;
            _firstPick = point;
            OnFeedback?.Invoke("CHAMFER: İkinci doğruyu seçin.");
            return;
        }

        if (line == _firstLine)
        {
            OnFeedback?.Invoke("CHAMFER: Lütfen BİRİNCİDEN FARKLI bir doğru seçin.");
            return;
        }

        ApplyChamfer(_firstLine, _firstPick, line, point);
    }

    private void ApplyChamfer(LineEntity a, Vector3D pickA, LineEntity b, Vector3D pickB)
    {
        bool ok = FilletChamferMath.TryComputeChamfer(
            a.StartPoint, a.EndPoint, b.StartPoint, b.EndPoint,
            _dist1, _dist2, pickA, pickB, out var result, out var error);

        if (!ok)
        {
            OnFeedback?.Invoke($"CHAMFER: {error} Tekrar deneyin (ESC ile iptal).");
            _firstLine = null;
            return;
        }

        var composite = new CompositeOperation("Chamfer Entities");
        composite.Add(new RemoveEntityOperation(_database, a));
        composite.Add(new RemoveEntityOperation(_database, b));

        var newA = new LineEntity(result.TrimmedAStart, result.TrimmedAEnd) { Color = a.Color, Layer = a.Layer, Linetype = a.Linetype };
        var newB = new LineEntity(result.TrimmedBStart, result.TrimmedBEnd) { Color = b.Color, Layer = b.Layer, Linetype = b.Linetype };
        var chamferLine = new LineEntity(result.ChamferStart, result.ChamferEnd) { Color = a.Color, Layer = a.Layer };

        composite.Add(new AddEntityOperation(_database, newA));
        composite.Add(new AddEntityOperation(_database, newB));
        composite.Add(new AddEntityOperation(_database, chamferLine));

        _transactionManager.Submit(composite);
        OnFeedback?.Invoke("CHAMFER: İki doğru pah ile birleştirildi.");
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
