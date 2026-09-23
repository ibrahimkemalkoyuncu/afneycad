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

    /*
       NE: Tüm Alt İşlemleri Uygula (Do) — Atomik (Hepsi ya da Hiçbiri)
       NEDEN: Önceden bir alt işlem (Do) ortasında exception fırlatırsa, o ana kadar
              uygulanmış alt işlemler veritabanında UYGULANMIŞ kalıyordu ama exception
              TransactionManager.Submit'i _undoStack.AddLast'e ulaşmadan kesintiye
              uğrattığı için hiçbir geri alma kaydı oluşmuyordu — kısmi, geri alınamaz
              bir mutasyon veritabanında sessizce kalıyordu. Artık bir alt işlem
              başarısız olursa, o ana kadar başarıyla uygulanmış olanlar ters sırayla
              geri alınıp (rollback) veritabanı Do() öncesi tutarlı durumuna döndürülür,
              ardından orijinal exception olduğu gibi yeniden fırlatılır.
    */
    public void Do()
    {
        int applied = 0;
        try
        {
            foreach (var op in _operations)
            {
                op.Do();
                applied++;
            }
        }
        catch
        {
            for (int i = applied - 1; i >= 0; i--)
            {
                try { _operations[i].Undo(); }
                catch (Exception rollbackEx)
                {
                    // Rollback'in kendisi başarısız olsa bile orijinal hatayı gizlememek için
                    // sadece izliyoruz (bu proje Serilog'a bağımlı değil — BCL Trace yeterli);
                    // veritabanı bu noktada tutarsız kalabilir ama orijinal exception fırlatılmaya devam ediyor.
                    System.Diagnostics.Trace.TraceError(
                        $"CompositeOperation rollback sırasında ikincil hata (alt işlem #{i}): {rollbackEx}");
                }
            }
            throw;
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