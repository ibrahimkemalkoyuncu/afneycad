using System;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Engine.Hydraulics;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Hardy-Cross Halkalı Şebeke Çözücü Testleri (HardyCrossSolverTests)
   NEDEN: HardyCrossSolver'ın halka tespiti (loop detection) ve spanning-tree tabanlı
          debi dağıtımı, önceden "// TODO" olarak işaretli ve tek-halka varsayımıyla
          çalışan eksik bir implementasyondu. Bu testler iki temel mühendislik
          kuralının artık gerçekten sağlandığını doğrular:
          1. Kirchhoff I (düğüm debi dengesi) — her düğümde net giren debi = tüketim.
          2. Kirchhoff II (halka basınç dengesi) — kapalı halka boyunca işaretli
             toplam yük kaybı ≈ 0.
*/
public class HardyCrossSolverTests
{
    private static NetworkNode Node(double x, double y, NodeType type = NodeType.Junction, double demand = 0)
        => new(new Vector3D(x, y, 0), type) { Demand = demand };

    // Kare halka: A(Kaynak) - B - C - D - A. Spanning tree A-B-C-D bırakır, D-A chord'u tek bağımsız halkayı oluşturur.
    private static (HydraulicNetwork network, NetworkNode a, NetworkNode b, NetworkNode c, NetworkNode d) BuildSquareLoop()
    {
        var network = new HydraulicNetwork();
        var a = Node(0, 0, NodeType.Source);
        var b = Node(10, 0, demand: 2.0);
        var c = Node(10, 10, demand: 2.0);
        var d = Node(0, 10, demand: 2.0);

        network.AddPipe(a, b, diameter: 50);
        network.AddPipe(b, c, diameter: 50);
        network.AddPipe(c, d, diameter: 50);
        network.AddPipe(d, a, diameter: 50); // chord — halkayı kapatan kenar

        return (network, a, b, c, d);
    }

    [Fact]
    public void Solve_SquareLoop_ChordPipeCarriesNonZeroFlow()
    {
        var (network, _, _, _, _) = BuildSquareLoop();

        new HardyCrossSolver().Solve(network);

        // Chord (D-A) başlangıçta 0 debiliydi; Hardy-Cross düzeltmesi çalıştıysa artık debi taşımalı.
        var chord = network.Pipes.Last();
        Assert.True(Math.Abs(chord.FlowRate) > 1e-6,
            "Chord kenarı hâlâ debisiz — halka tespiti veya Hardy-Cross düzeltmesi çalışmıyor olabilir.");
    }

    [Fact]
    public void Solve_SquareLoop_SatisfiesNodalContinuity()
    {
        var (network, a, b, c, d) = BuildSquareLoop();

        new HardyCrossSolver().Solve(network);

        double NetInflow(NetworkNode node) =>
            network.Pipes.Where(p => p.EndNode == node).Sum(p => p.FlowRate) -
            network.Pipes.Where(p => p.StartNode == node).Sum(p => p.FlowRate);

        // Her tüketim noktasında: net giren debi = tüketim (Kirchhoff I).
        Assert.Equal(b.Demand, NetInflow(b), precision: 6);
        Assert.Equal(c.Demand, NetInflow(c), precision: 6);
        Assert.Equal(d.Demand, NetInflow(d), precision: 6);

        // Kaynak düğüm, toplam tüketimi karşılayacak kadar debi sağlamalı.
        double totalDemand = b.Demand + c.Demand + d.Demand;
        Assert.Equal(-totalDemand, NetInflow(a), precision: 6);
    }

    [Fact]
    public void Solve_SquareLoop_HeadLossBalancesAroundLoop()
    {
        var (network, a, b, c, d) = BuildSquareLoop();

        new HardyCrossSolver().Solve(network);

        double SignedHeadLoss(NetworkPipe pipe, NetworkNode from)
        {
            double qAbs = Math.Abs(pipe.FlowRate);
            double bar = MechanicalCalculations.CalculatePressureDrop(pipe.Length, pipe.InnerDiameter, qAbs, pipe.Material, 20.0);
            double headLoss = bar * 10.197;
            double flowSign = Math.Sign(pipe.FlowRate);
            // "from" düğümünden ayrılış yönü boru Start->End ile aynıysa +1, değilse -1.
            int travelSign = pipe.StartNode == from ? 1 : -1;
            return travelSign * flowSign * headLoss;
        }

        var ab = network.Pipes[0];
        var bc = network.Pipes[1];
        var cd = network.Pipes[2];
        var da = network.Pipes[3];

        // A -> B -> C -> D -> A yönünde gezinerek toplam yük kaybı ≈ 0 olmalı (Kirchhoff II).
        double loopSum = SignedHeadLoss(ab, a) + SignedHeadLoss(bc, b) + SignedHeadLoss(cd, c) + SignedHeadLoss(da, d);

        Assert.True(Math.Abs(loopSum) < 0.01,
            $"Halka boyunca toplam yük kaybı dengelenmedi: {loopSum:F4} mSS (Hardy-Cross yakınsamadı).");
    }

    [Fact]
    public void Solve_TreeOnlyNetwork_NoLoops_DoesNotThrow()
    {
        // Halkasız (ağaç) şebeke: A - B - C, chord yok.
        var network = new HydraulicNetwork();
        var a = Node(0, 0, NodeType.Source);
        var b = Node(10, 0, demand: 1.5);
        var c = Node(20, 0, demand: 1.5);
        network.AddPipe(a, b, diameter: 40);
        network.AddPipe(b, c, diameter: 40);

        var exception = Record.Exception(() => new HardyCrossSolver().Solve(network));

        Assert.Null(exception);
        // Ağaç şebekede debi zaten tek yoldan belirlenir: A-B tüm alt-ağacı, B-C sadece C'yi taşır.
        Assert.Equal(3.0, Math.Abs(network.Pipes[0].FlowRate), precision: 6);
        Assert.Equal(1.5, Math.Abs(network.Pipes[1].FlowRate), precision: 6);
    }
}
