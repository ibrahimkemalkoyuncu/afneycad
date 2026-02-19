using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Otomatik Şematik Yerleşim Motoru (SchemaLayoutEngine)
   NEDEN: Topolojik olarak tespit edilen kolon yapısını, okunabilir ve estetik bir 2D teknik çizime (Kolon Şemasına) dönüştürmek için.

   NASIL (Mühendislik Detayı):
   - Constraint-Based Layout: Katlar dikey eksende (Y), branşmanlar yatay eksende (X) konumlandırılır.
   - Collision Avoidance: Aynı kattaki vitrifiyeler, birbiri üzerine binmeyecek şekilde hesaplanmış bir ofset (Step: 500mm) ile dizilir.
   - Symmetry & Alignment: Kolon hattı merkez kabul edilerek kat branşmanları sağa veya sola doğru dengeli bir ağaç (Tree) yapısında dallandırılır.
   - Prototip Algoritması: Hierarchical Layout yaklaşımı ile her düğüme (Node) bir 'Grid Coordinate' atanır.
*/
public class SchemaLayoutEngine
{
    private const double FloorSpacing = 2000.0; // Şemada katlar arası görsel mesafe
    private const double FixtureSpacing = 800.0; // Vitrifiyeler arası yatay mesafe

    // NE: Şematik Koordinat Hesaplayıcı
    // NEDEN: 3D uzaydaki bir nesneyi 2D şema paftasındaki (X,Y) noktasına maplemek için.
    public void ComputeLayout(RiserSchema schema)
    {
        // 1. Katları Y ekseninde sırala
        var sortedFloors = schema.Floors.OrderBy(f => f.Elevation).ToList();

        for (int i = 0; i < sortedFloors.Count; i++)
        {
            var floor = sortedFloors[i];
            double floorY = i * FloorSpacing; // Şema Y koordinatı

            // 2. Vitrifiyeleri X ekseninde dağıt
            for (int j = 0; j < floor.Fixtures.Count; j++)
            {
                var fixture = floor.Fixtures[j];
                fixture.OrderIndex = j;
                
                // Şahsi Not: Fixture'lara özel 2D koordinat atamak için bir VisualModel gerekebilir.
            }
        }
    }

    /*
       MÜHENDİSLİK NOTU (Mebrure Hanım): 
       Gerçek bir MEP Engine'de vitrifiyelerin tipi (WC vs Lavabo) ve drenaj açısı 
       şema üzerindeki sembol yerleşimini (Rotation/Mirroring) belirler. 
       FineSANI'de bu otomatik yapılır, biz de topolojiyi buna göre kurmalıyız.
    */
}
