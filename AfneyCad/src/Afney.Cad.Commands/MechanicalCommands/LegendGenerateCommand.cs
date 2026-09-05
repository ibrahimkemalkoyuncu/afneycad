using System;
using System.Collections.Generic;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.MechanicalCommands;

public class LegendGenerateCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;

    public string CommandName => "LEJANT";
    public Vector3D? ActivePoint => null;
    public List<CadEntity> SelectedEntities => new();

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public LegendGenerateCommand(CadDatabase database, TransactionManager transactionManager)
    {
        _database = database;
        _transactionManager = transactionManager;
    }

    public void Start()
    {
        OnFeedback?.Invoke("LEJANT: Tablonun yerleştirileceği noktayı seçin...");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var service = new LegendService(_database.GetAllEntities());
        var entities = service.GenerateLegend(point);

        if (entities.Count > 0)
        {
            var composite = new CompositeOperation("Lejant Tablosu Ekle");
            foreach (var ent in entities)
                composite.Add(new AddEntityOperation(_database, ent));
            _transactionManager.Submit(composite);
        }

        OnFeedback?.Invoke("Lejant tablosu oluşturuldu.");
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
