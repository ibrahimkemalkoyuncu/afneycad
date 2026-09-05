using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;
using System;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Metraj Tablosu Ekleme Komutu (InsertBOMTableCommand)
    NEDEN: FINE SANI standardında, proje metrajını çizimin içine profesyonel bir tablo olarak gömmek için.
*/
public class InsertBOMTableCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly BillOfMaterialsService _bomService;
    private TableEntity? _ghostTable;

    public string CommandName => "INSERT_BOM_TABLE";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public InsertBOMTableCommand(CadDatabase database, TransactionManager transactionManager)
    {
        _database = database;
        _transactionManager = transactionManager;
        _bomService = new BillOfMaterialsService(database);
    }

    public void Start()
    {
        OnFeedback?.Invoke("TABLO YERLEŞİMİ: Tablonun sol üst köşesini belirlemek için tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var table = _bomService.GenerateTable(point);
        _transactionManager.Submit(new AddEntityOperation(_database, table));

        OnFeedback?.Invoke("Metraj tablosu başarıyla eklendi.");
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point)
    {
        // Hayalet tablo gösterimi (Ghost rendering)
        _ghostTable = _bomService.GenerateTable(point);
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) Cancel();
    }

    public void Draw(Domain.Abstractions.IRenderContext context)
    {
        if (_ghostTable != null)
        {
            _ghostTable.Color = 0x88FFFFFF; // Yarı saydam
            _ghostTable.Draw(context);
        }
    }

    public void Cancel()
    {
        OnFeedback?.Invoke("İşlem iptal edildi.");
        OnCompleted?.Invoke();
    }
}
