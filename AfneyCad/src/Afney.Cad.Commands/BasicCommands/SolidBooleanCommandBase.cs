using System;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Geometry.Topology;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: CSG Boolean Komut Tabanı (SolidBooleanCommandBase)
   NEDEN: Denetim raporunun bulduğu asıl açık — `GeneralSolidUnion`/`GeneralSolidSubtractor`/
          `GeneralSolidIntersector` (Afney.Cad.Geometry.Topology.Boolean, 506 testle
          doğrulanmış CSG kernel'i) `src/Afney.Cad.Presentation/` katmanında SIFIR referansla
          duruyordu. Bu sınıf, AutoCAD'in UNION/SUBTRACT/INTERSECT komutlarıyla aynı 2-nesne-
          seç akışını (ConnectFixtureCommand'daki "seç → seç → uygula" desenine birebir sadık)
          kullanarak kernel'i ilk kez gerçek bir kullanıcı komutuna bağlıyor.

   AKIŞ:
   1. Kullanıcı ilk SolidEntity'yi tıklar (A).
   2. Kullanıcı ikinci SolidEntity'yi tıklar (B).
   3. Alt sınıfın Combine(a,b) metodu (Union/Subtract/Intersect) çağrılır.
   4. Sonuç yeni bir SolidEntity olarak eklenir, A ve B silinir — ÜÇÜ DE TEK BİR
      CompositeOperation içinde (bkz. Afney.Cad.Database.Transactions.CompositeOperation)
      gönderilir, böylece TEK bir Ctrl+Z ile tüm işlem (silme+silme+ekleme) geri alınır.

   SINIRLAMA (v1, dokümante edilen sonraki adım): Seçim, PickEntityCommand'daki gibi basit
   bir "tıklama noktasına en yakın SolidEntity" araması ile yapılır (DistanceTo — taban
   sınıfın varsayılanı, SolidEntity için bounding-box merkezine mesafedir). Karmaşık/iç içe
   solid sahnelerinde daha hassas bir hit-test (ör. yüzey bazlı ray-cast) gerekebilir; şu an
   için tipik (birkaç solid'lik) sahnelerde yeterlidir.
*/
public abstract class SolidBooleanCommandBase : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly double _pickToleranceMm;
    private SolidEntity? _first;

    public abstract string CommandName { get; }
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    protected SolidBooleanCommandBase(CadDatabase database, TransactionManager transactionManager, double pickToleranceMm = 5000.0)
    {
        _database = database;
        _transactionManager = transactionManager;
        _pickToleranceMm = pickToleranceMm;
    }

    /// <summary>Alt sınıf: iki Solid'i birleştir/çıkar/kesiştir (kernel çağrısı).</summary>
    protected abstract Solid Combine(Solid a, Solid b);

    /// <summary>Sonuç işlemi için Undo/Redo geçmişinde görünecek isim (ör. "UNION").</summary>
    protected abstract string OperationName { get; }

    public void Start()
    {
        _first = null;
        OnFeedback?.Invoke($"{CommandName}: Birinci katı cismi (Solid) seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var picked = FindNearestSolid(point);
        if (picked == null)
        {
            OnFeedback?.Invoke($"{CommandName}: Solid bulunamadı — daha yakın tıklayın (veya önce BOX ile bir katı cisim oluşturun).");
            return;
        }

        if (_first == null)
        {
            _first = picked;
            OnFeedback?.Invoke($"{CommandName}: İkinci katı cismi seçin.");
            return;
        }

        if (ReferenceEquals(picked, _first))
        {
            OnFeedback?.Invoke($"{CommandName}: Birinci ile aynı nesne — farklı bir Solid seçin.");
            return;
        }

        var second = picked;
        try
        {
            var resultSolid = Combine(_first.Solid, second.Solid);
            var resultEntity = new SolidEntity(resultSolid)
            {
                Layer = _first.Layer,
                Color = _first.Color
            };

            var composite = new CompositeOperation(OperationName);
            composite.Add(new RemoveEntityOperation(_database, _first));
            composite.Add(new RemoveEntityOperation(_database, second));
            composite.Add(new AddEntityOperation(_database, resultEntity));
            _transactionManager.Submit(composite);

            OnFeedback?.Invoke($"{CommandName}: Tamamlandı ({OperationName}).");
        }
        catch (Exception ex)
        {
            // NEDEN try/catch: CSG kernel'i topolojik olarak geçersiz/kesişmeyen girdilerde
            // istisna fırlatabilir (bkz. GeneralSolidUnion/Subtractor/Intersector testleri) —
            // kullanıcıya anlamlı geri bildirim vermek, uygulamayı çökertmemek için.
            OnFeedback?.Invoke($"{CommandName}: İşlem başarısız — {ex.Message}");
        }
        finally
        {
            _first = null;
            OnCompleted?.Invoke();
        }
    }

    private SolidEntity? FindNearestSolid(Vector3D point)
    {
        SolidEntity? best = null;
        double bestDist = _pickToleranceMm;
        foreach (var e in _database.GetAllEntities())
        {
            if (e is SolidEntity s)
            {
                double d = s.DistanceTo(point);
                if (d < bestDist) { bestDist = d; best = s; }
            }
        }
        return best;
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape)
            Cancel();
    }

    public void Draw(IRenderContext context)
    {
        if (_first != null)
        {
            var bb = _first.GetBoundingBox();
            context.DrawRectangle(bb.Min, bb.Max, 0xFF00FF00, 0);
        }
    }

    public void Cancel()
    {
        _first = null;
        OnCompleted?.Invoke();
    }
}
