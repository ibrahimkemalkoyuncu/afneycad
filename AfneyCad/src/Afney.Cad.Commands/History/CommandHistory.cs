using Afney.Cad.Database.Transactions;

namespace Afney.Cad.Commands.History;

/*
   NE: Komut Geçmişi Yöneticisi (CommandHistory)
   NEDEN: Kullanıcı işlemlerini (Undo/Redo) ve veritabanı işlemlerini (Transactions) koordine etmek için.
*/
public class CommandHistory
{
    // Transaction Manager (Veritabanı İşlemleri Yöneticisi)
    // Core Engine'in bir parçasıdır ve tüm veri değişikliklerini yönetir.
    public TransactionManager TransactionManager { get; }

    /*
       NE: Yapıcı Metod (Constructor)
       NEDEN: Veritabanı işlem yöneticisini (TransactionManager) devralmak ve herhangi bir değişiklikte (Undo/Redo durum değişimi) UI'yı uyarmak için olay bağlantısını kurmak için.
    */
    public CommandHistory(TransactionManager transactionManager)
    {
        TransactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        
        // TransactionManager durumu değiştiğinde kendi eventimizi tetikle
        TransactionManager.StateChanged += () => OnHistoryChanged?.Invoke();
    }

    public bool CanUndo => TransactionManager.CanUndo;
    public bool CanRedo => TransactionManager.CanRedo;
    
    public event Action? OnHistoryChanged;

    /*
       NE: Geri Al (Undo)
       NEDEN: Veritabanında yapılan son işlemi (Entity ekleme, silme, taşıma vb.) tersine çevirerek çizimi bir önceki kararlı durumuna döndürmek için.
    */
    public void Undo()
    {
        if (CanUndo)
        {
            TransactionManager.Undo();
        }
    }

    /*
       NE: Yinele (Redo)
       NEDEN: Geri alınan bir işlemi (Undo), kullanıcı isteğiyle tekrar kararlı duruma getirerek veri akışını ileri doğru sürdürmek için.
    */
    public void Redo()
    {
        if (CanRedo)
        {
            TransactionManager.Redo();
        }
    }
    
    // UI Metinleri (İleride IOperation üzerinden Description alınabilir)
    public string GetUndoText() => CanUndo ? "Geri Al" : "Geri Al";
    public string GetRedoText() => CanRedo ? "Yinele" : "Yinele";
}
