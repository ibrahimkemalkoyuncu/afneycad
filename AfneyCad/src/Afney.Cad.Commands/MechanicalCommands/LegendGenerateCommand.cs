using System;
using System.Collections.Generic;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.MechanicalCommands;

public class LegendGenerateCommand : ICadCommand
{
    private readonly CadDatabase _database;

    public string CommandName => "LEJANT";
    public Vector3D? ActivePoint => null;
    public List<CadEntity> SelectedEntities => new();

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public LegendGenerateCommand(CadDatabase database)
    {
        _database = database;
    }

    public void Start()
    {
        OnFeedback?.Invoke("LEJANT: Tablonun yerleştirileceği noktayı seçin...");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var service = new LegendService(_database.GetAllEntities());
        var entities = service.GenerateLegend(point);
        
        foreach (var ent in entities)
            _database.AddEntity(ent);

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
