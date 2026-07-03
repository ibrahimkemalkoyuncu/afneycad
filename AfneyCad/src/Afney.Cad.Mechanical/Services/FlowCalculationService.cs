using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Akış Hesaplama ve Otomatik Çaplandırma Servisi (FlowCalculationService)
   NEDEN: FINE SANI / DIN 1988 Standartlarında, tesisat ağındaki yükleri (FU/LU) toplayarak boru çaplarını otomatik belirlemek için.

   NASIL (Mühendislik Detayı):
   - Graf Tarama: MEP dikey/yatay ağını bir ağaç yapısı olarak kabul eder ve yapraklardan (Fixture) köke (Riser) doğru tarama yapar.
   - Eş Zamanlılık Faktorü: TS 1258 ve DIN 1988-3 standartlarındaki formülleri (Q = a * FU^b - c) kullanarak pik debiyi bulur.
   - Hız Kontrolü: Belirlenen debide hızın 0.5 - 2.0 m/s aralığında kalmasını sağlayan en küçük standard çapı seçer.
*/
public class FlowCalculationService
{
    private readonly MechanicalTopologyGraph _graph;

    // NE: Kullanım Eş Zamanlılık Faktörü (K)
    // NEDEN: Binadaki her vitrifiyenin aynı anda kullanılmayacağı varsayımıyla (Diversity Factor) debiyi normalize etmek için.
    // TS EN 12056: Konutlar için 0.5, İş yerleri için 0.7, Kamusal alanlar için 1.0 kullanılır.
    public double FrequencyFactor { get; set; } = 0.5;
    public double MinFixtureFlow { get; set; } = 0.5; // l/s (Sistemdeki en büyük cihaz debisinden küçük olamaz kuralı)
    public Enums.BuildingType CurrentBuildingType { get; set; } = Enums.BuildingType.Residential;

    /*
       NE: FlowCalculationService Yapıcı Metodu
       NEDEN: Topoloji grafını alarak sistemdeki akış yollarını analiz etmeye hazır hale getirir.
    */
    public FlowCalculationService(MechanicalTopologyGraph graph)
    {
        _graph = graph;
    }

    /*
       NE: Bina Katsayılarını Getir (GetCoefficients)
       NEDEN: Bina tipine göre (Otel, Hastane, Konut) akış hesap formülündeki a, b ve c katsayılarını standartlardan (TS 1258) seçmek için.
    */
    private (double a, double b, double c) GetCoefficients()
    {
        return CurrentBuildingType switch
        {
            Enums.BuildingType.Residential => (0.682, 0.45, 0.14),
            Enums.BuildingType.Hotel => (0.7, 0.48, 0.13),
            Enums.BuildingType.Hospital => (1.0, 0.5, 0),
            Enums.BuildingType.Office => (0.6, 0.5, 0.1),
            Enums.BuildingType.School => (0.8, 0.45, 0),
            _ => (1.0, 0.5, 0) // Industrial / Public
        };
    }

    /*
       NE: Yalnızca Akış Yönlerini Sapta (InferFlowDirections)
       NEDEN: Sistem çizilirken veya parçalar eklendiğinde tam hesap (çap/debi) yapılmadan önce boruların üzerindeki okların anında belirmesi için (Domain Requirement).
    */
    public void InferFlowDirections(IEnumerable<MechanicalEntity> entities)
    {
        var mechanicalEntities = entities.ToList();
        var entityMap = mechanicalEntities.ToDictionary(e => e.Id);

        // Önce yönleri sıfırla
        foreach (var pipe in mechanicalEntities.OfType<PipeEntity>())
        {
            pipe.FlowDirection = 0;
        }

        var sinks = mechanicalEntities.OfType<PipeEntity>()
            .Where(p => IsRiser(p))
            .Select(p => p.Id)
            .ToHashSet();

        // 1. Vitrifiyeler VE Manuel Yük Noktaları üzerinden akış yönü saptanır
        foreach (var entity in mechanicalEntities)
        {
            if (entity is not SanitaryFixtureEntity && entity is not MechanicalLoadNode) continue;

            var path = FindPathToNearestSink(entity.Id, sinks, entityMap);
            if (path != null)
            {
                // Sadece yol boyu boru yönlerini ata (FU veya hesap yapma)
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var currentId = path[i];
                    var nextId = path[i + 1];

                    if (entityMap.TryGetValue(nextId, out var nextEntity) && nextEntity is PipeEntity pipe)
                    {
                        UpdatePipeFlowDirection(pipe, currentId, nextId);
                    }
                }
            }
        }
    }

    /*
       NE: Tüm Sistemi Hesapla (CalculateSystemFlow)
       NEDEN: Proje genelindeki tüm boruların yüklerini (FU) uçtan kolona doğru (Dikey/Yatay ağ taraması ile) güncelleyerek debi, hız ve basınç kaybı (Darcy/Manning) hesaplarına temel oluşturmak için.
    */
    public void CalculateSystemFlow(IEnumerable<MechanicalEntity> entities)
    {
        var mechanicalEntities = entities.ToList();
        var entityMap = mechanicalEntities.ToDictionary(e => e.Id);

        // 1. Tüm boruların FU ve FlowRate değerlerini sıfırla
        foreach (var pipe in mechanicalEntities.OfType<PipeEntity>())
        {
            pipe.TotalFixtureUnits = 0;
            pipe.FlowRate = 0;
            pipe.IsCarryingWCLoad = false;
            pipe.FlowDirection = 0; // Sıfırla
        }

        // 2. Çıkış Noktalarını (Sinks) Saptanması
        // Mühendislik Mantığı: Riser olan borular sistemin toplama merkezleridir (Sink).
        var sinks = mechanicalEntities.OfType<PipeEntity>()
            .Where(p => IsRiser(p))
            .Select(p => p.Id)
            .ToHashSet();

        // 3. Her Vitrifiyeden En Yakın Sink'e Yol Bul ve Yükle
        // 3. Her Vitrifiye veya Yük Noktasından En Yakın Sink'e Yol Bul ve Yükle
        foreach (var entity in mechanicalEntities)
        {
            double fu = 0;
            bool isWC = false;

            if (entity is SanitaryFixtureEntity fixture)
            {
                fu = fixture.FixtureUnit;
                isWC = fixture.FixtureType.Contains("WC", StringComparison.OrdinalIgnoreCase);
            }
            else if (entity is MechanicalLoadNode loadNode)
            {
                fu = loadNode.LoadUnits;
                // LoadNode manuel yük olduğu için WC olup olmadığını SystemType veya isimlendirmeden çıkarabiliriz 
                // ya da LoadNode'a IsWCLoad property'si eklenebilir. Şimdilik SystemType kuralı kalsın.
                isWC = loadNode.SystemType == MechanicalSystemType.WasteWater; 
            }
            else continue;

            var path = FindPathToNearestSink(entity.Id, sinks, entityMap);
            if (path != null)
            {
                ApplyLoadToPath(path, fu, isWC, entityMap);
            }
        }

        // 4. Eş Zamanlı Debiyi (Q), Hızı (v) ve Basınç Kaybını (ΔP) Hesapla
        var (a, b, c) = GetCoefficients();

        foreach (var pipe in mechanicalEntities.OfType<PipeEntity>())
        {
            if (pipe.TotalFixtureUnits > 0)
            {
                double peakFlowRateLS = 0;

                if (pipe.SystemType == MechanicalSystemType.WasteWater)
                {
                    // TS EN 12056: Qww = K * sqrt(Sum DU)
                    peakFlowRateLS = FrequencyFactor * System.Math.Sqrt(pipe.TotalFixtureUnits);
                }
                else
                {
                    // DIN 1988-3 / TS 1258: Qp = a * (Sum LU)^b - c
                    if (pipe.TotalFixtureUnits > 0)
                    {
                        peakFlowRateLS = a * System.Math.Pow(pipe.TotalFixtureUnits, b) - c;
                    }
                }
                
                // MÜHENDİSLİK KURALI: Toplam debi, koldaki en büyük cihazın debisinden küçük olamaz.
                peakFlowRateLS = System.Math.Max(peakFlowRateLS, MinFixtureFlow);
                if (peakFlowRateLS < 0) peakFlowRateLS = MinFixtureFlow;
                
                pipe.FlowRate = peakFlowRateLS * 3.6; // l/s -> m3/h

                // --- MÜHENDİSLİK HESAPLARI (CORE KERNEL) ---

                double kinematicViscosity = WaterPropertiesService.GetKinematicViscosity(pipe.Temperature);
                double length_m = (pipe.EndPoint - pipe.StartPoint).Length() / 1000.0;
                double D = pipe.InnerDiameter / 1000.0; // m
                double area_m2 = System.Math.PI * System.Math.Pow(D / 2.0, 2);

                if (area_m2 > 0)
                {
                    pipe.Velocity = (peakFlowRateLS / 1000.0) / area_m2;

                    if (pipe.SystemType == MechanicalSystemType.WasteWater)
                    {
                        double S = pipe.Slope > 0 ? pipe.Slope : 0.01;
                        if (IsRiser(pipe)) S = 1.0;

                        // Kısmi doluluk analizi (Camp formülü — h/D eğrileri)
                        var partialResult = AdvancedHydraulicsService.CalculatePartialFlow(
                            peakFlowRateLS, pipe.InnerDiameter, S * 100.0);

                        pipe.Velocity = partialResult.ActualVelocity;
                        pipe.HasHydraulicViolation = partialResult.IsOverCapacity || !partialResult.SelfCleansingOk;
                    }
                    else
                    {
                        // TEMİZ SU: Darcy-Weisbach & İteratif Colebrook-White (Newton-Raphson)
                        double Re = pipe.Velocity * D / kinematicViscosity;
                        double roughnessMm = pipe.PipeMaterialType switch
                        {
                            PipeMaterial.Steel_Galvanized => 0.045,
                            PipeMaterial.PVC_SN4 => 0.007,
                            PipeMaterial.PEX_b => 0.007,
                            PipeMaterial.Silent_PP => 0.007,
                            _ => 0.007 // PP-R varsayılan
                        };

                        double lambda = AdvancedHydraulicsService.ColebrookWhiteFriction(Re, roughnessMm, pipe.InnerDiameter);

                        pipe.PressureDrop = lambda * (length_m / D) * (System.Math.Pow(pipe.Velocity, 2) / (2 * 9.81));
                    }
                }
            }
        }
    }

    /*
       NE: Kolon Kontrolü (IsRiser)
       NEDEN: Borunun dikey doğrultuda olup olmadığını saptayarak yerçekimi bazlı hesaplamaları (Pis su düşey boru kapasitesi vb.) ayırt etmek için.
    */
    private bool IsRiser(PipeEntity pipe)
    {
        // Basit kontrol: Dikey borular riserdır.
        var dir = (pipe.EndPoint - pipe.StartPoint).Normalize();
        return System.Math.Abs(dir.Z) > 0.8; 
    }

    /*
       NE: En Yakın Gideri/Kolonu Bul (FindPathToNearestSink)
       NEDEN: Bir lavabo veya WC'nin akışını, sistemdeki ana toplama noktasına (Riser/Sink) ulaştıran en kısa topolojik yolu Breadth-First Search (BFS) ile bulmak için.
    */
    private List<Guid>? FindPathToNearestSink(Guid startId, HashSet<Guid> sinks, Dictionary<Guid, MechanicalEntity> entityMap)
    {
        var queue = new Queue<(Guid current, List<Guid> path)>();
        queue.Enqueue((startId, new List<Guid> { startId }));
        var visited = new HashSet<Guid>();

        while (queue.Count > 0)
        {
            var (currentId, path) = queue.Dequeue();
            if (visited.Contains(currentId)) continue;
            visited.Add(currentId);

            if (sinks.Contains(currentId)) return path;

            var neighbors = _graph.GetNeighbors(currentId);
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor.OwnerId))
                {
                    var newPath = new List<Guid>(path) { neighbor.OwnerId };
                    queue.Enqueue((neighbor.OwnerId, newPath));
                }
            }
        }
        return null; // Sink bulunamadı (Açık hat)
    }

    /*
       NE: Yükü Güzergaha Dağıt (ApplyLoadToPath)
       NEDEN: Hesaplanmış bir güzergah üzerindeki tüm boru segmentlerine ilgili cihazın kullanım birimi (FU) yükünü eklemek için.
    */
    private void ApplyLoadToPath(List<Guid> path, double fu, bool isWC, Dictionary<Guid, MechanicalEntity> entityMap)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            var currentId = path[i];
            var nextId = path[i+1];

            if (entityMap.TryGetValue(nextId, out var entity) && entity is PipeEntity pipe)
            {
                pipe.TotalFixtureUnits += fu;
                if (isWC) pipe.IsCarryingWCLoad = true;

                // Akış Yönü Hesapla
                UpdatePipeFlowDirection(pipe, currentId, nextId);
            }
        }
    }

    /*
       NE: Akış Yönünü Güncelle (UpdatePipeFlowDirection)
       NEDEN: Borunun Start/End uçlarından hangisinin akış girişi hangisinin çıkışı olduğunu belirlemek ve render motoruna bildirmek için.
    */
    private void UpdatePipeFlowDirection(PipeEntity pipe, Guid fromId, Guid toId)
    {
        var node = _graph.GetNode(pipe.Id);
        if (node == null) return;

        var fromPort = node.Ports.FirstOrDefault(p => p.ConnectedEntityId == fromId);
        if (fromPort != null)
        {
            pipe.FlowDirection = fromPort.Name == "Start" ? 1 : -1;
        }
    }

    /*
       NE: Boruları Otomatik Çaplandır (AutoSizePipes)
       NEDEN: Hesaplanan pik debilere göre, temiz su hatlarında hız limitlerini (0.8-1.5 m/s) ve pis su hatlarında TS EN 12056 standartlarını gözeterek en uygun ekonomik çapı belirlemek için.
    */
    /*
       NE: Boruları Otomatik Çaplandır (AutoSizePipes)
       NEDEN: Hesaplanan pik debilere göre, temiz su hatlarında hız limitlerini (0.8-1.5 m/s) ve pis su hatlarında TS EN 12056 standartlarını gözeterek en uygun ekonomik çapı belirlemek için.
    */
    /*
       NE: Boruları Otomatik Çaplandır (AutoSizePipes)
       NEDEN: Hesaplanan pik debilere göre, temiz su hatlarında hız limitlerini (0.8-1.5 m/s) ve pis su hatlarında TS EN 12056 standartlarını gözeterek en uygun ekonomik çapı belirlemek için.
    */
    /*
       NE: Geometrik Eğimi Hesapla (CalculateGeometricSlope)
       NEDEN: Borunun iki ucu arasındaki kot farkını yatay mesafeye bölerek hidrolik akış kapasitesini saptamak için.
    */
    private double CalculateGeometricSlope(PipeEntity pipe)
    {
        double hDist = System.Math.Sqrt(System.Math.Pow(pipe.EndPoint.X - pipe.StartPoint.X, 2) + System.Math.Pow(pipe.EndPoint.Y - pipe.StartPoint.Y, 2));
        if (hDist < 1.0) return 0; // Dikey kolon
        return System.Math.Abs(pipe.EndPoint.Z - pipe.StartPoint.Z) / hDist;
    }

    public void AutoSizePipes(IEnumerable<MechanicalEntity> entities)
    {
        var pipes = entities.OfType<PipeEntity>().ToList();
        foreach (var pipe in pipes)
        {
            // MÜHENDİSLİK ZEKASI (Kemal): Eğer kullanıcı çapı manuel kilitlemişse, 
            // otomatik çaplandırma motoru bu boruya dokunmamalıdır.
            if (pipe.IsSizeLocked) 
            {
                // Ama hızı her halükarda güncellemeliyiz ki kullanıcı manuel çapın hızını görsün
                pipe.Velocity = pipe.GetVelocity();
                continue;
            }

            // 0. Otomatik Malzeme Ataması (Eğer generic ise)
            if (pipe.PipeMaterialType == PipeMaterial.Generic)
            {
                pipe.PipeMaterialType = pipe.SystemType == MechanicalSystemType.WasteWater 
                    ? PipeMaterial.PVC_SN4 
                    : PipeMaterial.PPRC_PN20;
            }

            if (pipe.TotalFixtureUnits <= 0 && pipe.FlowRate <= 0) continue;

            if (pipe.SystemType == MechanicalSystemType.WasteWater)
            {
                // TS EN 12056-2: minimum iç çap ihtiyacı (yük birimlerine göre)
                double requiredMinID = GetMinDiameterForWasteWater(pipe.TotalFixtureUnits, pipe.IsCarryingWCLoad);
                double standardDN = PipeSizer.GetStandardSize(requiredMinID, pipe.PipeMaterialType);
                pipe.InnerDiameter = PipeCatalog.GetInnerDiameter(pipe.PipeMaterialType, standardDN);

                // Eğim kontrolü: TS EN 12056-2 min %2 (0.02)
                pipe.Slope = CalculateGeometricSlope(pipe);
                if (pipe.Slope < 0.02 && !IsRiser(pipe))
                    pipe.HasHydraulicViolation = true;
            }
            else if (pipe.SystemType == MechanicalSystemType.RainWater)
            {
                // TS EN 12056-3: debi bazlı minimum DN
                double qLs = pipe.FlowRate > 0
                    ? pipe.FlowRate * 1000.0 / 3600.0  // m³/h → l/s
                    : pipe.TotalFixtureUnits * 0.3;     // yaklaşık: 1 DU ≈ 0.3 l/s yağmur için

                double rainID = GetMinDiameterForRainwater(qLs);
                double rainDN = PipeSizer.GetStandardSize(rainID, PipeMaterial.PVC_SN4);
                pipe.InnerDiameter = PipeCatalog.GetInnerDiameter(PipeMaterial.PVC_SN4, rainDN);

                // Eğim kontrolü: yağmur suyu yatay boruları da min %2
                pipe.Slope = CalculateGeometricSlope(pipe);
                if (pipe.Slope < 0.02 && !IsRiser(pipe))
                    pipe.HasHydraulicViolation = true;
            }
            else // Temiz Su (Basınçlı)
            {
                // 1. Debi ve Hız Kriterine Göre Gereken Teorik İç Çap
                double requiredID = PipeSizer.CalculateRequiredInnerDiameter(pipe.FlowRate * 1000.0 / 3600.0, 1.5); // l/s
                
                // 2. Katalogdan Seç
                double standardDN = PipeSizer.GetStandardSize(requiredID, pipe.PipeMaterialType);
                pipe.InnerDiameter = PipeCatalog.GetInnerDiameter(pipe.PipeMaterialType, standardDN);
            }
            
            // Hız Validasyonu
            pipe.Velocity = pipe.GetVelocity();
            if ((pipe.Velocity > 2.5 && !IsRiser(pipe)) || (pipe.Velocity < 0.1 && pipe.FlowRate > 0))
            {
                 // Hız kriterleri biraz daha esnek olabilir (2.5 m/s max)
                 pipe.HasHydraulicViolation = true;
            }
        }
    }

    /*
       NE: Pis Su Minimum Çap İhtiyacı (GetMinDiameterForWasteWater)
       NEDEN: TS EN 12056 standartlarına göre yük birimine (DU) karşılık gelen minimum *iç çap* gereksinimini belirlemek için.
    */
    private double GetMinDiameterForWasteWater(double fu, bool carriesWC)
    {
        // Standartlar genelde DN (Dış Çap) konuşur ama biz hidrolik iç çap ihtiyacını döndürelim,
        // PipeSizer bunu kataloğa bakarak DN'e çevirsin.
        // PVC için ortalama et kalınlığı düşülerek yaklaşık ID değerleri:
        
        double minID = carriesWC ? 96.0 : 36.0; // DN100 (~96mm ID) veya DN40 (~36mm ID)
        
        if (fu <= 0.5 && !carriesWC) return System.Math.Max(minID, 36.0);  // DN40
        if (fu <= 0.8 && !carriesWC) return System.Math.Max(minID, 44.0);  // DN50
        if (fu <= 1.5 && !carriesWC) return System.Math.Max(minID, 64.0);  // DN70/75
        if (fu <= 2.5 && !carriesWC) return System.Math.Max(minID, 80.0);  // DN90 (veya DN75 sınırda)
        if (fu <= 5.0) return System.Math.Max(minID, 96.0);                // DN100/110
        if (fu <= 12.0) return System.Math.Max(minID, 115.0);              // DN125
        return 150.0;
    }

    /*
       NE: Yağmur Suyu Minimum Çap (GetMinDiameterForRainwater)
       NEDEN: TS EN 12056-3 Tablo 3 — debi bazlı minimum iç çap (mm) seçimi.
    */
    private static double GetMinDiameterForRainwater(double qLs) => qLs switch
    {
        <= 0.5 => 44.0,  // DN50 iç çap ~44mm
        <= 1.0 => 64.0,  // DN75 iç çap ~64mm
        <= 2.0 => 80.0,  // DN90 iç çap ~80mm
        <= 4.0 => 96.0,  // DN110 iç çap ~96mm
        <= 8.0 => 115.0, // DN125 iç çap ~115mm
        _      => 150.0  // DN160 iç çap ~150mm
    };

    // CalculateOptimalDiameter ve GetStandardDiameterForWasteWater (Eski) kaldırıldı.
}
