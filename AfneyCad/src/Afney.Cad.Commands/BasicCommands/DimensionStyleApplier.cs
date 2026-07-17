using Afney.Cad.Domain.Entities.Annotation;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.BasicCommands;

/*
   NE: Ölçü Stili Uygulayıcı (DimensionStyleApplier)
   NEDEN: DimensionStyleService önceden hiçbir Dim* komutuna bağlı değildi — her komut sadece
          düz bir TextHeight değeri alıyor, ok boyu/uzatma boşluğu/hassasiyet/birim formatı hep
          sabit varsayılanlardaydı. Bu sınıf, aktif DimensionStyle'ı tüm 5 ölçü komutunda
          (Linear/Aligned/Radius/Angular/Continue) tek bir yerden tutarlı şekilde uygulamak için.
*/
public static class DimensionStyleApplier
{
    public static void Apply(DimensionEntity entity, DimensionStyle style)
    {
        entity.TextHeight  = style.TextHeight;
        entity.ArrowSize   = style.ArrowSize;
        entity.ExtLineGap  = style.ExtLineGap;
        entity.ExtLineOver = style.ExtLineOver;
        entity.Color       = style.Color;
        entity.Precision   = style.Precision;
        entity.ShowUnits   = style.ShowUnits;
        entity.UnitFormat  = style.UnitFormat;
        entity.ArrowStyle  = System.Enum.TryParse<DimensionArrowStyle>(style.ArrowStyle, out var parsed)
            ? parsed
            : DimensionArrowStyle.Filled;
    }
}
