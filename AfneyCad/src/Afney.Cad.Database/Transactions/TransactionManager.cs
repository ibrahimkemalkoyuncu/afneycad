using System.Collections.Generic;

namespace Afney.Cad.Database.Transactions;

/*
NE:
İşlem Yöneticisi (Transaction Manager).

NE İÇİN:
Yapılan işlemleri sırasıyla kaydetmek ve istendiğinde geri almak (Undo) veya yinelemek (Redo) için.

NEREDE:
Application Layer (Engine parçası).

NE ZAMAN:
Herhangi bir veri değişikliği işleminde.

AMAÇ:
Kullanıcı hatalarını tolere etmek (User Error Tolerance) ve veri bütünlüğünü korumak.
*/
public class TransactionManager
{
    private readonly Stack<IOperation> _undoStack = new();
    private readonly Stack<IOperation> _redoStack = new();

    public event Action? StateChanged; // UI (Button state) güncellemek için

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /*
       NE: İşlemi Gönder (Submit)
       NEDEN: Yeni bir komutu (Ekle, Sil, Taşı vb.) yürütmek, redo geçmişini temizlemek ve işlemi geri alınabilirler listesine dahil etmek için.
    */
    public void Submit(IOperation operation)
    {
        // 1. Redo stack temizlenir (Çünkü tarih değişti)
        _redoStack.Clear();
        
        // 2. İşlem uygulanır (Execute)
        operation.Do();

        // 3. Geçmişe eklenir
        _undoStack.Push(operation);

        StateChanged?.Invoke();
    }

    /*
       NE: Geri Al (Undo)
       NEDEN: En son yapılan işlemi tersine çevirerek veritabanını bir önceki tutarlı durumuna döndürmek ve işlemi yineleme (Redo) yığınına taşımak için.
    */
    public void Undo()
    {
        if (!CanUndo) return;

        var op = _undoStack.Pop();
        op.Undo();
        _redoStack.Push(op);

        StateChanged?.Invoke();
    }

    /*
       NE: Yinele (Redo)
       NEDEN: Daha önce geri alınmış (Undo) bir işlemi tekrar yürüterek kullanıcının son kararını ileriye doğru uygulamak ve işlemi tekrar geri alınabilirler (Undo) yığınına koymak için.
    */
    public void Redo()
    {
        if (!CanRedo) return;

        var op = _redoStack.Pop();
        op.Do();
        _undoStack.Push(op);

        StateChanged?.Invoke();
    }
}
