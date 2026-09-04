using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Metraj Servisi Test Birimi (BillOfMaterialsServiceTests)
   NEDEN: Denetim raporu, ValveEntity/DamperEntity/AirTerminalEntity kütüphanelerinin
          BillOfMaterialsService.GenerateTable çıktısına HİÇ girmediğini tespit etti
          (fittings filtresi yalnızca eski 'Valve' sınıfını tanıyordu, ElbowEntity/TeeEntity
          dışındaki yeni MEP elemanları BOM tablosuna düşmüyordu). Bu testler, bu üç tipin
          artık doğru poz no ve adetle tabloya girdiğini kilitler.
*/
public class BillOfMaterialsServiceTests
{
    [Fact]
    public void GenerateTable_WithValveEntity_AddsFittingRowWithPoz()
    {
        var db = new CadDatabase();
        var valve = new ValveEntity(new Vector3D(0, 0, 0), ValveType.BallValve, 50.0)
        {
            SystemType = MechanicalSystemType.DomesticColdWater
        };
        db.AddEntity(valve);

        var bom = new BillOfMaterialsService(db);
        var table = bom.GenerateTable(new Vector3D(0, 0, 0));

        Assert.True(ContainsRowWithText(table, "BallValve"), "Tabloda ValveEntity satırı bulunamadı.");
        Assert.True(ContainsRowWithText(table, "25.410."), "ValveEntity için beklenen poz no ön eki bulunamadı.");
    }

    [Fact]
    public void GenerateTable_WithDamperEntity_AddsFittingRowWithFireRating()
    {
        var db = new CadDatabase();
        var damper = new DamperEntity(new Vector3D(0, 0, 0), DamperType.Fire, 250.0)
        {
            SystemType = MechanicalSystemType.Ventilation
        };
        db.AddEntity(damper);

        var bom = new BillOfMaterialsService(db);
        var table = bom.GenerateTable(new Vector3D(0, 0, 0));

        Assert.True(ContainsRowWithText(table, "Fire Damper"), "Tabloda DamperEntity satırı bulunamadı.");
        Assert.True(ContainsRowWithText(table, "90dk"), "Fire damper için yangın direnç süresi tabloya yansımadı.");
        Assert.True(ContainsRowWithText(table, "25.430."), "DamperEntity için beklenen poz no ön eki bulunamadı.");
    }

    [Fact]
    public void GenerateTable_WithAirTerminalEntity_AddsFittingRowWithAirFlow()
    {
        var db = new CadDatabase();
        var terminal = new AirTerminalEntity(new Vector3D(0, 0, 0), AirTerminalType.SupplyDiffuser, 250.0)
        {
            SystemType = MechanicalSystemType.Ventilation
        };
        db.AddEntity(terminal);

        var bom = new BillOfMaterialsService(db);
        var table = bom.GenerateTable(new Vector3D(0, 0, 0));

        Assert.True(ContainsRowWithText(table, "SupplyDiffuser"), "Tabloda AirTerminalEntity satırı bulunamadı.");
        Assert.True(ContainsRowWithText(table, "250"), "AirTerminalEntity için hava debisi tabloya yansımadı.");
        Assert.True(ContainsRowWithText(table, "25.440."), "AirTerminalEntity için beklenen poz no ön eki bulunamadı.");
    }

    [Fact]
    public void GenerateTable_WithMultipleDampersOfSameType_GroupsIntoSingleRowWithCorrectCount()
    {
        var db = new CadDatabase();
        db.AddEntity(new DamperEntity(new Vector3D(0, 0, 0), DamperType.Volume, 250.0));
        db.AddEntity(new DamperEntity(new Vector3D(500, 0, 0), DamperType.Volume, 250.0));
        db.AddEntity(new DamperEntity(new Vector3D(1000, 0, 0), DamperType.Volume, 250.0));

        var bom = new BillOfMaterialsService(db);
        var table = bom.GenerateTable(new Vector3D(0, 0, 0));

        Assert.True(ContainsRowWithText(table, "3 Ad."), "3 adet aynı tip Volume damper tek satırda gruplanmadı.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static bool ContainsRowWithText(Afney.Cad.Domain.Entities.Basic.TableEntity table, string needle)
    {
        for (int r = 0; r < table.Rows; r++)
        {
            for (int c = 0; c < table.Columns; c++)
            {
                if (table.GetCell(r, c).Contains(needle))
                    return true;
            }
        }
        return false;
    }
}
