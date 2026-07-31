using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services
{
    /*
        NE: Otonom Mahal Tespit Motoru (Wall-to-Space Engine)
        NEDEN: Mevcut DWG planındaki tüm çizgileri okuyarak, Planar Graph (Düzlemsel Ağ) 
               yöntemiyle tüm kapalı odaları otonom olarak bulmak için.
    */
    public class SpaceDetectionEngine
    {
        private readonly CadDatabase _database;
        private const double MergeTolerance = 5.0; // mm (5mm altı noktalar birleşir)

        public SpaceDetectionEngine(CadDatabase database)
        {
            _database = database;
        }

        /*
           NE: Nokta Havuzu — Grid-Hash Tabanlı Dedup (NodePool)
           NEDEN: GroupIntoConnectedComponents ve ExtractPlanarFaces'te aynı "en yakın mevcut
                  düğümü bul, yoksa ekle" mantığı ayrı ayrı LİNEER taramayla yazılmıştı — her
                  segment ucu için nodes.Count kadar mesafe hesabı, yani toplamda O(n²). Birkaç
                  bin duvar/kapı/pencere segmenti içeren gerçekçi bir çok katlı planda Otonom
                  mahal tespitini (`OnAutoDetectSpacesCommand`) gözle görülür şekilde
                  yavaşlatıyordu. Çözüm: MergeTolerance boyutunda hücrelere göre grid-hash —
                  arama sadece noktanın bulunduğu 3x3 komşu hücreyle sınırlanıyor, ortalama
                  O(1) ekleme/arama sağlanıyor. İki yerdeki tekrarlı mantık burada birleştirildi.
        */
        private sealed class NodePool
        {
            private readonly double _tolerance;
            private readonly Dictionary<(long, long), List<int>> _grid = new();
            public List<Vector3D> Nodes { get; } = new();

            public NodePool(double tolerance) { _tolerance = tolerance; }

            private (long, long) CellOf(Vector3D p) =>
                ((long)Math.Floor(p.X / _tolerance), (long)Math.Floor(p.Y / _tolerance));

            public int GetOrAdd(Vector3D p)
            {
                var (cx, cy) = CellOf(p);
                for (long dx = -1; dx <= 1; dx++)
                    for (long dy = -1; dy <= 1; dy++)
                        if (_grid.TryGetValue((cx + dx, cy + dy), out var indices))
                            foreach (var idx in indices)
                                if (Nodes[idx].DistanceTo(p) <= _tolerance) return idx;

                Nodes.Add(p);
                int newIndex = Nodes.Count - 1;
                if (!_grid.TryGetValue((cx, cy), out var list))
                {
                    list = new List<int>();
                    _grid[(cx, cy)] = list;
                }
                list.Add(newIndex);
                return newIndex;
            }
        }

        /*
           NE: Ana Tetikleyici Fonksiyon
           NEDEN: Tüm işlem adımlarını sırayla koşturup RoomEntity'leri üretmek için.
        */
        public List<List<Vector3D>> DetectAllSpaces()
        {
            Serilog.Log.Information("[SpaceDetectionEngine] Otonom Mahal Tespiti Başlıyor...");

            // 1. Çizgileri Topla ve Segmentlere Ayır
            var segments = ExtractSegments();
            Serilog.Log.Information("[SpaceDetectionEngine] Bulunan ham segment sayısı: {Count}", segments.Count);

            if (segments.Count == 0) return new List<List<Vector3D>>();

            // 2. Kesişimleri Çöz (Intersection Resolving)
            // Birbiri üzerinden geçen T veya X şeklindeki duvarları tam kesim noktalarından böl
            var splitSegments = ResolveIntersections(segments);
            Serilog.Log.Information("[SpaceDetectionEngine] Kesişim sonrası segment sayısı: {Count}", splitSegments.Count);

            // GERÇEK HATA (kullanıcı bildirdi — birden fazla kat aynı dosyada, yan yana/farklı
            // X,Y bölgelerinde çizili): Eskiden TÜM duvarlar TEK bir düzlemsel grafa besleniyor,
            // `FilterOuterBoundary` de sadece TEK bir GLOBAL en büyük poligonu "dış kabuk"
            // sayıp eliyordu. Ama fiziksel olarak BAĞLANTISIZ duvar adaları (ör. bodrum planı
            // ile çatı planı birbirine değmiyor) varsa, HER adanın KENDİ dış kabuğu vardır —
            // eski kod sadece BİRİNİ eliyor, diğer katların dış hatları da yanlışlıkla "oda"
            // olarak (o katın toplam alanı kadar, dev bir "Oda_N") ekleniyordu. Çözüm: önce
            // duvar ağını BAĞLANTILI BİLEŞENLERE (her biri fiziksel olarak ayrı bir "ada" —
            // pratikte ayrı bir kat/bina planı) ayır, planar-face + dış-kabuk-eleme işlemini
            // HER bileşen için AYRI AYRI çalıştır.
            var components = GroupIntoConnectedComponents(splitSegments);
            Serilog.Log.Information("[SpaceDetectionEngine] Bağlantılı duvar bileşeni (ayrı kat/bina adası) sayısı: {Count}", components.Count);

            var validRooms = new List<List<Vector3D>>();
            foreach (var componentSegments in components)
            {
                var rooms = ExtractPlanarFaces(componentSegments);
                var filtered = FilterOuterBoundary(rooms);
                validRooms.AddRange(filtered);
            }
            Serilog.Log.Information("[SpaceDetectionEngine] Dış kabuklar (bileşen bazında) filtrelendi, kalan geçerli oda: {Count}", validRooms.Count);

            return validRooms;
        }

        /*
           NE: Bağlantılı Bileşenlere Ayırma (GroupIntoConnectedComponents)
           NEDEN: Union-Find ile, ortak köşe (vertex) paylaşan segmentleri aynı gruba toplar —
                  fiziksel olarak birbirine DEĞMEYEN duvar ağları (ör. sayfada uzak bir bölgeye
                  çizilmiş farklı bir kat planı) ayrı gruplara düşer.
        */
        private List<List<(Vector3D P1, Vector3D P2)>> GroupIntoConnectedComponents(List<(Vector3D P1, Vector3D P2)> segments)
        {
            var pool = new NodePool(MergeTolerance);

            var segNodeIndices = new List<(int A, int B)>(segments.Count);
            foreach (var seg in segments)
                segNodeIndices.Add((pool.GetOrAdd(seg.P1), pool.GetOrAdd(seg.P2)));

            var parent = new int[pool.Nodes.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            void Union(int a, int b)
            {
                a = Find(a); b = Find(b);
                if (a != b) parent[a] = b;
            }

            foreach (var (a, b) in segNodeIndices) Union(a, b);

            var groups = new Dictionary<int, List<(Vector3D, Vector3D)>>();
            for (int i = 0; i < segments.Count; i++)
            {
                int root = Find(segNodeIndices[i].A);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<(Vector3D, Vector3D)>();
                    groups[root] = list;
                }
                list.Add(segments[i]);
            }

            return groups.Values.ToList();
        }

        /*
           ADIM 1: Çizgileri Parçala
        */
        // Duvar olarak kabul edilen katmanlar
        private static readonly HashSet<string> WallLayerKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "WALL", "DUVAR", "A-WALL", "ARCH-WALL", "S-WALL", "STRUCTURAL",
            "KOLON", "COLUMN", "PARTITION", "BÖLME", "0"
        };

        private bool IsWallLayer(string? layer)
        {
            if (string.IsNullOrEmpty(layer)) return true; // Layer yoksa dahil et
            foreach (var keyword in WallLayerKeywords)
            {
                if (layer.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private List<(Vector3D P1, Vector3D P2)> ExtractSegments()
        {
            var segs = new List<(Vector3D, Vector3D)>();
            foreach (var ent in _database.GetAllEntities())
            {
                // Layer filtresi — sadece duvar/mimari katmanları
                if (!IsWallLayer(ent.Layer)) continue;

                if (ent is LineEntity line && line.StartPoint.DistanceTo(line.EndPoint) > MergeTolerance)
                {
                    segs.Add((line.StartPoint, line.EndPoint));
                }
                else if (ent is LwPolylineEntity poly && poly.Vertices.Count >= 2)
                {
                    for (int i = 0; i < poly.Vertices.Count - 1; i++)
                    {
                        if (poly.Vertices[i].DistanceTo(poly.Vertices[i + 1]) > MergeTolerance)
                            segs.Add((poly.Vertices[i], poly.Vertices[i + 1]));
                    }
                    if (poly.IsClosed && poly.Vertices.Count > 2)
                    {
                        if (poly.Vertices[poly.Vertices.Count - 1].DistanceTo(poly.Vertices[0]) > MergeTolerance)
                            segs.Add((poly.Vertices[poly.Vertices.Count - 1], poly.Vertices[0]));
                    }
                }
                // Eğrisel duvar desteği — Arc entity'lerini segmentlere böl
                else if (ent is ArcEntity arc)
                {
                    var arcSegs = ArcToSegments(arc, 12); // 12 segment yaklaşımı
                    segs.AddRange(arcSegs);
                }
            }
            return segs;
        }

        // Arc → doğrusal segment yaklaşımı (tessellation)
        private List<(Vector3D P1, Vector3D P2)> ArcToSegments(ArcEntity arc, int segmentCount)
        {
            var segs = new List<(Vector3D, Vector3D)>();
            double startAngle = arc.StartAngle;
            double endAngle = arc.EndAngle;
            if (endAngle < startAngle) endAngle += 2 * Math.PI;
            double step = (endAngle - startAngle) / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                double a1 = startAngle + i * step;
                double a2 = startAngle + (i + 1) * step;
                var p1 = new Vector3D(arc.Center.X + arc.Radius * Math.Cos(a1), arc.Center.Y + arc.Radius * Math.Sin(a1), 0);
                var p2 = new Vector3D(arc.Center.X + arc.Radius * Math.Cos(a2), arc.Center.Y + arc.Radius * Math.Sin(a2), 0);
                if (p1.DistanceTo(p2) > MergeTolerance) segs.Add((p1, p2));
            }
            return segs;
        }

        // Oda içindeki text entity'lerden oda adı çıkarma
        public string? DetectRoomNameFromTexts(List<Vector3D> roomBoundary)
        {
            foreach (var ent in _database.GetAllEntities())
            {
                if (ent is TextEntity text && !string.IsNullOrWhiteSpace(text.Text))
                {
                    if (IsPointInPolygon(text.Position, roomBoundary))
                    {
                        string t = text.Text.Trim();
                        // Kısa kodlar ve sayıları atla (oda adı en az 2 karakter olmalı)
                        if (t.Length >= 2 && !double.TryParse(t, out _))
                            return t;
                    }
                }
            }
            return null;
        }

        private bool IsPointInPolygon(Vector3D p, List<Vector3D> polygon)
        {
            bool inside = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                if (((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)) &&
                    (p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
                j = i;
            }
            return inside;
        }

        // Alan hesabı (Gauss/Shoelace) — m²
        public static double CalculateAreaM2(List<Vector3D> polygon)
        {
            return Math.Abs(ComputeSignedArea(polygon)) / 1_000_000.0; // mm² → m²
        }

        // Çevre hesabı (m)
        public static double CalculatePerimeterM(List<Vector3D> polygon)
        {
            double perimeter = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                perimeter += polygon[i].DistanceTo(polygon[(i + 1) % polygon.Count]);
            }
            return perimeter / 1000.0; // mm → m
        }

        /*
           NE: Segment Aday Izgarası (SegmentGrid)
           NEDEN: Darboğaz denetimi + araştırma ajanı bulgusu — `ResolveIntersections`'ın iç
                  döngüsü segA'yı KALAN HER segB ile (O(n²) çift) test ediyordu; bugün eklenen
                  AABB ön-filtresi (bkz. `BoundingBoxesOverlap`) her çiftin maliyetini düşürdü
                  ama ÇİFT SAYISINI değiştirmedi. Tam bir Bentley-Ottmann sweep-line yeniden
                  yazımı (araştırıldı — bkz. Kullanici_kitabi.md Session #55) bu kod tabanı için
                  YÜKSEK RİSK/EFOR, DÜŞÜK EK KAZANÇ bulundu: gerçek kazanç, adayları bir grid-hash
                  ile daraltıp sadece YAKINDAKİ segment çiftlerini denemekten geliyor — mevcut
                  "pass içinde split, aynı sırada test et" mutasyon davranışını (satır ~347-351)
                  HİÇ bozmadan. Bu sınıf tam olarak bunu yapar: segment index'lerini AABB'lerinin
                  kapladığı hücrelere göre saklar, `CandidatesAbove` sadece segA'nın yakın
                  hücrelerindeki (ve index > i olan) adayları döndürür.
        */
        private sealed class SegmentGrid
        {
            private readonly double _cellSize;
            private readonly Dictionary<(long, long), List<int>> _cells = new();

            public SegmentGrid(double cellSize) => _cellSize = Math.Max(cellSize, 1.0);

            private (long MinX, long MinY, long MaxX, long MaxY) CellRange((Vector3D P1, Vector3D P2) seg)
            {
                double minX = Math.Min(seg.P1.X, seg.P2.X), maxX = Math.Max(seg.P1.X, seg.P2.X);
                double minY = Math.Min(seg.P1.Y, seg.P2.Y), maxY = Math.Max(seg.P1.Y, seg.P2.Y);
                return ((long)Math.Floor(minX / _cellSize), (long)Math.Floor(minY / _cellSize),
                        (long)Math.Floor(maxX / _cellSize), (long)Math.Floor(maxY / _cellSize));
            }

            public void Index(int idx, (Vector3D P1, Vector3D P2) seg)
            {
                var (minX, minY, maxX, maxY) = CellRange(seg);
                for (long cx = minX; cx <= maxX; cx++)
                    for (long cy = minY; cy <= maxY; cy++)
                    {
                        if (!_cells.TryGetValue((cx, cy), out var list))
                        {
                            list = new List<int>();
                            _cells[(cx, cy)] = list;
                        }
                        list.Add(idx);
                    }
            }

            public void Rebuild(List<(Vector3D P1, Vector3D P2)> segments)
            {
                _cells.Clear();
                for (int i = 0; i < segments.Count; i++) Index(i, segments[i]);
            }

            /// <summary>segA'nın hücrelerindeki, index'i minExclusiveIndex'ten büyük aday segment index'lerini (artan sırada, tekrarsız) döndürür.</summary>
            public List<int> CandidatesAbove(int minExclusiveIndex, (Vector3D P1, Vector3D P2) seg)
            {
                var (minX, minY, maxX, maxY) = CellRange(seg);
                var found = new SortedSet<int>();
                for (long cx = minX; cx <= maxX; cx++)
                    for (long cy = minY; cy <= maxY; cy++)
                        if (_cells.TryGetValue((cx, cy), out var list))
                            foreach (var idx in list)
                                if (idx > minExclusiveIndex) found.Add(idx);
                return found.ToList();
            }
        }

        /*
           ADIM 2: Kesişimleri Çöz (Intersection Resolving)
           Tüm segmentleri birbiriyle test edip kesiştiklerinde böler (Split).
        */
        private List<(Vector3D P1, Vector3D P2)> ResolveIntersections(List<(Vector3D P1, Vector3D P2)> segments)
        {
            var result = new List<(Vector3D P1, Vector3D P2)>(segments);
            bool newlySplit = true;
            int limit = 5; // Sonsuz döngü koruması

            while (newlySplit && limit > 0)
            {
                newlySplit = false;
                limit--;
                var nextPass = new List<(Vector3D P1, Vector3D P2)>();

                /*
                   MÜHENDİSLİK: Hücre boyutu MergeTolerance'ın çok üstünde, tipik duvar
                   uzunluğu mertebesinde sabit bir değer — gerçek kat planlarında (duvarlar
                   genelde birkaç yüz - birkaç bin mm) hücre başına makul (küçük, sabit)
                   sayıda segment düşürüp aday sayısını n²'den n'e yakın bir şeye indiriyor.
                */
                const double CellSize = 3000.0;
                var grid = new SegmentGrid(CellSize);
                grid.Rebuild(result);

                for (int i = 0; i < result.Count; i++)
                {
                    var segA = result[i];
                    bool wasSplitThisPass = false;

                    // NOT: Aday listesi bu i için BİR KEZ materyalize ediliyor — güvenli, çünkü
                    // bir split bulunur bulunmaz hemen `break` ile bu i'nin taramasından
                    // çıkılıyor (aşağıdaki grid.Index çağrısı SONRAKİ i'ler için geçerli olur).
                    foreach (int j in grid.CandidatesAbove(i, segA))
                    {
                        if (j >= result.Count) continue; // savunma: index kaymışsa atla (olmamalı)
                        var segB = result[j];

                        if (!BoundingBoxesOverlap(segA, segB, MergeTolerance)) continue;

                        var intersection = GetIntersection(segA.P1, segA.P2, segB.P1, segB.P2);

                        if (intersection.HasValue)
                        {
                            // Kesişim noktasının uçlara olan uzaklığı toleranstan büyükse gerçekten kesişiyordur
                            bool splitA = intersection.Value.DistanceTo(segA.P1) > MergeTolerance && intersection.Value.DistanceTo(segA.P2) > MergeTolerance;
                            bool splitB = intersection.Value.DistanceTo(segB.P1) > MergeTolerance && intersection.Value.DistanceTo(segB.P2) > MergeTolerance;

                            if (splitA || splitB)
                            {
                                // A yı böl
                                if (splitA)
                                {
                                    nextPass.Add((segA.P1, intersection.Value));
                                    nextPass.Add((intersection.Value, segA.P2));
                                }
                                else
                                {
                                    nextPass.Add(segA);
                                }

                                // B yi böl ve orijinal listemden çıkar (sonraki turlarda hata yapmamak için)
                                if (splitB)
                                {
                                    result[j] = (segB.P1, intersection.Value); // B'nin ilk yarısı
                                    var secondHalf = (intersection.Value, segB.P2);
                                    result.Add(secondHalf); // B'nin ikinci yarısını sıraya ekle
                                    grid.Index(result.Count - 1, secondHalf); // yeni parçayı grid'e de ekle
                                }

                                wasSplitThisPass = true;
                                newlySplit = true;
                                break; // segA 'yı böldüysek bu i döngüsünden çıkıp baştan test etmeliyiz
                            }
                        }
                    }

                    if (!wasSplitThisPass)
                    {
                        nextPass.Add(segA);
                    }
                }
                result = nextPass;
            }

            return result;
        }

        /*
           NE: Segment Çiftinin AABB'leri Çakışıyor mu? (BoundingBoxesOverlap)
           NEDEN: ResolveIntersections'daki O(n²) çift taramasında pahalı (bölmeli)
                  GetIntersection çağrısına girmeden önce ucuz bir erken-çıkış sağlar —
                  iki segmentin eksen-hizalı sınırlayıcı kutuları çakışmıyorsa kesişmeleri
                  matematiksel olarak imkansızdır. `tolerance` payı, ResolveIntersections'ın
                  kendisinin de MergeTolerance dahilindeki "neredeyse değen" uçları kesişim
                  saydığı toleransla tutarlı tutuluyor.
        */
        private static bool BoundingBoxesOverlap((Vector3D P1, Vector3D P2) a, (Vector3D P1, Vector3D P2) b, double tolerance)
        {
            double aMinX = Math.Min(a.P1.X, a.P2.X) - tolerance, aMaxX = Math.Max(a.P1.X, a.P2.X) + tolerance;
            double aMinY = Math.Min(a.P1.Y, a.P2.Y) - tolerance, aMaxY = Math.Max(a.P1.Y, a.P2.Y) + tolerance;
            double bMinX = Math.Min(b.P1.X, b.P2.X), bMaxX = Math.Max(b.P1.X, b.P2.X);
            double bMinY = Math.Min(b.P1.Y, b.P2.Y), bMaxY = Math.Max(b.P1.Y, b.P2.Y);

            return aMinX <= bMaxX && aMaxX >= bMinX && aMinY <= bMaxY && aMaxY >= bMinY;
        }

        /*
            Çizgi-Çizgi Kesişimi (X,Y düzleminde)
        */
        private Vector3D? GetIntersection(Vector3D p1, Vector3D p2, Vector3D p3, Vector3D p4)
        {
            double denominator = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);
            if (Math.Abs(denominator) < 1e-9) return null; // Paralel

            double ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / denominator;
            double ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / denominator;

            if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
            {
                double x = p1.X + ua * (p2.X - p1.X);
                double y = p1.Y + ua * (p2.Y - p1.Y);
                return new Vector3D(x, y, 0);
            }
            return null;
        }

        /*
           ADIM 3: Planar Graph & Face Detection (Düzlemsel Yüzey Çıkarma)
           "En Sola Dönüş" (Left-Most Turn) logiğini baz alan minimum cycle ağ tespiti.
        */
        private List<List<Vector3D>> ExtractPlanarFaces(List<(Vector3D P1, Vector3D P2)> segments)
        {
            // 1. Düğümü (Vertex) Havuzunu ve Komşulukları (Adjacency List) Oluştur
            var pool = new NodePool(MergeTolerance);
            var adjacency = new Dictionary<int, List<int>>();

            foreach (var seg in segments) {
                int n1 = pool.GetOrAdd(seg.P1);
                int n2 = pool.GetOrAdd(seg.P2);
                if (!adjacency.ContainsKey(n1)) adjacency[n1] = new List<int>();
                if (!adjacency.ContainsKey(n2)) adjacency[n2] = new List<int>();
                if (n1 != n2) {
                    if (!adjacency[n1].Contains(n2)) adjacency[n1].Add(n2);
                    if (!adjacency[n2].Contains(n1)) adjacency[n2].Add(n1);
                }
            }

            var nodes = pool.Nodes;

            // 2. Yönlendirilmiş Kenarları (Directed Edges) Oluştur ve Açıya Göre Sırala
            var sortedNeighbors = new Dictionary<int, List<int>>();
            foreach (var kvp in adjacency) {
                int u = kvp.Key;
                Vector3D pU = nodes[u];
                var sorted = kvp.Value.OrderBy(v => {
                    Vector3D pV = nodes[v];
                    return Math.Atan2(pV.Y - pU.Y, pV.X - pU.X);
                }).ToList();
                sortedNeighbors[u] = sorted;
            }

            // 3. Yüzeyleri (Faces) Bulmak İçin Gezin (Traverse)
            var visitedEdges = new HashSet<(int, int)>();
            var faces = new List<List<Vector3D>>();

            foreach (var u in sortedNeighbors.Keys) {
                foreach (var v in sortedNeighbors[u]) {
                    if (visitedEdges.Contains((u, v))) continue;
                    
                    var faceIndices = new List<int>();
                    int curr = u;
                    int next = v;
                    
                    bool validFace = true;
                    
                    while (!visitedEdges.Contains((curr, next))) {
                        visitedEdges.Add((curr, next));
                        faceIndices.Add(curr);
                        
                        // "next" üzerinden devam edecek en sağ/sol dönüşlü komşuyu bul
                        var neighborsOfNext = sortedNeighbors[next];
                        int incomingIdx = neighborsOfNext.IndexOf(curr);
                        
                        if (incomingIdx == -1) // Olmaması gerekir
                        {
                            validFace = false; break;
                        }
                        
                        // Sonraki CCW kenar
                        int nextOutIdx = (incomingIdx + 1) % neighborsOfNext.Count;
                        int nextNext = neighborsOfNext[nextOutIdx];
                        
                        curr = next;
                        next = nextNext;
                    }
                    
                    if (validFace && faceIndices.Count >= 3) {
                        int loopStart = faceIndices.IndexOf(curr);
                        if (loopStart != -1) {
                            var loopIndices = faceIndices.Skip(loopStart).ToList();
                            if (loopIndices.Count >= 3) {
                                var loopPoints = loopIndices.Select(i => nodes[i]).ToList();
                                faces.Add(loopPoints);
                            }
                        }
                    }
                }
            }

            return faces;
        }

        /*
           ADIM 4: Outer Boundary Filtering (Binanın dış kabuğunu ele)
           Kapalı döngüler içinden en büyüğünü dış kabuk olarak varsayıp siler.
        */
        /*
           GERÇEK HATA (kullanıcı gerçek bir projede test etti — 1202 "oda" bulundu, 6 katlık
           bir planda bu açıkça yanlıştı): bu metodun küçük-poligon eleme eşiği `faceAreas[i]
           > 1.0` idi — kodun HER YERİNDE koordinatlar mm cinsinden tutuluyor (bkz. MahalEntity.
           CalculateGeometry: `Area = ... / 1_000_000.0 // mm² → m²`), yani bu eşik pratikte
           1mm² (bir toplu iğne başından küçük) — neredeyse HİÇBİR kapalı döngüyü elemiyordu.
           Mobilya sembolleri, sıhhi tesisat armatür çizimleri, kapı/pencere kanat yayları,
           merdiven basamakları gibi mimari OLMAYAN ama kapalı olan binlerce küçük şekil
           "oda" olarak tespit ediliyordu. Çözüm: gerçekçi bir minimum oda alanı (0.25 m² —
           en küçük gerçekçi pano/tesisat şaftından bile küçük, güvenli bir alt sınır).
        */
        private const double MinValidRoomAreaMm2 = 250_000.0; // 0.25 m²

        private List<List<Vector3D>> FilterOuterBoundary(List<List<Vector3D>> rawFaces)
        {
            var validFaces = new List<List<Vector3D>>();
            if (rawFaces.Count == 0) return validFaces;

            double maxArea = -1;
            int maxIdx = -1;
            var faceAreas = new List<double>();

            for(int i = 0; i < rawFaces.Count; i++) {
                double area = Math.Abs(ComputeSignedArea(rawFaces[i]));
                faceAreas.Add(area);
                if (area > maxArea) {
                    maxArea = area;
                    maxIdx = i;
                }
            }

            for(int i = 0; i < rawFaces.Count; i++) {
                // En büyük alanı taşıyan poligon dış duvardır, eyle
                if (i == maxIdx) continue;

                // Gerçekçi minimum oda alanı altındaki (mobilya/sembol/detay kapalı şekilleri) at
                if (faceAreas[i] > MinValidRoomAreaMm2) {
                    validFaces.Add(rawFaces[i]);
                }
            }

            return validFaces;
        }

        /*
            Gauss (Shoelace) yöntemi ile poligon alanı
        */
        private static double ComputeSignedArea(List<Vector3D> poly)
        {
            int n = poly.Count;
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                area += (a.X * b.Y) - (b.X * a.Y);
            }
            return area / 2.0;
        }
    }
}
