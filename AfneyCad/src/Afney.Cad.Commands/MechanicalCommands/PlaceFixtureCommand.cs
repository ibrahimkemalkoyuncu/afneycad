using System;
using System.Collections.Generic;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   NE: Vitrifiye Yerleştirme Komutu (PlaceFixtureCommand)
   NEDEN: Lavabo, Klozet gibi uç birimleri harita üzerinde istenen koordinata yerleştirmek için.

   NASIL (Mühendislik Detayı):
   - Kullanıcıdan bir yerleşim noktası (Point) bekler.
   - Yerleştirilen nesneyi 'SanitaryFixtureEntity' olarak modellere ekler.
   - Nesnenin tipine göre (WC vs Lavabo) varsayılan FU ve çap bilgisini atar.
   - İşlemi geri alınabilir (Undoable) bir veritabanı operasyonu olarak kaydeder.
*/
public class PlaceFixtureCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private string _fixtureType = "Washbasin";
    private double _fu = 0.5;

    public string CommandName => "PLACEFIXTURE";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;
    public event Action<CadEntity>? OnEntityPlaced;

    public PlaceFixtureCommand(CadDatabase database)
    {
        _database = database;
    }

    public void SetFixtureType(string type, double fu)
    {
        _fixtureType = type;
        _fu = fu;
    }

    public void Start()
    {
        OnFeedback?.Invoke($"PLACE FIXTURE: {_fixtureType} yerleşimi için nokta seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        var fixture = new SanitaryFixtureEntity(point, _fixtureType, _fu)
        {
            Color = 0xFF0000FF, // Mavi (Temsili)
            SystemType = MechanicalSystemType.WasteWater
        };

        // Veritabanına nesneyi ekle
        _database.AddEntity(fixture);
        
        // Geri alma listesi (History) için üst katmana bildir
        OnEntityPlaced?.Invoke(fixture);

        OnFeedback?.Invoke($"{_fixtureType} yerleştirildi.");
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape)
        {
            OnCompleted?.Invoke();
        }
    }

    public void Draw(IRenderContext context) { }

    public void Cancel() { }
}
