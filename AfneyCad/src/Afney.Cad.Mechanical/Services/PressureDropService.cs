using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Engine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Basınç Kaybı Hesap Servisi (PressureDropService)
    NEDEN: Tesisattaki kritik hat direncini bulmak ve pompa/hidrofor seçimini doğru yapmak için.
    
    NASIL (Mühendislik Modu):
    1. Darcy-Weisbach veya Colebrook-White denklemini kullanarak boru sürtünme kaybını hesaplar.
    2. Yerel kayıpları (Dirsekler, vanalar, te'ler) "K" katsayıları veya "Eşdeğer Boru Boyu" yöntemiyle ekler.
    3. En uzun veya en dirençli hattı (Kritik Hat) tespit eder.
*/
public class PressureDropService
{
    private readonly CadDatabase _database;
    private readonly MechanicalProjectSettings _settings;
    private readonly MechanicalTopologyGraph _graph;
    private const double KinematicViscosity = 1.004e-6; // Su (20°C) m²/s
    private const double Gravity = 9.81;

    /*
       NE: PressureDropService Yapıcı Metodu
       NEDEN: Topoloji, veritabanı ve ayarları alarak tesisattaki direnç hesaplarını (ΔP) yapmaya hazır hale gelir.
    */
    public PressureDropService(MechanicalTopologyGraph graph, MechanicalProjectSettings settings, CadDatabase? database = null)
    {
        _database = database!;
        _graph = graph;
        _settings = settings;
    }

    /*
        NE: Tekil Boru Basınç Kaybı (Darcy-Weisbach)
        NEDEN: Boru sürtünme direncini fiziksel parametrelerle hesaplamak için.
    */
    public double CalculatePipePressureDrop(PipeEntity pipe)
    {
        if (pipe.InnerDiameter <= 0 || pipe.FlowRate <= 0) return 0;

        double v = pipe.GetVelocity();
        double dMe = pipe.InnerDiameter / 1000.0;
        double lengthMetre = pipe.GetLength() / 1000.0;

        // 1. Reynolds Sayısı
        double Re = (v * dMe) / KinematicViscosity;
        if (Re < 2300) // Laminer Akış
        {
            double f_lam = 64.0 / Re;
            double h_lam = f_lam * (lengthMetre / dMe) * (Math.Pow(v, 2) / (2 * Gravity));
            return h_lam * (1.0 + _settings.LocalLossAllowance);
        }

        // 2. Sürtünme Katsayısı (f) - Colebrook-White (Swamee-Jain)
        double eps = _settings.PipeRoughness / 1000.0; // mm -> m
        double f = 0.25 / Math.Pow(Math.Log10((eps / (3.7 * dMe)) + (5.74 / Math.Pow(Re, 0.9))), 2);

        // 3. Darcy-Weisbach: hf = f * (L/D) * (v²/2g)
        double linearLoss = f * (lengthMetre / dMe) * (Math.Pow(v, 2) / (2 * Gravity));
        
        // 4. Yerel Kayıplar (Zeta payı dahil - FINE SANI Standardı)
        double totalLoss = linearLoss * (1.0 + _settings.LocalLossAllowance);
        
        return totalLoss; // Birim: Metre Su Sütunu (mSS)
    }

    /*
        NE: Kritik Hat Analizi
        NEDEN: Sistemin en dezavantajlı fixtures'ına giden en dirençli yolu bulmak için.
    */
    /*
       NE: Kritik Hattı Bul (FindCriticalPath)
       NEDEN: Sistemin en dezavantajlı (en dirençli/en uçta) noktasına giden yolu, kümülatif basınç kayıplarını analiz ederek saptamak ve pompa basıncı hesabına baz oluşturmak için.
    */
    public List<PipeEntity> FindCriticalPath(Guid sinkId)
    {
        var mechEntities = _database.GetAllEntities().OfType<MechanicalEntity>().ToDictionary(e => e.Id);
        
        // Dikstra/BFS varyasyonu ile her düğüme giden maksimum direnci hesapla
        var distances = new Dictionary<Guid, double>();
        var previous = new Dictionary<Guid, Guid>();
        var queue = new PriorityQueue<Guid, double>(); // Negatif kullanarak "Maksimum" bulacağız

        distances[sinkId] = 0;
        queue.Enqueue(sinkId, 0);

        Guid mostDisadvantagedId = sinkId;
        double maxLoss = 0;

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            double currentDist = distances[currentId];

            var neighbors = _graph.GetNeighbors(currentId);
            foreach (var neighbor in neighbors)
            {
                if (mechEntities.TryGetValue(neighbor.OwnerId, out var entity) && entity is PipeEntity pipe)
                {
                    double loss = CalculatePipePressureDrop(pipe);
                    double totalLoss = currentDist + loss;

                    if (!distances.ContainsKey(neighbor.OwnerId) || totalLoss > distances[neighbor.OwnerId])
                    {
                        distances[neighbor.OwnerId] = totalLoss;
                        previous[neighbor.OwnerId] = currentId;
                        queue.Enqueue(neighbor.OwnerId, -totalLoss); // Maksimum path için negatif öncelik

                        if (totalLoss > maxLoss)
                        {
                            maxLoss = totalLoss;
                            mostDisadvantagedId = neighbor.OwnerId;
                        }
                    }
                }
            }
        }

        // Yolu geri topla
        var path = new List<PipeEntity>();
        var traceId = mostDisadvantagedId;
        while (previous.ContainsKey(traceId))
        {
            if (mechEntities[traceId] is PipeEntity p) path.Add(p);
            traceId = previous[traceId];
        }
        path.Reverse();
        return path;
    }

    /*
        NE: Detaylı Mühendislik Raporu Üret
        NEDEN: Kemal Bey ve Mebrure Hanım'ın proje onay dosyalarında kullanabileceği profesyonel çıktı üretmek için.
    */
    /*
       NE: Kritik Hat Raporu Üret (GenerateReport)
       NEDEN: Tesisat projesinde tespit edilen kritik hattı, sürtünme kayıplarını, statik yükseklik farklarını ve gerekli uç basıncını adım adım listeleyerek teknik onay föyü oluşturmak için.
    */
    public CriticalPathReport GenerateReport(Guid sinkId)
    {
        var path = FindCriticalPath(sinkId);
        var report = new CriticalPathReport
        {
            SystemType = path.FirstOrDefault()?.SystemType.ToString() ?? "Unknown",
            RequiredResidualPressure = _settings.RequiredResidualPressure
        };

        double cumulative = 0;
        double totalStatic = 0;

        foreach (var pipe in path)
        {
            double loss = CalculatePipePressureDrop(pipe);
            cumulative += loss;

            // Statik yükseklik farkı (mm -> m)
            // Akış yönü burada önemli ama basitçe dikey farkı ekleyelim (Yukarı çıkış dirençtir)
            double dz = (pipe.EndPoint.Z - pipe.StartPoint.Z) / 1000.0;
            if (pipe.FlowDirection == -1) dz = -dz; // Ters yönde akıyorsa
            
            // Sadece yükselmeler (pozitif dz) statik kayıp oluşturur (Basitleştirilmiş)
            // Aslında net fark önemlidir ama sürtünme ile birleşince path analizi kritik.
            totalStatic += dz;

            report.Segments.Add(new CriticalPathSegment
            {
                PipeId = pipe.Id.ToString().Substring(0, 8),
                Diameter = pipe.InnerDiameter,
                FlowRate = pipe.FlowRate,
                Length = pipe.GetLength() / 1000.0,
                Velocity = pipe.Velocity,
                PressureDrop = loss,
                StaticHead = dz,
                CumulativeLoss = cumulative + totalStatic
            });

            if (pipe.Velocity > report.MaxVelocity) report.MaxVelocity = pipe.Velocity;
        }

        report.TotalLinearLoss = cumulative;
        report.StaticHead = totalStatic;
        report.TotalPressureRequired = report.TotalLinearLoss + report.StaticHead + report.RequiredResidualPressure;
        
        if (path.Any()) report.DisadvantagedFixture = "Terminal " + report.Segments.Last().PipeId;

        return report;
    }

    /*
        NE: Tüm Sistemin Basınç Kaybını Güncelle
        NEDEN: Projedeki tüm boruların sürtünme değerlerini tek seferde hesaplayıp nesne üzerine yazmak için.
    */
    /*
       NE: Basınç Kayıplarını Hesapla (CalculatePressureDrops)
       NEDEN: Verilen nesne koleksiyonundaki tüm boruların sürtünme kaybı (hf) değerlerini Darcy-Weisbach formülüyle tek tıkla hesaplamak için.
    */
    public void CalculatePressureDrops(IEnumerable<MechanicalEntity> entities)
    {
        foreach (var pipe in entities.OfType<PipeEntity>())
        {
            pipe.PressureDrop = CalculatePipePressureDrop(pipe);
        }
    }
}
