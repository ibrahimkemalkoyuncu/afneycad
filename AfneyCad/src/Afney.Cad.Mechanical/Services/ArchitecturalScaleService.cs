using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Mimari Ölçeklendirme Servisi (ArchitecturalScaleService)
    NEDEN: Autocad çizimleri genellikle mm veya cm olurken, FineSANI ve AfneyCAD metre (m) bazında çalışır.
    
    MANTIKSAL OTOMASYON:
    1. Çizimdeki tüm LineEntity'leri analiz eder.
    2. Duvar kalınlığı olabilecek (10-25 birim arası cm, 100-250 birim arası mm) mesafeleri saptar.
    3. Programın "METRE" bazına geçmesi için gerekli scale katsayısını önerir/uygular.
*/
public class ArchitecturalScaleService
{
    public enum DrawingUnit { Millimeters, Centimeters, Meters, Unknown }

    public (DrawingUnit detectedUnit, double scaleFactor) DetectScale(IEnumerable<CadEntity> entities)
    {
        var lines = entities.OfType<LineEntity>().ToList();
        if (lines.Count < 10) return (DrawingUnit.Unknown, 1.0);

        // Örnekleme: En çok tekrarlanan kısa mesafeleri bul (Duvar kalınlığı tahmini)
        var lengths = lines.Select(l => l.GetLength()).Where(len => len > 0).ToList();
        double avgLen = lengths.Average();

        // Basit Sezgisel Analiz:
        // Ortalama bir oda duvarı 3-5 metredir.
        // Eğer ortalama çizgi 3000-5000 ise -> mm (Scale: 0.001)
        // Eğer ortalama çizgi 300-500 ise -> cm (Scale: 0.01)
        // Eğer ortalama çizgi 3-5 ise -> m (Scale: 1.0)
        
        if (avgLen > 1000) return (DrawingUnit.Millimeters, 0.001);
        if (avgLen > 50) return (DrawingUnit.Centimeters, 0.01);
        
        return (DrawingUnit.Meters, 1.0);
    }

    /*
        NE: Metreye Dönüştür
        NEDEN: Kemal Bey'in isteği üzerine tüm çizimi 1.0 = 1 metre olacak şekilde ölçeklemek için.
    */
    public void ScaleToMeters(Afney.Cad.Database.Core.CadDatabase database)
    {
        var entities = database.GetAllEntities().ToList();
        var (unit, factor) = DetectScale(entities);

        if (factor != 1.0)
        {
            var matrix = Matrix4x4.Scaling(factor, factor, factor);
            foreach (var entity in entities)
            {
                entity.Transform(matrix);
            }
        }
    }
    
    /*
        NE: Referans Noktasına Göre Hizala (WBlock Mantığı)
        NEDEN: Katların üst üste binmesi için ortak bir (0,0,0) noktası belirlemek.
    */
    public void AlignToOrigin(Afney.Cad.Database.Core.CadDatabase database, Vector3D referencePoint)
    {
        var entities = database.GetAllEntities().ToList();
        var translation = new Vector3D(-referencePoint.X, -referencePoint.Y, -referencePoint.Z);
        var matrix = Matrix4x4.TranslationMatrix(translation.X, translation.Y, translation.Z);

        foreach (var entity in entities)
        {
            entity.Transform(matrix);
        }
    }
}
