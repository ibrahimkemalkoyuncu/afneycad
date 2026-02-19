namespace Afney.Cad.Database.Transactions;

public class CompositeOperation : IOperation
{
    private readonly List<IOperation> _operations = new();
    public string Name { get; }

    public CompositeOperation(string name)
    {
        Name = name;
    }

    public void Add(IOperation operation)
    {
        _operations.Add(operation);
    }

    public void Do()
    {
        foreach (var op in _operations)
        {
            op.Do();
        }
    }

    public void Undo()
    {
        // Geri alma (Undo) işlemi için sıralamayı tersine çevir
        for (int i = _operations.Count - 1; i >= 0; i--)
        {
            _operations[i].Undo();
        }
    }
}