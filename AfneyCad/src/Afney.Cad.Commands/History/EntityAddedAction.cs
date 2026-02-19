using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;

namespace Afney.Cad.Commands.History;

/*
   NE: Nesne Ekleme Geri Alma Aksiyonu (EntityAddedAction)
   NEDEN: Kullanıcının çizdiği bir nesneyi geri alabilmesi (Undo) veya tekrar ileri alabilmesi (Redo) için.

   MÜHENDİSLİK DETAYI:
   - Command Pattern ve Memento yaklaşımlarının bir parçasıdır.
   - Nesnenin kendisini (Entity) ve bağlı olduğu veritabanını (Database) referans olarak tutar.
   - Undo/Redo işlemleri veritabanı olaylarını (Events) tetikleyerek ekranın güncellenmesini sağlar.
*/
public class EntityAddedAction : IReversibleAction
{
    private readonly CadDatabase _database;
    private readonly CadEntity _entity;
    
    public string DisplayName => $"Nesne Ekle: {_entity.GetType().Name}";

    public EntityAddedAction(CadDatabase database, CadEntity entity)
    {
        _database = database;
        _entity = entity;
    }

    /// <summary>
    /// Nesneyi veritabanından ID ile kaldırarak çizimi geri alır.
    /// </summary>
    public void Undo()
    {
        _database.RemoveEntity(_entity.Id);
    }

    /// <summary>
    /// Daha önce eklenmiş olan nesne referansını veritabanına geri koyar.
    /// </summary>
    public void Redo()
    {
        _database.AddEntity(_entity);
    }
}

