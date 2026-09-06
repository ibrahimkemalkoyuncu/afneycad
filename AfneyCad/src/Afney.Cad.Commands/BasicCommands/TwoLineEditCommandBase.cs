using System;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: İki-Doğru Düzenleme Komutu Ortak Tabanı (TwoLineEditCommandBase)
   NEDEN — Session #75 mimari denetiminde bulunan kod tekrarı: FilletCommand ve ChamferCommand
          neredeyse birebir aynı iskeleti (en yakın doğruyu bul, birinci/ikinci tıklama akışı,
          hata mesajı formatı, ESC/Enter iptal) kopyala-yapıştır ile taşıyordu — SolidBooleanCommandBase
          (Union/Subtract/Intersect için) zaten aynı desenin doğru soyutlamasıydı, ama Fillet/Chamfer'a
          hiç uygulanmamıştı. Ortak akış burada toplanıp, sadece komuta özgü kısım (parametre
          doğrulama + FilletChamferMath çağrısı + üretilecek entity'ler) alt sınıflara bırakıldı.
   KAPSAM: SADECE iki ayrı LineEntity (bkz. FilletChamferMath dosya başı notu — bu sınırlama
          değişmedi, sadece iskelet birleştirildi).
*/
public abstract class TwoLineEditCommandBase : ICadCommand
{
    protected readonly CadDatabase Database;
    protected readonly TransactionManager TransactionManager;
    private readonly double _hitTolerance;

    private LineEntity? _firstLine;
    private Vector3D _firstPick;

    public abstract string CommandName { get; }
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    protected TwoLineEditCommandBase(CadDatabase database, TransactionManager transactionManager, double currentZoom)
    {
        Database = database;
        TransactionManager = transactionManager;
        _hitTolerance = 10.0 / Math.Max(0.001, currentZoom);
    }

    /// <summary>Komuta özgü parametreleri doğrular (ör. yarıçap/mesafe pozitif mi).</summary>
    protected abstract bool ValidateParameters(out string? error);

    /// <summary>Başlangıç geri bildirim mesajının komuta özgü öneki (ör. "FILLET (R=2.00)").</summary>
    protected abstract string StartupPrompt();

    /// <summary>İşlem başarıyla tamamlandığında gösterilecek mesaj.</summary>
    protected abstract string SuccessMessage();

    /// <summary>
    /// FilletChamferMath'i çağırıp sonucu bir CompositeOperation'a (2 doğrunun silinmesi +
    /// yeni entity'lerin eklenmesi) dönüştürür. Başarısızlıkta false döner, composite null kalır.
    /// </summary>
    protected abstract bool TryBuildOperation(
        LineEntity a, Vector3D pickA, LineEntity b, Vector3D pickB,
        out CompositeOperation composite, out string? error);

    public void Start()
    {
        if (!ValidateParameters(out var error))
        {
            OnFeedback?.Invoke($"{CommandName}: {error} Komut iptal.");
            OnCompleted?.Invoke();
            return;
        }
        OnFeedback?.Invoke($"{StartupPrompt()}: Birinci doğruyu seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var line = FindNearestLine(point);
        if (line == null)
        {
            OnFeedback?.Invoke($"{CommandName}: Bu noktada bir doğru (Line) bulunamadı.");
            return;
        }

        if (_firstLine == null)
        {
            _firstLine = line;
            _firstPick = point;
            OnFeedback?.Invoke($"{CommandName}: İkinci doğruyu seçin.");
            return;
        }

        if (line == _firstLine)
        {
            OnFeedback?.Invoke($"{CommandName}: Lütfen BİRİNCİDEN FARKLI bir doğru seçin.");
            return;
        }

        Apply(_firstLine, _firstPick, line, point);
    }

    private void Apply(LineEntity a, Vector3D pickA, LineEntity b, Vector3D pickB)
    {
        if (!TryBuildOperation(a, pickA, b, pickB, out var composite, out var error))
        {
            OnFeedback?.Invoke($"{CommandName}: {error} Tekrar deneyin (ESC ile iptal).");
            _firstLine = null;
            return;
        }

        TransactionManager.Submit(composite);
        OnFeedback?.Invoke(SuccessMessage());
        _firstLine = null;
        OnCompleted?.Invoke();
    }

    private LineEntity? FindNearestLine(Vector3D point)
    {
        LineEntity? target = null;
        double minDst = _hitTolerance;

        var lines = Database.GetAllEntities().OfType<LineEntity>().ToList();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            double d = lines[i].DistanceTo(point);
            if (d < minDst)
            {
                minDst = d;
                target = lines[i];
            }
        }
        return target;
    }

    public void OnPointerMoved(Vector3D point) { }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape || key == InputKey.Enter)
            Cancel();
    }

    public void Cancel()
    {
        _firstLine = null;
        OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context) { }
}
