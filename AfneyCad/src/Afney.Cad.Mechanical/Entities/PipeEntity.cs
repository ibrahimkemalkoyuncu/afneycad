using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Engine;

namespace Afney.Cad.Mechanical.Entities;

/*
   NE: Boru Varlığı (PipeEntity)
   NEDEN: Fiziksel bir boruyu CAD ortamında temsil etmek için.

   MÜHENDİSLİK DETAYI:
   - Başlangıç ve Bitiş (Start/End Point) koordinatlarına sahiptir.
   - Çap (Diameter), Et Kalınlığı ve Malzeme özelliklerini taşır.
   - Hidrolik hesaplamalar için Debi (FlowRate), Basınç (Pressure) ve Sıcaklık verilerini tutar.
   - Render motoruna kendini çizdirir (2 silindir hattı + merkez).
*/

public class PipeEntity : MechanicalEntity
{
    /// <summary>
    /// Boru başlangıç noktası
    /// </summary>
    public Vector3D StartPoint { get; set; }
    
    /// <summary>
    /// Boru bitiş noktası
    /// </summary>
    public Vector3D EndPoint { get; set; }
    

    
    /// <summary>
    /// Akış debisi (m³/h)
    /// </summary>
    public double FlowRate { get; set; }
    
    /// <summary>
    /// İşletme basıncı (bar)
    /// </summary>
    public double Pressure { get; set; }
    
    /// <summary>
    /// Sıcaklık (°C)
    /// </summary>
    public double Temperature { get; set; } = 20.0;

    // NE: Eğim (Slope)
    // NEDEN: Atık su (Pis su) tesisatında suyun cazibe ile akması için gerekli olan eğim (%) bilgisini tutmak için.
    // Örn: 0.02 -> %2 eğim.
    public double Slope { get; set; } = 0.0;

    // NE: Yükleme Birimi (Fixture Unit / Load Unit - TS 1258)
    public double LoadUnits { get; set; } = 1.0; 
    // Alias for compatibility
    public double TotalFixtureUnits { get => LoadUnits; set => LoadUnits = value; }

    // NE: Klozet Yükü Taşıyor Mu?
    // NEDEN: Yönetmelik gereği klozet bağlanan boruların minimum DN 100 olması gerektiğini doğrulamak için.
    public bool IsCarryingWCLoad { get; set; } = false;

    // NE: Akış Yönü (Flow Direction)
    // NEDEN: Tesisat şemalarında suyun hangi yönde aktığını görselleştirmek ve hidrolik dengeleme yapmak için.
    // 0: Akış yok veya belirsiz
    // 1: Start -> End
    // -1: End -> Start
    public int FlowDirection { get; set; } = 0;

    // NE: Akış Hızı (m/s)
    // NEDEN: Gürültü ve aşınma kontrolü için.
    public double Velocity { get; set; } = 0.0;

    // NE: Basınç Kaybı (mSS veya kPa)
    // NEDEN: Kritik hat hesabında pompa basma yüksekliğini belirlemek için.
    public double PressureDrop { get; set; } = 0.0;

    // NE: Hidrolik Hata Durumu (Step 5)
    // NEDEN: Hız veya basınç kaybı limitleri aşıldığında görsel uyarı vermek için.
    public bool HasHydraulicViolation { get; set; } = false;



    /// <summary>
    /// Sistem tipine göre boru rengini otomatik olarak ayarlar.
    /// </summary>
    public void ApplySystemColor()
    {
        Color = SystemType switch
        {
            MechanicalSystemType.DomesticColdWater => 0xFF00AAFF, // Mavi
            MechanicalSystemType.DomesticHotWater => 0xFFFF3333,  // Kırmızı
            MechanicalSystemType.WasteWater => 0xFF888888,       // Gri
            MechanicalSystemType.FireProtection => 0xFFFF0000,   // Parlak Kırmızı
            MechanicalSystemType.Gas => 0xFFFFFF00,              // Sarı
            _ => 0xFFFFFFFF                                      // Beyaz (Tanımsız)
        };
    }



    /*
       NE: PipeEntity Yapıcı Metodu
       NEDEN: Başlangıç/bitiş noktaları ve çap bilgisiyle tesisat sisteminin temel taşı olan boru nesnesini oluşturmak için.
    */
    public PipeEntity(Vector3D start, Vector3D end, double diameter)
    {
        StartPoint = start;
        EndPoint = end;
        InnerDiameter = diameter;
        EntityType = MechanicalEntityType.Pipe;
    }

    /*
    METOD ADI:
    GetLength

    AMACI:
    Boru uzunluğunu hesaplamak.

    GİRDİLER:
    Yok.

    ÇIKTILAR:
    double - Boru uzunluğu (metre).

    KULLANIM SENARYOSU:
    Metraj çıkarma, malzeme hesabı.

    PERFORMANS NOTU:
    O(1) - Vektör uzaklık hesabı.
    */
    public double GetLength()
    {
        return StartPoint.DistanceTo(EndPoint);
    }

    public double Length => GetLength();

    /*
       NE: Noktanın Boruya Olan En Kısa Mesafesi (Line Segment Distance)
       NEDEN: Farenin (Hit-Testing) silindirik borunun üzerinde olup olmadığını anlamak için. (Fare koordinatı boru çizgisine dik olarak ne kadar uzakta?)
    */
    public override double DistanceTo(Vector3D p)
    {
        var v = StartPoint;
        var w = EndPoint;
        
        // Borunun karesel uzunluğu
        double l2 = Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2) + Math.Pow(v.Z - w.Z, 2);
        
        if (l2 == 0.0) return p.DistanceTo(v); // Boru tek bir noktaysa
        
        // T parametresini bul (noktanın boru üzerindeki izdüşümü: t=0 -> Start, t=1 -> End)
        double t = Math.Max(0, Math.Min(1, ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y) + (p.Z - v.Z) * (w.Z - v.Z)) / l2));
        
        // İzdüşüm noktası (Projection)
        var projection = new Vector3D(
            v.X + t * (w.X - v.X),
            v.Y + t * (w.Y - v.Y),
            v.Z + t * (w.Z - v.Z)
        );
        
        // Gerçek dik mesafe
        return p.DistanceTo(projection);
    }

    /*
    METOD ADI:
    GetVelocity

    AMACI:
    Borudaki akış hızını hesaplamak.

    GİRDİLER:
    Yok (FlowRate ve InnerDiameter kullanır).

    ÇIKTILAR:
    double - Akış hızı (m/s).

    KULLANIM SENARYOSU:
    Hız kontrol validasyonu (maksimum 2 m/s olmalı).

    PERFORMANS NOTU:
    Basit aritmetik işlem.
    */
    /*
       NE: Akış Hızını Hesapla (GetVelocity)
       NEDEN: Boru içindeki suyun hızını (V = Q/A) hesaplayarak gürültü ve aşınma limitlerinin (Örn: 2 m/s) aşılıp aşılmadığını denetlemek için.
    */
    public double GetVelocity()
    {
        if (InnerDiameter <= 0 || FlowRate <= 0)
            return 0;

        // Alan = π * r²
        double radiusMeters = (InnerDiameter / 1000.0) / 2.0; // mm -> m
        double areaM2 = Math.PI * radiusMeters * radiusMeters;

        // Hız = Debi / Alan  (m³/h -> m³/s)
        double flowRateM3S = FlowRate / 3600.0;
        return flowRateM3S / areaM2;
    }

    protected override CadBoundingBox CalculateBoundingBox()
    {
        double minX = Math.Min(StartPoint.X, EndPoint.X);
        double minY = Math.Min(StartPoint.Y, EndPoint.Y);
        double minZ = Math.Min(StartPoint.Z, EndPoint.Z);

        double maxX = Math.Max(StartPoint.X, EndPoint.X);
        double maxY = Math.Max(StartPoint.Y, EndPoint.Y);
        double maxZ = Math.Max(StartPoint.Z, EndPoint.Z);

        return new CadBoundingBox(
            new Vector3D(minX, minY, minZ),
            new Vector3D(maxX, maxY, maxZ)
        );
    }

    /*
    METOD ADI: Draw
    AMACI: Boruyu ve üzerindeki teknik etiketi ekrana çizmek.
    NASIL (Mete Bey): 
    - Gövdeyi 'DrawLine' ile çizer.
    - Etiketi 'DrawText' ile tam orta noktaya, boru açısında yerleştirir.
    - Metnin ters (baş aşağı) gelmemesi için 90-270 derece arası takla attırılır.
    */
    public override void Draw(IRenderContext ctx)
    {
        // 1. Boru gövdesini çiz
        // Mühendislik Modu: 
        // - Hesaplama geçersizse (Dirty) SARI
        // - Hata durumunda KIRMIZI
        // - Normalde sistem rengi.
        uint drawColor = Color;
        
        if (!IsCalculationUpToDate)
        {
            drawColor = 0xFFFFFF00; // Sarı (Uyarı - Tekrar hesaplanmalı)
        }
        else if (HasHydraulicViolation)
        {
            drawColor = 0xFFFF0000; // Kırmızı (Validasyon Hatası - Örn: Hızaşıldı)
        }

        if (InnerDiameter > 0)
        {
            ctx.DrawSolidLine(StartPoint, EndPoint, drawColor, InnerDiameter, InnerDiameter + 4.0); 
        }
        else
        {
            ctx.DrawLine(StartPoint, EndPoint, drawColor, 1.0);
        }

        // 2. Teknik Etiketi Çiz (Annotation)
        var dir = (EndPoint - StartPoint).Normalize();
        var center = (StartPoint + EndPoint) * 0.5;
        
        // Açıyı hesapla (Radyan -> Derece)
        double angleRad = Math.Atan2(dir.Y, dir.X);
        double angleDeg = angleRad * (180.0 / Math.PI);

        // Metnin her zaman okunabilir (yukarı bakacak şekilde) kalmasını sağla
        if (angleDeg > 90 || angleDeg < -90)
        {
            angleDeg += 180;
        }

        string text = $"Ø{InnerDiameter:F0}";
        if (Math.Abs(Slope) > 0)
        {
             text += $" %{Slope * 100:F1}";
        }

        // Metni borunun biraz üzerine kaydır (Offset)
        var normal = new Vector3D(-dir.Y, dir.X, 0) * (InnerDiameter / 50.0);
        var textPos = center + normal;

        ctx.DrawText(text, textPos, angleDeg, 12.0, drawColor);

        // --- Step 2: Akış Yönü Oklarını Çiz ---
        if (FlowDirection != 0)
        {
            DrawFlowArrow(ctx, center, dir * FlowDirection, drawColor);
        }
    }

    private void DrawFlowArrow(IRenderContext ctx, Vector3D center, Vector3D direction, uint drawColor)
    {
        double arrowSize = Math.Max(5.0, InnerDiameter / 3.0);
        var p1 = center + direction * arrowSize;
        
        var normal = new Vector3D(-direction.Y, direction.X, 0) * (arrowSize * 0.6);
        var side1 = center - direction * arrowSize + normal;
        var side2 = center - direction * arrowSize - normal;

        ctx.DrawLine(p1, side1, drawColor, 1.0);
        ctx.DrawLine(p1, side2, drawColor, 1.0);
        ctx.DrawLine(side1, side2, drawColor, 1.0); // Kapalı üçgen
    }

    public override CadEntity Clone()
    {
        return new PipeEntity(StartPoint, EndPoint, InnerDiameter)
        {
            Id = Guid.NewGuid(),
            Layer = this.Layer,
            Color = this.Color,
            PipeMaterialType = this.PipeMaterialType,
            FlowRate = this.FlowRate,
            Pressure = this.Pressure,
            Temperature = this.Temperature
        };
    }

    public override void Move(Vector3D delta)
    {
        StartPoint += delta;
        EndPoint += delta;
    }

    /*
    METOD ADI: Transform
    
    AMACI:
    Boruyu matris transformasyonuna (döndürme, ölçekleme, yansıtma) tabi tutmak.
    
    GİRDİLER:
    - matrix: 4x4 transformasyon matrisi
    
    KULLANIM SENARYOSU:
    CAD komutları (Rotate, Scale, Mirror) için temel işlem.
    
    MÜHENDİSLİK NOTU (Kemal):
    StartPoint ve EndPoint matris ile çarpılarak yeni konumları hesaplanır.
    */
    public override void Transform(Matrix4x4 matrix)
    {
        StartPoint = matrix.Transform(StartPoint);
        EndPoint = matrix.Transform(EndPoint);
    }

    public override IEnumerable<SnapPoint> GetSnapPoints()
    {
        // Endpoint: Başlangıç
        yield return new SnapPoint(StartPoint, SnapPointType.Endpoint);
        
        // Endpoint: Bitiş
        yield return new SnapPoint(EndPoint, SnapPointType.Endpoint);
        
        // Midpoint: Orta nokta
        var midpoint = new Vector3D(
            (StartPoint.X + EndPoint.X) / 2.0,
            (StartPoint.Y + EndPoint.Y) / 2.0,
            (StartPoint.Z + EndPoint.Z) / 2.0
        );
        yield return new SnapPoint(midpoint, SnapPointType.Midpoint);
    }

    /*
       NE: Bağlantı Portlarını Getir (GetPorts)
       NEDEN: Borunun iki ucunda (Start/End) diğer tesisat elemanlarıyla (Dirsek, Vana vb.) birleşebileceği noktaları ve akış yönlerini topoloji grafı için tanımlamak için.
    */
    public override List<MechanicalPort> GetPorts()
    {
        var dir = (EndPoint - StartPoint).Normalize();
        return new List<MechanicalPort>
        {
            new MechanicalPort(this.Id, "Start", StartPoint, dir * -1),
            new MechanicalPort(this.Id, "End", EndPoint, dir)
        };
    }

    public override IEnumerable<Vector3D> GetGripPoints()
    {
        yield return StartPoint;
        yield return EndPoint;
        yield return new Vector3D((StartPoint.X + EndPoint.X) / 2.0, (StartPoint.Y + EndPoint.Y) / 2.0, (StartPoint.Z + EndPoint.Z) / 2.0);
    }

    public override void MoveGripPointAt(int index, Vector3D newPosition)
    {
        if (index == 0) StartPoint = newPosition;
        else if (index == 1) EndPoint = newPosition;
        else if (index == 2)
        {
            var delta = newPosition - new Vector3D((StartPoint.X + EndPoint.X) / 2.0, (StartPoint.Y + EndPoint.Y) / 2.0, (StartPoint.Z + EndPoint.Z) / 2.0);
            Move(delta);
        }
        base.MoveGripPointAt(index, newPosition);
    }
}
