using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Database.Transactions.Operations;

/*
NE: Taşıma İşlemi (Move)
NEDEN: Nesneyi hareket ettiren operasyonları Undo/Redo ile yönetmek için.

MÜHENDİSLİK NOTU (Kemal):
Move sonrası database.UpdateEntity çağrısı yapılarak topoloji grafı güncellenir.
*/
public class MoveEntityOperation : IOperation
{
    private readonly CadEntity _entity;
    private readonly Vector3D _delta;
    private readonly CadDatabase? _database; // YENİ: Topoloji güncellemesi için

    public string Name => "Nesne taşıma";

    public MoveEntityOperation(CadEntity entity, Vector3D delta, CadDatabase? database = null)
    {
        _entity = entity;
        _delta = delta;
        _database = database;
    }

    public void Do()
    {
        _entity.Move(_delta);
        _database?.UpdateEntity(_entity); // Topoloji güncelle
    }

    public void Undo()
    {
        // Ters yön vektörü hesapla
        var reverse = new Vector3D(-_delta.X, -_delta.Y, -_delta.Z);
        _entity.Move(reverse);
        _database?.UpdateEntity(_entity); // Topoloji güncelle
    }
}