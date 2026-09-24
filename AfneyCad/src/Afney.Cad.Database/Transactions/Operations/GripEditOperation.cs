using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Database.Transactions.Operations;

/*
   NE: Grip Düzenleme İşlemi (GripEditOperation)
   NEDEN: Grip ile nesne şekil değiştirme (stretch) önceden hiçbir işlem kaydı oluşturmuyordu —
          geri alınamıyordu ve uzamsal indeks/topoloji güncellenmiyordu. Nesnenin grip noktaları
          düzenleme ÖNCESİ ve SONRASI olarak saklanır; Do/Undo grip'leri sırayla ilgili duruma
          taşır (MoveGripPointAt, nesnenin kendi düzenleme mantığıyla) ve UpdateEntity çağırır.
   NOT: Uygulama sırası indeks sırasıdır; uç noktalar ORTA/tüm-nesne grip'inden önce gelir, bu
        yüzden son grip zaten hedef konumda olur (delta 0) ve sonuç deterministiktir.
*/
public class GripEditOperation : IOperation
{
    private readonly CadEntity _entity;
    private readonly IReadOnlyList<Vector3D> _before;
    private readonly IReadOnlyList<Vector3D> _after;
    private readonly CadDatabase? _database;

    public string Name => "Grip düzenleme";

    public GripEditOperation(CadEntity entity, IReadOnlyList<Vector3D> before, IReadOnlyList<Vector3D> after, CadDatabase? database = null)
    {
        _entity = entity;
        _before = before;
        _after = after;
        _database = database;
    }

    public void Do() => Apply(_after);

    public void Undo() => Apply(_before);

    private void Apply(IReadOnlyList<Vector3D> grips)
    {
        for (int i = 0; i < grips.Count; i++)
            _entity.MoveGripPointAt(i, grips[i]);

        _database?.UpdateEntity(_entity);
    }
}
