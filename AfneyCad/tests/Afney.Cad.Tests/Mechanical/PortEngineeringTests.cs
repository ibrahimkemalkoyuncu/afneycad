using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Geometry.Primitives;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: MEP Port Mühendisliği Testleri (PortEngineeringTests)
   NEDEN: Faz 29 kapsamında eklenen Diameter, PipeMaterialType ve static
          factory metodlarının TS 1258 standartlarına uygun çalıştığını doğrulamak.
*/
public class PortEngineeringTests
{
    // ─── 1. MechanicalPort Diameter ──────────────────────────────
    [Fact]
    public void MechanicalPort_FullConstructor_SetsDiameter()
    {
        var port = new MechanicalPort(
            System.Guid.NewGuid(), "ColdWater",
            new Vector3D(0, 0, 0), Vector3D.ZAxis,
            diameter: 15.0);

        Assert.Equal(15.0, port.Diameter);
    }

    [Fact]
    public void MechanicalPort_DefaultConstructor_DiameterIsZero()
    {
        var port = new MechanicalPort(
            System.Guid.NewGuid(), "Test",
            new Vector3D(0, 0, 0), Vector3D.ZAxis);

        Assert.Equal(0.0, port.Diameter);
    }

    // ─── 2. SanitaryFixtureEntity Static Factory Metodlar ────────
    [Fact]
    public void CreateWashbasin_ShouldHaveCorrectLUAndSize()
    {
        var e = SanitaryFixtureEntity.CreateWashbasin(Vector3D.Zero);
        // TS 1258: Lavabo LU değeri = 0.5 (InitializeDefaults tarafından atanır)
        Assert.Equal(0.5, e.FixtureUnit);
        Assert.Equal(550, e.Width);
        Assert.Equal(450, e.Depth);
    }

    [Fact]
    public void CreateWC_ShouldHaveNoHotWaterPort()
    {
        var e = SanitaryFixtureEntity.CreateWC(Vector3D.Zero);
        Assert.Equal(3.0, e.FixtureUnit);
        // WC'nin HotWaterOffset = Zero olmalı → sıcak su portu üretilmez
        Assert.Equal(Vector3D.Zero, e.HotWaterOffset);

        var ports = e.GetPorts();
        Assert.DoesNotContain(ports, p => p.Name == "HotWater");
    }

    [Fact]
    public void CreateWC_DrainPort_ShouldBeDN100()
    {
        var e = SanitaryFixtureEntity.CreateWC(Vector3D.Zero);
        var ports = e.GetPorts();
        var drain = ports.Find(p => p.Name == "Drainage");
        Assert.NotNull(drain);
        Assert.Equal(100.0, drain!.Diameter);
    }

    [Fact]
    public void CreateWashbasin_DrainPort_ShouldBeDN40()
    {
        var e = SanitaryFixtureEntity.CreateWashbasin(Vector3D.Zero);
        var ports = e.GetPorts();
        var drain = ports.Find(p => p.Name == "Drainage");
        Assert.NotNull(drain);
        Assert.Equal(40.0, drain!.Diameter);
    }

    [Fact]
    public void CreateShower_ShouldHaveThreePorts()
    {
        var e = SanitaryFixtureEntity.CreateShower(Vector3D.Zero);
        var ports = e.GetPorts();
        Assert.Equal(3, ports.Count); // ColdWater + HotWater + Drainage
    }

    [Fact]
    public void CreateFloorDrain_ShouldHaveOnlyDrainPort()
    {
        var e = SanitaryFixtureEntity.CreateFloorDrain(Vector3D.Zero);
        var ports = e.GetPorts();
        // Soğuk ve sıcak su ofsetleri Zero → sadece Drainage portu olmalı
        Assert.Single(ports);
        Assert.Equal("Drainage", ports[0].Name);
    }

    // ─── 3. FixtureLibraryService.CreateEntity ───────────────────
    [Fact]
    public void FixtureLibraryService_CreateEntity_WC_SetsCorrectSize()
    {
        var svc = new FixtureLibraryService();
        var entity = svc.CreateEntity("WC-001", Vector3D.Zero);

        // Katalog: WC-001 SymbolWidth=400, SymbolHeight=600
        Assert.Equal(400, entity.Width);
        Assert.Equal(600, entity.Depth);
    }

    [Fact]
    public void FixtureLibraryService_CreateEntity_WC_NoHotWaterOffset()
    {
        var svc = new FixtureLibraryService();
        var entity = svc.CreateEntity("WC-001", Vector3D.Zero);

        // WC RequiresHotWater=false → HotWaterOffset = Zero
        Assert.Equal(Vector3D.Zero, entity.HotWaterOffset);
    }

    // ─── 4. Manuel Yük Noktası (LoadNode) Hesaplama ──────────────
    [Fact]
    public void FlowCalculation_WithManualLoadNode_ShouldUpdatePipeFU()
    {
        // 1. Setup Varlıklar
        var loadNode = new MechanicalLoadNode(new Vector3D(0, 0, 0), 5.0); // 5 LU Manuel Yük
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25.0);
        var riser = new PipeEntity(new Vector3D(1000, 0, 0), new Vector3D(1000, 0, 3000), 25.0); // Riser
        
        var entities = new List<MechanicalEntity> { loadNode, pipe, riser };
        var graph = new MechanicalTopologyGraph();

        // 2. Graf Montajı (Manual Build)
        graph.AddEntity(loadNode);
        graph.AddEntity(pipe);
        graph.AddEntity(riser);

        // Bağlantılar:
        // LoadNode [LoadInlet] <-> Pipe [Start]
        var lnPort = graph.GetNode(loadNode.Id)!.Ports[0];
        var pStart = graph.GetNode(pipe.Id)!.Ports.First(p => p.Name == "Start");
        graph.Connect(lnPort, pStart);

        // Pipe [End] <-> Riser [Start]
        var pEnd = graph.GetNode(pipe.Id)!.Ports.First(p => p.Name == "End");
        var rStart = graph.GetNode(riser.Id)!.Ports.First(p => p.Name == "Start");
        graph.Connect(pEnd, rStart);

        // 3. Hesaplama
        var calcService = new FlowCalculationService(graph);
        // Bina tipi konut (default) -> a=0.682, b=0.45, c=0.14
        calcService.CalculateSystemFlow(entities);

        // 4. Doğrulama
        // Pipe, LoadNode'daki 5.0 LU yükü taşımalıdır
        Assert.Equal(5.0, pipe.TotalFixtureUnits);
        Assert.Equal(5.0, riser.TotalFixtureUnits);
        
        // Debi kontrolü: Q = 0.682 * (5.0^0.45) - 0.14 ~= 1.26 l/s
        Assert.True(pipe.FlowRate > 0, "Debi sıfırdan büyük olmalı");
    }

    // ─── 5. Reaktif Hesaplama (Reactive Calc) ──────────────
    [Fact]
    public void ReactiveCalculation_AfterDiameterChange_ShouldAutoRecalculate()
    {
        // Setup
        var kernel = new MechanicalKernel();
        kernel.Metadata.ProjectName = "REACTIVE_TEST";

        // MÜHENDİSLİK DETAYI: Kernel bir fitting (Elbow/Reducer) eklemek istediğinde 
        // bunu tekrar OnEntityAddedToDatabase'e göndererek topolojiye dahil ediyoruz.
        kernel.OnRequestAddEntity += e => kernel.OnEntityAddedToDatabase(e);

        // Koordinatlar birbirine değecek şekilde (AutoConnect için)
        var loadNode = new MechanicalLoadNode(new Vector3D(0, 0, 0), 2.0);
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 25.0);
        var riser = new PipeEntity(new Vector3D(1000, 0, 0), new Vector3D(1000, 0, 3000), 25.0);

        // NE/NEDEN — GERÇEK, ÖNCEDEN VAR OLAN BİR TEST HATASI: riser'ın uç noktası
        // (1000,0,3000) hiçbir şeye bağlı değildi — "açık uç" (DomainGuardService.CheckOpenEnds,
        // _database'den bağımsız çalışır, bu testte de devrede). RecalculateProject artık
        // ValidationGate'ten geçmeden hesaplama YAPMIYOR (bilinçli, sertleştirilmiş davranış —
        // gerçek bir sistemde açık uçlu bir boru hattında hesaplama yapmak anlamsızdır). Bu
        // yüzden riser'ın ucuna tek portlu bir MechanicalLoadNode (sink) eklenip açık uç
        // kapatılıyor — artık gerçek, uçtan uca kapalı bir şebeke test ediliyor.
        var sinkNode = new MechanicalLoadNode(new Vector3D(1000, 0, 3000), 1.0);

        // Kernel'e ekle (OnEntityAdded -> AutoConnectPorts tetiklenir)
        kernel.OnEntityAddedToDatabase(loadNode);
        kernel.OnEntityAddedToDatabase(pipe);
        kernel.OnEntityAddedToDatabase(riser);
        kernel.OnEntityAddedToDatabase(sinkNode);

        // Manuel Connect'e gerek yok; çünkü AutoConnectPorts (Mühendislik Zekası) 
        // koordinatlar 0mm farkla örtüştüğü için bunları bağlamış olmalı.
        // Doğrula:
        var pipeNode = kernel.TopologyGraph.GetNode(pipe.Id);
        Assert.NotNull(pipeNode);
        Assert.True(pipeNode.Ports.Any(p => p.IsConnected), "Boru otomatik olarak bağlanmış olmalı.");

        // İlk tam hesaplama (Bağlantılar kurulduktan sonra)
        var allEntities = new System.Collections.Generic.List<Afney.Cad.Domain.Abstractions.CadEntity> { loadNode, pipe, riser, sinkNode };
        kernel.RecalculateProject(allEntities);

        double initialVelocity = pipe.Velocity;
        Assert.True(initialVelocity > 0, $"İlk hesaplamadan sonra Velocity > 0 olmalı. Mevcut: {initialVelocity}");

        // ACT: Çapı manuel kitle ve değiştir (Reactive trigger)
        pipe.IsSizeLocked = true; 
        pipe.InnerDiameter = 50.0; 

        // ASSERT
        Assert.True(pipe.Velocity < initialVelocity, $"Çap arttığında hız otomatik olarak düşmeli. Önceki: {initialVelocity:F4}, Şimdiki: {pipe.Velocity:F4}");
        Assert.True(pipe.IsCalculationUpToDate, "Hesaplama otomatik olarak güncel (Up-to-Date) işaretlenmeli");
    }
}
