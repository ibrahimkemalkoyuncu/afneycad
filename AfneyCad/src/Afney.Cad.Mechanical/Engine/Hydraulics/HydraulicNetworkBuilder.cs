using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Engine.Hydraulics;

/*
   NE: Hidrolik Ağ Oluşturucu (HydraulicNetworkBuilder)
   NEDEN: HardyCrossSolver ve HydraulicNetwork yazılmıştı ama hiçbir komut onları çizimdeki
          gerçek PipeEntity'lerden bir HydraulicNetwork kurup çağırmıyordu — halka tespiti hiç
          tetiklenmiyordu. Bu sınıf, uç noktaları konumsal olarak kümeleyerek (aynı fiziksel
          düğüme değen borular aynı NetworkNode'u paylaşır) PipeEntity listesinden gerçek bir
          graf kurar.

   BİRİM DÖNÜŞÜMÜ: PipeEntity.Length/InnerDiameter mm cinsindendir (bkz. PressureDropService'in
   aynı /1000.0 dönüşümü); HardyCrossSolver'ın kullandığı MechanicalCalculations.CalculatePressureDrop
   metre bekler.
*/
public static class HydraulicNetworkBuilder
{
    public class BuildResult
    {
        public HydraulicNetwork Network { get; } = new();
        public Dictionary<PipeEntity, NetworkPipe> PipeMap { get; } = new();
    }

    public static BuildResult Build(IEnumerable<PipeEntity> pipes, double clusterToleranceMm = 10.0)
    {
        var result = new BuildResult();
        var clusteredNodes = new List<(Vector3D Position, NetworkNode Node)>();

        NetworkNode GetOrCreateNode(Vector3D position)
        {
            foreach (var (pos, node) in clusteredNodes)
            {
                if (pos.DistanceTo(position) <= clusterToleranceMm)
                    return node;
            }

            var newNode = new NetworkNode(position);
            clusteredNodes.Add((position, newNode));
            return newNode;
        }

        foreach (var pipe in pipes)
        {
            var start = GetOrCreateNode(pipe.StartPoint);
            var end = GetOrCreateNode(pipe.EndPoint);

            result.Network.AddPipe(start, end, pipe.InnerDiameter, pipe.PipeMaterialType.ToString());
            var netPipe = result.Network.Pipes[^1];
            netPipe.Length = pipe.Length / 1000.0; // mm → m

            result.PipeMap[pipe] = netPipe;
        }

        // Derece-1 (uç/yaprak) düğümlere, o düğüme değen tek borunun mevcut (ağaç bazlı hesapla
        // bulunmuş) FlowRate değerini talep (Demand) olarak ata. İç bağlantı (junction)
        // düğümlerinde talep sıfırdır — akış sadece borular arasında yeniden dağıtılır.
        var degree = result.Network.Pipes
            .SelectMany(p => new[] { p.StartNode, p.EndNode })
            .GroupBy(n => n)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (pipe, netPipe) in result.PipeMap)
        {
            double demand = System.Math.Abs(pipe.FlowRate);
            if (degree.GetValueOrDefault(netPipe.StartNode) == 1) netPipe.StartNode.Demand = demand;
            if (degree.GetValueOrDefault(netPipe.EndNode) == 1) netPipe.EndNode.Demand = demand;
        }

        return result;
    }
}
