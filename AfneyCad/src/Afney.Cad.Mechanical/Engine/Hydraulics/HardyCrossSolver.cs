using Afney.Cad.Mechanical.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Engine.Hydraulics;

/*
   NE: Hardy-Cross Hidrolik Çözücü (HardyCrossSolver)
   NEDEN: Kapalı döngü (halkalı) boru şebekelerinde, basınç dengesini (Kirchhoff II. Yasası) sağlayacak debi dağılımını hesaplamak için.

   MÜHENDİSLİK DETAYI:
   - Iterative Correction (Ardışıl Yaklaşım) yöntemini kullanır.
   - Her halka için ΔQ = -Σ(hL) / Σ(n * hL / Q) formülü uygulanır.
   - n değeri Darcy-Weisbach akış rejimi için 2.0 (veya akışa göre 1.85-2.0 arası) kabul edilir.
   - Çözümün geçerliliği için şebekenin topolojik olarak "Düğüm Noktası Dengesi" (Kirchhoff I. Yasası) sağlanmış olmalıdır.

   HALKA TESPİTİ (Loop Detection):
   - Şebeke bir spanning tree (BFS) + kalan "chord" kenarlarına ayrıştırılır.
   - Her chord kenarı, ağaçtaki iki ucu arasındaki tek yol ile birleşerek bir "fundamental cycle" (bağımsız halka) oluşturur.
   - Şebeke birden fazla bağlantısız bileşenden oluşuyorsa her bileşen kendi köküyle ayrı bir ağaç kurar (orman/forest).

   DEBİ BAŞLANGIÇ DEĞERİ (Initial Flow):
   - Spanning tree üzerindeki her kenarın debisi, o kenarın kökten uzak tarafındaki alt ağacın toplam tüketimine (Demand) eşitlenir
     (post-order/alt-ağaç toplamı). Bu, Kirchhoff I. Kanunu'nu (düğüm debi dengesi) otomatik olarak sağlar.
   - Chord kenarları başlangıçta debisiz (0) kabul edilir; Hardy-Cross iterasyonu bu debiyi halka boyunca yeniden dağıtır.

   KULLANIM:
   - Şebeke tasarımı tamamlandıktan sonra debi ve basınç analizi için tetiklenir.
*/
public class HardyCrossSolver
{
    private const int MaxIterations = 100;
    private const double Tolerance = 0.001; // 1 L/h hassasiyet (m³/h cinsinden)

    private sealed class LoopEdge
    {
        public NetworkPipe Pipe = null!;
        // +1: halka gezinme yönü boru Start->End yönüyle aynı, -1: ters yönde.
        public int Sign;
    }

    public void Solve(HydraulicNetwork network)
    {
        if (network.Pipes.Count == 0) return;

        var adjacency = BuildAdjacency(network);
        var (parent, parentEdge) = BuildSpanningForest(network, adjacency);

        // ÖNŞART: Kirchhoff I. Kanunu (Debi Dengesi) sağlanmış olmalı.
        InitializeFlows(network, parent, parentEdge);

        var treeEdges = new HashSet<NetworkPipe>(parentEdge.Values);
        var loops = FindFundamentalCycles(network, parent, parentEdge, treeEdges);

        if (loops.Count > 0)
        {
            for (int iter = 0; iter < MaxIterations; iter++)
            {
                double maxDeltaQ = 0;

                foreach (var loop in loops)
                {
                    double sumHeadLoss = 0;
                    double sumDerivative = 0; // Σ(n * hL / Q)

                    foreach (var edge in loop)
                    {
                        double qAbs = Math.Abs(edge.Pipe.FlowRate);

                        double pressureDropBar = MechanicalCalculations.CalculatePressureDrop(
                            edge.Pipe.Length,
                            edge.Pipe.InnerDiameter,
                            qAbs,
                            edge.Pipe.Material ?? "Steel",
                            20.0
                        );

                        double headLoss = pressureDropBar * 10.197; // 1 bar ≈ 10.197 mSS
                        double flowSign = Math.Sign(edge.Pipe.FlowRate);

                        sumHeadLoss += edge.Sign * flowSign * headLoss;

                        if (qAbs > 1e-6)
                            sumDerivative += 2.0 * headLoss / qAbs; // Darcy-Weisbach için n=2
                    }

                    // Payda sıfır kontrolü (Mühendislik Emniyeti)
                    if (Math.Abs(sumDerivative) < 1e-12) continue;

                    // Hardy-Cross Düzeltme Faktörü (Correction Factor) — halka gezinme yönünde
                    double deltaQ = -sumHeadLoss / sumDerivative;

                    foreach (var edge in loop)
                        edge.Pipe.FlowRate += edge.Sign * deltaQ;

                    maxDeltaQ = Math.Max(maxDeltaQ, Math.Abs(deltaQ));
                }

                if (maxDeltaQ < Tolerance) break;
            }
        }

        UpdateResults(network);
    }

    private static Dictionary<NetworkNode, List<NetworkPipe>> BuildAdjacency(HydraulicNetwork network)
    {
        var adjacency = network.Nodes.ToDictionary(n => n, _ => new List<NetworkPipe>());
        foreach (var pipe in network.Pipes)
        {
            adjacency[pipe.StartNode].Add(pipe);
            adjacency[pipe.EndNode].Add(pipe);
        }
        return adjacency;
    }

    /*
       NE: Spanning Forest Kurulumu (BFS)
       NEDEN: Şebekeyi (olası bağlantısız bileşenler dahil) bir ağaç yapısına indirger — halka tespiti ve
              başlangıç debi dağıtımı bu ağaç üzerinden yapılır. Kaynak (Source) düğümü varsa kök olarak tercih edilir.
    */
    private static (Dictionary<NetworkNode, NetworkNode> parent, Dictionary<NetworkNode, NetworkPipe> parentEdge) BuildSpanningForest(
        HydraulicNetwork network, Dictionary<NetworkNode, List<NetworkPipe>> adjacency)
    {
        var visited = new HashSet<NetworkNode>();
        var parent = new Dictionary<NetworkNode, NetworkNode>();
        var parentEdge = new Dictionary<NetworkNode, NetworkPipe>();

        var roots = network.Nodes.Where(n => n.Type == NodeType.Source).ToList();
        var startCandidates = roots.Concat(network.Nodes).Distinct();

        foreach (var candidateRoot in startCandidates)
        {
            if (visited.Contains(candidateRoot)) continue;

            var queue = new Queue<NetworkNode>();
            queue.Enqueue(candidateRoot);
            visited.Add(candidateRoot);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var pipe in adjacency[current])
                {
                    var other = pipe.StartNode == current ? pipe.EndNode : pipe.StartNode;
                    if (visited.Contains(other)) continue;

                    visited.Add(other);
                    parent[other] = current;
                    parentEdge[other] = pipe;
                    queue.Enqueue(other);
                }
            }
        }

        return (parent, parentEdge);
    }

    /*
       NE: Spanning-Tree Tabanlı Debi Dağıtıcı
       NEDEN: Rastgele başlangıç debisi yerine, her ağaç kenarına kökten uzak taraftaki alt ağacın toplam
              tüketimini (Demand) atayarak Kirchhoff I. Kanunu'nu (düğüm debi dengesi) baştan sağlamak için.
    */
    private static void InitializeFlows(
        HydraulicNetwork network,
        Dictionary<NetworkNode, NetworkNode> parent,
        Dictionary<NetworkNode, NetworkPipe> parentEdge)
    {
        // Kökten uzaklığa göre (derinlik azalan) sırala — yapraklar önce işlenir (post-order).
        var depth = new Dictionary<NetworkNode, int>();
        foreach (var node in network.Nodes)
        {
            int d = 0;
            var cur = node;
            while (parent.TryGetValue(cur, out var p)) { d++; cur = p; }
            depth[node] = d;
        }

        var subtreeDemand = network.Nodes.ToDictionary(n => n, n => n.Demand);

        foreach (var node in network.Nodes.OrderByDescending(n => depth[n]))
        {
            if (!parentEdge.TryGetValue(node, out var edge)) continue; // kök düğüm

            var parentNode = parent[node];
            subtreeDemand[parentNode] += subtreeDemand[node];

            // Akış yönü: ebeveynden çocuğa (tüketimi karşılamak üzere) — Start->End pozitif kabulüyle işaretlenir.
            edge.FlowRate = edge.StartNode == node ? -subtreeDemand[node] : subtreeDemand[node];
        }

        // Chord (ağaç dışı) kenarlar Hardy-Cross tarafından yeniden dağıtılana kadar debisiz kabul edilir.
        foreach (var pipe in network.Pipes)
        {
            if (!parentEdge.ContainsValue(pipe))
                pipe.FlowRate = 0;
        }
    }

    /*
       NE: Bağımsız Halka Tespiti (Fundamental Cycle Basis)
       NEDEN: Spanning tree dışında kalan her "chord" kenarı, ağaçtaki tek yol ile birleşerek Hardy-Cross'un
              çalışacağı bağımsız bir halka oluşturur. Her halka kenarına, gezinme yönüne göre ±1 işareti atanır.
    */
    private static List<List<LoopEdge>> FindFundamentalCycles(
        HydraulicNetwork network,
        Dictionary<NetworkNode, NetworkNode> parent,
        Dictionary<NetworkNode, NetworkPipe> parentEdge,
        HashSet<NetworkPipe> treeEdges)
    {
        var loops = new List<List<LoopEdge>>();

        List<NetworkNode> PathToRoot(NetworkNode node)
        {
            var path = new List<NetworkNode> { node };
            var cur = node;
            while (parent.TryGetValue(cur, out var p)) { path.Add(p); cur = p; }
            return path;
        }

        foreach (var chord in network.Pipes)
        {
            if (treeEdges.Contains(chord)) continue;

            var u = chord.StartNode;
            var v = chord.EndNode;

            var pathU = PathToRoot(u);
            var pathV = PathToRoot(v);
            var ancestorsU = new HashSet<NetworkNode>(pathU);

            NetworkNode? lca = pathV.FirstOrDefault(n => ancestorsU.Contains(n));
            if (lca == null) continue; // farklı bileşenlerde — halka oluşturmaz (fiziksel olarak beklenmez)

            var loop = new List<LoopEdge>();

            // Segment 1: u -> lca (ağaç üzerinden yukarı doğru)
            for (var node = u; node != lca; node = parent[node])
            {
                var edge = parentEdge[node];
                int sign = edge.StartNode == node ? 1 : -1;
                loop.Add(new LoopEdge { Pipe = edge, Sign = sign });
            }

            // Segment 2: lca -> v (v'nin kökle yolunun tersi, yukarıdan aşağı)
            var segment2 = new List<LoopEdge>();
            for (var node = v; node != lca; node = parent[node])
            {
                var edge = parentEdge[node];
                int sign = edge.EndNode == node ? 1 : -1; // parent->node yönü
                segment2.Add(new LoopEdge { Pipe = edge, Sign = sign });
            }
            segment2.Reverse();
            loop.AddRange(segment2);

            // Segment 3: chord — v'den u'ya (halkayı kapatır)
            int chordSign = (chord.StartNode == v && chord.EndNode == u) ? 1 : -1;
            loop.Add(new LoopEdge { Pipe = chord, Sign = chordSign });

            loops.Add(loop);
        }

        return loops;
    }

    private void UpdateResults(HydraulicNetwork network)
    {
        foreach (var pipe in network.Pipes)
        {
            double qAbs = Math.Abs(pipe.FlowRate);
            double pBar = MechanicalCalculations.CalculatePressureDrop(
                pipe.Length, pipe.InnerDiameter, qAbs, pipe.Material, 20.0);

            pipe.HeadLoss = pBar * 10.197;
        }
    }
}
