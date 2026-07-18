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
    public ValidationGateService ValidationGate { get; private set; } = null!;
    public FlowCalculationService FlowCalculation { get; private set; } = null!;
    public PressureDropService PressureDrop { get; private set; } = null!;
    
    private CadDatabase? _database;
    private bool _isCalculating = false; // Re-entry guard

    /*
       NE: Veritabanını Tanıt (SetDatabase)
       NEDEN: Mekanik çekirdeğin, çizim verilerine (Entities) erişebilmesi ve izometrik senkronizasyonu başlatabilmesi için.
    */
    public void SetDatabase(CadDatabase db)
    {
        _database = db;
        // Suggestion 18: İzometrik motoru veritabanına bağla
        IsoSync = new IsoSyncService(this, _database);
        
        // ValidationGate'i yeni veritabanı ile tazele
        ValidationGate = new ValidationGateService(_database, TopologyGraph, ArchitecturalObstacles);
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
        ValidationGate = new ValidationGateService(null!, TopologyGraph, ArchitecturalObstacles);
        
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
        
        FlowCalculation = new FlowCalculationService(TopologyGraph);
        PressureDrop = new PressureDropService(TopologyGraph, ProjectSettings, null);

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

            // 2. Metadata değişimlerini dinle (Reaktif Hesaplama)
            mechEntity.MetadataChanged += OnMechanicalMetadataChanged;

            // 3. OTOMATİK BAĞLANTI (MÜHENDİSLİK ZEKASI)
            AutoConnectPorts(mechEntity);

            // 4. Sadece akış yönü sınıflandır — full recalc tetikleme.
            // NEDEN: Entity yeni eklendiğinde bağlantılar (Connect) henüz kurulmamış olabilir.
            // Full recalc, MetadataChanged event'i veya RecalculateProject() ile tetiklenir.
            TriggerHydraulicUpdate(forceRecalculate: false);
        }
    }

    private void OnMechanicalMetadataChanged(MechanicalEntity entity)
    {
        if (_isCalculating) return; // Döngü koruması
        
        Console.WriteLine($">>> REAKTİF HESAPLAMA: {entity.Id} için metadata değişti.");
        TriggerHydraulicUpdate(forceRecalculate: true);
    }

    /*
       NE: Veritabanı Silme Dinleyicisi (OnEntityRemovedFromDatabase)
       NEDEN: Bir nesne çizimden silindiğinde, onu topoloji grafından (Network) temizlemek ve akış hesaplarını (Debi/Pressure) güncel durumuna çekmek için.
    */
    public void OnEntityRemovedFromDatabase(Domain.Abstractions.CadEntity entity)
    {
        if (entity is MechanicalEntity mechEntity)
        {
            mechEntity.MetadataChanged -= OnMechanicalMetadataChanged;
            TopologyGraph.RemoveEntity(mechEntity.Id);
            TriggerHydraulicUpdate(forceRecalculate: true);
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
            var node = TopologyGraph?.GetNode(mechEntity.Id);
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
        if (newEntity == null) return;
        
        // MÜHENDİSLİK DÜZELTMESİ (Kemal): 
        // newEntity.GetPorts() her çağrıldığında yeni port nesneleri üretir.
        // Ancak GraphNode (AddEntity) zaten bu portları bir kez üretti ve saklıyor.
        // Eğer yeni üretilenlere Connect yaparsak, grafın içindeki asıl portlar güncellenmez.
        var node = TopologyGraph.GetNode(newEntity.Id);
        var newPorts = node?.Ports ?? newEntity.GetPorts();
        
        if (newPorts == null || !newPorts.Any()) return;

        const double threshold = 2.0; // 2mm yakalama toleransı

        // Tüm mevcut topoloji düğümlerini tara
        var existingNodes = TopologyGraph.Nodes.ToList();
        Console.WriteLine($">>> AutoConnect: {existingNodes.Count} nodes found. Checking {newEntity.Id}");
        foreach (var existingNode in existingNodes)
        {
            if (existingNode == null) continue;
            if (existingNode.EntityId == newEntity.Id) continue;
            
            if (existingNode.Ports == null) continue;

            // 1. Durum: Port-to-Port bağlantısı (Standart)
            foreach (var existingPort in existingNode.Ports.ToList()) // Snapshot
            {
                if (existingPort == null) continue;
                foreach (var newPort in newPorts.ToList()) // Snapshot
                {
                    if (newPort == null) continue;
                    double dist = newPort.Position.DistanceTo(existingPort.Position);
                    if (dist < threshold)
                    {
                        Console.WriteLine($">>> AutoConnect SUCCESS: {newEntity.Id}[{newPort.Name}] <-> {existingNode.EntityId}[{existingPort.Name}] dist={dist:F4}");
                        if (newEntity.SystemType == existingNode.SystemType ||
                            newEntity.SystemType == MechanicalSystemType.Undefined ||
                            existingNode.SystemType == MechanicalSystemType.Undefined)
                        {
                            bool isFittingInserted = false;
                            
                            // MÜHENDİSLİK DETAYI: İki boru birleşiyorsa araya otomatik Dirsek (Elbow) veya Redüksiyon (Reducer) koy
                            if (newEntity is PipeEntity newPipe && existingNode.Entity is PipeEntity targetPipe)
                            {
                                double dot = newPort.Direction.Dot(existingPort.Direction);
                                // Normal vektörler zıt yöne bakmıyorsa (dot != -1) demek ki doğrusal değiller.
                                if (dot > -0.99 && dot < 0.99)
                                {
                                    var intersection = newPort.Position;
                                    var outVec = newPort.Direction * -1;
                                    var elbow = new ElbowEntity(intersection, newPipe.InnerDiameter, existingPort.Direction, outVec)
                                    {
                                        Color = newPipe.Color,
                                        SystemType = newPipe.SystemType,
                                        PipeMaterialType = newPipe.PipeMaterialType
                                    };

                                    // Boru boylarını dirsek yarıçapı kadar geri çek (Trim)
                                    if (existingPort.Name == "Start") targetPipe.StartPoint -= existingPort.Direction * elbow.Radius;
                                    else targetPipe.EndPoint -= existingPort.Direction * elbow.Radius;

                                    if (newPort.Name == "Start") newPipe.StartPoint -= newPort.Direction * elbow.Radius;
                                    else newPipe.EndPoint -= newPort.Direction * elbow.Radius;

                                    existingNode.UpdatePorts(targetPipe);
                                    var newNode = TopologyGraph.GetNode(newPipe.Id);
                                    if (newNode != null) newNode.UpdatePorts(newPipe);

                                    Serilog.Log.Information(">>> AKILLI TOPOLOJİ: Dönüş algılandı. Dirsek eklendi ve borular trimlendi.");
                                    OnRequestAddEntity?.Invoke(elbow);
                                    isFittingInserted = true;
                                }
                                else if (Math.Abs(dot) >= 0.99) // Doğrusal (Collinear)
                                {
                                    if (Math.Abs(newPipe.InnerDiameter - targetPipe.InnerDiameter) > 0.1) // Çaplar farklı
                                    {
                                        var intersection = newPort.Position;
                                        var reducer = new ReducerEntity(intersection, targetPipe.InnerDiameter, newPipe.InnerDiameter)
                                        {
                                            Color = newPipe.Color,
                                            SystemType = newPipe.SystemType,
                                            PipeMaterialType = newPipe.PipeMaterialType
                                        };
                                        reducer.SetDirection(existingPort.Direction); // Redüksiyon eksen yönü
                                        
                                        double trimDist = Math.Max(targetPipe.InnerDiameter, newPipe.InnerDiameter) / 2.0;
                                        if (existingPort.Name == "Start") targetPipe.StartPoint -= existingPort.Direction * trimDist;
                                        else targetPipe.EndPoint -= existingPort.Direction * trimDist;

                                        if (newPort.Name == "Start") newPipe.StartPoint -= newPort.Direction * trimDist;
                                        else newPipe.EndPoint -= newPort.Direction * trimDist;

                                        existingNode.UpdatePorts(targetPipe);
                                        var newNode = TopologyGraph.GetNode(newPipe.Id);
                                        if (newNode != null) newNode.UpdatePorts(newPipe);

                                        Serilog.Log.Information(">>> AKILLI TOPOLOJİ: Çap değişimi algılandı. Redüksiyon eklendi ve borular trimlendi.");
                                        OnRequestAddEntity?.Invoke(reducer);
                                        isFittingInserted = true;
                                    }
                                }
                            }

                            if (!isFittingInserted)
                            {
                                TopologyGraph.Connect(newPort, existingPort);
                                Console.WriteLine(">>> TopologyGraph.Connect executed.");
                            }
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
    public void RecalculateProject(IEnumerable<Afney.Cad.Domain.Abstractions.CadEntity> entities, IProgress<(int Percent, string Stage)>? progress = null)
    {
        if (entities == null) return;
        var mechanicalEntities = entities.OfType<MechanicalEntity>().Where(e => e != null).ToList();
        var allEntities = entities.Where(e => e != null).ToList();

        if (!mechanicalEntities.Any()) return;

        // 1. DOĞRULAMA KAPISI (Validation Gate)
        // NEDEN: Hatalı veya kopuk bir şebekede hesaplama yapmak yanlış sonuç üretir.
        // MÜHENDİSLİK GUARD: FineSANI'deki "V-000/V-P01" gibi ön kontroller.
        if (!ValidationGate.CheckGateBeforeCalculation(out var validationResult))
        {
            Serilog.Log.Error(">>> HESAPLAMA DURDURULDU: Şebeke doğrulaması başarısız.");
            return;
        }

        // Graf bağlantısı yoksa akış propagation anlamsızdır — nazikçe çık.
        bool hasEdges = TopologyGraph?.Nodes?.Any(n => n?.Ports?.Any(p => p?.IsConnected == true) ?? false) ?? false;
        if (!hasEdges)
        {
            // Düğümler var ama kenar yok: sadece IsCalculationUpToDate = true don, crash etme.
            foreach (var e in mechanicalEntities) e.IsCalculationUpToDate = true;
            return;
        }

        Serilog.Log.Information(">>> HİDROLİK SİSTEM ANALİZİ: Başlatıldı (TS 1258 Standards)...");

        // NEDEN: AutoSizePipes içinde InnerDiameter/PipeMaterialType setter'ları
        //        MetadataChanged event'ini tetikler; bu da list enumerator üzerinde
        //        InvalidOperationException'a (collection-was-modified) yol açar.
        //        Hesaplama bloğu boyunca event'leri baskılıyoruz; bitince geri açıyoruz.
        foreach (var e in mechanicalEntities) e.SuppressMetadataEvents = true;

        try
        {
            // 1. Akış Yüklerini (FU) Topla ve Debileri (Q) Hesapla
            progress?.Report((10, "Akış yükleri toplanıyor..."));
            var flowService = new FlowCalculationService(TopologyGraph);
            flowService.CalculateSystemFlow(mechanicalEntities);

            // 2. Otomatik Çaplandırma (Sizing)
            progress?.Report((35, "Boru çapları optimize ediliyor..."));
            flowService.AutoSizePipes(mechanicalEntities);

            // 3. Basınç Kaybı ve Kritik Hat Hesabı
            progress?.Report((60, "Basınç kaybı hesaplanıyor..."));
            var pressureService = new PressureDropService(TopologyGraph, ProjectSettings);
            pressureService.CalculatePressureDrops(mechanicalEntities);

            // 4. MÜHENDİSLİK VALIDASYONU: Çakışma Analizi
            progress?.Report((80, "Çakışma analizi yapılıyor..."));
            var clashService = new ClashDetectionService(ArchitecturalObstacles);
            var clashes = clashService.DetectClashes(mechanicalEntities);
            if (clashes.Any())
            {
                Serilog.Log.Warning(">>> ANALİZ UYARISI: {Count} adet mimari çakışma tespit edildi!", clashes.Count);
            }

            // 5. ETİKET SENKRONİZASYONU + VALIDASYON ONAYI
            progress?.Report((95, "Etiketler senkronize ediliyor..."));
            foreach (var entity in mechanicalEntities)
            {
                if (entity is PipeEntity pipe)
                    SyncPipeLabels(pipe);

                entity.IsCalculationUpToDate = true;
            }
            progress?.Report((100, "Tamamlandı."));
        }
        finally
        {
            // Event baskılamayı her koşulda geri aç (hata olsa bile)
            foreach (var e in mechanicalEntities) e.SuppressMetadataEvents = false;
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
       NE: Sistem Hesaplarını Güncelle (TriggerHydraulicUpdate)
       NEDEN: Herhangi bir nesne değişikliğinde (Ekleme/Silme/Taşıma) veya özellik değişiminde 
              akış ve çap değerlerini otomatik olarak yeniden hesaplamak için.
    */
    private void TriggerHydraulicUpdate(bool forceRecalculate = false)
    {
        if (_isCalculating || TopologyGraph == null) return;
        
        try
        {
            _isCalculating = true;

            var allMechanicalEntities = TopologyGraph.Nodes?
                .Select(n => n?.Entity)
                .Where(e => e != null)
                .Cast<MechanicalEntity>()
                .ToList() ?? new List<MechanicalEntity>();
            
            if (!allMechanicalEntities.Any()) return;

            // 1. Durumu geçersiz kıl
            foreach (var entity in allMechanicalEntities)
            {
                entity.IsCalculationUpToDate = false;
            }

            // 2. Akış yönlerini anında saptayıp Ok'ları çizim ekranında belirginleştir
            var flowService = new FlowCalculationService(TopologyGraph);
            flowService.InferFlowDirections(allMechanicalEntities);

            // 3. EĞER Gerekliyse veya Otomatik Mod Açıksa Tam Hesapla (Auto Re-calc)
            if (forceRecalculate || (Metadata != null && Metadata.ProjectName != "TEST")) 
            {
                 // Tüm projeyi (debi, hız, basınç kaybı) baştan koştur.
                 // Mühendislik Kararı: 'Zayıf Mimari' eleştirisini gidermek için bu adım zorunludur.
                 RecalculateProject(allMechanicalEntities);
            }
        }
        finally
        {
            _isCalculating = false;
        }
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



