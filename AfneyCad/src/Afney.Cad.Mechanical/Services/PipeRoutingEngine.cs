using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Standards;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Boru Yönlendirme Motoru (PipeRoutingEngine)
   NEDEN: Kullanıcı fare ile çizim yaparken otomatik olarak boru ağı oluşturmak, dirsek ve branşmanları yönetmek için.
   
   NASIL (Mühendislik Detayı):
   - Pure Logic: Veritabanından bağımsız çalışır, sadece oluşturulan nesneleri döner.
   - Sorumluluk Ayrımı: Nesnelerin veritabanına eklenmesi Komut (Command) katmanının sorumluluğundadır.
   - Süreklilik: Önceki boru hatlarını takip ederek köşe dönüşlerinde (Dirsek) ve ayrılma noktalarında (Tee) geometrik hesaplamaları yapar.
   - Hassasiyet: Vana montajı gibi işlemlerde boruyu milimetrik olarak böler ve araya vana yerleştirir.
*/
public class PipeRoutingEngine
{
    private PipeEntity? _lastPipe;
    private Vector3D? _lastPoint;
    private double _currentDiameter = 100.0;
    private double _currentSlope = 0.0; // % olarak eğim
    
    public double CurrentSlope => _currentSlope;
    private MechanicalSystemType _currentSystemType = MechanicalSystemType.DomesticColdWater;
    
    public Vector3D? LastPoint => _lastPoint;

    public PipeRoutingEngine()
    {
    }

    /// <summary>
    /// Yeni bir boru rotası başlatır.
    /// </summary>
    public void StartRoute(Vector3D startPoint, double diameter)
    {
        _lastPoint = startPoint;
        _lastPipe = null;
        _currentDiameter = diameter;
    }

    /// <summary>
    /// Rotalama için varsayılan çapı ayarlar.
    /// </summary>
    public void SetDiameter(double diameter)
    {
        _currentDiameter = diameter;
    }

    public void SetSystemType(MechanicalSystemType systemType)
    {
        _currentSystemType = systemType;
    }

    private PipeDefinition? _currentStandardDef;

    /// <summary>
    /// Rotalama için standart fiziksel özelliklerini ayarlar.
    /// </summary>
    public void SetStandardDefinition(PipeDefinition? def)
    {
        _currentStandardDef = def;
        if (def != null)
        {
            // Eğer standarttan veri gelirse, çap bilgisini buradan güncelle
            _currentDiameter = def.InnerDiameter;
        }
    }

    /// <summary>
    /// Rotalama için eğim (%) değerini ayarlar.
    /// </summary>
    public void SetSlope(double slope)
    {
        _currentSlope = slope;
    }

    /// <summary>
    /// Belirlenen noktaya yeni bir boru segmenti ve gerekiyorsa dirsek ekler.
    /// NASIL: Eğim (Slope) varsa, bitiş noktasının Z koordinatını yatay mesafe üzerinden otomatik hesaplar.
    /// </summary>
    /// <returns>Oluşturulan yeni nesne listesi.</returns>
    private AutoFittingSelector? _fittingSelector;

    public void SetFittingSelector(AutoFittingSelector selector)
    {
        _fittingSelector = selector;
    }

    public List<CadEntity> AddPoint(Vector3D newPoint)
    {
        var createdEntities = new List<CadEntity>();
        
        if (_lastPoint == null)
        {
            _lastPoint = newPoint;
            return createdEntities;
        }
        
        var start = _lastPoint.Value;
        
        var adjustedEnd = newPoint;
        if (Math.Abs(_currentSlope) > 0.0001)
        {
            double dx = newPoint.X - start.X;
            double dy = newPoint.Y - start.Y;
            double horizontalDist = Math.Sqrt(dx * dx + dy * dy);
            
            double dz = horizontalDist * (_currentSlope / 100.0);
            adjustedEnd = new Vector3D(newPoint.X, newPoint.Y, start.Z - dz); 
        }
        
        // Yeni boru oluştur
        var newPipe = new PipeEntity(start, adjustedEnd, _currentDiameter)
        {
            SystemType    = _currentSystemType,
            PipeMaterialType = GetMaterialForSystem(_currentSystemType),
            Slope  = _currentSlope,
            Layer  = GetLayerNameForSystem(_currentSystemType)
        };
        newPipe.ApplySystemColor();

        // Eğer önceki boru varsa, köşeye dirsek yerleştir
        if (_lastPipe != null)
        {
            MechanicalEntity? fitting = null;
            
            var v1 = (_lastPipe.EndPoint - _lastPipe.StartPoint).Normalize();
            var v2 = (adjustedEnd - start).Normalize();
            double dot = v1.Dot(v2);
            bool isCollinear = Math.Abs(dot) >= 0.99;

            if (_fittingSelector != null)
            {
                if (isCollinear)
                {
                    if (Math.Abs(_lastPipe.InnerDiameter - _currentDiameter) > 0.1)
                    {
                        fitting = _fittingSelector.SelectReducer(_lastPipe, newPipe);
                        if (fitting is ReducerEntity reducer)
                        {
                            reducer.SetDirection(v1);
                        }
                    }
                }
                else
                {
                    fitting = _fittingSelector.SelectElbow(_lastPipe, newPipe);
                }
            }
            else
            {
                if (isCollinear)
                {
                    if (Math.Abs(_lastPipe.InnerDiameter - _currentDiameter) > 0.1)
                    {
                        fitting = new ReducerEntity(start, _lastPipe.InnerDiameter, _currentDiameter)
                        {
                            Color = 0xFFFFA500,
                            SystemType = _currentSystemType,
                            PipeMaterialType = PipeMaterial.PPRC_PN20
                        };
                        ((ReducerEntity)fitting).SetDirection(v1);
                    }
                }
                else
                {
                    // Fallback: Manuel Dirsek
                     fitting = new ElbowEntity(start, _currentDiameter, 
                        v1, 
                        v2 
                    )
                    {
                         Color = 0xFFFFA500,
                         SystemType = _currentSystemType
                    };
                }
            }
            
            if (fitting != null)
            {
                createdEntities.Add(fitting);
            }
        }

        createdEntities.Add(newPipe);
        
        _lastPipe = newPipe;
        _lastPoint = adjustedEnd;
        
        return createdEntities;
    }

    /// <summary>
    /// Mevcut bir boru hattından yeni bir branşman (ayrım) başlatır.
    /// </summary>
    /// <param name="targetPipe">Bölünecek ana boru.</param>
    /// <param name="branchPoint">T-Parçasının yerleşeceği nokta.</param>
    /// <returns>Oluşturulan (Yeni Borular, Tee) ve silinmesi gereken (Eski Boru) nesneler hakkında bilgi içeren bir yapı döndürebilir. Şimdilik sadece yenileri döner.</returns>
    public List<CadEntity> StartBranch(PipeEntity targetPipe, Vector3D branchPoint)
    {
        var createdEntities = new List<CadEntity>();
        
        // Geometriyi hesapla
        var dir = (targetPipe.EndPoint - targetPipe.StartPoint).Normalize();
        var branchDir = new Vector3D(-dir.Y, dir.X, 0); 
        
        double diameter = targetPipe.InnerDiameter;
        
        var tee = new TeeEntity(branchPoint, diameter, diameter, dir, branchDir);
        tee.Color = 0xFFFFA500;
        
        // Orijinal boruyu bölerek iki yeni boru oluştur
        var pipeA = new PipeEntity(targetPipe.StartPoint, branchPoint, diameter)
        {
            Color = targetPipe.Color,
            PipeMaterialType = targetPipe.PipeMaterialType,
            FlowRate = targetPipe.FlowRate,
            Layer = targetPipe.Layer,
            SystemType = targetPipe.SystemType
        };
        
        var pipeB = new PipeEntity(branchPoint, targetPipe.EndPoint, diameter)
        {
            Color = targetPipe.Color,
            PipeMaterialType = targetPipe.PipeMaterialType,
            FlowRate = targetPipe.FlowRate,
            Layer = targetPipe.Layer,
            SystemType = targetPipe.SystemType
        }; 
        
        createdEntities.Add(pipeA);
        createdEntities.Add(pipeB);
        createdEntities.Add(tee);
        
        _lastPoint = branchPoint;
        _lastPipe = null; 
        
        return createdEntities;
    }

    /// <summary>
    /// Boru hattı üzerine bir vana yerleştirir ve boruyu böler.
    /// </summary>
    public List<CadEntity> InsertValve(PipeEntity targetPipe, Vector3D insertPoint)
    {
        var createdEntities = new List<CadEntity>();
        
        var dir = (targetPipe.EndPoint - targetPipe.StartPoint).Normalize();
        double angle = Math.Atan2(dir.Y, dir.X); 
        
        double diameter = targetPipe.InnerDiameter;
        
        var valve = new Valve()
        {
            Position = insertPoint,
            InnerDiameter = diameter,
            RotationAngle = angle,
            Color = 0xFFFF0000,
            PipeMaterialType = targetPipe.PipeMaterialType,
            SystemType = targetPipe.SystemType
        };
        
        // Vana boyutu kadar boşluk bırak (Gap)
        double gap = diameter / 2.0; 
        
        var p1End = insertPoint - (dir * gap);
        var p2Start = insertPoint + (dir * gap);
        
        var pipeA = new PipeEntity(targetPipe.StartPoint, p1End, diameter)
        {
            Color = targetPipe.Color,
            PipeMaterialType = targetPipe.PipeMaterialType,
            FlowRate = targetPipe.FlowRate,
            Layer = targetPipe.Layer,
            SystemType = targetPipe.SystemType
        };
        
        var pipeB = new PipeEntity(p2Start, targetPipe.EndPoint, diameter)
        {
             Color = targetPipe.Color,
            PipeMaterialType = targetPipe.PipeMaterialType,
            FlowRate = targetPipe.FlowRate,
            Layer = targetPipe.Layer,
            SystemType = targetPipe.SystemType
        };
        
        createdEntities.Add(pipeA);
        createdEntities.Add(valve);
        createdEntities.Add(pipeB);
        
        return createdEntities;
    }
    private static PipeMaterial GetMaterialForSystem(MechanicalSystemType sysType) => sysType switch
    {
        MechanicalSystemType.DomesticColdWater => PipeMaterial.PPRC_PN20,
        MechanicalSystemType.DomesticHotWater  => PipeMaterial.PPRC_PN20,
        MechanicalSystemType.WasteWater        => PipeMaterial.PVC_SN4,
        MechanicalSystemType.RainWater         => PipeMaterial.PVC_SN4,
        MechanicalSystemType.FireProtection    => PipeMaterial.Steel_Galvanized,
        MechanicalSystemType.Gas               => PipeMaterial.Steel_Galvanized,
        _                                      => PipeMaterial.PPRC_PN20
    };

    private static string GetLayerNameForSystem(MechanicalSystemType sysType) => sysType switch
    {
        MechanicalSystemType.DomesticColdWater => "MEK_TEMIZ_SU",
        MechanicalSystemType.DomesticHotWater  => "MEK_SICAK_SU",
        MechanicalSystemType.WasteWater        => "MEK_PIS_SU",
        MechanicalSystemType.RainWater         => "MEK_YAGMUR",
        MechanicalSystemType.FireProtection    => "MEK_YANGIN",
        MechanicalSystemType.Gas               => "MEK_GAZ",
        MechanicalSystemType.Ventilation       => "MEK_HAVALAND",
        _                                      => "MEK_GENEL"
    };
}

