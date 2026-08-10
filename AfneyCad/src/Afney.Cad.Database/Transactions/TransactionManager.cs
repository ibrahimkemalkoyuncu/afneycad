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
    /*
       NOT: _undoStack, Stack<T> yerine LinkedList<T> ile LIFO (yığın) olarak kullanılıyor.
       Neden: Kapasite limiti aşıldığında en ESKİ (tabandaki) işlemi atmamız gerekiyor.
       Stack<T> sadece tepeden Push/Pop destekler, tabandan silme yapamaz (O(n) kopyalama gerektirir).
       LinkedList<T> ise AddLast/RemoveLast (Push/Pop, O(1)) ile RemoveFirst (taban eviction, O(1))
       işlemlerinin ikisini de O(1) karmaşıklıkla sağlar.
    */
    private readonly LinkedList<IOperation> _undoStack = new();
    private readonly Stack<IOperation> _redoStack = new();

    // Sınırsız büyümeyi önlemek için maksimum undo seviyesi.
    // Referans: Afney.Cad.Application.Services.UndoRedoService.MaxStackSize (kullanılmayan ölü koddu, silindi).
    public int MaxUndoLevels { get; set; } = 200;

    public event Action? StateChanged; // UI (Button state) güncellemek için

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? PeekUndoName() => _undoStack.Count > 0 ? _undoStack.Last!.Value.Name : null;
    public string? PeekRedoName() => _redoStack.Count > 0 ? _redoStack.Peek().Name : null;

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
        _undoStack.AddLast(operation);

        // 4. Kapasite limiti aşıldıysa en eski işlemi (tabandaki) at.
        // Uzun oturumlarda (1000+ işlem) sınırsız bellek birikimini önler.
        while (_undoStack.Count > MaxUndoLevels)
        {
            _undoStack.RemoveFirst();
        }

        StateChanged?.Invoke();
    }

    /*
       NE: Geri Al (Undo)
       NEDEN: En son yapılan işlemi tersine çevirerek veritabanını bir önceki tutarlı durumuna döndürmek ve işlemi yineleme (Redo) yığınına taşımak için.
    */
    public void Undo()
    {
        if (!CanUndo) return;

        var op = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
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
        _undoStack.AddLast(op);

        // Redo sırasında da limit aşılabilir (teorik olarak MaxUndoLevels'i aşmaz çünkü
        // undo/redo arasında toplam işlem sayısı sabittir, ama güvenlik için kontrol edilir).
        while (_undoStack.Count > MaxUndoLevels)
        {
            _undoStack.RemoveFirst();
        }

        StateChanged?.Invoke();
    }
}
