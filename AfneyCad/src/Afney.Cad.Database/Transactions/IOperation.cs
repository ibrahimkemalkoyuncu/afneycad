namespace Afney.Cad.Database.Transactions;

public interface IOperation
{
    string Name { get; }
    void Do();
    void Undo();
}
