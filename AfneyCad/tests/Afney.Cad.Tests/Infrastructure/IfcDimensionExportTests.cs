using System;
using System.IO;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Infrastructure.Export;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: IFC Ölçü (Dimension) Export Testleri
   NEDEN: DimensionEntity'ler önceden IFC dışa aktarımında hiç ele alınmıyordu (export
          type-switch'inde hiç geçmiyordu). Bu testler artık IfcAnnotation + GeometricCurveSet
          olarak (IFC'nin gerçek dünya BIM araçlarının da kullandığı yöntem — IFC'de birinci
          sınıf bir "DIMENSION" tipi yoktur) aktarıldığını doğruluyor.
*/
public class IfcDimensionExportTests
{
    [Fact]
    public void Export_LinearDimension_ProducesIfcAnnotationWithCurveSet()
    {
        var svc = new IfcExportService();
        var dim = new DimensionEntity(new Vector3D(0, 0, 0), new Vector3D(2000, 0, 0), new Vector3D(0, 300, 0), DimensionType.Linear);
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_ifcdim_test_{Guid.NewGuid():N}.ifc");

        try
        {
            svc.ExportToIfc(new CadEntity[] { dim }, path);
            string content = File.ReadAllText(path);

            Assert.Contains("IFCANNOTATION", content);
            Assert.Contains("IFCGEOMETRICCURVESET", content);
            Assert.Contains("IFCPOLYLINE", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Export_NoDimensions_DoesNotProduceAnnotation()
    {
        var svc = new IfcExportService();
        string path = Path.Combine(Path.GetTempPath(), $"afneycad_ifcdim_test_{Guid.NewGuid():N}.ifc");

        try
        {
            svc.ExportToIfc(Array.Empty<CadEntity>(), path);
            string content = File.ReadAllText(path);

            Assert.DoesNotContain("IFCANNOTATION", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
