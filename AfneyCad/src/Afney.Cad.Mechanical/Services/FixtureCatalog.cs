using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Vitrifiye Kataloğu (FixtureCatalog)
   NEDEN: Sıhhi tesisat projelerinde sık kullanılan vitrifiye (uç birim) nesnelerini standart ölçüler ve bağlantı portları ile hazır şablon olarak sunmak için.
   
   MÜHENDİSLİK DETAYI:
   - TS EN ve DIN standartlarına uygun boyutlandırma.
   - Her cihaz için tipik Hidrolik Yük Birimi (FU) tanımları.
*/
public static class FixtureCatalog
{
    public static class FixtureTypes
    {
        public const string Washbasin = "Washbasin";
        public const string WC_Reservoir = "WC_Reservoir";
        public const string Shower = "Shower";
        public const string KitchenSink = "KitchenSink";
        public const string WashingMachine = "WashingMachine";
    }

    public static SanitaryFixtureEntity Create(string type, Vector3D position)
    {
        return type switch
        {
            FixtureTypes.Washbasin => CreateWashbasin(position),
            FixtureTypes.WC_Reservoir => CreateWC(position),
            FixtureTypes.Shower => CreateShower(position),
            FixtureTypes.KitchenSink => CreateKitchenSink(position),
            FixtureTypes.WashingMachine => CreateWashingMachine(position),
            _ => new SanitaryFixtureEntity(position, "Generic", 1.0)
        };
    }

    private static SanitaryFixtureEntity CreateWashbasin(Vector3D pos)
    {
        // Yarım Ayak Lavabo (50x40)
        var f = new SanitaryFixtureEntity(pos, FixtureTypes.Washbasin, 0.5);
        f.Width = 500;
        f.Depth = 450;
        f.ColdWaterOffset = new Vector3D(80, -50, -550); // Duvardan 5cm çıkık, yerden 55cm yukarı (Z ekseni aşağı ise)
        // Koordinat sistemi Z=0 Döşeme Üstü kabul edelim.
        // O zaman batarya yüksekliği Z=1100 mm.
        // Duvardan çıkış Z=600 mm.
        return f;
    }

    private static SanitaryFixtureEntity CreateWC(Vector3D pos)
    {
        // Klozet (Rezervuarlı)
        var f = new SanitaryFixtureEntity(pos, FixtureTypes.WC_Reservoir, 1.0);
        f.Width = 400;
        f.Depth = 650;
        return f;
    }

    private static SanitaryFixtureEntity CreateShower(Vector3D pos)
    {
        // Duş Teknesi (90x90)
        var f = new SanitaryFixtureEntity(pos, FixtureTypes.Shower, 0.8);
        f.Width = 900;
        f.Depth = 900;
        return f;
    }
    
    private static SanitaryFixtureEntity CreateKitchenSink(Vector3D pos)
    {
        // Mutfak Eviyesi (Tek Gözlü)
        var f = new SanitaryFixtureEntity(pos, FixtureTypes.KitchenSink, 0.8);
        f.Width = 500;
        f.Depth = 500;
        return f;
    }
    
    private static SanitaryFixtureEntity CreateWashingMachine(Vector3D pos)
    {
        // Çamaşır Makinesi
        var f = new SanitaryFixtureEntity(pos, FixtureTypes.WashingMachine, 1.0);
        f.Width = 600;
        f.Depth = 600;
        return f;
    }
}
