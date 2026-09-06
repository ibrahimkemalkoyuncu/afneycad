using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
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
   NOT: İki-doğru seçim/tıklama iskeleti TwoLineEditCommandBase'de (FilletCommand ile ortak,
        Session #75 mimari denetiminde birleştirildi) — burada sadece CHAMFER'a özgü kısım var.
*/
public class ChamferCommand : TwoLineEditCommandBase
{
    private readonly double _dist1;
    private readonly double _dist2;

    public override string CommandName => "CHAMFER";

    public ChamferCommand(CadDatabase database, TransactionManager transactionManager, double currentZoom, double dist1, double dist2)
        : base(database, transactionManager, currentZoom)
    {
        _dist1 = dist1;
        _dist2 = dist2;
    }

    protected override bool ValidateParameters(out string? error)
    {
        if (_dist1 <= 0 || _dist2 <= 0)
        {
            error = "Mesafeler pozitif olmalı.";
            return false;
        }
        error = null;
        return true;
    }

    protected override string StartupPrompt() => $"CHAMFER (D1={_dist1:F2}, D2={_dist2:F2})";

    protected override string SuccessMessage() => "CHAMFER: İki doğru pah ile birleştirildi.";

    protected override bool TryBuildOperation(
        LineEntity a, Vector3D pickA, LineEntity b, Vector3D pickB,
        out CompositeOperation composite, out string? error)
    {
        bool ok = FilletChamferMath.TryComputeChamfer(
            a.StartPoint, a.EndPoint, b.StartPoint, b.EndPoint,
            _dist1, _dist2, pickA, pickB, out var result, out error);

        if (!ok)
        {
            composite = null!;
            return false;
        }

        composite = new CompositeOperation("Chamfer Entities");
        composite.Add(new RemoveEntityOperation(Database, a));
        composite.Add(new RemoveEntityOperation(Database, b));

        var newA = new LineEntity(result.TrimmedAStart, result.TrimmedAEnd) { Color = a.Color, Layer = a.Layer, Linetype = a.Linetype };
        var newB = new LineEntity(result.TrimmedBStart, result.TrimmedBEnd) { Color = b.Color, Layer = b.Layer, Linetype = b.Linetype };
        var chamferLine = new LineEntity(result.ChamferStart, result.ChamferEnd) { Color = a.Color, Layer = a.Layer };

        composite.Add(new AddEntityOperation(Database, newA));
        composite.Add(new AddEntityOperation(Database, newB));
        composite.Add(new AddEntityOperation(Database, chamferLine));

        return true;
    }
}
