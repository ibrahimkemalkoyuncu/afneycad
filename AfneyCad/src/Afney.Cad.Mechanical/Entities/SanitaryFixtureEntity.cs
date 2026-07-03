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

    // NE: Sadece Bağlantı Noktası Modu (OtoNET "ST Cihazları" akıllı bağlantı noktaları)
    // NEDEN: Plan yoğun olduğunda vitrifiye sembolü yerine sadece port konumunu işaretlemek için.
    public bool IsPortOnly { get; set; } = false;

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
        // Yalnızca geometri ve bağlantı ofsetleri — LU/FixtureUnit burada DEĞİŞTİRİLMEZ.
        // LU değeri constructor'dan veya FixtureLibraryService kataloğundan gelir.
        if (type.Contains("Lavabo") || type.Contains("Washbasin"))
        {
            Width = 550; Depth = 450;
            ColdWaterOffset = new Vector3D(80, -50, -500);
            HotWaterOffset  = new Vector3D(-80, -50, -500);
            DrainOffset     = new Vector3D(0, 0, -550);
        }
        else if (type.Contains("WC") || type.Contains("Toilet") || type.Contains("Klozet") || type.Contains("Alaturka"))
        {
            Width = 400; Depth = 600;
            ColdWaterOffset = new Vector3D(-150, -550, 200);
            HotWaterOffset  = Vector3D.Zero;
            DrainOffset     = new Vector3D(0, -250, -100);
        }
        else if (type.Contains("Duş") || type.Contains("Shower"))
        {
            Width = 900; Depth = 900;
            ColdWaterOffset = new Vector3D(80, 0, 1000);
            HotWaterOffset  = new Vector3D(-80, 0, 1000);
            DrainOffset     = new Vector3D(0, 450, 0);
        }
        else if (type.Contains("Küvet") || type.Contains("Bathtub"))
        {
            Width = 700; Depth = 1600;
            ColdWaterOffset = new Vector3D(80, -700, 500);
            HotWaterOffset  = new Vector3D(-80, -700, 500);
            DrainOffset     = new Vector3D(0, 730, 0);
        }
        else if (type.Contains("Eviye") || type.Contains("Sink"))
        {
            Width = 500; Depth = 400;
            ColdWaterOffset = new Vector3D(80, -150, -400);
            HotWaterOffset  = new Vector3D(-80, -150, -400);
            DrainOffset     = new Vector3D(0, 0, -450);
        }
        else if (type.Contains("Pisuvar") || type.Contains("Urinal"))
        {
            Width = 350; Depth = 300;
            ColdWaterOffset = new Vector3D(0, -100, 800);
            HotWaterOffset  = Vector3D.Zero;
            DrainOffset     = new Vector3D(0, 0, -200);
        }
        else if (type.Contains("Çamaşır") || type.Contains("Washing"))
        {
            Width = 600; Depth = 600;
            ColdWaterOffset = new Vector3D(0, -250, 700);
            HotWaterOffset  = Vector3D.Zero;
            DrainOffset     = new Vector3D(200, -250, 0);
        }
        else if (type.Contains("Bulaşık") || type.Contains("Dish"))
        {
            Width = 600; Depth = 600;
            ColdWaterOffset = new Vector3D(0, -250, 700);
            HotWaterOffset  = new Vector3D(-100, -250, 700);
            DrainOffset     = new Vector3D(200, -250, 0);
        }
        else if (type.Contains("Süzgeç") || type.Contains("Drain") || type.Contains("Gider"))
        {
            Width = 200; Depth = 200;
            ColdWaterOffset = Vector3D.Zero;
            HotWaterOffset  = Vector3D.Zero;
            DrainOffset     = new Vector3D(0, 0, -300);
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
             double rx = offset.X * cos - offset.Y * sin;
             double ry = offset.X * sin + offset.Y * cos;
             return new Vector3D(Position.X + rx, Position.Y + ry, Position.Z + offset.Z);
        }

        // Çap kararları: TS 1258 minimum boru bağlantı çapları
        bool isWC     = FixtureType.Contains("WC") || FixtureType.Contains("Toilet")
                     || FixtureType.Contains("Klozet") || FixtureType.Contains("Alaturka")
                     || FixtureType.Contains("Pisuvar") || FixtureType.Contains("Urinal");
        bool isLavabo = FixtureType.Contains("Lavabo") || FixtureType.Contains("Washbasin");
        bool isDush   = FixtureType.Contains("Duş") || FixtureType.Contains("Shower");
        bool isKuvet  = FixtureType.Contains("Küvet") || FixtureType.Contains("Bathtub");
        bool isEviye  = FixtureType.Contains("Eviye") || FixtureType.Contains("Sink");

        double cwDN   = 15.0; // Soğuk su: DN15 (min TS 1258)
        double hwDN   = 15.0; // Sıcak su: DN15
        double drDN   = isWC ? 100.0 :           // WC: DN100
                        (isDush || isKuvet || isEviye) ? 50.0 : // Duş/Küvet/Eviye: DN50
                        40.0;                    // Lavabo ve diğer: DN40

        // Temiz su malzemesi: Genellikle PPRC, Pis su: PVC
        var cwMaterial = Afney.Cad.Mechanical.Enums.PipeMaterial.PPRC_PN20;
        var drMaterial = Afney.Cad.Mechanical.Enums.PipeMaterial.PVC_SN4;

        // 1. Soğuk Su Portu (Mavi)
        bool hasCold = ColdWaterOffset.X != 0 || ColdWaterOffset.Y != 0 || ColdWaterOffset.Z != 0;
        if (hasCold)
            ports.Add(new MechanicalPort(Id, "ColdWater", TransformOffset(ColdWaterOffset), Vector3D.ZAxis, cwDN, cwMaterial)
                { FlowType = FlowDirection.In });
            
        // 2. Sıcak Su Portu (Kırmızı) — WC ve FloorDrain için yok
        bool needsHot = !isWC && !FixtureType.Contains("FloorDrain") && !FixtureType.Contains("Yer Süz");
        bool hasHot   = HotWaterOffset.X != 0 || HotWaterOffset.Y != 0 || HotWaterOffset.Z != 0;
        if (needsHot && hasHot)
            ports.Add(new MechanicalPort(Id, "HotWater", TransformOffset(HotWaterOffset), Vector3D.ZAxis, hwDN, cwMaterial)
                { FlowType = FlowDirection.In });

        // 3. Pis Su Portu (Kahverengi) — her zaman var (X, Y veya Z offset tanımlıysa)
        bool hasDrain = DrainOffset.X != 0 || DrainOffset.Y != 0 || DrainOffset.Z != 0;
        if (hasDrain)
            ports.Add(new MechanicalPort(Id, "Drainage", TransformOffset(DrainOffset), -Vector3D.ZAxis, drDN, drMaterial)
                { FlowType = FlowDirection.Out });

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
        // Port-only mod: vitrifiye sembolü yerine her bağlantı noktasına küçük çarpı sembolü çiz
        if (IsPortOnly)
        {
            DrawPortSymbols(context);
            return;
        }

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
        
        // 2. Tipe Özel Detaylar — TS/DIN Standart MEP Plan Sembolleri
        if (FixtureType.Contains("Lavabo") || FixtureType.Contains("Washbasin"))
        {
            // Oval iç hazne
            double ovalW = halfW * 0.75;
            double ovalD = halfD * 0.6;
            context.DrawArc(Trans(new Vector3D(0, -halfD * 0.1, 0)), ovalW, 0, Math.PI, color, thick * 0.7);
            context.DrawArc(Trans(new Vector3D(0, -halfD * 0.1, 0)), ovalW, Math.PI, 2 * Math.PI, color, thick * 0.7);
            // Gider noktası
            context.DrawCircle(Trans(new Vector3D(0, -halfD * 0.15, 0)), 15, color, thick * 0.5);
            // Batarya kolu
            context.DrawLine(Trans(new Vector3D(-30, halfD * 0.7, 0)), Trans(new Vector3D(30, halfD * 0.7, 0)), color, thick);
            context.DrawCircle(Trans(new Vector3D(0, halfD * 0.7, 0)), 12, color, thick * 0.5);
            // Soğuk/sıcak su işaretleri
            context.DrawCircle(Trans(new Vector3D(-halfW * 0.5, halfD * 0.7, 0)), 8, 0xFF0088FF, thick * 0.4);
            context.DrawCircle(Trans(new Vector3D(halfW * 0.5, halfD * 0.7, 0)), 8, 0xFFFF4444, thick * 0.4);
        }
        else if (FixtureType.Contains("WC") || FixtureType.Contains("Toilet") ||
                 FixtureType.Contains("Klozet") || FixtureType.Contains("Alaturka"))
        {
            // Rezervuar kutusu (arkada)
            double rH = halfD * 0.35;
            context.DrawLine(Trans(new Vector3D(-halfW * 0.85, -halfD, 0)), Trans(new Vector3D(halfW * 0.85, -halfD, 0)), color, thick);
            context.DrawLine(Trans(new Vector3D(-halfW * 0.85, -halfD, 0)), Trans(new Vector3D(-halfW * 0.85, -halfD + rH, 0)), color, thick);
            context.DrawLine(Trans(new Vector3D(halfW * 0.85, -halfD, 0)), Trans(new Vector3D(halfW * 0.85, -halfD + rH, 0)), color, thick);
            context.DrawLine(Trans(new Vector3D(-halfW * 0.85, -halfD + rH, 0)), Trans(new Vector3D(halfW * 0.85, -halfD + rH, 0)), color, thick);
            // Oturak (oval)
            double ovalCenterY = halfD * 0.2;
            context.DrawArc(Trans(new Vector3D(0, ovalCenterY, 0)), halfW * 0.7, 0, Math.PI, color, thick * 0.7);
            context.DrawArc(Trans(new Vector3D(0, ovalCenterY, 0)), halfW * 0.7, Math.PI, 2 * Math.PI, color, thick * 0.7);
            // Kapak menteşe çizgisi
            context.DrawLine(Trans(new Vector3D(-halfW * 0.5, -halfD + rH, 0)), Trans(new Vector3D(halfW * 0.5, -halfD + rH, 0)), color, thick * 0.4);
        }
        else if (FixtureType.Contains("Duş") || FixtureType.Contains("Shower"))
        {
            // Duş teknesi ızgara deseni
            double step = Math.Min(Width, Depth) / 6;
            for (int i = 1; i < 6; i++)
            {
                context.DrawLine(Trans(new Vector3D(-halfW + step * i, -halfD, 0)),
                                 Trans(new Vector3D(-halfW + step * i, halfD, 0)), color, thick * 0.3);
            }
            // Gider noktası (merkez)
            context.DrawCircle(Position, 20, color, thick);
            context.DrawCircle(Position, 8, color, thick * 0.5);
            // Duş başlığı sembolü (köşede daire)
            context.DrawCircle(Trans(new Vector3D(halfW * 0.6, halfD * 0.6, 0)), 25, color, thick * 0.5);
        }
        else if (FixtureType.Contains("Küvet") || FixtureType.Contains("Bathtub"))
        {
            // İç profil (yuvarlatılmış dikdörtgen — ark ile köşeler)
            double inset = 40;
            var ip1 = Trans(new Vector3D(-halfW + inset, -halfD + inset, 0));
            var ip2 = Trans(new Vector3D(halfW - inset, -halfD + inset, 0));
            var ip3 = Trans(new Vector3D(halfW - inset, halfD - inset, 0));
            var ip4 = Trans(new Vector3D(-halfW + inset, halfD - inset, 0));
            context.DrawLine(ip1, ip2, color, thick * 0.6);
            context.DrawLine(ip2, ip3, color, thick * 0.6);
            context.DrawLine(ip3, ip4, color, thick * 0.6);
            context.DrawLine(ip4, ip1, color, thick * 0.6);
            // Batarya (bir uçta)
            context.DrawCircle(Trans(new Vector3D(0, -halfD + 80, 0)), 15, color, thick * 0.5);
            context.DrawLine(Trans(new Vector3D(-30, -halfD + 80, 0)), Trans(new Vector3D(30, -halfD + 80, 0)), color, thick);
            // Gider (diğer uçta)
            context.DrawCircle(Trans(new Vector3D(0, halfD - 80, 0)), 12, color, thick * 0.5);
        }
        else if (FixtureType.Contains("Eviye") || FixtureType.Contains("Sink"))
        {
            // Çift gözlü eviye
            double gozW = halfW * 0.42;
            double gozD = halfD * 0.65;
            // Sol göz
            context.DrawRectangle(Trans(new Vector3D(-halfW * 0.48 - gozW, -gozD, 0)),
                                   Trans(new Vector3D(-halfW * 0.48 + gozW, gozD, 0)), color, thick * 0.5);
            // Sağ göz
            context.DrawRectangle(Trans(new Vector3D(halfW * 0.48 - gozW, -gozD, 0)),
                                   Trans(new Vector3D(halfW * 0.48 + gozW, gozD, 0)), color, thick * 0.5);
            // Gider noktaları
            context.DrawCircle(Trans(new Vector3D(-halfW * 0.48, 0, 0)), 10, color, thick * 0.4);
            context.DrawCircle(Trans(new Vector3D(halfW * 0.48, 0, 0)), 10, color, thick * 0.4);
            // Batarya
            context.DrawCircle(Trans(new Vector3D(0, halfD * 0.7, 0)), 12, color, thick * 0.5);
        }
        else if (FixtureType.Contains("Pisuvar") || FixtureType.Contains("Urinal"))
        {
            // Pisuvar — yarım daire üst + dikdörtgen alt
            context.DrawArc(Trans(new Vector3D(0, 0, 0)), halfW * 0.7, 0, Math.PI, color, thick * 0.7);
            context.DrawLine(Trans(new Vector3D(-halfW * 0.7, 0, 0)), Trans(new Vector3D(-halfW * 0.7, -halfD * 0.6, 0)), color, thick * 0.7);
            context.DrawLine(Trans(new Vector3D(halfW * 0.7, 0, 0)), Trans(new Vector3D(halfW * 0.7, -halfD * 0.6, 0)), color, thick * 0.7);
            // Gider
            context.DrawCircle(Trans(new Vector3D(0, -halfD * 0.3, 0)), 10, color, thick * 0.5);
        }
        else if (FixtureType.Contains("Çamaşır") || FixtureType.Contains("Washing"))
        {
            // Çamaşır makinesi — daire (tambur) + dikdörtgen gövde
            context.DrawCircle(Position, halfW * 0.6, color, thick * 0.6);
            context.DrawCircle(Position, halfW * 0.25, color, thick * 0.4);
        }
        else if (FixtureType.Contains("Bulaşık") || FixtureType.Contains("Dish"))
        {
            // Bulaşık makinesi — ızgara + kapak çizgisi
            context.DrawLine(Trans(new Vector3D(-halfW, halfD * 0.3, 0)), Trans(new Vector3D(halfW, halfD * 0.3, 0)), color, thick * 0.5);
            context.DrawLine(Trans(new Vector3D(-halfW * 0.3, -halfD, 0)), Trans(new Vector3D(-halfW * 0.3, halfD * 0.3, 0)), color, thick * 0.3);
            context.DrawLine(Trans(new Vector3D(halfW * 0.3, -halfD, 0)), Trans(new Vector3D(halfW * 0.3, halfD * 0.3, 0)), color, thick * 0.3);
        }
        else if (FixtureType.Contains("Isıtıcı") || FixtureType.Contains("Heater") || FixtureType.Contains("Şofben"))
        {
            // Elektrikli su ısıtıcı — daire + yıldırım sembolü
            context.DrawCircle(Position, halfW * 0.7, color, thick * 0.7);
            context.DrawLine(Trans(new Vector3D(-10, halfD * 0.4, 0)), Trans(new Vector3D(10, 0, 0)), color, thick);
            context.DrawLine(Trans(new Vector3D(10, 0, 0)), Trans(new Vector3D(-10, -halfD * 0.4, 0)), color, thick);
        }
        else if (FixtureType.Contains("FloorDrain") || FixtureType.Contains("Yer Süz"))
        {
            // Yer süzgeci — kare + çapraz
            context.DrawLine(p1, p3, color, thick * 0.4);
            context.DrawLine(p2, p4, color, thick * 0.4);
            context.DrawCircle(Position, 15, color, thick * 0.5);
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

    // Port-only sembolü: her bağlantı noktasına artı (+) çizer, tipe ait kısa etiket ekler
    private void DrawPortSymbols(IRenderContext context)
    {
        const double r = 80; // çarpı yarı kolu, mm
        uint colCold  = 0xFF00BBFF;
        uint colHot   = 0xFFFF6644;
        uint colDrain = 0xFF886633;
        uint colLabel = 0xFFCCCCCC;

        void DrawCross(Vector3D pt, uint col)
        {
            context.DrawLine(new Vector3D(pt.X - r, pt.Y, pt.Z), new Vector3D(pt.X + r, pt.Y, pt.Z), col, 1.5);
            context.DrawLine(new Vector3D(pt.X, pt.Y - r, pt.Z), new Vector3D(pt.X, pt.Y + r, pt.Z), col, 1.5);
        }

        var coldPt  = new Vector3D(Position.X + ColdWaterOffset.X, Position.Y + ColdWaterOffset.Y, Position.Z);
        var hotPt   = new Vector3D(Position.X + HotWaterOffset.X,  Position.Y + HotWaterOffset.Y,  Position.Z);
        var drainPt = new Vector3D(Position.X + DrainOffset.X,     Position.Y + DrainOffset.Y,     Position.Z);

        DrawCross(coldPt,  colCold);
        DrawCross(hotPt,   colHot);
        DrawCross(drainPt, colDrain);

        // Cihaz tipi kısa etiketi ortada
        string shortLabel = FixtureType.Length > 10 ? FixtureType[..10] : FixtureType;
        context.DrawText(shortLabel, Position + new Vector3D(0, r + 40, 0), 0, 80, colLabel);
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
            SystemType = this.SystemType,
            IsPortOnly = this.IsPortOnly
        };
    }

    // ── STATIC FACTORY METODLAR (TS 1258 Standart Değerler) ──────────────────────
    // NE: Standart cihazları tek satırda örneğini alma.
    // NEDEN: Birim testlerde, sihirbazda ve DWG import'ta hızlı obje üretimi için.

    /// <summary>Standart yarım ayak lavabo — 550×450mm, DN40 gider, DN15 sıcak+soğuk.</summary>
    public static SanitaryFixtureEntity CreateWashbasin(Vector3D position)
        => new(position, "Lavabo (Yarım Ayak)", 1.5)
        {
            Width = 550, Depth = 450,
            ColdWaterOffset = new Vector3D(80, -50, -500),
            HotWaterOffset  = new Vector3D(-80, -50, -500),
            DrainOffset     = new Vector3D(0, 0, -550),
            Color = 0xFF00FFFF
        };

    /// <summary>Rezervuarlı klozet — 400×600mm, DN100 gider, DN15 soğuk, sıcak su YOK.</summary>
    public static SanitaryFixtureEntity CreateWC(Vector3D position)
        => new(position, "Klozet (Rezervuarlı)", 3.0)
        {
            Width = 400, Depth = 600,
            ColdWaterOffset = new Vector3D(-150, -550, 200),
            HotWaterOffset  = Vector3D.Zero, // WC'de sıcak su bağlantısı yok
            DrainOffset     = new Vector3D(0, -250, -100),
            Color = 0xFF00FFFF
        };

    /// <summary>Duş teknesi — 800×800mm, DN50 gider, DN15 sıcak+soğuk.</summary>
    public static SanitaryFixtureEntity CreateShower(Vector3D position)
        => new(position, "Duş Teknesi", 2.0)
        {
            Width = 800, Depth = 800,
            ColdWaterOffset = new Vector3D(80, 0, 1000),
            HotWaterOffset  = new Vector3D(-80, 0, 1000),
            DrainOffset     = new Vector3D(0, 380, 0),
            Color = 0xFF00FFFF
        };

    /// <summary>Banyo küveti — 700×1600mm, DN50 gider, DN15 sıcak+soğuk.</summary>
    public static SanitaryFixtureEntity CreateBathtub(Vector3D position)
        => new(position, "Banyo Küveti", 3.0)
        {
            Width = 700, Depth = 1600,
            ColdWaterOffset = new Vector3D(80, -700, 500),
            HotWaterOffset  = new Vector3D(-80, -700, 500),
            DrainOffset     = new Vector3D(0, 730, 0),
            Color = 0xFF00FFFF
        };

    /// <summary>Mutfak eviyesi (tek) — 500×400mm, DN50 gider, DN15 sıcak+soğuk.</summary>
    public static SanitaryFixtureEntity CreateSink(Vector3D position)
        => new(position, "Mutfak Eviyesi (Tek)", 2.0)
        {
            Width = 500, Depth = 400,
            ColdWaterOffset = new Vector3D(80, -150, -400),
            HotWaterOffset  = new Vector3D(-80, -150, -400),
            DrainOffset     = new Vector3D(0, 0, -450),
            Color = 0xFF00FFFF
        };

    /// <summary>Döşeme süzgeci — 200×200mm, yalnızca DN75 pis su çıkışı.</summary>
    public static SanitaryFixtureEntity CreateFloorDrain(Vector3D position)
        => new(position, "Döşeme Süzgeci", 0.5)
        {
            Width = 200, Depth = 200,
            ColdWaterOffset = Vector3D.Zero, // Soğuk su bağlantısı yok
            HotWaterOffset  = Vector3D.Zero,
            DrainOffset     = new Vector3D(0, 0, -300),
            Color = 0xFF00FFFF
        };

    public static SanitaryFixtureEntity CreateUrinal(Vector3D position)
        => new(position, "Pisuvar", 2.0)
        {
            Width = 350, Depth = 300,
            ColdWaterOffset = new Vector3D(0, -100, 800),
            HotWaterOffset  = Vector3D.Zero,
            DrainOffset     = new Vector3D(0, 0, -200),
            Color = 0xFF00FFFF
        };

    public static SanitaryFixtureEntity CreateWashingMachine(Vector3D position)
        => new(position, "Çamaşır Makinesi", 2.0)
        {
            Width = 600, Depth = 600,
            ColdWaterOffset = new Vector3D(0, -250, 700),
            HotWaterOffset  = Vector3D.Zero,
            DrainOffset     = new Vector3D(200, -250, 0),
            Color = 0xFF00FFFF
        };

    public static SanitaryFixtureEntity CreateDishwasher(Vector3D position)
        => new(position, "Bulaşık Makinesi", 2.0)
        {
            Width = 600, Depth = 600,
            ColdWaterOffset = new Vector3D(0, -250, 700),
            HotWaterOffset  = new Vector3D(-100, -250, 700),
            DrainOffset     = new Vector3D(200, -250, 0),
            Color = 0xFF00FFFF
        };

    public static SanitaryFixtureEntity CreateWaterHeater(Vector3D position)
        => new(position, "Elektrikli Su Isıtıcı (Şofben)", 0.0)
        {
            Width = 400, Depth = 300,
            ColdWaterOffset = new Vector3D(-100, 0, -200),
            HotWaterOffset  = new Vector3D(100, 0, -200),
            DrainOffset     = Vector3D.Zero,
            Color = 0xFFFF8800
        };

    public static SanitaryFixtureEntity CreateDoubleSink(Vector3D position)
        => new(position, "Mutfak Eviyesi (Çift Göz)", 3.0)
        {
            Width = 800, Depth = 450,
            ColdWaterOffset = new Vector3D(80, -150, -400),
            HotWaterOffset  = new Vector3D(-80, -150, -400),
            DrainOffset     = new Vector3D(0, 0, -450),
            Color = 0xFF00FFFF
        };

    public void SetPortsByRule(string ruleName) { }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield return new SnapPoint(Position, SnapPointType.Center);
        // Port noktaları da snap olmalı
        foreach(var port in GetPorts())
             yield return new SnapPoint(port.Position, SnapPointType.Connection);
    }
}
