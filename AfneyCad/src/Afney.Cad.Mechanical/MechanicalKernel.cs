using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Rules;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Serilog;
using Afney.Cad.Mechanical.Standards;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Mechanical;

/*
   NE: Mekanik Çekirdek (MechanicalKernel)
   NEDEN: Projenin "Mühendislik Zekası"dır. Topoloji, kurallar, bağlantılar ve standartlar buradan yönetilir.
*/
public class MechanicalKernel
{
    public MechanicalTopologyGraph TopologyGraph { get; private set; } = null!;
    public PipeConnectionEngine ConnectionEngine { get; private set; } = null!;
    public Afney.Cad.Mechanical.Engine.Constraints.ConstraintSolver ConstraintSolver { get; private set; } = null!;
    
    private CadDatabase? _database;

    /*
       NE: Veritabanını Tanıt (SetDatabase)
       NEDEN: Mekanik çekirdeğin, çizim verilerine (Entities) erişebilmesi ve izometrik senkronizasyonu başlatabilmesi için.
    */
    public void SetDatabase(CadDatabase db)
    {
        _database = db;
        // Suggestion 18: İzometrik motoru veritabanına bağla
        IsoSync = new IsoSyncService(this, _database);
    }
    
    // NE: Proje Bilgileri (Step 1)
    public ProjectMetadata Metadata { get; set; }

    // NE: Sistem Konfigürasyonları (Step 3)
    public Dictionary<MechanicalSystemType, MechanicalSystemConfig> SystemConfigs { get; } = new();

    // NE: Kat Yönetim Servisi (FINE SANI Benzeri - Step 2)
    public LevelManager LevelManager { get; private set; }

    // NE: Proje Ayarları
    public MechanicalProjectSettings ProjectSettings { get; private set; }

    // NE: Mühendislik Kuralları Listesi
    // NEDEN: Nesnelerin standartlara ve mühendislik prensiplerine uygunluğunu kontrol eden kuralları tutmak için.
    public List<IEngineeringRule> Rules { get; } = new();
    public StandardsLibrary PipeStandards { get; }

    // NE: Otomatik Fittings Seçici
    // NEDEN: Boru bağlantılarında (Köşe, T) doğru fittings parçasını seçmek için.
    public AutoFittingSelector FittingSelector { get; }

    // NE: Mimari Engeller (Architectural Obstacles)
    // NEDEN: Boru rotalama ve cihaz yerleşimi sırasında duvar, kapı, kolon gibi unsurları dikkate almak için.
    public List<ArchitecturalObstacle> ArchitecturalObstacles { get; } = new();

    // NE: BIM Bina Modeli (Suggestion 17/18/19)
    public BuildingModel ProjectModel { get; private set; }

    // NE: Rotalama Servisi
    public PipingPathfinderService Pathfinder { get; private set; }

    // NE: Canlı İzometrik Senkronizasyon (Suggestion 18)
    public IsoSyncService IsoSync { get; private set; }

    public MechanicalKernel()
    {
        Metadata = new ProjectMetadata();
        TopologyGraph = new MechanicalTopologyGraph();
        ConnectionEngine = new PipeConnectionEngine();
        PipeStandards = new StandardsLibrary();
        
        // Varsayılan Sistemleri Kur (Step 3)
        SystemConfigs[MechanicalSystemType.DomesticColdWater] = new MechanicalSystemConfig(MechanicalSystemType.DomesticColdWater);
        SystemConfigs[MechanicalSystemType.DomesticHotWater] = new MechanicalSystemConfig(MechanicalSystemType.DomesticHotWater);
        SystemConfigs[MechanicalSystemType.WasteWater] = new MechanicalSystemConfig(MechanicalSystemType.WasteWater);

        FittingSelector = new AutoFittingSelector(PipeStandards);
        LevelManager = new LevelManager();
        ProjectSettings = MechanicalProjectSettings.CreateDefault();
        ProjectModel = new BuildingModel();
        Pathfinder = new PipingPathfinderService(ArchitecturalObstacles);
        IsoSync = new IsoSyncService(this, null!); // SetDatabase ile güncellenecek

        RegisterDefaultRules();

        // Constraint Solver - Kuralları kullanarak kısıtları çözer
        ConstraintSolver = new Engine.Constraints.ConstraintSolver(Rules);
    }

    /*
       NE: Varsayılan Kuralları Kaydet (RegisterDefaultRules)
       NEDEN: Standartlara (TS 1258, DIN 1988 vb.) uygunluk kontrolü yapacak olan kural motorunun temel kontrollerini yüklemek için.
    */
    private void RegisterDefaultRules()
    {
        Rules.Add(new Rules.PipeDiameterRule());
        Rules.Add(new Rules.Plumbing.FixtureLoadCapacityRule());
        Rules.Add(new Rules.Plumbing.WCDiameterRule());
    }

    // NE: Kolon Şeması Verisi Üret
    // NEDEN: 3D modeldeki tüm dikey hatları analiz edip 2D şema için gerekli topolojik veriyi hazırlamak için.
    /*
       NE: Kolon Şeması Verisi Üret (GetRiserSchemas)
       NEDEN: 3D modeldeki tüm dikey hatları analiz edip 2D şema için gerekli topolojik veriyi hazırlamak için.
    */
    public List<RiserSchema> GetRiserSchemas(IEnumerable<MechanicalEntity> entities)
    {
        var riserEngine = new RiserEngine();
        return riserEngine.GenerateSchemas(entities, LevelManager.GetLevels().ToList(), TopologyGraph);
    }

    // NE: Mühendislik Validasyonu
    // NEDEN: Nesnenin standartlara uygunluğunu (Örn: Max hız aşımı) kontrol etmek için.
    /*
       NE: Mühendislik Validasyonu (ValidateEntity)
       NEDEN: Nesnenin standartlara (Hız, Debi, Çakışma vb.) uygunluğunu kontrol etmek ve kullanıcıya hataları raporlamak için.
    */
    public bool ValidateEntity(MechanicalEntity entity, out string validationErrors)
    {
        validationErrors = string.Empty;
        bool allValid = true;

        foreach (var rule in Rules)
        {
            var result = rule.Check(entity);
            if (!result.IsValid)
            {
                validationErrors += result.Message + Environment.NewLine;
                allValid = false;
            }
        }

        return allValid;
    }

    // NE: Veritabanı Olay Dinleyicisi (Ekleme)
    // NEDEN: Nesne çizildiğinde onu mantıksal ağa bağlamak için.
    /*
       NE: Veritabanı Ekleme Dinleyicisi (OnEntityAddedToDatabase)
       NEDEN: Yeni bir nesne çizildiğinde onu otomatik olarak topoloji grafına dahil etmek ve akıllı bağlantıları (AutoConnect) kurmak için.
    */
    public void OnEntityAddedToDatabase(Domain.Abstractions.CadEntity entity)
    {
        if (entity is MechanicalEntity mechEntity)
        {
            // 1. Grafiğe düğüm olarak ekle
            TopologyGraph.AddEntity(mechEntity);

            // 2. OTOMATİK BAĞLANTI (MÜHENDİSLİK ZEKASI)
            // Yakındaki diğer mekanik nesne portlarını tara ve bağla
            AutoConnectPorts(mechEntity);

            // 3. Canlı Hesaplama
            TriggerHydraulicUpdate();
        }
    }

    /*
       NE: Veritabanı Silme Dinleyicisi (OnEntityRemovedFromDatabase)
       NEDEN: Bir nesne çizimden silindiğinde, onu topoloji grafından (Network) temizlemek ve akış hesaplarını (Debi/Pressure) güncel durumuna çekmek için.
    */
    public void OnEntityRemovedFromDatabase(Domain.Abstractions.CadEntity entity)
    {
        if (entity is MechanicalEntity mechEntity)
        {
            TopologyGraph.RemoveEntity(mechEntity.Id);
            TriggerHydraulicUpdate();
        }
    }

    /*
    NE: Veritabanı Olay Dinleyicisi (Güncelleme) - YENİ
    NEDEN: Nesne taşındığında (Move) veya değiştiğinde topoloji grafını güncellemek için.
    
    NASIL:
    1. GraphNode'daki portları güncelle (yeni koordinatlar)
    2. Eski bağlantıları koru
    3. Yeni konumda otomatik bağlantı dene
    
    MÜHENDİSLİK NOTU (Kemal):
    Bu metod sayesinde kullanıcı boruyu taşıdığında sistem "canlı" kalıyor.
    Akış hesaplamaları ve validasyonlar otomatik tetikleniyor.
    */
    /*
       NE: Veritabanı Güncelleme Dinleyicisi (OnEntityUpdatedInDatabase)
       NEDEN: Bir nesnenin geometrisi değiştiğinde (Move/Stretch), bağlı olduğu topolojik ağı, akış hesaplarını ve akıllı etiketleri canlı olarak güncellemek için.
    */
    public void OnEntityUpdatedInDatabase(Domain.Abstractions.CadEntity entity)
    {
        if (entity is MechanicalEntity mechEntity)
        {
            // Topoloji grafındaki node'u bul ve portları güncelle
            var node = TopologyGraph.Nodes.FirstOrDefault(n => n.EntityId == mechEntity.Id);
            if (node != null)
            {
                node.UpdatePorts(mechEntity);
            }
            
            // Canlı Hesaplama Tetikle
            TriggerHydraulicUpdate();

            // --- AKILLI ETİKET SENKRONİZASYONU (YENİ) ---
            if (mechEntity is PipeEntity pipe)
            {
                SyncPipeLabels(pipe);
            }
        }

        // Phase 3: Mimari Senkronizasyon (Associative Geometry)
        // NEDEN: Duvar taşındığında üzerindeki vitrifiyeleri de taşı.
        var obstacle = ArchitecturalObstacles.FirstOrDefault(o => o.SourceEntityId == entity.Id);
        if (obstacle != null)
        {
            // 1. Engelin sınırlarını güncelle
            UpdateObstacleBoundary(obstacle, entity);

            // 2. Bu engele bağlı tüm cihazları bul ve güncelle
            var attachedFixtures = TopologyGraph.Nodes
                .Select(n => n.Entity)
                .OfType<SanitaryFixtureEntity>()
                .Where(f => f.AttachedObstacleId == obstacle.Id)
                .ToList();

            if (attachedFixtures.Any() && obstacle.Boundary.Count >= 2)
            {
                var pStart = obstacle.Boundary[0];
                var pEnd = obstacle.Boundary[1];
                var wallDir = (pEnd - pStart).Normalize();
                var normal = new Vector3D(-wallDir.Y, wallDir.X, 0);

                foreach (var fixture in attachedFixtures)
                {
                    // Yeni pozisyon = Start + (Dir * Offset) + (Normal * Distance)
                    var oldPos = fixture.Position;
                    fixture.Position = pStart + (wallDir * fixture.WallOffset);
                    fixture.Position += normal * fixture.WallDistance; 
                    
                    // Rotasyon güncelleme
                    fixture.Rotation = Math.Atan2(normal.Y, normal.X);

                    // --- Phase 1: Dinamik Boru Senkronizasyonu (Stretching) ---
                    // Cihaz taşındığında bağlı boruları "esnet"
                    SyncConnectedPipes(fixture);
                }
            }
        }
    }

    /*
       NE: Bağlı Boru Senkronizasyonu (SyncConnectedPipes)
       NEDEN: Bir cihaz (vites) taşındığında, ona bağlı olan boru uçlarını da sürükleyerek tesisat bütünlüğünü (Stretching) korumak için.
    */
    private void SyncConnectedPipes(SanitaryFixtureEntity fixture)
    {
        var node = TopologyGraph.GetNode(fixture.Id);
        if (node == null) return;


        // Cihazın GÜNCEL port yerleşimlerini al
        var updatedPorts = fixture.GetPorts();

        foreach (var port in node.Ports)
        {
            if (port.IsConnected && port.ConnectedEntityId.HasValue)
            {
                var connectedEntity = TopologyGraph.GetNode(port.ConnectedEntityId.Value)?.Entity;
                if (connectedEntity is PipeEntity pipe)
                {
                    // Bu portun yeni koordinatını bul
                    var updatedPort = updatedPorts.FirstOrDefault(p => p.Name == port.Name);
                    if (updatedPort == null) continue;

                    // Borunun hangi ucu bu porta bağlı? (Start mı End mi?)
                    if (port.ConnectedPortName == "Start")
                    {
                        pipe.StartPoint = updatedPort.Position;
                    }
                    else if (port.ConnectedPortName == "End")
                    {
                        pipe.EndPoint = updatedPort.Position;
                    }
                }
            }
        }
    }

    private void UpdateObstacleBoundary(ArchitecturalObstacle obs, Domain.Abstractions.CadEntity entity)
    {
        obs.Boundary.Clear();
        if (entity is Domain.Entities.Basic.LineEntity line)
        {
            obs.Boundary.Add(line.StartPoint);
            obs.Boundary.Add(line.EndPoint);
        }
        else if (entity is Domain.Entities.Basic.LwPolylineEntity poly)
        {
            obs.Boundary.AddRange(poly.Vertices);
        }
        // Diğer tipler eklenebilir.
    }

    /*
       NE: Otomatik Port Bağlantı Mantığı (AutoConnectPorts)
       NEDEN: Yeni eklenen nesnenin portlarını, mevcut nesnelerin portlarıyla mesafe bazlı karşılaştırıp sisteme dahil etmek ve gerekirse boru bölme işlemi yapmak için.
    */
    private void AutoConnectPorts(MechanicalEntity newEntity)
    {
        var newPorts = newEntity.GetPorts();
        if (!newPorts.Any()) return;

        const double threshold = 2.0; // 2mm yakalama toleransı

        // Tüm mevcut topoloji düğümlerini tara
        var existingNodes = TopologyGraph.Nodes.ToList();
        foreach (var existingNode in existingNodes)
        {
            if (existingNode.EntityId == newEntity.Id) continue;

            // 1. Durum: Port-to-Port bağlantısı (Standart)
            foreach (var existingPort in existingNode.Ports)
            {
                foreach (var newPort in newPorts)
                {
                    double dist = newPort.Position.DistanceTo(existingPort.Position);
                    if (dist < threshold)
                    {
                        if (newEntity.SystemType == existingNode.SystemType ||
                            newEntity.SystemType == MechanicalSystemType.Undefined ||
                            existingNode.SystemType == MechanicalSystemType.Undefined)
                        {
                            TopologyGraph.Connect(newPort, existingPort);
                        }
                    }
                }
            }

            // 2. Durum: Port-to-Body bağlantısı (MÜHENDİSLİK ZEKASI - Mete & Kemal)
            // Eğer yeni nesnenin ucu, mevcut bir borunun ORTASINA değiyorsa boruyu ikiye böl.
            if (existingNode.Entity is PipeEntity existingPipe)
            {
                foreach (var newPort in newPorts)
                {
                    // Noktanın çizgi üzerindeki izdüşümünü ve mesafesini bul
                    var p = newPort.Position;
                    var p1 = existingPipe.StartPoint;
                    var p2 = existingPipe.EndPoint;
                    
                    double lineLenSq = (p2 - p1).LengthSquared();
                    if (lineLenSq < 1e-6) continue;
                    
                    double t = System.Math.Max(0, System.Math.Min(1, (p - p1).Dot(p2 - p1) / lineLenSq));
                    var projection = p1 + (p2 - p1) * t;
                    double dist = p.DistanceTo(projection);

                    // Eğer borunun uçlarına çok yakınsa (threshold) zaten Case 1 bunu yönetiyor.
                    // Eğer uca uzaksa ama gövdeye yakınsa (t > 0.01 && t < 0.99)
                    if (dist < threshold && t > 0.01 && t < 0.99)
                    {
                        SplitPipeAndConnect(existingPipe, newPort, projection);
                    }
                }
            }
        }
    }

    /*
       NE: Boruyu Böl ve Bağla (SplitPipeAndConnect)
       NEDEN: Port-to-Body bağlantılarında boruyu fiziksel olarak ikiye parçalayıp araya T-parçası ekleyerek tesisat ağını genişletmek için.
    */
    private void SplitPipeAndConnect(PipeEntity targetPipe, MechanicalPort sourcePort, Vector3D splitPoint)
    {
        Serilog.Log.Information(">>> AKILLI TOPOLOJİ: Boru kesişimi algılandı. Bölünüyor...");

        // 1. Mevcut borunun bitişini split point yap
        var oldEnd = targetPipe.EndPoint;
        targetPipe.EndPoint = splitPoint;

        // 2. Yeni bir boru oluştur (splitPoint'den oldEnd'e)
        var newPipeSegment = (PipeEntity)targetPipe.Clone();
        newPipeSegment.StartPoint = splitPoint;
        newPipeSegment.EndPoint = oldEnd;

        // 3. MÜHENDİSLİK DETAYI (Mete & Mebrure): Araya T-Parçası (Tee) Ekle
        // Sadece görsel değil, metrajda (BOM) çıksın diye fiziksel nesne ekliyoruz.
        var mainDir = (oldEnd - targetPipe.StartPoint).Normalize();
        var branchDir = (sourcePort.Position - splitPoint).Normalize(); // Branch yönü (Bağlanan nesneden T'ye)
        
        // Cihazdan boruya geliyorsa yönü ters çeviriyoruz ki T düzgün dursun
        var tee = new TeeEntity(splitPoint, targetPipe.InnerDiameter, targetPipe.InnerDiameter, mainDir, branchDir * -1)
        {
            Color = targetPipe.Color,
            SystemType = targetPipe.SystemType,
            PipeMaterialType = targetPipe.PipeMaterialType
        };

        // 4. Veritabanına ekle (MainWindow event'i üzerinden)
        OnRequestAddEntity?.Invoke(newPipeSegment);
        OnRequestAddEntity?.Invoke(tee);
    }

    // NE: Projeyi Baştan Hesapla ve Çaplandır
    // NEDEN: Kullanıcı çizimi bitirdiğinde veya bir değişiklik yaptığında tüm projenin TS 1258 standartlarına uygunluğunu tek tıkla doğrulamak için.
    public void RecalculateProject(IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> entities)
    {
        Serilog.Log.Information(">>> HİDROLİK SİSTEM ANALİZİ: Başlatıldı (TS 1258 Standards)...");
        
        var mechanicalEntities = entities.OfType<MechanicalEntity>().ToList();
        var allEntities = entities.ToList();

        // 1. Akış Yüklerini (FU) Topla ve Debileri (Q) Hesapla
        var flowService = new FlowCalculationService(TopologyGraph);
        flowService.CalculateSystemFlow(mechanicalEntities);
        
        // 2. Otomatik Çaplandırma (Sizing)
        flowService.AutoSizePipes(mechanicalEntities);
        
        // 3. Basınç Kaybı ve Kritik Hat Hesabı
        var pressureService = new PressureDropService(TopologyGraph, ProjectSettings);
        pressureService.CalculatePressureDrops(mechanicalEntities);

        // 4. MÜHENDİSLİK VALIDASYONU: Çakışma Analizi (YENİ)
        var clashService = new ClashDetectionService(ArchitecturalObstacles);
        var clashes = clashService.DetectClashes(mechanicalEntities);
        if (clashes.Any())
        {
            Serilog.Log.Warning(">>> ANALİZ UYARISI: {Count} adet mimari çakışma tespit edildi!", clashes.Count);
        }
        
        // 5. ETİKET SENKRONİZASYONU (Associative Labels)
        foreach (var pipe in mechanicalEntities.OfType<PipeEntity>())
        {
            SyncPipeLabels(pipe);
        }
        
        Serilog.Log.Information(">>> HİDROLİK SİSTEM ANALİZİ: Tamamlandı.");
    }

    public event Action<Domain.Abstractions.CadEntity>? OnRequestAddEntity;
    public event Action<Domain.Abstractions.CadEntity>? OnRequestDeleteEntity;

    /*
       NE: Tüm Çakışmaları Otomatik Çöz (ResolveAllClashes)
       NEDEN: Projedeki tüm boru çakışmalarını tarayıp, her biri için standartlara uygun kavisleri (atlamaları) tek tıkla oluşturmak için.
    */
    public void ResolveAllClashes(IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> entities)
    {
        var mechanicalEntities = entities.OfType<MechanicalEntity>().ToList();
        var clashService = new ClashDetectionService(ArchitecturalObstacles);
        var clashes = clashService.DetectClashes(mechanicalEntities);

        foreach (var clash in clashes)
        {
            if (clash.Type == ClashType.MechanicalVsMechanical)
            {
                var newPieces = clashService.ResolveClash(clash, mechanicalEntities);
                if (newPieces.Any())
                {
                    // 1. Yeni Kavisli Parçaları Ekle
                    foreach (var piece in newPieces)
                        OnRequestAddEntity?.Invoke(piece);

                    // 2. Orijinal Çakışan Boruyu Sil
                    var originalA = mechanicalEntities.FirstOrDefault(e => e.Id == clash.EntityA_Id);
                    if (originalA != null)
                        OnRequestDeleteEntity?.Invoke(originalA);
                }
            }
        }
    }

    /*
       NE: Hidrolik Güncelleme Tetikleyicisi (TriggerHydraulicUpdate)
       NEDEN: Herhangi bir nesne değişikliğinde (Ekleme/Silme/Taşıma) tüm tesisatın akış, debi ve çap değerlerini TS standartlarına göre (arka planda) yeniden hesaplamak için.
    */
    private void TriggerHydraulicUpdate()
    {
        // NOT: Büyük projelerde bu işlem bir 'Task' veya throttling ile yapılmalıdır.
        // Şimdilik her değişimde tüm topolojiyi analiz ediyoruz.
        var allMechanicalEntities = TopologyGraph.Nodes.Select(n => n.Entity).ToList();
        
        var flowService = new FlowCalculationService(TopologyGraph)
        {
            FrequencyFactor = ProjectSettings.FrequencyFactor // Step 3: Ayarları uygula
        };
        
        // 1. Akışları hesapla (Yükleme birimi toplama)
        flowService.CalculateSystemFlow(allMechanicalEntities);
        
        // 2. Çapları kontrol et ve gerekirse otomatik büyüt (Auto-Sizing)
        flowService.AutoSizePipes(allMechanicalEntities);
    }

    private void SyncPipeLabels(PipeEntity pipe)
    {
        if (_database == null) return;

        // Bu boruya ait etiketleri bul
        var labels = _database.GetAllEntities()
            .OfType<PipeLabelEntity>();

        foreach (var label in labels)
        {
            // Reflection veya bir ID check ile boruya bağlı mı bak (Şimdilik tüm etiketleri güncellemek pahalı ama güvenli)
            // Daha iyisi etiketin içindeki ID'yi kontrol etmek
            label.SyncWithPipe(pipe); // Bu metod etiketin pozisyonunu boru ortasına çeker
        }
    }
}



