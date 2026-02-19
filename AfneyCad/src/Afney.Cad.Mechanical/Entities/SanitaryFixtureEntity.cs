using System;
using System.Collections.Generic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Sıhhi Tesisat Uç Birimi (SanitaryFixtureEntity)
   NEDEN: Lavabo, Klozet, Duş gibi su tüketen veya tahliye eden birimleri MEP grafında temsil etmek için.

   NASIL (Mühendislik Detayı):
   - Her uç birimin bir 'Yükleme Birimi' (FU - Fixture Unit) vardır; bu değer boru çapı hesabını (TS 1258 / DIN 1988) tetikler.
   - Topoloji grafında (MEP Graph) 'Düğüm' (Node) olarak görev yapar.
   - Kolon şeması üretilirken yatay branşmanların sonlandığı 'Terminal' noktalarıdır.
   - Mimari engellerle (Duvarlar) ilişkilendirilerek akıllı yerleşim özelliklerini destekler.
*/
public class SanitaryFixtureEntity : MechanicalEntity
{
    // NE: Vitrifiye Tipi (WC, Lavabo, Bathtub vs.)
    public string FixtureType { get; set; } = "Washbasin";

    // NE: Yerleşim Noktası (Genelde arka duvarın ortası veya cihazın merkezi)
    public Vector3D Position { get; set; }
    
    // NE: Yönelim Açısı (Radyan)
    public double Rotation { get; set; } = 0.0;

    // NE: Fiziksel Boyutlar (mm)
    public double Width { get; set; } = 500.0;
    public double Depth { get; set; } = 450.0;

    // NE: Yükleme Birimi (Fixture Unit / Load Unit - TS 1258)
    public double LoadUnits { get; set; } = 1.0; 
    // Alias for compatibility
    public double FixtureUnit { get => LoadUnits; set => LoadUnits = value; } 

    // NE: Mimari Bağlantı (Associative Geometry) - Phase 3
    // NEDEN: Duvar kaydırıldığında cihazın da beraber kayması için.
    public Guid? AttachedObstacleId { get; set; }
    public double WallOffset { get; set; } // Duvarın başlangıcından olan mesafe
    public double WallDistance { get; set; } // Duvardan olan dik mesafe

    // BAĞLANTI OFSETLERİ (Merkeze göre local koordinatlar)
    // Örn: Lavabo için sıcak su solda, soğuk su sağda, pis su ortada.
    public Vector3D ColdWaterOffset { get; set; } = new Vector3D(100, 0, 0); // Sağ
    public Vector3D HotWaterOffset { get; set; } = new Vector3D(-100, 0, 0); // Sol
    public Vector3D DrainOffset { get; set; } = new Vector3D(0, 50, -500); // Alt

    /*
       NE: SanitaryFixtureEntity Yapıcı Metodu
       NEDEN: Vitrifiye tipine (Washbasin, WC vb.) göre varsayılan boyut ve port ofsetlerini yükleyerek yeni bir uç birim oluşturmak için.
    */
    public SanitaryFixtureEntity(Vector3D position, string fixtureType, double fu)
    {
        Position = position;
        FixtureType = fixtureType;
        FixtureUnit = fu;
        InnerDiameter = 15.0; // Varsayılan
        EntityType = MechanicalEntityType.SanitaryFixture;
        InitializeDefaults(fixtureType);
    }
    
    private void InitializeDefaults(string type)
    {
        // Varsayılan boyutlar (TS Standartlarına Yakın)
        if (type.Contains("Lavabo") || type.Contains("Washbasin"))
        {
            Width = 550; Depth = 450; FixtureUnit = 0.5;
            ColdWaterOffset = new Vector3D(80, -50, -500); // Duvardan 5cm çıkık
            HotWaterOffset = new Vector3D(-80, -50, -500);
            DrainOffset = new Vector3D(0, 0, -550); // Yerden veya duvardan
        }
        else if (type.Contains("WC") || type.Contains("Toilet"))
        {
            Width = 400; Depth = 600; FixtureUnit = 1.0; // Tank tipi
            ColdWaterOffset = new Vector3D(-150, -550, 200); // Taharet musluğu
            DrainOffset = new Vector3D(0, -250, -100); // Alttan çıkış (S)
        }
        else if (type.Contains("Duş") || type.Contains("Shower"))
        {
            Width = 900; Depth = 900; FixtureUnit = 0.8;
            ColdWaterOffset = new Vector3D(80, 0, 1000); // Batarya Yüksekliği
            HotWaterOffset = new Vector3D(-80, 0, 1000);
            DrainOffset = new Vector3D(0, 450, 0); // Yer süzgeci
        }
    }

    /*
       NE: Bağlantı Portlarını Getir (GetPorts)
       NEDEN: Cihazın tipine göre (Soğuk su girişi, Pis su çıkışı vb.) tanımlanmış bağlantı noktalarını, cihazın mevcut konumu ve rotasyonuna göre hesaplayıp topoloji grafına sunmak için.
    */
    public override List<MechanicalPort> GetPorts()
    {
        var ports = new List<MechanicalPort>();
        
        // Rotasyon Matrisi
        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);

        Vector3D TransformOffset(Vector3D offset)
        {
             // 2D Rotasyon (Z ekseni etrafında) + Translate
             double rx = offset.X * cos - offset.Y * sin;
             double ry = offset.X * sin + offset.Y * cos;
             return new Vector3D(Position.X + rx, Position.Y + ry, Position.Z + offset.Z);
        }

        // 1. Soğuk Su Portu (Mavi)
        if (ColdWaterOffset.X != 0 || ColdWaterOffset.Y != 0) // Eğer tanımlıysa
            ports.Add(new MechanicalPort(Id, "ColdWater", TransformOffset(ColdWaterOffset), Vector3D.ZAxis) { FlowType = FlowDirection.In });
            
        // 2. Sıcak Su Portu (Kırmızı)
        if (HotWaterOffset.X != 0 || HotWaterOffset.Y != 0)
            ports.Add(new MechanicalPort(Id, "HotWater", TransformOffset(HotWaterOffset), Vector3D.ZAxis) { FlowType = FlowDirection.In });

        // 3. Pis Su Portu (Kahverengi/Siyah)
        if (DrainOffset.X != 0 || DrainOffset.Y != 0)
            ports.Add(new MechanicalPort(Id, "Drainage", TransformOffset(DrainOffset), -Vector3D.ZAxis) { FlowType = FlowDirection.Out }); // Aşağı yönlü

        return ports;
    }

    /*
    METOD ADI: Draw
    AMACI: Vitrifiyeyi CAD standartlarına uygun sembolüyle ekrana çizmek.
    NASIL: 
    - Pozisyon ve rotasyon bilgisini kullanarak koordinatları transforme eder.
    - Tipine göre (Lavabo, WC vb.) özelleşmiş iç detaylar çizer.
    */
    public override void Draw(IRenderContext context)
    {
        double halfW = Width / 2.0;
        double halfD = Depth / 2.0;
        
        // 1. Dış Çerçeve (Tüm Vitrifiyeler İçin Dörtgen)
        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);

        Vector3D Trans(Vector3D p)
        {
             double rx = p.X * cos - p.Y * sin;
             double ry = p.X * sin + p.Y * cos;
             return new Vector3D(Position.X + rx, Position.Y + ry, Position.Z + p.Z);
        }

        uint color = IsSelected ? 0xFFFFFFFF : (Color != 0 ? Color : 0xFF00FFFF); 
        double thick = IsSelected ? 2.5 : 1.5;

        var p1 = Trans(new Vector3D(-halfW, -halfD, 0));
        var p2 = Trans(new Vector3D(halfW, -halfD, 0));
        var p3 = Trans(new Vector3D(halfW, halfD, 0));
        var p4 = Trans(new Vector3D(-halfW, halfD, 0));

        context.DrawLine(p1, p2, color, thick);
        context.DrawLine(p2, p3, color, thick);
        context.DrawLine(p3, p4, color, thick);
        context.DrawLine(p4, p1, color, thick);
        
        // 2. Tipe Özel Detaylar (Sembolizm)
        if (FixtureType.Contains("Lavabo") || FixtureType.Contains("Washbasin"))
        {
            // İç Hazne (Daire)
            context.DrawCircle(Position, Math.Min(Width, Depth) * 0.4, color, thick * 0.7);
            
            // Batarya sembolü (Küçük bir çizgi)
            context.DrawLine(Trans(new Vector3D(0, halfD, 0)), Trans(new Vector3D(0, halfD - 50, 0)), color, thick);
        }
        else if (FixtureType.Contains("WC") || FixtureType.Contains("Toilet"))
        {
             // Rezervuar ve Oturak detayı
             var rLineStart = Trans(new Vector3D(-halfW, -halfD + 150, 0));
             var rLineEnd = Trans(new Vector3D(halfW, -halfD + 150, 0));
             context.DrawLine(rLineStart, rLineEnd, color, thick);

             // Oval oturak (İç elips yerine basitleştirilmiş daire)
             context.DrawCircle(Trans(new Vector3D(0, 75, 0)), halfW * 0.8, color, thick * 0.5);
        }
        else if (FixtureType.Contains("Duş") || FixtureType.Contains("Shower"))
        {
            // Köşegen çizgiler (Duş teknesi sembolü)
            context.DrawLine(p1, p3, color, thick * 0.5);
            context.DrawLine(p2, p4, color, thick * 0.5);
        }

        // 3. Etiket (Sadece Seçiliyken veya Mühendislik Modunda Opsiyonel)
        if (IsSelected)
        {
            context.DrawText($"{FixtureType} (FU:{FixtureUnit})", Position + new Vector3D(0, halfD + 100, 0), 0, 10.0, 0xFFFFFFFF);
        }
    }

    /*
    METOD ADI: GetBoundingBox
    AMACI: Nesneyi içine alan en küçük dikdörtgeni hesaplamak.
    NASIL: Rotasyonlu dikdörtgenin 4 köşesini de transforme ederek min/max değerlerini bulur.
    */
    protected override CadBoundingBox CalculateBoundingBox()
    {
        double halfW = Width / 2.0;
        double halfD = Depth / 2.0;

        // 4 Köşe (Local)
        var corners = new[]
        {
            new Vector3D(-halfW, -halfD, 0),
            new Vector3D(halfW, -halfD, 0),
            new Vector3D(halfW, halfD, 0),
            new Vector3D(-halfW, halfD, 0)
        };

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        double cos = Math.Cos(Rotation);
        double sin = Math.Sin(Rotation);

        foreach (var p in corners)
        {
            double rx = p.X * cos - p.Y * sin;
            double ry = p.X * sin + p.Y * cos;
            
            double wx = Position.X + rx;
            double wy = Position.Y + ry;

            if (wx < minX) minX = wx;
            if (wy < minY) minY = wy;
            if (wx > maxX) maxX = wx;
            if (wy > maxY) maxY = wy;
        }

        return new CadBoundingBox(
            new Vector3D(minX, minY, Position.Z),
            new Vector3D(maxX, maxY, Position.Z)
        );
    }

    public override void Move(Vector3D delta) => Position += delta;

    public override void Transform(Matrix4x4 matrix) 
    {
        Position = matrix.Transform(Position);
        // Rotasyon matristen çıkarılmalı ama şimdilik sadece pozisyon.
    }

    public override CadEntity Clone()
    {
        return new SanitaryFixtureEntity(Position, FixtureType, FixtureUnit)
        {
            Id = Guid.NewGuid(),
            Width = this.Width,
            Depth = this.Depth,
            Rotation = this.Rotation,
            ColdWaterOffset = this.ColdWaterOffset,
            HotWaterOffset = this.HotWaterOffset,
            DrainOffset = this.DrainOffset,
            Color = this.Color,
            Layer = this.Layer,
            SystemType = this.SystemType
        };
    }

    public void SetPortsByRule(string ruleName) { /* To implement later */ } // kurallara göre port ayarla

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Center);
        // Port noktaları da snap olmalı
        foreach(var port in GetPorts())
             yield return new SnapPoint(port.Position, SnapPointType.Connection);
    }
}
