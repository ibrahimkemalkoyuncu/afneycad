using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Boru Ağı Topoloji Analizi (NetworkTopologyAnalysisService)
   NEDEN: TS 1258 gereği tesisat ağındaki topolojik hataları — döngü (loop), açık uç, bağlantısız segment —
          bulmak ve kritik yolu (en uzun hat) tespit etmek için.

   YAKLAŞIM:
   - Boruların StartPoint/EndPoint noktaları yakınlık toleransıyla (SnapTol) birleştirilir → adjacency graf.
   - DFS ile döngü tespiti, BFS ile bağlantısız bileşen tespiti yapılır.
   - Kritik yol: Dijkstra ile en uzun (en yüksek kümülatif uzunluk) yol.
*/
public class NetworkTopologyAnalysisService
{
    private readonly CadDatabase _database;
    public double SnapTolerance { get; set; } = 20.0; // mm

    public NetworkTopologyAnalysisService(CadDatabase database) { _database = database; }

    // ── Sonuç ────────────────────────────────────────────────────────────────────

    public class AnalysisResult
    {
        public bool HasLoops          { get; set; }
        public bool HasOpenEnds       { get; set; }
        public bool HasDisconnected   { get; set; }
        public int  LoopCount         { get; set; }
        public int  OpenEndCount      { get; set; }
        public int  ComponentCount    { get; set; }
        public double CriticalPathM   { get; set; }
        public List<Guid> OpenEndPipes        { get; set; } = [];
        public List<Guid> DisconnectedPipes   { get; set; } = [];
        public List<Guid> CriticalPathPipes   { get; set; } = [];
        public List<string> Summary           { get; set; } = [];
    }

    // ── Ana Hesap ─────────────────────────────────────────────────────────────────

    public AnalysisResult Analyze()
    {
        var result = new AnalysisResult();
        var pipes  = _database.GetAllEntities().OfType<PipeEntity>().ToList();

        if (pipes.Count == 0)
        {
            result.Summary.Add("Veritabanında boru bulunamadı.");
            return result;
        }

        // Tüm uç noktaları topla, yakın olanları birleştir (node clustering)
        var nodes = BuildNodeGraph(pipes, out var pipeToNodes, out var adj);

        // Açık Uç Tespiti
        var openEndPipes = FindOpenEnds(pipes, pipeToNodes, adj);
        result.OpenEndCount = openEndPipes.Count;
        result.HasOpenEnds  = openEndPipes.Count > 0;
        result.OpenEndPipes = openEndPipes.Select(p => p.Id).ToList();

        // Bağlantısız Bileşen Tespiti
        int componentCount = CountComponents(nodes.Count, adj);
        result.ComponentCount  = componentCount;
        result.HasDisconnected = componentCount > 1;

        if (componentCount > 1)
        {
            var mainComp = LargestComponent(nodes.Count, adj);
            var disconnected = new List<Guid>();
            foreach (var p in pipes)
            {
                if (pipeToNodes.TryGetValue(p.Id, out var nn) && !mainComp.Contains(nn.n1) && !mainComp.Contains(nn.n2))
                    disconnected.Add(p.Id);
            }
            result.DisconnectedPipes = disconnected;
        }

        // Döngü Tespiti (DFS)
        bool hasLoop = DetectCycle(nodes.Count, adj);
        result.HasLoops   = hasLoop;
        result.LoopCount  = hasLoop ? CountCycles(nodes.Count, adj) : 0;

        // Kritik Yol (Dijkstra — en uzun yol)
        var (pathLen, pathPipes) = FindCriticalPath(pipes, pipeToNodes, adj, nodes.Count);
        result.CriticalPathM     = Math.Round(pathLen / 1000.0, 2);
        result.CriticalPathPipes = pathPipes;

        // Özet
        if (!result.HasLoops && !result.HasOpenEnds && !result.HasDisconnected)
            result.Summary.Add("✅ Topoloji temiz — döngü, açık uç veya kopuk segment yok.");
        if (result.HasLoops)
            result.Summary.Add($"🔄 {result.LoopCount} döngü (loop/mesh) tespit edildi.");
        if (result.HasOpenEnds)
            result.Summary.Add($"⚠ {result.OpenEndCount} açık uçlu boru bulundu.");
        if (result.HasDisconnected)
            result.Summary.Add($"⚡ {result.ComponentCount} ayrı bağlantısız bileşen var ({result.DisconnectedPipes.Count} boru kopuk).");
        result.Summary.Add($"📏 Kritik yol uzunluğu: {result.CriticalPathM:F2} m");
        result.Summary.Add($"📊 Toplam boru: {pipes.Count} segment");

        return result;
    }

    // ── Graf İnşası ───────────────────────────────────────────────────────────────

    // Returns list of clustered node positions; fills adjacency as nodeIdx→List<(neighborIdx, pipeId)>
    private List<Vector3D> BuildNodeGraph(
        List<PipeEntity> pipes,
        out Dictionary<Guid, (int n1, int n2)> pipeToNodes,
        out List<List<(int neighbor, Guid pipeId)>> adj)
    {
        var nodes    = new List<Vector3D>();
        pipeToNodes  = new Dictionary<Guid, (int, int)>();
        var adjLocal = new List<List<(int, Guid)>>();
        adj          = adjLocal;

        int FindOrAdd(Vector3D pt)
        {
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].DistanceTo(pt) <= SnapTolerance) return i;
            nodes.Add(pt);
            adjLocal.Add([]);
            return nodes.Count - 1;
        }

        foreach (var pipe in pipes)
        {
            int n1 = FindOrAdd(pipe.StartPoint);
            int n2 = FindOrAdd(pipe.EndPoint);
            pipeToNodes[pipe.Id] = (n1, n2);
            adj[n1].Add((n2, pipe.Id));
            adj[n2].Add((n1, pipe.Id));
        }

        return nodes;
    }

    // ── Açık Uç ──────────────────────────────────────────────────────────────────

    private static List<PipeEntity> FindOpenEnds(
        List<PipeEntity> pipes,
        Dictionary<Guid, (int n1, int n2)> pipeToNodes,
        List<List<(int neighbor, Guid pipeId)>> adj)
    {
        // Bir uç nokta (node), sadece 1 bağlantıya sahipse açık uçtur
        var openEnds = new List<PipeEntity>();
        foreach (var pipe in pipes)
        {
            if (!pipeToNodes.TryGetValue(pipe.Id, out var nodes)) continue;
            if (adj[nodes.n1].Count == 1 || adj[nodes.n2].Count == 1)
                openEnds.Add(pipe);
        }
        return openEnds;
    }

    // ── Bağlantısız Bileşen ───────────────────────────────────────────────────────

    private static int CountComponents(int nodeCount, List<List<(int neighbor, Guid pipeId)>> adj)
    {
        var visited = new bool[nodeCount];
        int count = 0;
        for (int i = 0; i < nodeCount; i++)
        {
            if (visited[i]) continue;
            BFS(i, adj, visited);
            count++;
        }
        return count;
    }

    private static HashSet<int> LargestComponent(int nodeCount, List<List<(int neighbor, Guid pipeId)>> adj)
    {
        var visited = new bool[nodeCount];
        HashSet<int>? largest = null;
        for (int i = 0; i < nodeCount; i++)
        {
            if (visited[i]) continue;
            var comp = new HashSet<int>();
            BFS(i, adj, visited, comp);
            if (largest == null || comp.Count > largest.Count) largest = comp;
        }
        return largest ?? [];
    }

    private static void BFS(int start, List<List<(int neighbor, Guid pipeId)>> adj, bool[] visited, HashSet<int>? comp = null)
    {
        var queue = new Queue<int>();
        queue.Enqueue(start);
        visited[start] = true;
        comp?.Add(start);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (var (nb, _) in adj[cur])
            {
                if (visited[nb]) continue;
                visited[nb] = true;
                comp?.Add(nb);
                queue.Enqueue(nb);
            }
        }
    }

    // ── Döngü Tespiti (DFS) ───────────────────────────────────────────────────────

    private static bool DetectCycle(int nodeCount, List<List<(int neighbor, Guid pipeId)>> adj)
    {
        var visited = new bool[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            if (!visited[i] && DFSCycle(i, -1, adj, visited)) return true;
        }
        return false;
    }

    private static bool DFSCycle(int node, int parent, List<List<(int neighbor, Guid pipeId)>> adj, bool[] visited)
    {
        visited[node] = true;
        foreach (var (nb, _) in adj[node])
        {
            if (!visited[nb]) { if (DFSCycle(nb, node, adj, visited)) return true; }
            else if (nb != parent) return true;
        }
        return false;
    }

    private static int CountCycles(int nodeCount, List<List<(int, Guid)>> adj)
    {
        // Euler formula: loops = edges - nodes + components
        int edges = adj.Sum(a => a.Count) / 2;
        int components = CountComponents(nodeCount, adj);
        return Math.Max(0, edges - nodeCount + components);
    }

    // ── Kritik Yol (BFS en uzun yol) ─────────────────────────────────────────────

    private (double lenMM, List<Guid> pipes) FindCriticalPath(
        List<PipeEntity> pipes,
        Dictionary<Guid, (int n1, int n2)> pipeToNodes,
        List<List<(int neighbor, Guid pipeId)>> adj,
        int nodeCount)
    {
        if (nodeCount == 0) return (0, []);

        // En uzun yolu BFS: kaynak (derece=1) → hedef (derece=1)
        var pipeLengths = pipes.ToDictionary(p => p.Id, p => (p.EndPoint - p.StartPoint).Length());

        double maxLen = 0;
        List<Guid> bestPath = [];

        // Tüm yaprak düğümlerden BFS yap
        var leaves = Enumerable.Range(0, nodeCount).Where(i => adj[i].Count == 1).ToList();
        if (leaves.Count == 0) leaves = [0]; // Döngü varsa herhangi bir düğümden başla

        foreach (int src in leaves.Take(4)) // Performans için max 4 kaynak
        {
            var dist     = new double[nodeCount];
            var prev     = new int[nodeCount];
            var prevPipe = new Guid[nodeCount];
            Array.Fill(dist, -1); Array.Fill(prev, -1);
            dist[src] = 0;
            var pq = new PriorityQueue<int, double>(Comparer<double>.Create((a, b) => b.CompareTo(a)));
            pq.Enqueue(src, 0);

            while (pq.Count > 0)
            {
                int cur = pq.Dequeue();
                foreach (var (nb, pid) in adj[cur])
                {
                    double nd = dist[cur] + (pipeLengths.TryGetValue(pid, out double pl) ? pl : 0);
                    if (nd > dist[nb])
                    {
                        dist[nb] = nd;
                        prev[nb]  = cur;
                        prevPipe[nb] = pid;
                        pq.Enqueue(nb, nd);
                    }
                }
            }

            int farthest = Array.IndexOf(dist, dist.Max());
            if (dist[farthest] > maxLen)
            {
                maxLen = dist[farthest];
                bestPath = [];
                int cur = farthest;
                while (prev[cur] != -1) { bestPath.Add(prevPipe[cur]); cur = prev[cur]; }
            }
        }

        return (maxLen, bestPath);
    }
}
