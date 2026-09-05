using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Commands.MechanicalCommands;

public class RiserGenerateCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly MechanicalKernel _kernel;

    public string CommandName => "KOLON_SEMA";
    public Vector3D? ActivePoint { get; private set; }

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public RiserGenerateCommand(CadDatabase database, TransactionManager transactionManager)
    {
        _database = database;
        _transactionManager = transactionManager;
        _kernel = new MechanicalKernel();
    }

    public RiserGenerateCommand(CadDatabase database, MechanicalKernel kernel, TransactionManager transactionManager)
    {
        _database = database;
        _kernel = kernel;
        _transactionManager = transactionManager;
    }

    public void Start()
    {
        OnFeedback?.Invoke("KOLON ŞEMASI: Şemanın yerleştirileceği noktayı seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        try
        {
            ActivePoint = point;

            var allEntities = _database.GetAllEntities().OfType<MechanicalEntity>();
            if (!allEntities.Any())
            {
                 OnFeedback?.Invoke("UYARI: Çizimde hiç mekanik tesisat nesnesi bulunamadı.");
                 OnCompleted?.Invoke();
                 return;
            }

            var service = new RiserDiagramService(_database, _kernel.LevelManager);
            var schemaEntities = service.GenerateRiserDiagram(point);

            if (!schemaEntities.Any())
            {
                OnFeedback?.Invoke("UYARI: Kolon (Riser) hattı tespit edilemedi. Dikey boruları kontrol edin.");
                OnCompleted?.Invoke();
                return;
            }

            var composite = new CompositeOperation("Kolon Şeması Oluştur");
            foreach (var ent in schemaEntities)
            {
                composite.Add(new AddEntityOperation(_database, ent));
            }
            _transactionManager.Submit(composite);

            OnFeedback?.Invoke($"BAŞARILI: Kolon şeması oluşturuldu ({schemaEntities.Count} nesne).");
        }
        catch (Exception ex)
        {
            OnFeedback?.Invoke($"HATA: Şema oluşturulurken bir sorun oluştu. {ex.Message}");
        }
        finally
        {
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { if (key == InputKey.Escape) Cancel(); }
    public void Draw(IRenderContext context) { }
    public void Cancel() { OnCompleted?.Invoke(); }
}
