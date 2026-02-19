using System;
using System.Collections.Generic;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Infrastructure.Import;

public class CadImporter
{
    /*
       NE: Dosya İçe Aktar (Import)
       NEDEN: DWG veya DXF uzantılı harici mimari projeleri okumak, AutoCAD standartlarındaki verileri AfneyCAD iç dünyasına taşımak için.
    */
    public List<CadEntity> Import(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLower();

        try
        {
            if (extension == ".dwg")
            {
                // YENİ: Profesyonel DWG Servisi
                var service = new DwgImportService();
                return service.ImportDwg(filePath);
            }
            else if (extension == ".dxf")
            {
                // YENİ: Profesyonel DXF Servisi
                var service = new DxfImportService();
                return service.ImportDxf(filePath);
            }
            // else if (.afney) -> handled by serializer outside
        }
        catch (Exception ex)
        {
            throw new Exception($"Dosya okunamadı: {ex.Message}", ex);
        }

        return new List<CadEntity>();
    }
}
