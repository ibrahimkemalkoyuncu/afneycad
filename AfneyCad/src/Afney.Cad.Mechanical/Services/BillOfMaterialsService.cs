using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Metraj ve Malzeme Listesi Servisi (BillOfMaterialsService)
    NEDEN: Projedeki toplam boru miktarını ve cihaz adetlerini bir liste (Keşif) olarak sunmak için.
    
    ÇIKTI:
    - Borular: Çap bazlı toplam uzunluk (m)
    - Cihazlar: Tip bazlı adet
*/
public class BillOfMaterialsService
{
    private readonly CadDatabase _database;
    private readonly PozKatalogService _katalog = new();

    public BillOfMaterialsService(CadDatabase database)
    {
        _database = database;
    }

    /*
    METOD ADI: GenerateTable
    AMACI: Projedeki tüm mekanik bileşenleri sayarak resmi hakediş ve keşif özetine uygun metraj tablosu üretmek.
    NASIL: 
    - Veritabanındaki tüm Pipe (Boru), Fixture (Vitrifiye) ve Fitting (Ek Parça) nesnelerini gruplar.
    - Çevre, Şehircilik ve İklim Değişikliği Bakanlığı (eski Bayındırlık) poz numaralarını otomatik eşleştirir.
    - Metraj cetveli formatında bir TableEntity (CAD Tablosu) döndürür.
    */
    public TableEntity GenerateTable(Vector3D position)
    {
        var pipes = _database.GetAllEntities().OfType<PipeEntity>()
            .GroupBy(p => new { p.SystemType, p.InnerDiameter })
            .OrderBy(g => g.Key.SystemType).ThenBy(g => g.Key.InnerDiameter)
            .ToList();

        var fixtures = _database.GetAllEntities().OfType<SanitaryFixtureEntity>()
            .GroupBy(f => f.FixtureType)
            .OrderBy(g => g.Key)
            .ToList();

        var fittings = _database.GetAllEntities().OfType<MechanicalEntity>()
            .Where(e => e is ElbowEntity || e is TeeEntity || e is Valve)
            .GroupBy(e => new { Type = e.GetType().Name, e.InnerDiameter })
            .ToList();

        // Satır Sayısı Hesabı: Başlıklar + Borular + Vitrifiyeler + Fittings
        int rowCount = 2 + pipes.Count + fixtures.Count + fittings.Count + 4;
        var table = new TableEntity(position, rowCount, 3); // 3 Kolon: Poz No, Açıklama, Miktar

        int currentRow = 0;
        
        // BAŞLIK
        table.SetCell(currentRow, 0, "POZ NO");
        table.SetCell(currentRow, 1, "MALZEME VE İŞÇİLİK CİNSİ");
        table.SetCell(currentRow, 2, "MİKTAR / BİRİM");
        currentRow++;

        // 1. BORULAR
        table.SetCell(currentRow, 1, "--- BORU TESİSATI ---");
        currentRow++;
        foreach (var group in pipes)
        {
            double totalMeters = group.Sum(p => p.GetLength()) / 1000.0;
            var kalem = _katalog.FindForPipe(group.Key.SystemType, group.Key.InnerDiameter);
            string poz  = kalem?.PozNo ?? GetPipePoz(group.Key.SystemType, (int)group.Key.InnerDiameter);
            string desc = kalem?.Tanim ?? GetPipeDescription(group.Key.SystemType, (int)group.Key.InnerDiameter);

            table.SetCell(currentRow, 0, poz);
            table.SetCell(currentRow, 1, desc);
            table.SetCell(currentRow, 2, $"{totalMeters:F2} m");
            currentRow++;
        }

        // 2. VİTRİFİYELER
        table.SetCell(currentRow, 1, "--- VİTRİFİYE VE ARMATÜRLER ---");
        currentRow++;
        foreach (var group in fixtures)
        {
            var kalem = _katalog.FindForFixture(group.Key);
            string poz = kalem?.PozNo ?? GetFixturePoz(group.Key);
            table.SetCell(currentRow, 0, poz);
            table.SetCell(currentRow, 1, kalem?.Tanim ?? group.Key);
            table.SetCell(currentRow, 2, $"{group.Count()} Ad.");
            currentRow++;
        }

        // 3. FITTINGS VE EKİPMAN
        table.SetCell(currentRow, 1, "--- EK PARÇALAR VE VANALAR ---");
        currentRow++;
        foreach (var group in fittings)
        {
            table.SetCell(currentRow, 0, "Set-Poz"); // Dinamik fitting pozu eklenebilir
            table.SetCell(currentRow, 1, $"{group.Key.InnerDiameter}mm {group.Key.Type}");
            table.SetCell(currentRow, 2, $"{group.Count()} Ad.");
            currentRow++;
        }

        return table;
    }

    /*
    NE: Resmi Poz No Sorgulama
    NEDEN: Türkiye'deki kamu ve özel projelerde kullanılan standart poz kodlarına uygunluk için.
    */
    private string GetPipePoz(Afney.Cad.Mechanical.Enums.MechanicalSystemType type, int diameter)
    {
        return type switch
        {
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.WasteWater        => $"25.310.{1000 + diameter}",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.RainWater         => $"25.315.{1000 + diameter}",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.DomesticColdWater => $"25.305.{2000 + diameter}",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.DomesticHotWater  => $"25.305.{3000 + diameter}",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.FireProtection    => $"25.320.{4000 + diameter}",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.Gas               => $"25.325.{5000 + diameter}",
            _                                                                  => "25.xxx.xxxx"
        };
    }

    private string GetPipeDescription(Afney.Cad.Mechanical.Enums.MechanicalSystemType type, int diameter)
    {
        string mat = type switch
        {
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.WasteWater        => "PVC-U",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.RainWater         => "PVC-U",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.FireProtection    => "Galv. Çelik",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.Gas               => "Galv. Çelik",
            _                                                                  => "PP-R"
        };
        string sys = type switch
        {
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.WasteWater        => "Pis Su",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.RainWater         => "Yağmur Suyu",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.FireProtection    => "Yangın",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.Gas               => "Gaz",
            Afney.Cad.Mechanical.Enums.MechanicalSystemType.DomesticHotWater  => "Sıcak Su",
            _                                                                  => "Soğuk Su"
        };
        return $"{mat} {sys} Borusu (DN {diameter})";
    }

    private string GetFixturePoz(string name)
    {
        if (name.Contains("WC")) return "25.370.1101";
        if (name.Contains("Lavabo")) return "25.385.1101";
        if (name.Contains("Eviye")) return "25.390.1101";
        if (name.Contains("Duş")) return "25.405.1101";
        return "25.400.xxxx";
    }
}
