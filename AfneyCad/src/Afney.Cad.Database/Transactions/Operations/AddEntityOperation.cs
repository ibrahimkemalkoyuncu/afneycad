using System;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;

namespace Afney.Cad.Database.Transactions.Operations;

/*
NE:
Varlık Ekleme Operasyonu (Command Objects).

NE İÇİN:
Veritabanına yeni bir nesne eklendiğinde bu işlemi kapsüllemek (Encapsulate) için.

NEREDE:
Transaction Katmanında.

AMAÇ:
Atomik, geri alınabilir veri girişi.
*/
public class AddEntityOperation : IOperation
{
    private readonly CadDatabase _database;
    private readonly CadEntity _entity;

    public string Name => "Add Entity";

    public AddEntityOperation(CadDatabase database, CadEntity entity)
    {
        _database = database;
        _entity = entity;
    }

    public void Do()
    {
        _database.AddEntity(_entity);
    }

    public void Undo()
    {
        _database.RemoveEntity(_entity.Id);
    }
}
