using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;

namespace Afney.Cad.Commands.BasicCommands;

/// <summary>UNION: iki seçili SolidEntity'yi CSG birleşimiyle (GeneralSolidUnion) tek katı cisimde toplar.</summary>
public class SolidUnionCommand : SolidBooleanCommandBase
{
    public override string CommandName => "UNION";
    protected override string OperationName => "Katı Birleştirme (UNION)";

    public SolidUnionCommand(CadDatabase database, TransactionManager transactionManager)
        : base(database, transactionManager)
    {
    }

    protected override Solid Combine(Solid a, Solid b) => GeneralSolidUnion.Union(a, b);
}
