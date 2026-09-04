using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;

namespace Afney.Cad.Commands.BasicCommands;

/// <summary>INTERSECT: iki seçili SolidEntity'nin CSG kesişimini (GeneralSolidIntersector) tek katı cisimde üretir.</summary>
public class SolidIntersectCommand : SolidBooleanCommandBase
{
    public override string CommandName => "INTERSECT";
    protected override string OperationName => "Katı Kesiştirme (INTERSECT)";

    public SolidIntersectCommand(CadDatabase database, TransactionManager transactionManager)
        : base(database, transactionManager)
    {
    }

    protected override Solid Combine(Solid a, Solid b) => GeneralSolidIntersector.Intersect(a, b);
}
