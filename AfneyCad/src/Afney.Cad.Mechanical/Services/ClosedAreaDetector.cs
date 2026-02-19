using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Domain.Entities.Basic;

namespace Afney.Cad.Mechanical.Services;

/*
NE: Kapalı Alan Tespit Servisi (ClosedAreaDetector)
NEDEN: Mimari plandaki duvar çizgilerinden odaları (mahalleri) otomatik tespit etmek için.

MÜHENDİSLİK DETAYI (FineSANI Yaklaşımı):
- Çizgileri bir Graph (Düğüm-Kenar) yapısına dönüştürür.
- Cycle Detection algoritması ile kapalı döngüleri bulur.
- Bulunan döngünün alanını hesaplayıp 'RoomEntity' oluşturulmasına olanak tanır.
*/
public class ClosedAreaDetector
{
    private class Node
    {
        public Vector3D Position { get; }
        public List<Node> Neighbors { get; } = new();

        public Node(Vector3D pos) => Position = pos;
    }

    /*
    NE: Verilen Entity Listesinden Kapalı Alanları Bulur
    */
    /*
    NE: Kapalı Alanları Tespit Et (FindClosedAreas)
    NEDEN: Verilen çizim nesneleri (LineEntity) içindeki uç uca birleşmiş tüm kapalı döngüleri tespit ederek, mimari kat planındaki odaları (mahalleri) otomatik tanımlamak için.
    */
    public List<List<Vector3D>> FindClosedAreas(IEnumerable<CadEntity> entities)
    {
        // 1. Sadece çizgileri al (LineEntity)
        var lines = entities.OfType<LineEntity>().ToList();
        if (!lines.Any()) return new List<List<Vector3D>>();

        // 2. Graph oluştur
        var nodes = BuildGraph(lines);

        // 3. Döngüleri bul (Basitleştirilmiş Cycle Detection)
        // NOT: Tam kapsamlı bir Planar Graph Face detection algoritması çok karmaşıktır.
        // Burada "Minimum Cycle Basis" yaklaşımının basitleştirilmiş halini kullanacağız.
        var cycles = FindCycles(nodes);

        return cycles;
    }

    private List<Node> BuildGraph(List<LineEntity> lines)
    {
        var nodeMap = new Dictionary<string, Node>();

        Node GetOrCreateNode(Vector3D pos)
        {
            // Hassasiyet toleransı (1mm)
            string key = $"{Math.Round(pos.X, 0)}_{Math.Round(pos.Y, 0)}";
            if (!nodeMap.TryGetValue(key, out var node))
            {
                node = new Node(pos);
                nodeMap[key] = node;
            }
            return node;
        }

        foreach (var line in lines)
        {
            var n1 = GetOrCreateNode(line.StartPoint);
            var n2 = GetOrCreateNode(line.EndPoint);

            if (!n1.Neighbors.Contains(n2)) n1.Neighbors.Add(n2);
            if (!n2.Neighbors.Contains(n1)) n2.Neighbors.Add(n1);
        }

        return nodeMap.Values.ToList();
    }

    private List<List<Vector3D>> FindCycles(List<Node> nodes)
    {
        var cycles = new List<List<Vector3D>>();

        // Iterative DFS ile döngü arama (StackOverflow Korumalı)
        // Her düğümden başlayarak kısa döngüleri (odaları) arıyoruz.
        foreach (var startNode in nodes)
        {
            var stack = new Stack<(Node current, List<Node> path)>();
            stack.Push((startNode, new List<Node> { startNode }));

            while (stack.Count > 0)
            {
                var (current, path) = stack.Pop();

                // Derinlik sınırı (Oda kenar sayısı genelde 4-8 arasıdır, 12 güvenli sınır)
                if (path.Count > 12) continue;

                foreach (var neighbor in current.Neighbors)
                {
                    // Başlangıç düğümüne geri döndük mü?
                    if (neighbor == startNode && path.Count > 2)
                    {
                        var points = path.Select(n => n.Position).ToList();
                        
                        // Alan kontrolü (Çok küçük hatalı döngüleri ele)
                        // 100000 mm² = 0.1 m²
                        if (CalculatePolygonArea(points) > 100000) 
                        {
                            // Aynı odayı tekrar eklememek için kontrol (A-B-C-D vs B-C-D-A)
                            if (!cycles.Any(c => IsSameCycle(c, points)))
                            {
                                cycles.Add(points);
                            }
                        }
                        continue;
                    }

                    // Henüz ziyaret edilmediyse yola ekle ve devam et
                    if (!path.Contains(neighbor))
                    {
                        var newPath = new List<Node>(path) { neighbor };
                        stack.Push((neighbor, newPath));
                    }
                }
            }
        }

        return cycles;
    }

    private bool IsSameCycle(List<Vector3D> c1, List<Vector3D> c2)
    {
        if (c1.Count != c2.Count) return false;
        // Alanları aynıysa büyük ihtimalle aynı odadır (Centroid kontrolü daha kesin olur ama pahalı)
        double a1 = CalculatePolygonArea(c1);
        double a2 = CalculatePolygonArea(c2);
        // Merkez noktalarının yakınlığına da bakılabilir
        return Math.Abs(a1 - a2) < 100.0; // 1cm² tolerans
    }


    /*
    NE: Polygon Alan Hesabı (Shoelace Formula)
    */
    private double CalculatePolygonArea(List<Vector3D> points)
    {
        double area = 0.0;
        int j = points.Count - 1;

        for (int i = 0; i < points.Count; i++)
        {
            area += (points[j].X + points[i].X) * (points[j].Y - points[i].Y);
            j = i;
        }

        return Math.Abs(area / 2.0);
    }
}
