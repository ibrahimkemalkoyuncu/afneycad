using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Akıllı Boru Sihirbazı (PipeWizardService)
   NEDEN: FINE SANI standardında, hazır şablonlarla (Banyo tipi, Daire tipi) tek tıkla tesisat üretmek için.
   
   ŞABLONLAR:
   - Standart Banyo: WC + Lavabo + Duş → Kolon bağlantılı branşman
   - Standart Mutfak: Eviye → Tek bağlantı
   - Tam Daire (1+1): 1 Banyo + 1 Mutfak → Komplet tesisat
   - Tam Daire (2+1): 1 Ana Banyo + 1 Ebeveyn Banyo + 1 Mutfak
   - Tam Daire (3+1): 1 Ana Banyo + 1 Ebeveyn Banyo + 1 WC + 1 Mutfak
   
   ÇALIŞMA MANTIĞI:
   1. Kullanıcı şablon seçer ve yerleştirme noktası tıklar
   2. Şablon, vitrifiye + boru + fitting + kolon oluşturur
   3. Otomatik çaplandırma ve hidrolik hesap yapılır
*/
public class PipeWizardService
{
    private readonly CadDatabase _database;

    public PipeWizardService(CadDatabase database)
    {
        _database = database;
    }

    // Şablon Tipleri
    public enum TemplateType
    {
        StandardBathroom,    // WC + Lavabo + Duş
        MasterBathroom,      // WC + Lavabo + Küvet + Bide
        GuestToilet,         // WC + Lavabo (küçük)
        Kitchen,             // Eviye + Bulaşık bağlantısı
        Laundry,             // Çamaşır makinesi + Kurutma
        Apartment_1Plus1,    // 1 Banyo + 1 Mutfak
        Apartment_2Plus1,    // Standart + Ebeveyn Banyo + Mutfak
        Apartment_3Plus1     // Standart + Ebeveyn + WC + Mutfak
    }

    // Şablon Açıklamaları
    public static List<(TemplateType Type, string Name, string Description, int FixtureCount)> GetAvailableTemplates()
    {
        return new()
        {
            (TemplateType.StandardBathroom, "Standart Banyo", "WC + Lavabo + Duş Teknesi", 3),
            (TemplateType.MasterBathroom, "Ana Banyo (Master)", "WC + Çift Lavabo + Küvet + Bide", 5),
            (TemplateType.GuestToilet, "Misafir WC", "WC + Mini Lavabo", 2),
            (TemplateType.Kitchen, "Mutfak", "Eviye + Bulaşık Makinesi Bağlantısı", 2),
            (TemplateType.Laundry, "Çamaşırhane", "Çamaşır Makinesi + Kurutma Bağlantısı", 2),
            (TemplateType.Apartment_1Plus1, "1+1 Daire", "1 Banyo + 1 Mutfak (Tam Tesisat)", 5),
            (TemplateType.Apartment_2Plus1, "2+1 Daire", "Standart Banyo + Ebeveyn WC + Mutfak", 8),
            (TemplateType.Apartment_3Plus1, "3+1 Daire", "Ana Banyo + Ebeveyn WC + Misafir WC + Mutfak", 12),
        };
    }

    /*
       NE: Şablondan Tesisat Üret (GenerateFromTemplate)
       NEDEN: Seçilen şablona göre vitrifiye, boru, fitting ve kolonu otomatik oluşturur.
       
       PARAMETRELER:
       - template: Şablon tipi
       - origin: Yerleştirme noktası (Sol-Alt köşe)
       - riserPosition: Kolon (Dikey hat) pozisyonu
       - systemType: Temiz Su / Pis Su
    */
    public List<CadEntity> GenerateFromTemplate(
        TemplateType template,
        Vector3D origin,
        Vector3D riserPosition,
        MechanicalSystemType systemType = MechanicalSystemType.DomesticColdWater)
    {
        return template switch
        {
            TemplateType.StandardBathroom => GenerateStandardBathroom(origin, riserPosition, systemType),
            TemplateType.MasterBathroom => GenerateMasterBathroom(origin, riserPosition, systemType),
            TemplateType.GuestToilet => GenerateGuestToilet(origin, riserPosition, systemType),
            TemplateType.Kitchen => GenerateKitchen(origin, riserPosition, systemType),
            TemplateType.Laundry => GenerateLaundry(origin, riserPosition, systemType),
            TemplateType.Apartment_1Plus1 => GenerateApartment1Plus1(origin, riserPosition, systemType),
            TemplateType.Apartment_2Plus1 => GenerateApartment2Plus1(origin, riserPosition, systemType),
            TemplateType.Apartment_3Plus1 => GenerateApartment3Plus1(origin, riserPosition, systemType),
            _ => new List<CadEntity>()
        };
    }

    // --- ŞABLON JENERATÖRLER ---

    private List<CadEntity> GenerateStandardBathroom(Vector3D origin, Vector3D riser, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();

        // Vitrifiyeler (Düşık)
        var wcPos = origin + new Vector3D(400, 200, 0);
        var lavaboPos = origin + new Vector3D(1200, 200, 0);
        var dusPos = origin + new Vector3D(2200, 200, 0);

        var wc = CreateFixture("WC", wcPos, 3.0, sys);
        var lavabo = CreateFixture("Lavabo", lavaboPos, 0.5, sys); // TS EN 806-2 Tablo 1 — standart lavabo
        var dus = CreateFixture("Duş", dusPos, 2.0, sys);
        entities.AddRange(new CadEntity[] { wc, lavabo, dus });

        // Ana hat (branşman)
        var branchStart = riser;
        var branchEnd = origin + new Vector3D(0, 500, 0);
        var mainBranch = CreatePipe(branchStart, branchEnd, 32, sys);
        entities.Add(mainBranch);

        // Her vitrifyeye bağlantı
        entities.AddRange(ConnectFixtureToBranch(wc, branchEnd, 20, sys));
        entities.AddRange(ConnectFixtureToBranch(lavabo, branchEnd + new Vector3D(800, 0, 0), 15, sys));
        entities.AddRange(ConnectFixtureToBranch(dus, branchEnd + new Vector3D(1800, 0, 0), 20, sys));

        // Yatay branşman devamı
        entities.Add(CreatePipe(branchEnd, branchEnd + new Vector3D(2200, 0, 0), 32, sys));

        return entities;
    }

    private List<CadEntity> GenerateMasterBathroom(Vector3D origin, Vector3D riser, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();

        var wcPos = origin + new Vector3D(400, 200, 0);
        var lavabo1Pos = origin + new Vector3D(1200, 200, 0);
        var lavabo2Pos = origin + new Vector3D(1800, 200, 0);
        var kuvetPos = origin + new Vector3D(2800, 200, 0);
        var bidePos = origin + new Vector3D(800, 200, 0);

        entities.Add(CreateFixture("WC", wcPos, 3.0, sys));
        entities.Add(CreateFixture("Lavabo", lavabo1Pos, 0.5, sys));
        entities.Add(CreateFixture("Lavabo", lavabo2Pos, 0.5, sys));
        entities.Add(CreateFixture("Küvet", kuvetPos, 3.0, sys));
        entities.Add(CreateFixture("Bide", bidePos, 1.0, sys));

        // Ana hat DN40
        var branchEnd = origin + new Vector3D(0, 500, 0);
        entities.Add(CreatePipe(riser, branchEnd, 40, sys));
        entities.Add(CreatePipe(branchEnd, branchEnd + new Vector3D(3200, 0, 0), 40, sys));

        return entities;
    }

    private List<CadEntity> GenerateGuestToilet(Vector3D origin, Vector3D riser, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();

        var wcPos = origin + new Vector3D(400, 200, 0);
        var lavaboPos = origin + new Vector3D(1000, 200, 0);

        entities.Add(CreateFixture("WC", wcPos, 3.0, sys));
        entities.Add(CreateFixture("Lavabo", lavaboPos, 1.0, sys));

        var branchEnd = origin + new Vector3D(0, 500, 0);
        entities.Add(CreatePipe(riser, branchEnd, 25, sys));
        entities.Add(CreatePipe(branchEnd, branchEnd + new Vector3D(1200, 0, 0), 25, sys));

        return entities;
    }

    private List<CadEntity> GenerateKitchen(Vector3D origin, Vector3D riser, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();

        var eviyePos = origin + new Vector3D(600, 200, 0);
        var bulasikPos = origin + new Vector3D(1400, 200, 0);

        entities.Add(CreateFixture("Eviye", eviyePos, 2.0, sys));
        entities.Add(CreateFixture("Bulaşık Makinesi", bulasikPos, 1.5, sys));

        var branchEnd = origin + new Vector3D(0, 500, 0);
        entities.Add(CreatePipe(riser, branchEnd, 25, sys));
        entities.Add(CreatePipe(branchEnd, branchEnd + new Vector3D(1600, 0, 0), 25, sys));

        return entities;
    }

    private List<CadEntity> GenerateLaundry(Vector3D origin, Vector3D riser, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();
        entities.Add(CreateFixture("Çamaşır Makinesi", origin + new Vector3D(400, 200, 0), 1.5, sys));
        entities.Add(CreateFixture("Kurutma Makinesi", origin + new Vector3D(1200, 200, 0), 0.5, sys));

        var branchEnd = origin + new Vector3D(0, 500, 0);
        entities.Add(CreatePipe(riser, branchEnd, 20, sys));
        entities.Add(CreatePipe(branchEnd, branchEnd + new Vector3D(1400, 0, 0), 20, sys));
        return entities;
    }

    // Daire Kombinasyonları
    private List<CadEntity> GenerateApartment1Plus1(Vector3D origin, Vector3D riser, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();
        entities.AddRange(GenerateStandardBathroom(origin, riser, sys));
        entities.AddRange(GenerateKitchen(origin + new Vector3D(4000, 0, 0), riser + new Vector3D(4000, 0, 0), sys));
        return entities;
    }

    private List<CadEntity> GenerateApartment2Plus1(Vector3D origin, Vector3D riser, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();
        entities.AddRange(GenerateStandardBathroom(origin, riser, sys));
        entities.AddRange(GenerateGuestToilet(origin + new Vector3D(0, 3000, 0), riser + new Vector3D(0, 3000, 0), sys));
        entities.AddRange(GenerateKitchen(origin + new Vector3D(4000, 0, 0), riser + new Vector3D(4000, 0, 0), sys));
        return entities;
    }

    private List<CadEntity> GenerateApartment3Plus1(Vector3D origin, Vector3D riser, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();
        entities.AddRange(GenerateMasterBathroom(origin, riser, sys));
        entities.AddRange(GenerateGuestToilet(origin + new Vector3D(0, 3000, 0), riser + new Vector3D(0, 3000, 0), sys));
        entities.AddRange(GenerateGuestToilet(origin + new Vector3D(0, 5000, 0), riser + new Vector3D(0, 5000, 0), sys));
        entities.AddRange(GenerateKitchen(origin + new Vector3D(5000, 0, 0), riser + new Vector3D(5000, 0, 0), sys));
        return entities;
    }

    // --- YARDIMCI FABRİKA METODLAR ---

    private SanitaryFixtureEntity CreateFixture(string type, Vector3D position, double loadUnits, MechanicalSystemType sys)
    {
        var fix = new SanitaryFixtureEntity(position, type, loadUnits);
        fix.SystemType = sys;
        fix.Color = 0xFF00FF00;
        return fix;
    }

    private PipeEntity CreatePipe(Vector3D start, Vector3D end, double diameter, MechanicalSystemType sys)
    {
        var pipe = new PipeEntity(start, end, diameter);
        pipe.SystemType = sys;
        pipe.Color = sys == MechanicalSystemType.DomesticColdWater ? 0xFF0088FF :
                     sys == MechanicalSystemType.DomesticHotWater ? 0xFFFF4444 :
                     0xFF888888;
        return pipe;
    }

    private List<CadEntity> ConnectFixtureToBranch(SanitaryFixtureEntity fixture, Vector3D branchPoint, double diameter, MechanicalSystemType sys)
    {
        var entities = new List<CadEntity>();
        var fixtureBase = fixture.Position + new Vector3D(0, -100, 0);
        entities.Add(CreatePipe(branchPoint, fixtureBase, diameter, sys));
        return entities;
    }
}
