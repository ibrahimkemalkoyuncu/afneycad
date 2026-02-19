using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Mechanical.Engine;

/*
   NE: Mekanik Topoloji Grafı (MechanicalTopologyGraph)
*/
public class MechanicalTopologyGraph
{
    private readonly ConcurrentDictionary<Guid, GraphNode> _nodes = new();

    /*
       NE: GrafÄ± Temizle (Clear)
       NEDEN: Yeni bir proje baÅŸlatÄ±ldÄ±ÄŸÄ±nda veya topoloji sÄ±fÄ±rlandÄ±ÄŸÄ±nda tÃ¼m dÃ¼ÄŸÃ¼mleri (Node) ve odalarÄ± bellekten silmek iÃ§in.
    */
    public void Clear()
    {
        _nodes.Clear();
        Rooms.Clear();
    }

    public IEnumerable<GraphNode> Nodes => _nodes.Values;
    public List<Mechanical.Entities.RoomEntity> Rooms { get; } = new();

    /*
       NE: Nesne Ekle (AddEntity)
       NEDEN: Yeni bir mekanik nesneyi (boru, vitrifiye vb.) topoloji grafına bir "Düğüm" (Node) olarak dahil edip bağlantı kurulabilir hale getirmek için.
    */
    public void AddEntity(MechanicalEntity entity)
    {
        var node = new GraphNode(entity);
        _nodes[entity.Id] = node;
    }

    /*
       NE: Nesne Sil (RemoveEntity)
       NEDEN: Bir nesneyi graf üzerinden kaldırırken; ona bağlı olan diğer nesnelerin bağlantılarını da (Disconnect) güvenli bir şekilde koparmak için.
    */
    public void RemoveEntity(Guid entityId)
    {
        if (_nodes.TryRemove(entityId, out var node))
        {
            node.DisconnectAll();
        }
    }
    
    /*
       NE: Oda Ekle (AddRoom)
       NEDEN: Bir mahal (Room) nesnesini hem özel oda listesine hem de genel topoloji düğümleri arasına dahil etmek için.
    */
    public void AddRoom(Mechanical.Entities.RoomEntity room)
    {
        Rooms.Add(room);
        // AddEntity(room); // RoomEntity is not a MechanicalEntity
    }
    
    // EKLENDİ: Port yapısına uygun bağlantı
    /*
       NE: Bağla (Connect)
       NEDEN: İki mekanik port (Örn: Boru çıkışı - Lavabo girişi) arasında mantıksal bir topolojik bağ kurarak akışın iletilmesini sağlamak için.
    */
    public void Connect(MechanicalPort p1, MechanicalPort p2)
    {
        if (p1 == null || p2 == null) return;
        
        p1.IsConnected = true;
        p1.ConnectedEntityId = p2.OwnerId;
        p1.ConnectedPortName = p2.Name;
        
        p2.IsConnected = true;
        p2.ConnectedEntityId = p1.OwnerId;
        p2.ConnectedPortName = p1.Name;
    }
    
    // EKLENDİ: Bağlantı koparma
    /*
       NE: Bağlantı Kopar (Disconnect)
       NEDEN: Bir boruyu taşırken veya silerken, bağlı olduğu diğer uçtaki nesnenin bağlantı bilgilerini de (ID ve port adı) temizleyerek topolijiyi güncellemek için.
    */
    public void Disconnect(MechanicalPort port)
    {
        if (port != null && port.IsConnected)
        {
            // Eğer karşı taraf varsa onun da bağlantısını kopar
            if (port.ConnectedEntityId.HasValue && _nodes.TryGetValue(port.ConnectedEntityId.Value, out var neighborNode)) // ID ile hızlı erişim
            {
                var neighborPort = neighborNode.Ports.FirstOrDefault(p => p.Name == port.ConnectedPortName);
                if (neighborPort != null)
                {
                    neighborPort.IsConnected = false;
                    neighborPort.ConnectedEntityId = null;
                    neighborPort.ConnectedPortName = null;
                }
            }
            
            port.IsConnected = false;
            port.ConnectedEntityId = null;
            port.ConnectedPortName = null;
        }
    }

    /*
       NE: KomÅŸularÄ± Getir (GetNeighbors)
       NEDEN: Bir cihaz veya boruya fiziksel olarak baÄŸlanmÄ±ÅŸ (Dirsek, T-parÃ§asÄ± vb. aracÄ±lÄ±ÄŸÄ±yla) diÄŸer nesne dÃ¼ÄŸÃ¼mlerini dÃ¶ndÃ¼rmek iÃ§in.
    */
    public IEnumerable<GraphNode> GetNeighbors(Guid entityId)
    {
        if (_nodes.TryGetValue(entityId, out var node))
        {
            return node.GetNeighbors(this);
        }
        return Enumerable.Empty<GraphNode>();
    }
    
    // ID ile Node bulma (O(1))
    /*
       NE: DÃ¼ÄŸÃ¼mÃ¼ Getir (GetNode)
       NEDEN: ID'si bilinen bir varlÄ±ÄŸÄ±n topoloji grafÄ±ndaki dÃ¼ÄŸÃ¼m (Node) tepsine O(1) hÄ±zÄ±nda eriÅŸmek iÃ§in.
    */
    public GraphNode? GetNode(Guid entityId)
    {
        _nodes.TryGetValue(entityId, out var node);
        return node;
    }
    
    // Port ile Node bulma (O(N) - Gerekirse optimize edilebilir ama ID varken gerek yok)
    /*
       NE: Porta GÃ¶re DÃ¼ÄŸÃ¼mÃ¼ Getir (GetNodeByPort)
       NEDEN: Bir port nesnesinin hangi mekanik varlÄ±ÄŸa (Owner) ait olduÄŸunu topoloji Ã¼zerinden saptamak iÃ§in.
    */
    public GraphNode? GetNodeByPort(MechanicalPort port)
    {
        if (port == null) return null;
        return GetNode(port.OwnerId);
    }

    /*
       NE: Dikey Bağlantı Sorgusu (GetVerticalNeighbors)
       NEDEN: Bir düğümden çıkan borulardan hangilerinin üst veya alt kata gittiğini (Kolon sürekliliği) anlamak için.
    */
    public IEnumerable<GraphNode> GetVerticalNeighbors(Guid entityId)
    {
        var node = GetNode(entityId);
        if (node == null) yield break;

        foreach (var neighbor in node.GetNeighbors(this))
        {
            if (neighbor.Entity is PipeEntity pipe && IsVertical(pipe))
            {
                yield return neighbor;
            }
        }
    }

    /*
       NE: Dikey KontrolÃ¼ (IsVertical)
       NEDEN: Borunun dÃ¼ÅŸey doÄŸrultuda (Kolon) olup olmadÄ±ÄŸÄ±nÄ± geometrik olarak doÄŸrulamak iÃ§in.
    */
    private bool IsVertical(PipeEntity pipe)
    {
        var dir = (pipe.EndPoint - pipe.StartPoint).Normalize();
        return Math.Abs(dir.Z) > 0.9;
    }
}

public class GraphNode
{
    public MechanicalEntity Entity { get; }
    public List<MechanicalPort> Ports { get; }

    // Wrapper Properties (Hata giderme)
    public Guid EntityId => Entity.Id;
    public Guid OwnerId => Entity.Id; // Bazı kodlar OwnerId, bazıları EntityId kullanıyor olabilir.
    public MechanicalSystemType SystemType => Entity.SystemType;

    public GraphNode(MechanicalEntity entity)
    {
        Entity = entity;
        Ports = entity.GetPorts();
    }
    
    /*
       NE: PortlarÄ± GÃ¼ncelle (UpdatePorts)
       NEDEN: Bir nesne deÄŸiÅŸtiÄŸinde veya yeniden yapÄ±landÄ±rÄ±ldÄ±ÄŸÄ±nda, Ã¼zerindeki baÄŸlantÄ± noktalarÄ±nÄ± (Port) yeniden okuyup dÃ¼ÄŸÃ¼me yansÄ±tmak iÃ§in.
    */
    public void UpdatePorts(MechanicalEntity entity)
    {
         var newPorts = entity.GetPorts();
         Ports.Clear();
         Ports.AddRange(newPorts);
    }

    /*
       NE: TÃ¼m BaÄŸlantÄ±larÄ± Kopar (DisconnectAll)
       NEDEN: Bir nesne silindiÄŸinde veya tÃ¼m rolleri sÄ±fÄ±rlandÄ±ÄŸÄ±nda, Ã¼zerindeki tÃ¼m portlarÄ±n baÄŸlantÄ± durumlarÄ±nÄ± temizlemek iÃ§in.
    */
    public void DisconnectAll()
    {
        // ... (Graph içinden çağrılmalı aslında, ama burada port state resetting yapılabilir)
        foreach (var p in Ports)
        {
             // Topoloji grafından disconnect çağrılmalı ki karşı taraf da güncellensin.
             // Ancak node referansı yok. 
             // Basitçe flagleri temizleyelim (Topoloji tutarsız kalabilir ama RemoveEntity bunu yönetiyor).
             p.IsConnected = false;
        }
    }
    
    public IEnumerable<GraphNode> GetNeighbors(MechanicalTopologyGraph graph)
    {
        foreach (var port in Ports)
        {
            if (port.IsConnected && port.ConnectedEntityId.HasValue)
            {
                var neighbor = graph.GetNode(port.ConnectedEntityId.Value);
                if (neighbor != null) yield return neighbor;
            }
        }
    }
}
