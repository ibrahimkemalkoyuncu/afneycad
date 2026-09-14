using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Çekirdek İş Akışı Uçtan Uca Testi (CoreWorkflowIntegrationTests)
   NEDEN — Session #75'te bulunup düzeltilen iki kritik hata (DWG import mm/m ölçek
          hatası, AutoRouteService'in Türkçe katman adlarını tanımaması) her ikisi de
          önceki denetimlerin KAÇIRDIĞI, sadece "çekirdek iş akışının" (mimari tanı →
          otomatik rotalama → metraj) UÇTAN UCA çalıştırılmasıyla ortaya çıkan
          hatalardı — hiçbir birim testi bu zinciri baştan sona doğrulamıyordu. Bu test
          o boşluğu kapatır: Türkçe katmanlı bir mimari duvar → tanıma → duvardan
          kaçınan otomatik rota → metraj (mm→m) zincirinin bütün olarak doğru
          çalıştığını kilitler, böylece bu sınıftaki hatalar gelecekte sessizce geri
          gelemez.
*/
public class CoreWorkflowIntegrationTests
{
    [Fact]
    public void ArchitecturalRecognition_ToAutoRoute_ToBom_ProducesObstacleAvoidingPipeWithCorrectMetraj()
    {
        var db = new CadDatabase();

        // Türkçe mimari katman adıyla bir bölme duvarı (Türkiye'de en yaygın DWG kuralı).
        // Başlangıç (0,1000) ile bitiş (4000,1000) arasındaki DOĞRUDAN hattı X=2000'de keser.
        var wall = new LineEntity(
            new Vector3D(2000, -1000, 0),
            new Vector3D(2000, 3000, 0))
        {
            Layer = "DUVAR"
        };
        db.AddEntity(wall);

        // 1. Mimari Tanıma — canonical servis, Türkçe katman adını tanımalı.
        var obstacles = new ArchitecturalRecognitionService(db).RecognizeObstacles();
        Assert.Single(obstacles);
        Assert.Equal(Afney.Cad.Mechanical.Models.ObstacleType.Wall, obstacles[0].Type);

        // 2. Otomatik Rotalama — AutoRouteService paylaşılan (canonical) engel listesini kullanarak
        //    duvardan kaçınmalı (doğrudan hat üzerinden DEĞİL).
        var start = new Vector3D(0, 1000, 0);
        var end = new Vector3D(4000, 1000, 0);
        var options = new RouteOptions { GridStep = 200, WallOffset = 150, Diameter = 20, AvoidObstacles = true };

        var routeService = new AutoRouteService(db, obstacles);
        var route = routeService.FindRoute(start, end, options);

        Assert.True(route.Success);
        Assert.True(route.Waypoints.Count > 2, "Rota, duvarı aşmak için en az bir ara nokta (dirsek) içermeli — düz bir hat duvarın içinden geçerdi.");

        // Rotanın hiçbir segmenti, duvarın genişletilmiş (WallOffset uygulanmış) sınır kutusunun
        // İÇİNDEN geçmemeli.
        double wallMinX = 2000 - options.WallOffset, wallMaxX = 2000 + options.WallOffset;
        for (int i = 0; i < route.Waypoints.Count; i++)
        {
            var p = route.Waypoints[i];
            bool insideWallBand = p.X > wallMinX && p.X < wallMaxX && p.Y > -1000 - 1 && p.Y < 3000 + 1;
            Assert.False(insideWallBand, $"Waypoint {p} duvarın genişletilmiş sınırı içinde — engelden kaçınma başarısız.");
        }

        // 3. Rotadan Boru Üret ve Veritabanına Ekle
        var pipes = routeService.CreatePipesFromRoute(route, options);
        Assert.NotEmpty(pipes);
        foreach (var pipe in pipes)
        {
            pipe.PipeMaterialType = PipeMaterial.PPRC_PN20;
            db.AddEntity(pipe);
        }

        // 4. Metraj — BomService'in "Boru" satırı GERÇEK uzunluğu metre cinsinden vermeli
        //    (madde 72'de düzeltilen mm→m hatasının bu zincirde geri gelmediğini kilitler).
        double expectedTotalMeters = pipes.Sum(p => p.GetLength()) / 1000.0;
        var bom = new BomService(db).GenerateBom();
        var pipeItem = Assert.Single(bom, b => b.Category == "Boru");

        Assert.Equal(expectedTotalMeters, pipeItem.Quantity, precision: 2);
        Assert.Equal("m", pipeItem.Unit);
        // Rota duvarı dolanmak zorunda kaldığı için gerçek uzunluk, düz hattın (4m) uzunluğundan
        // GÖRÜNÜR şekilde daha fazla olmalı — aksi halde rota aslında düz çizilmiş demektir.
        Assert.True(expectedTotalMeters > 4.0, $"Beklenen: düz hattan (4m) daha uzun bir dolanma rotası, gerçek: {expectedTotalMeters}m");
    }
}
