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

        // Performans: O(n²) lineer arama yerine grid-hash tabanlı O(1) ortalama arama.
        // Koordinatlar SnapTolerance boyutlu hücrelere yuvarlanır; bir noktanın SnapTolerance
        // içindeki komşu node'ları sadece 3x3 (27 3D) komşu hücrede aranır — davranış (tolerans
        // mantığı: en yakın DEĞİL, ilk bulunan <= SnapTolerance node'a bağlanma) AYNI kalır.
        double cell = SnapTolerance > 0 ? SnapTolerance : 1.0;
        var grid = new Dictionary<(long, long, long), List<int>>();

        static (long, long, long) CellOf(Vector3D pt, double cellSize)
            => ((long)Math.Floor(pt.X / cellSize), (long)Math.Floor(pt.Y / cellSize), (long)Math.Floor(pt.Z / cellSize));

        int FindOrAdd(Vector3D pt)
        {
            var (cx, cy, cz) = CellOf(pt, cell);
            // Orijinal davranış: nodes listesinde İLK (en küçük index) uygun eşleşme seçilirdi.
            // 27 hücreyi tarayıp adayları topluyor, sonra en küçük index'i seçiyoruz —
            // böylece hücre tarama sırası sonucu etkilemez, tolerans-eşleşme mantığı birebir korunur.
            int best = -1;
            for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
            for (long dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue((cx + dx, cy + dy, cz + dz), out var bucket)) continue;
                foreach (var i in bucket)
                    if ((best == -1 || i < best) && nodes[i].DistanceTo(pt) <= SnapTolerance) best = i;
            }
            if (best != -1) return best;

            nodes.Add(pt);
            adjLocal.Add([]);
            int newIndex = nodes.Count - 1;

            var key = (cx, cy, cz);
            if (!grid.TryGetValue(key, out var list))
            {
                list = new List<int>();
                grid[key] = list;
            }
            list.Add(newIndex);

            return newIndex;
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

    /*
       NE: Kritik Yol Bulma (FindCriticalPath) — Çift Süpürme (Double-Sweep) Algoritması
       NEDEN: Önceden sadece ilk 4 yaprak (derece=1) düğümden BFS/Dijkstra yapılıp en uzunu
              seçiliyordu ("Performans için max 4 kaynak") — 4'ten fazla dallı büyük
              ağlarda gerçek kritik yolu (ağacın çapı) KAÇIRABİLİYORDU, çünkü doğru cevabı
              veren uç noktalar ilk 4 arasında olmayabilirdi.

              Ağaç yapıları (döngüsüz) için matematiksel olarak KESİN sonuç veren çift-süpürme
              algoritmasına geçildi: 1) herhangi bir düğümden en uzağı bul (A), 2) A'dan en
              uzağı bul (B) — A-B yolu ağacın gerçek çapıdır (kanıtlanmış graf teorisi sonucu).
              Bu hem daha DOĞRU (kaç yaprak olursa olsun kesin sonuç) hem daha HIZLI (O(2V)
              vs eski O(4V)). Şebeke birden fazla bağlantısız bileşenden oluşabileceği için
              (örn. iki ayrı bina hattı) her bileşen için ayrı ayrı çift-süpürme yapılıp en
              iyisi seçiliyor. Döngü içeren (halkalı) ağlarda double-sweep artık kesin değil
              ama yine de rastgele 4 yaprak seçmekten çok daha güvenilir bir sezgiseldir.

       AYRICA GERÇEK BİR HATA DÜZELTİLDİ: RunLongestPath önceden PriorityQueue tabanlı bir
       "gevşetme" (relaxation) döngüsü kullanıyordu ve HİÇBİR "ziyaret edildi" işareti
       yoktu — yönsüz (undirected) grafta bir düğüm, komşusuna gidip oradan GERİ kendisine
       dönerek (ebeveyn↔çocuk pinpon) her turda mesafesini artırabiliyordu. 3+ düğümlü HER
       bağlı grafta bu sonsuz döngüye giriyordu (kuyruk sınırsız büyüyor, mesafe sonsuza
       gidiyordu) — bu metod muhtemelen daha önce hiç gerçek bir ağ üzerinde test edilmemişti.
       Artık her düğüm sadece BİR KEZ ziyaret ediliyor (klasik BFS) — ağaçlarda bu zaten
       doğru ve kesin sonucu verir (iki düğüm arası tek yol vardır).
    */
    private (double lenMM, List<Guid> pipes) FindCriticalPath(
        List<PipeEntity> pipes,
        Dictionary<Guid, (int n1, int n2)> pipeToNodes,
        List<List<(int neighbor, Guid pipeId)>> adj,
        int nodeCount)
    {
        if (nodeCount == 0) return (0, []);

        var pipeLengths = pipes.ToDictionary(p => p.Id, p => (p.EndPoint - p.StartPoint).Length());

        (double[] dist, int[] prev, Guid[] prevPipe) RunLongestPath(int start)
        {
            var dist = new double[nodeCount];
            var prev = new int[nodeCount];
            var prevPipe = new Guid[nodeCount];
            var visited = new bool[nodeCount];
            Array.Fill(dist, -1); Array.Fill(prev, -1);
            dist[start] = 0;
            visited[start] = true;

            var queue = new Queue<int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (var (nb, pid) in adj[cur])
                {
                    if (visited[nb]) continue; // KRİTİK: geri-sekmeyi (parent↔child) engeller, sonsuz döngüyü önler
                    visited[nb] = true;
                    dist[nb] = dist[cur] + (pipeLengths.TryGetValue(pid, out double pl) ? pl : 0);
                    prev[nb] = cur;
                    prevPipe[nb] = pid;
                    queue.Enqueue(nb);
                }
            }
            return (dist, prev, prevPipe);
        }

        double maxLen = 0;
        List<Guid> bestPath = [];
        var globallyVisited = new bool[nodeCount];

        for (int compStart = 0; compStart < nodeCount; compStart++)
        {
            if (globallyVisited[compStart]) continue;

            var comp = new HashSet<int>();
            BFS(compStart, adj, globallyVisited, comp);
            if (comp.Count < 2) continue; // Tek düğümlük bileşende yol yok

            // 1. Süpürme: bileşendeki herhangi bir düğümden en uzağı bul.
            var (dist1, _, _) = RunLongestPath(compStart);
            int farthestFromStart = comp.OrderByDescending(n => dist1[n]).First();

            // 2. Süpürme: bulunan uçtan en uzağı bul — bileşenin gerçek çapı budur.
            var (dist2, prev2, prevPipe2) = RunLongestPath(farthestFromStart);
            int endNode = comp.OrderByDescending(n => dist2[n]).First();

            if (dist2[endNode] > maxLen)
            {
                maxLen = dist2[endNode];
                var path = new List<Guid>();
                int cur = endNode;
                while (prev2[cur] != -1) { path.Add(prevPipe2[cur]); cur = prev2[cur]; }
                bestPath = path;
            }
        }

        return (maxLen, bestPath);
    }
}
