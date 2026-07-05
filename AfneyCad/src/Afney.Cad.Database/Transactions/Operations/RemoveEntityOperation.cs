using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;

namespace Afney.Cad.Database.Transactions.Operations;

/*
NE: Nesne Silme Operasyonu (Remove)
NEDEN: Bir nesnenin veritabanından silinmesini kapsülleyerek Undo (Geri Al) ile nesneyi tekrar geri getirebilmek için.
*/
public class RemoveEntityOperation : IOperation
{
    private readonly CadDatabase _database;
    private readonly CadEntity _entity;

    public string Name => "Nesne silme";

    public RemoveEntityOperation(CadDatabase database, CadEntity entity)
    {
        _database = database;
        _entity = entity;
    }

    /*
       NE: Ä°ÅŸlemi Yap (Do)
       NEDEN: Nesneyi ID Ã¼zerinden veritabanÄ±ndan ve mekansal indisten kaldÄ±rmak iÃ§in.
    */
    public void Do()
    {
        _database.RemoveEntity(_entity.Id);
    }

    /*
       NE: Ä°ÅŸlemi Geri Al (Undo)
       NEDEN: Daha Ã¶nce silinen nesne referansÄ±nÄ± veritabanÄ±na tekrar ekleyerek geri getirmek iÃ§in.
    */
    public void Undo()
    {
        _database.AddEntity(_entity);
    }
}