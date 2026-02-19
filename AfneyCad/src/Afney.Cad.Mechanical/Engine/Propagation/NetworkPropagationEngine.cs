using Afney.Cad.Mechanical.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Engine.Propagation;

/*
    NE: Şebeke Yayılım Motoru (NetworkPropagationEngine)
    NEDEN: FINE SANI / 4M standardında, bir uç noktadaki (fixture) yük birimi veya debi değiştiğinde, tüm boru hattındaki değerlerin otomatik güncellenmesi için.
    
    ALGORİTMA: Dependency Graph & Topological Sort (DFS)
    1. Musluklardan (fixtures) başlayarak toplayıcıya (meter/collector) doğru debiyi toplar.
    2. Boru çaplarını bu toplam debiye göre anlık günceller.
    3. Hız ve basınç kayıplarını yeniden hesaplar.
*/
public class NetworkPropagationEngine
{
    private readonly MechanicalTopologyGraph _graph;

    public NetworkPropagationEngine(MechanicalTopologyGraph graph)
    {
        _graph = graph;
    }

    /*
        NE: Debiyi Güncelle (PropagateFlow)
        AMACI: Tüm ağdaki debi (flow) ve yük birimi (LU) değerlerini hiyerarşik olarak yeniden hesaplamak.
    */
    public void PropagateFlow()
    {
        // 1. Tüm muslukları bulun
        var fixtures = _graph.Nodes.Where(n => n.Entity is SanitaryFixtureEntity).ToList();
        
        // 2. Musluklardan başlayarak yukarı doğru çık (Topological Traversal)
        // Şimdilik basitleştirilmiş bir yukarı doğru toplama (recursive) yapıyoruz.
        var processedPipes = new HashSet<Guid>();
        
        foreach (var fixtureNode in fixtures)
        {
            var fixture = (SanitaryFixtureEntity)fixtureNode.Entity;
            double loadUnits = fixture.LoadUnits;

            // Bu musluğun bağlı olduğu borudan başla
            var neighbors = fixtureNode.GetNeighbors(_graph);
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Entity is PipeEntity pipe)
                {
                    PropagateRecursive(pipe, loadUnits, processedPipes);
                }
            }
        }
    }

    private void PropagateRecursive(PipeEntity pipe, double luToAdd, HashSet<Guid> processed)
    {
        // NOT: LU (Load Units) kümülatif toplanır.
        // Amaç: Ana hattan geçen toplam yükü bulmak.
        
        // Bu boruya yük ekle (Basit toplama, gerçekte eş zamanlılık faktörü uygulanır)
        pipe.LoadUnits += luToAdd;
        
        // Debiyi yeniden hesapla (Örn: Q = 0.25 * sqrt(LU))
        pipe.FlowRate = 0.25 * Math.Sqrt(pipe.LoadUnits);

        // Bir sonraki bağlı boruyu bul (Collectora doğru giden)
        var node = _graph.GetNode(pipe.Id);
        if (node == null) return;

        var nextNeighbors = node.GetNeighbors(_graph);
        foreach (var neighbor in nextNeighbors)
        {
            if (neighbor.Entity is PipeEntity nextPipe && !processed.Contains(nextPipe.Id))
            {
                // processed.Add(nextPipe.Id); // Döngü koruması
                PropagateRecursive(nextPipe, luToAdd, processed);
            }
        }
    }

    /*
        NE: Çap Kontrolü (Constraint Solver)
        AMACI: Borudaki debiye göre çapın yetersiz kalıp kalmadığını kontrol eder.
    */
    public void SolveConstraints()
    {
        var pipes = _graph.Nodes.Select(n => n.Entity).OfType<PipeEntity>();
        foreach (var pipe in pipes)
        {
            // Velocity constraint: v < 2.0 m/s
            if (pipe.GetVelocity() > 2.0)
            {
                // Çapı bir boy büyüt (Örn: DN 20 -> DN 25)
                // Bu kısım Standart kütüphanesi ile entegre edilmelidir.
            }
        }
    }
}
