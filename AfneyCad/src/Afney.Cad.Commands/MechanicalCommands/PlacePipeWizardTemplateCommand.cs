using System;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   NE: Boru Sihirbazı Şablonu Yerleştirme Komutu (PlacePipeWizardTemplateCommand)
   NEDEN — GERÇEK HATA (Session #75 iş akışı denetiminde bulundu): `PipeWizardDialog.Place_Click`
          şablonu HER ZAMAN sabit dünya orijinine (`Vector3D(0,0,0)`) yerleştiriyordu — kullanıcının
          tıklayarak bir nokta seçmesinin hiçbir yolu yoktu, her yerleştirmeden sonra elle taşımak
          gerekiyordu. Ayrıca `_database.AddEntity` doğrudan çağrılıyordu — TransactionManager'dan
          hiç geçmiyordu (Ctrl+Z bu işlemi geri alamıyordu, Session #75 mimari denetiminde 13 komut
          için düzeltilen aynı hata sınıfı, bu diyalog o taramada yakalanmamıştı).
   NASIL: Diyalog artık sadece şablon/sistem tipini seçtirip kapanıyor; gerçek yerleştirme, tıpkı
          diğer "PlaceXCommand" ailesi gibi, kullanıcının viewport'ta tıkladığı noktada gerçekleşiyor
          ve tek bir CompositeOperation olarak Submit ediliyor.
*/
public class PlacePipeWizardTemplateCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly PipeWizardService.TemplateType _templateType;
    private readonly MechanicalSystemType _systemType;

    public string CommandName => "PIPEWIZARD";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public PlacePipeWizardTemplateCommand(
        CadDatabase database, TransactionManager transactionManager,
        PipeWizardService.TemplateType templateType, MechanicalSystemType systemType)
    {
        _database = database;
        _transactionManager = transactionManager;
        _templateType = templateType;
        _systemType = systemType;
    }

    public void Start()
    {
        OnFeedback?.Invoke("BORU SİHİRBAZI: Şablonun yerleştirileceği noktayı tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var wizard = new PipeWizardService(_database);
        var riser = point + new Vector3D(-500, 0, 0);
        var entities = wizard.GenerateFromTemplate(_templateType, point, riser, _systemType);

        if (entities.Count > 0)
        {
            var composite = new CompositeOperation("Boru Sihirbazı Şablonu");
            foreach (var ent in entities)
                composite.Add(new AddEntityOperation(_database, ent));
            _transactionManager.Submit(composite);
        }

        OnFeedback?.Invoke($"BORU SİHİRBAZI: {entities.Count} nesne yerleştirildi.");
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) Cancel();
    }

    public void Draw(IRenderContext context) { }

    public void Cancel() => OnCompleted?.Invoke();
}
