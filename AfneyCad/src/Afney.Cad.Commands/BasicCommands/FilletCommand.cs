using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
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
   NOT: İki-doğru seçim/tıklama iskeleti TwoLineEditCommandBase'de (ChamferCommand ile ortak,
        Session #75 mimari denetiminde birleştirildi) — burada sadece FILLET'e özgü kısım var.
*/
public class FilletCommand : TwoLineEditCommandBase
{
    private readonly double _radius;

    public override string CommandName => "FILLET";

    public FilletCommand(CadDatabase database, TransactionManager transactionManager, double currentZoom, double radius)
        : base(database, transactionManager, currentZoom)
    {
        _radius = radius;
    }

    protected override bool ValidateParameters(out string? error)
    {
        if (_radius <= 0)
        {
            error = "Yarıçap pozitif olmalı.";
            return false;
        }
        error = null;
        return true;
    }

    protected override string StartupPrompt() => $"FILLET (R={_radius:F2})";

    protected override string SuccessMessage() => "FILLET: İki doğru kavisle birleştirildi.";

    protected override bool TryBuildOperation(
        LineEntity a, Vector3D pickA, LineEntity b, Vector3D pickB,
        out CompositeOperation composite, out string? error)
    {
        bool ok = FilletChamferMath.TryComputeFillet(
            a.StartPoint, a.EndPoint, b.StartPoint, b.EndPoint,
            _radius, pickA, pickB, out var result, out error);

        if (!ok)
        {
            composite = null!;
            return false;
        }

        composite = new CompositeOperation("Fillet Entities");
        composite.Add(new RemoveEntityOperation(Database, a));
        composite.Add(new RemoveEntityOperation(Database, b));

        var newA = new LineEntity(result.TrimmedAStart, result.TrimmedAEnd) { Color = a.Color, Layer = a.Layer, Linetype = a.Linetype };
        var newB = new LineEntity(result.TrimmedBStart, result.TrimmedBEnd) { Color = b.Color, Layer = b.Layer, Linetype = b.Linetype };
        var arc = new ArcEntity(result.ArcCenter, result.ArcRadius, result.ArcStartAngle, result.ArcEndAngle) { Color = a.Color, Layer = a.Layer };

        composite.Add(new AddEntityOperation(Database, newA));
        composite.Add(new AddEntityOperation(Database, newB));
        composite.Add(new AddEntityOperation(Database, arc));

        return true;
    }
}
