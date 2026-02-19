using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;

namespace Afney.Cad.Commands.History;

/*
   NE: Nesne Silme Geri Alma Aksiyonu (EntityRemovedAction)
   NEDEN: Silinen bir nesneyi geri alma (Undo) veya tekrar silme (Redo) işlemlerini yönetmek için.

   MÜHENDİSLİK DETAYI:
   - Command Pattern yapısına uygundur.
   - Nesnenin kendisini referans olarak tutar, böylece geri ekleme (Undo) işlemi sırasında tüm öznitelikleri korunur.
*/
public class EntityRemovedAction : IReversibleAction
{
    private readonly CadDatabase _database;
    private readonly CadEntity _entity;
    
    public string DisplayName => $"Remove {_entity.GetType().Name}";

    public EntityRemovedAction(CadDatabase database, CadEntity entity)
    {
        _database = database;
        _entity = entity;
    }

    public void Undo()
    {
        _database.AddEntity(_entity);
    }

    public void Redo()
    {
        _database.RemoveEntity(_entity.Id);
    }
}
