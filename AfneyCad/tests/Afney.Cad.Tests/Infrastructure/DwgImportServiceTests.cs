using System;
using System.IO;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Infrastructure.Import;
using Xunit;

namespace Afney.Cad.Tests.Infrastructure;

/*
   NE: DWG İçe Aktarma — INSUNITS Ölçek Testleri
   NEDEN: `DwgImportService.ImportDwg()` DWG header'ından $INSUNITS'i okuyup doğru bir
          `unitScale` hesaplıyordu AMA hiçbir entity'ye uygulamıyordu — model space
          dönüşümü her zaman `Matrix4x4.Identity` ile yapılıyordu. Yani dosya milimetre
          DIŞINDA bir birimde (metre/santimetre) çizilmişse içe aktarılan TÜM koordinatlar
          "zaten mm" sanılıp olduğu gibi kullanılıyordu — mahal alan/çevre hesapları gibi
          birim-bağımlı her ölçüm sessizce yanlış çıkıyordu. Bu, gerçek bir kullanıcı
          testinde (aylarca fark edilmeden) ortaya çıktı. Bu testler, düzeltmenin
          (başlangıç transformunun artık `Matrix4x4.CreateScale(unitScale)` olması)
          kalıcı olduğunu doğrular — regresyon durumunda kırmızı düşer.
*/
public class DwgImportServiceTests
{
    private static string WriteMinimalDwg(ACadSharp.Types.Units.UnitsType insUnits, double lineLengthInDrawingUnits)
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = insUnits;

        var line = new Line(new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(lineLengthInDrawingUnits, 0, 0));
        doc.Entities.Add(line);

        var path = Path.Combine(Path.GetTempPath(), $"afneycad_dwg_unit_test_{Guid.NewGuid():N}.dwg");
        using (var writer = new DwgWriter(path, doc))
        {
            writer.Write();
        }
        return path;
    }

    [Fact]
    public void ImportDwg_MetersDrawing_ScalesCoordinatesToMillimeters()
    {
        // 3 metre uzunluğunda bir çizgi, INSUNITS=Meters ile çizilmiş bir DWG
        string path = WriteMinimalDwg(ACadSharp.Types.Units.UnitsType.Meters, 3.0);
        try
        {
            var entities = new DwgImportService().ImportDwg(path);
            var line = Assert.Single(entities.OfType<LineEntity>());

            // Ölçek uygulanmasaydı uzunluk "3" (mm) kalırdı — doğru davranışta 3000mm (3m) olmalı.
            double length = line.StartPoint.DistanceTo(line.EndPoint);
            Assert.True(Math.Abs(length - 3000.0) < 0.5,
                $"Beklenen ~3000mm (3m INSUNITS ölçeklenmiş), gerçek: {length}mm — unitScale uygulanmıyor olabilir.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ImportDwg_MillimeterDrawing_LeavesCoordinatesUnscaled()
    {
        // INSUNITS zaten Millimeters ise unitScale=1.0 — davranış değişmemeli (regresyon koruması).
        string path = WriteMinimalDwg(ACadSharp.Types.Units.UnitsType.Millimeters, 500.0);
        try
        {
            var entities = new DwgImportService().ImportDwg(path);
            var line = Assert.Single(entities.OfType<LineEntity>());

            double length = line.StartPoint.DistanceTo(line.EndPoint);
            Assert.True(Math.Abs(length - 500.0) < 0.5,
                $"Beklenen 500mm (mm birimde ölçeksiz), gerçek: {length}mm.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /*
       NE: TB-03 — Düz TEXT'te \U+XXXX Türkçe Karakter Kaçışı Çözülüyor
       NEDEN: Önceden sadece MTEXT (CleanMText) bu çözümlemeyi yapıyordu; düz tek-satırlık
              TEXT entity'leri (eski AutoCAD çizimlerinde Türkçe karakterler genelde böyle
              saklanır) hiç decode edilmiyordu — ekranda "\U+015E" gibi ham kaçışlar
              görünüyordu. DecodeUnicodeEscapes artık MapText'te de çağrılıyor.
    */
    [Fact]
    public void ImportDwg_PlainTextEntity_DecodesUnicodeEscapes()
    {
        var doc = new CadDocument();
        // "Şişli" → \U+015E i\U+015F li (Ş ve ş kaçışlı, gerçek bir eski-AutoCAD DWG'sinde olduğu gibi)
        var text = new ACadSharp.Entities.TextEntity { Value = "\\U+015Ei\\U+015Fli", InsertPoint = new CSMath.XYZ(0, 0, 0), Height = 10 };
        doc.Entities.Add(text);

        var path = Path.Combine(Path.GetTempPath(), $"afneycad_dwg_text_unicode_test_{Guid.NewGuid():N}.dwg");
        try
        {
            using (var writer = new DwgWriter(path, doc)) writer.Write();

            var entities = new DwgImportService().ImportDwg(path);
            var imported = Assert.Single(entities.OfType<Afney.Cad.Domain.Entities.Basic.TextEntity>());

            Assert.Equal("Şişli", imported.Text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /*
       NE: TB-03 — Desteklenmeyen Entity Tipleri Artık Sessiz Değil
       NEDEN: ConvertEntity'nin ana switch'i eşleşmeyen tipler (Mesh, Solid3D/ACIS, MLeader,
              TableEntity, Leader, Viewport, Ray, XLine, ProxyEntity vb.) için hiçbir uyarı
              olmadan atlıyordu. Artık LastUnsupportedEntityCounts'ta sayılıyor.
    */
    [Fact]
    public void ImportDwg_UnsupportedEntityType_IsCountedNotSilentlyDropped()
    {
        var doc = new CadDocument();
        doc.Entities.Add(new Line(new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(100, 0, 0))); // desteklenen
        doc.Entities.Add(new ACadSharp.Entities.Ray { StartPoint = new CSMath.XYZ(0, 0, 0), Direction = new CSMath.XYZ(1, 0, 0) }); // desteklenmeyen

        var path = Path.Combine(Path.GetTempPath(), $"afneycad_dwg_unsupported_test_{Guid.NewGuid():N}.dwg");
        try
        {
            using (var writer = new DwgWriter(path, doc)) writer.Write();

            var importer = new DwgImportService();
            var entities = importer.ImportDwg(path);

            Assert.Single(entities.OfType<LineEntity>()); // desteklenen entity hâlâ geliyor
            Assert.True(importer.LastUnsupportedEntityCounts.TryGetValue(nameof(ACadSharp.Entities.Ray), out int count));
            Assert.Equal(1, count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
