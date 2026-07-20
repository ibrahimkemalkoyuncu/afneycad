using System;
using System.IO;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Infrastructure.Export;
using Afney.Cad.Mechanical.Entities;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: IFC Duvar Export Testleri — B-Rep Kaynaklı
   NEDEN: WallEntity'ler önceden IFC dışa aktarımında hiç ele alınmıyordu. Bu testler, artık
          WallBRepService (kapı/pencere boşluklu segmentasyon dahil) + BRepTessellator
          üzerinden gerçek B-Rep geometrisinin IFC4 tessellation temsiline (IFCCARTESIANPOINTLIST3D
          + IFCPOLYGONALFACESET) doğru şekilde aktarıldığını doğruluyor.
*/
public class IfcWallExportTests
{
    [Fact]
    public void Export_SimpleWall_ProducesIfcWallWithTessellatedGeometry()
    {
        var svc = new IfcExportService();
        var wall = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), thickness: 200) { HeightMm = 3000 };
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_ifcwall_test_{Guid.NewGuid():N}.ifc");

        try
        {
            svc.ExportToIfc(new CadEntity[] { wall }, path);
            string content = File.ReadAllText(path);

            Assert.Contains("IFCWALL", content);
            Assert.Contains("IFCCARTESIANPOINTLIST3D", content);
            Assert.Contains("IFCPOLYGONALFACESET", content);
            Assert.Contains("IFCINDEXEDPOLYGONALFACE", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Export_WallWithDoor_ProducesMultipleTessellationItemsForOneWallProduct()
    {
        // Kapılı duvar birden fazla B-Rep segmentine (sol dilim + lento) bölünüyor — ama
        // IFC'de hâlâ TEK bir IfcWall ürünü olmalı (birden fazla ayrı duvar elemanı değil).
        var svc = new IfcExportService();
        var wall = new WallEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), thickness: 200) { HeightMm = 3000 };
        var door = new DoorEntity(new Vector3D(2500, 0, 0), width: 900, height: 2100);
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_ifcwall_test_{Guid.NewGuid():N}.ifc");

        try
        {
            svc.ExportToIfc(new CadEntity[] { wall, door }, path);
            string content = File.ReadAllText(path);

            int wallCount = CountOccurrences(content, "= IFCWALL(");
            int pointListCount = CountOccurrences(content, "= IFCCARTESIANPOINTLIST3D(");

            Assert.Equal(1, wallCount);       // tek duvar ürünü
            Assert.True(pointListCount >= 2); // en az 2 B-Rep segmenti (sol dilim + lento)
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Export_NoWalls_DoesNotProduceIfcWall()
    {
        var svc = new IfcExportService();
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_ifcwall_test_{Guid.NewGuid():N}.ifc");

        try
        {
            svc.ExportToIfc(Array.Empty<CadEntity>(), path);
            string content = File.ReadAllText(path);

            Assert.DoesNotContain("IFCWALL", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
