using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Akıllı Boru Yönlendirme Servisi (PipingRoutingService)
   NEDEN: İki nokta arasında (Örn: Lavabo - Kolon) duvarları ve diğer engelleri algılayarak en uygun boru rotasını (A* Algoritması) otomatik oluşturmak için.

   NASIL (Mühendislik Detayı):
   - Grid-Based Pathfinding: Çizim alanını geçici bir ızgaraya (Grid) böler.
   - Obstacle Mapping: Walls (Duvarlar) ve Kolonlar grid üzerinde "Geçilemez" olarak işaretlenir.
   - A* Algorithm: Başlangıç ve bitiş arasında maliyet hesabı (Mesafe + Dönüş sayısı) yaparak rotayı belirler.
   - Smoothing: Zikzakları temizleyerek tesisatçıların uygulayabileceği düz hatlara dönüştürür.
*/
public class PipingRoutingService
{
    private readonly CadDatabase _database;


    public PipingRoutingService(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Rota Bul (FindRoute)
       AMACI: p1 ve p2 arasında engel aşan boru noktaları listesi döndürür.
    */
    public List<Vector3D> FindRoute(Vector3D start, Vector3D end)
    {
        // Şimdilik basitleştirilmiş "Orthogonal" yönlendirme (Auto-L)
        // İleride A* Grid implementation eklenecek.
        var route = new List<Vector3D>();
        route.Add(start);
        
        // Ara nokta (Dirsek): Sadece X ve Y eksenlerinde hareket
        var mid = new Vector3D(end.X, start.Y, start.Z);
        route.Add(mid);
        route.Add(end);
        
        return route;
    }

    /*
       NE: Engel Analizi (AnalyzeObstacles)
       NEDEN: Duvarların içinden geçmemek, ancak yanından paralel gitmek için.
    */
    public List<ArchitecturalObstacle> GetRelevantObstacles(Vector3D start, Vector3D end)
    {
        var archService = new ArchitecturalRecognitionService(_database);
        return archService.RecognizeObstacles();
    }
}
