using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: İzo-Metrik Senkronizasyon Servisi (IsoSyncService)
   NEDEN: 2D plan üzerinde yapılan her değişikliğin (Boru çapı, armatür ekleme vb.) anlık olarak 3D izometrik şemaya yansımasını sağlamak için. (Suggestion 18)
   
   PRENSİP: 
   "Single Source of Truth" (Tek Doğruluk Kaynağı). 
   2D plan ana veridir; İzometrik şema bu verinin 3D projeksiyonudur.
*/
public class IsoSyncService
{
    private readonly MechanicalKernel _kernel;
    private readonly CadDatabase? _database;

    public event Action? OnSyncRequired;

    public IsoSyncService(MechanicalKernel kernel, CadDatabase? database)
    {
        _kernel = kernel;
        _database = database;
        
        if (_database != null)
        {
            // Veritabanı değişimlerini dinle
            _database.EntityAdded += (e) => RequestSync();
            _database.EntityRemoved += (e) => RequestSync();
            _database.EntityUpdated += (e) => RequestSync();
        }
    }

    private void RequestSync()
    {
        // Throttling uygulanabilir (Çok hızlı değişimlerde performansı korumak için)
        OnSyncRequired?.Invoke();
    }

    /*
       NE: İzometrik Dönüşüm (Isometric Projection)
       NEDEN: 3D Dünya koordinatlarını (X, Y, Z), 2D izometrik şema düzlemine (30-30 kuralı) iz düşürmek için.
    */
    /*
       NE: İzometrik Dönüşüm (ProjectToIsometric)
       NEDEN: 3D Dünya koordinatlarını (X, Y, Z), 2D izometrik şema düzlemine (30-30 kuralı) iz düşürerek tesisatın derinlik algısını korumak için.
    */
    public Vector3D ProjectToIsometric(Vector3D worldPos)
    {
        // Standart İzometrik Açı: 30 Derece
        double cos30 = Math.Cos(Math.PI / 6);
        double sin30 = Math.Sin(Math.PI / 6);
        
        // İzometrik X = (X - Y) * cos(30)
        // İzometrik Y = (X + Y) * sin(30) + Z
        double xIso = (worldPos.X - worldPos.Y) * cos30;
        double yIso = (worldPos.X + worldPos.Y) * sin30 + worldPos.Z;
        
        return new Vector3D(xIso, yIso, 0);
    }

    /*
       NE: İzometrik Şema Deplasmanı
       NEDEN: Her kolonun şemada birbirine karışmaması için belirli bir ofset ile çizilmesi gerekir.
    */
    /*
       NE: İzometrik Şema Üret (GenerateIsometricScheme)
       NEDEN: Veritabanındaki tüm mekanik nesneleri (boru, vitrifiye vb.) izometrik düzlem koordinatlarına klonlayarak dikey bir kolon şeması oluşturmak için.
    */
    public List<Afney.Cad.Domain.Abstractions.CadEntity> GenerateIsometricScheme()
    {
        if (_database == null) return new List<Afney.Cad.Domain.Abstractions.CadEntity>();

        var allEntities = _database.GetAllEntities().OfType<MechanicalEntity>().ToList();
        var schemeEntities = new List<Afney.Cad.Domain.Abstractions.CadEntity>();

        foreach (var entity in allEntities)
        {
            if (entity is PipeEntity pipe)
            {
                var pStart = ProjectToIsometric(pipe.StartPoint);
                var pEnd = ProjectToIsometric(pipe.EndPoint);
                
                var isoPipe = new PipeEntity(pStart, pEnd, pipe.InnerDiameter)
                {
                    SystemType = pipe.SystemType,
                    Color = pipe.Color,
                    Layer = "ISO_SCHEME_" + pipe.SystemType
                };
                schemeEntities.Add(isoPipe);
            }
            else if (entity is SanitaryFixtureEntity fixture)
            {
                var pPos = ProjectToIsometric(fixture.Position);
                // Armatürler şemada sembolik olarak (Block) gösterilir.
                var isoSymbol = new Afney.Cad.Domain.Entities.Basic.CircleEntity(pPos, 150.0) 
                { 
                    Layer = "ISO_SYMBOLS",
                    Color = fixture.Color 
                };
                schemeEntities.Add(isoSymbol);
            }
        }

        return schemeEntities;
    }
}
