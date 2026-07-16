using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine.Hydraulics;
using Afney.Cad.Mechanical.Entities;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: HydraulicNetworkBuilder Testleri
   NEDEN: HardyCrossSolver ve HydraulicNetwork yazılmıştı ama çizimdeki gerçek PipeEntity'lerden
          bir ağ kuran hiçbir kod yoktu — halka analizi hiçbir komuttan tetiklenemiyordu. Bu
          testler, uç noktaları konumsal olarak kümeleyerek (aynı fiziksel noktaya değen
          borular aynı düğümü paylaşmalı) doğru bir graf kurulduğunu doğruluyor.
*/
public class HydraulicNetworkBuilderTests
{
    [Fact]
    public void Build_SquareLoopOfFourPipes_ClustersSharedEndpointsIntoFourNodes()
    {
        // Kare halka: (0,0)-(1000,0)-(1000,1000)-(0,1000)-(0,0) — dört ayrı PipeEntity,
        // ama uç noktalar paylaşılıyor (aynı fiziksel köşe noktaları).
        var pipes = new[]
        {
            new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 50),
            new PipeEntity(new Vector3D(1000, 0, 0), new Vector3D(1000, 1000, 0), 50),
            new PipeEntity(new Vector3D(1000, 1000, 0), new Vector3D(0, 1000, 0), 50),
            new PipeEntity(new Vector3D(0, 1000, 0), new Vector3D(0, 0, 0), 50),
        };

        var result = HydraulicNetworkBuilder.Build(pipes);

        Assert.Equal(4, result.Network.Nodes.Count); // 8 uç nokta değil, 4 paylaşılan köşe
        Assert.Equal(4, result.Network.Pipes.Count);
        Assert.Equal(4, result.PipeMap.Count);
    }

    [Fact]
    public void Build_LinearChain_LeafNodesGetDemandFromAdjacentPipeFlowRate()
    {
        // Doğrusal zincir: A -[boru1: FlowRate=5]- B -[boru2: FlowRate=5]- C
        // A ve C birer yaprak (derece=1) düğüm; B iç bağlantı (derece=2) düğümü.
        var pipe1 = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(1000, 0, 0), 50) { FlowRate = 5.0 };
        var pipe2 = new PipeEntity(new Vector3D(1000, 0, 0), new Vector3D(2000, 0, 0), 50) { FlowRate = 5.0 };

        var result = HydraulicNetworkBuilder.Build(new[] { pipe1, pipe2 });

        Assert.Equal(3, result.Network.Nodes.Count);

        var leafNodes = result.Network.Nodes.Where(n => n.Demand > 0).ToList();
        Assert.Equal(2, leafNodes.Count); // A ve C
        Assert.All(leafNodes, n => Assert.Equal(5.0, n.Demand, precision: 6));

        var junctionNode = result.Network.Nodes.Single(n => n.Demand == 0);
        Assert.Equal(1000, junctionNode.Position.X, precision: 6);
    }

    [Fact]
    public void Build_PipeLength_ConvertedFromMillimetersToMeters()
    {
        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 50); // 5000 mm = 5 m

        var result = HydraulicNetworkBuilder.Build(new[] { pipe });

        var netPipe = result.PipeMap[pipe];
        Assert.Equal(5.0, netPipe.Length, precision: 6);
    }
}
