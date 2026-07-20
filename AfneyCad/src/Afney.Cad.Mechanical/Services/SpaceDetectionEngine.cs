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
            var nodes = new List<Vector3D>();
            int GetOrAddNode(Vector3D p)
            {
                for (int i = 0; i < nodes.Count; i++)
                    if (nodes[i].DistanceTo(p) <= MergeTolerance) return i;
                nodes.Add(p);
                return nodes.Count - 1;
            }

            var segNodeIndices = new List<(int A, int B)>(segments.Count);
            foreach (var seg in segments)
                segNodeIndices.Add((GetOrAddNode(seg.P1), GetOrAddNode(seg.P2)));

            var parent = new int[nodes.Count];
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

                for (int i = 0; i < result.Count; i++)
                {
                    var segA = result[i];
                    bool wasSplitThisPass = false;

                    for (int j = i + 1; j < result.Count; j++)
                    {
                        var segB = result[j];
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
                                    result.Add((intersection.Value, segB.P2)); // B'nin ikinci yarısını sıraya ekle
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
            var nodes = new List<Vector3D>();
            var adjacency = new Dictionary<int, List<int>>();
            
            int GetOrAddNode(Vector3D p) {
                for(int i = 0; i < nodes.Count; i++) {
                    if (nodes[i].DistanceTo(p) <= MergeTolerance) return i;
                }
                nodes.Add(p);
                adjacency[nodes.Count - 1] = new List<int>();
                return nodes.Count - 1;
            }

            foreach (var seg in segments) {
                int n1 = GetOrAddNode(seg.P1);
                int n2 = GetOrAddNode(seg.P2);
                if (n1 != n2) {
                    if (!adjacency[n1].Contains(n2)) adjacency[n1].Add(n2);
                    if (!adjacency[n2].Contains(n1)) adjacency[n2].Add(n1);
                }
            }

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
