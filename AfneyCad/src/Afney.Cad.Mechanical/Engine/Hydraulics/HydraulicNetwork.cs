using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Engine.Hydraulics;

/*
   NE: Hidrolik Ağ Modeli (HydraulicNetwork)
   NEDEN: Sıhhi tesisat projelerindeki boru ve cihazları bir "Graf" (Düğüm ve Kenar) yapısında modelleyerek debi, basınç ve hız analizi yapabilmek için.

   MÜHENDİSLİK DETAYI:
   - Düğümler (NetworkNode): Tüketim noktalarını (klozet, lavabo vb.) veya kaynakları (sayaç, pompa) temsil eder.
   - Kenarlar (NetworkPipe): Boru hatlarını temsil eder ve uzunluk, çap, sürtünme gibi hidrolik özellikleri taşır.
   - Kirchhoff Yasaları'nın uygulanabilmesi için şebeke bütünlüğü bu model üzerinden kurulur.
*/

public enum NodeType
{
    Junction, // Bağlantı / Branşman noktası
    Source,   // Besleme noktası (Sayaç / Depo çıkışı)
    Outlet,   // Uç nokta / Tüketim birimi (Batarya, Rezervuar vb.)
    Tank      // Rezervuar / Toplama tankı
}

/*
   NE: Ağ Düğüm Noktası (NetworkNode)
   NEDEN: Boru bağlantı noktalarını ve buralardaki debi (Demand) ihtiyacını tanımlamak için.
*/
public class NetworkNode
{
    public Guid Id { get; } = Guid.NewGuid();
    public Vector3D Position { get; set; }
    public NodeType Type { get; set; }
    
    // Tüketim debisi (m³/h)
    public double Demand { get; set; } 
    
    // Hesaplanan Hidrolik Yük (Head) - mSS (Metre Su Sütunu)
    public double HydraulicHead { get; set; }
    
    // Geometrik Kot (Elevation) - m
    public double Elevation { get; set; }

    public NetworkNode(Vector3D position, NodeType type = NodeType.Junction)
    {
        Position = position;
        Type = type;
        Elevation = position.Z; 
    }
}

/*
   NE: Ağ Boru Tanımı (NetworkPipe)
   NEDEN: İki düğüm arasındaki fiziksel borunun hidrolik parametrelerini saklamak için.
*/
public class NetworkPipe
{
    public Guid Id { get; } = Guid.NewGuid();
    
    public NetworkNode StartNode { get; set; }
    public NetworkNode EndNode { get; set; }
    
    public double Length { get; set; } // m
    public double InnerDiameter { get; set; } // mm (Anma çapı yerine iç çap kullanılır)
    public string Material { get; set; } = "Steel"; // Boru malzemesi (Sürtünme katsayısı için)
    public double Roughness { get; set; } // mm (Pürüzlülük katsayısı ε)
    
    // Hesaplanan debi (m³/h) [Pozitif: Start -> End]
    public double FlowRate { get; set; } 
    
    // Hesaplanan yük kaybı (mSS)
    public double HeadLoss { get; set; }

    public NetworkPipe(NetworkNode start, NetworkNode end, double diameter, string material = "Steel")
    {
        StartNode = start;
        EndNode = end;
        InnerDiameter = diameter;
        Material = material;
        Roughness = 0.045; // Çelik için standart değer
        Length = start.Position.DistanceTo(end.Position);
    }
}

/*
   NE: Hidrolik Ağ Nesnesi (HydraulicNetwork)
   NEDEN: Projenin tüm hidrolik topolojisini tek bir kapta toplamak için.
*/
public class HydraulicNetwork
{
    public List<NetworkNode> Nodes { get; } = new();
    public List<NetworkPipe> Pipes { get; } = new();

    // Tasarım suyu sıcaklığı (°C) — HardyCrossSolver'ın basınç kaybı hesabında (WaterPropertiesService
    // üzerinden yoğunluk/viskozite) kullanır. Varsayılan 20°C (MechanicalSystemConfig.DesignTemperature
    // ile aynı varsayılan), böylece sıcaklık ayarlanmazsa mevcut davranış korunur.
    public double WaterTemperatureC { get; set; } = 20.0;

    public void AddPipe(NetworkNode start, NetworkNode end, double diameter, string material = "Steel")
    {
        if (!Nodes.Contains(start)) Nodes.Add(start);
        if (!Nodes.Contains(end)) Nodes.Add(end);
        
        Pipes.Add(new NetworkPipe(start, end, diameter, material));
    }
}

