using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Database.Transactions.Operations;

/*
NE: Transform İşlemi (Döndürme, Ölçekleme, Yansıtma)
NEDEN: Nesne geometrisini değiştiren operasyonları Undo/Redo ile yönetmek için.

MÜHENDİSLİK NOTU (Kemal):
Transform sonrası database.UpdateEntity çağrısı yapılarak topoloji grafı güncellenir.
*/
public class TransformEntityOperation : IOperation
{
    private readonly CadEntity _entity;
    private readonly Matrix4x4 _transform;
    private readonly Matrix4x4 _inverseTransform;
    private readonly CadDatabase? _database; // YENİ: Topoloji güncellemesi için


    public string Name => "Nesne dönüşümü";

    public TransformEntityOperation(CadEntity entity, Matrix4x4 transform, Matrix4x4 inverseTransform, CadDatabase? database = null)
    {
        _entity = entity;
        _transform = transform;
        _inverseTransform = inverseTransform;
        _database = database;
    }

    /*
       NE: Ä°ÅŸlemi Yap (Do)
       NEDEN: Matris transformasyonunu nesneye uygulayÄ±p veritabanÄ±nÄ± ve topolojiyi gÃ¼ncellemek iÃ§in.
    */
    public void Do()
    {
        _entity.Transform(_transform);
        _database?.UpdateEntity(_entity); // Topoloji güncelle
    }

    /*
       NE: Ä°ÅŸlemi Geri Al (Undo)
       NEDEN: Ters matrisi uygulayarak nesneyi eski konumuna/boyutuna getirmek iÃ§in.
    */
    public void Undo()
    {
        _entity.Transform(_inverseTransform);
        _database?.UpdateEntity(_entity); // Topoloji güncelle
    }
}
