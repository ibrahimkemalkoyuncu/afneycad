using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Geometry.Topology;
using Afney.Cad.Geometry.Topology.Boolean;

namespace Afney.Cad.Commands.BasicCommands;

/// <summary>SUBTRACT: birinci seçilen SolidEntity'den ikincisini CSG çıkarmasıyla (GeneralSolidSubtractor) çıkarır.</summary>
public class SolidSubtractCommand : SolidBooleanCommandBase
{
    public override string CommandName => "SUBTRACT";
    protected override string OperationName => "Katı Çıkarma (SUBTRACT)";

    public SolidSubtractCommand(CadDatabase database, TransactionManager transactionManager)
        : base(database, transactionManager)
    {
    }

    protected override Solid Combine(Solid a, Solid b) => GeneralSolidSubtractor.Subtract(a, b);
}
