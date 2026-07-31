using System.Linq;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;
using Xunit;

namespace Afney.Cad.Tests.Commands;

/*
   NE: DimensionStyleService Kablo Testleri
   NEDEN: DimensionStyleService önceden hiçbir yerde kullanılmıyordu (yazılmış ama hiç bağlanmamış
          bir servisti) — Dim* komutları sadece bir TextHeight double'ı alıyordu, ok boyu/uzatma
          boşluğu/hassasiyet/birim formatı hep sabit oranlardı. Artık her komut bir DimensionStyle
          alıyor ve DimensionStyleApplier ile tüm alanları DimensionEntity'ye aktarıyor.
*/
public class DimensionStyleWiringTests
{
    [Fact]
    public void LinearDimCommand_UsesProvidedStyle_NotJustTextHeight()
    {
        var db = new CadDatabase();
        var style = new DimensionStyle { Name = "ISO-25", TextHeight = 350, ArrowSize = 280, Precision = 1 };

        var cmd = new LinearDimCommand(db, db.TransactionManager, style);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));
        cmd.OnPointerPressed(new Vector3D(1000, 0, 0));
        cmd.OnPointerPressed(new Vector3D(500, 300, 0));

        var dim = db.GetAllEntities().OfType<DimensionEntity>().Single();
        Assert.Equal(350, dim.TextHeight, precision: 6);
        Assert.Equal(280, dim.ArrowSize, precision: 6);
        Assert.Equal(1, dim.Precision);
    }

    [Fact]
    public void LinearDimCommand_NoStyleProvided_FallsBackToStandardDefaults()
    {
        var db = new CadDatabase();
        var cmd = new LinearDimCommand(db, db.TransactionManager);
        cmd.Start();
        cmd.OnPointerPressed(new Vector3D(0, 0, 0));
        cmd.OnPointerPressed(new Vector3D(1000, 0, 0));
        cmd.OnPointerPressed(new Vector3D(500, 300, 0));

        // NOT: TextHeight/ArrowSize varsayılanları 250/200 → 100/80 küçültüldü (araştırma ajanı
        // bulgusu — DoorEntity/WindowEntity etiketleriyle [fontSize=80mm] görsel tutarlılık için,
        // bkz. Kullanici_kitabi.md Session #55 madde 27).
        var dim = db.GetAllEntities().OfType<DimensionEntity>().Single();
        Assert.Equal(100, dim.TextHeight, precision: 6);
        Assert.Equal(80, dim.ArrowSize, precision: 6);
    }

    [Fact]
    public void DimensionStyleService_ActiveStyle_ReflectsSetActiveStyle()
    {
        var svc = new DimensionStyleService();
        svc.SetActiveStyle("Compact");
        Assert.Equal("Compact", svc.ActiveStyleName);
        // NOT: Compact TextHeight 125 → 50 küçültüldü (bkz. yukarıdaki not).
        Assert.Equal(50, svc.ActiveStyle.TextHeight, precision: 6);
    }
}
