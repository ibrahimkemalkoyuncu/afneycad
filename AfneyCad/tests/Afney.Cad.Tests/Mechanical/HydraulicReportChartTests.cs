using System.Collections.Generic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Mechanical;

/*
   NE: Hidrolik Rapor SVG Grafik Testleri
   NEDEN: PdfReportService, hic cagrilmayan (dead code) bir servisti; onun tek gercekten
          faydali parcasi olan basinc kaybi cubuk grafigi (SvgChartService uzerinden)
          bu oturumda canli/cagrilan HydraulicReportService'e tasindi ve PdfReportService
          silindi. Bu test grafigin gercekten HTML ciktisina gomuldugunu dogruluyor.
*/
public class HydraulicReportChartTests
{
    [Fact]
    public void GenerateHtmlReport_PipesWithPressureDrop_IncludesSvgChart()
    {
        var kernel = new MechanicalKernel();
        var reportService = new HydraulicReportService(kernel.PressureDrop);

        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            PressureDrop = 1.25
        };

        var html = reportService.GenerateHtmlReport(new List<PipeEntity> { pipe }, "Test Projesi");

        Assert.Contains("<svg", html);
        Assert.Contains("Basınç Kaybı Dağılımı", html);
    }

    [Fact]
    public void GenerateHtmlReport_NoPressureDrop_OmitsChart()
    {
        var kernel = new MechanicalKernel();
        var reportService = new HydraulicReportService(kernel.PressureDrop);

        var pipe = new PipeEntity(new Vector3D(0, 0, 0), new Vector3D(5000, 0, 0), 20)
        {
            SystemType = MechanicalSystemType.DomesticColdWater,
            PressureDrop = 0
        };

        var html = reportService.GenerateHtmlReport(new List<PipeEntity> { pipe }, "Test Projesi");

        Assert.DoesNotContain("<svg", html);
    }
}
