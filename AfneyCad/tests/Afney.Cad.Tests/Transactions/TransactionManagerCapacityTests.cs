using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Transactions;

/*
   NE: TransactionManager Kapasite Limiti Testleri
   NEDEN: Kod denetiminde TransactionManager._undoStack'in sınırsız büyüdüğü (kapasite kontrolü
          yok) tespit edildi — uzun oturumlarda (1000+ işlem) bellek sürekli birikiyordu.
          MaxUndoLevels eklendi (varsayılan 200, referans: eski/kullanılmayan UndoRedoService).
          Bu testler: (1) limit aşıldığında en eski işlemin atıldığını, (2) son N işlemin hâlâ
          geri alınabildiğini, (3) mevcut Undo/Redo/Submit semantiğinin (redo temizleme dahil)
          bozulmadığını kanıtlar.
*/
public class TransactionManagerCapacityTests
{
    private static LineEntity MakeLine(double x) =>
        new(new Vector3D(x, 0, 0), new Vector3D(x, 100, 0));

    [Fact]
    public void Submit_ExceedingMaxUndoLevels_EvictsOldestEntries()
    {
        var db = new CadDatabase();
        db.TransactionManager.MaxUndoLevels = 5;

        for (int i = 0; i < 8; i++)
        {
            db.TransactionManager.Submit(new AddEntityOperation(db, MakeLine(i)));
        }

        // 8 işlem gönderildi ama limit 5 — undo yığınında en fazla 5 işlem kalmalı.
        // (LinkedList Count'a doğrudan erişim yok, davranışı Undo çağırarak doğruluyoruz.)
        Assert.Equal(8, db.GetAllEntities().OfType<LineEntity>().Count());

        int undoneCount = 0;
        while (db.TransactionManager.CanUndo)
        {
            db.TransactionManager.Undo();
            undoneCount++;
        }

        // Sadece son 5 işlem geri alınabilir olmalı (en eski 3'ü atıldı ve artık geri alınamaz).
        Assert.Equal(5, undoneCount);

        // En eski 3 satır (i=0,1,2) geri alınamadığı için veritabanında kalmalı,
        // en yeni 5 satır (i=3..7) geri alındığı için silinmiş olmalı.
        Assert.Equal(3, db.GetAllEntities().OfType<LineEntity>().Count());
    }

    [Fact]
    public void Submit_WithinLimit_AllEntriesRemainUndoable()
    {
        var db = new CadDatabase();
        db.TransactionManager.MaxUndoLevels = 200;

        for (int i = 0; i < 10; i++)
        {
            db.TransactionManager.Submit(new AddEntityOperation(db, MakeLine(i)));
        }

        int undoneCount = 0;
        while (db.TransactionManager.CanUndo)
        {
            db.TransactionManager.Undo();
            undoneCount++;
        }

        Assert.Equal(10, undoneCount);
        Assert.Empty(db.GetAllEntities().OfType<LineEntity>());
    }

    [Fact]
    public void Submit_ClearsRedoStack_StandardSemanticsPreserved()
    {
        var db = new CadDatabase();
        var tm = db.TransactionManager;

        tm.Submit(new AddEntityOperation(db, MakeLine(0)));
        tm.Submit(new AddEntityOperation(db, MakeLine(1)));

        tm.Undo();
        Assert.True(tm.CanRedo);

        // Yeni bir işlem gönderildiğinde redo geçmişi temizlenmeli (standart undo/redo semantiği).
        tm.Submit(new AddEntityOperation(db, MakeLine(2)));

        Assert.False(tm.CanRedo);
    }

    [Fact]
    public void Undo_Then_Redo_RestoresEntity()
    {
        var db = new CadDatabase();
        var tm = db.TransactionManager;
        var line = MakeLine(0);

        tm.Submit(new AddEntityOperation(db, line));
        Assert.Single(db.GetAllEntities().OfType<LineEntity>());

        tm.Undo();
        Assert.Empty(db.GetAllEntities().OfType<LineEntity>());

        tm.Redo();
        Assert.Single(db.GetAllEntities().OfType<LineEntity>());
    }
}
