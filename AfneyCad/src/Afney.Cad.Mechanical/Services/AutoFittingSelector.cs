using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Standards;
using System;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Otomatik Fittings Seçici (AutoFittingSelector)
   NEDEN: Boru tesisatındaki bağlantı noktalarına (Köşe, T, Redüksiyon) en uygun fittings parçasını seçmek için.
   
   NASIL (Mebrure Hanım'ın Kütüphanesi):
   - Standartlar Kütüphanesi'nden (StandardsLibrary) boru malzemesini ve normunu sorgular.
   - Açıyı kontrol eder (45°, 90°).
   - Çap değişimlerini (Redüksiyon) algılar.
*/
public class AutoFittingSelector
{
    private readonly StandardsLibrary _standardsLibrary;

    public AutoFittingSelector(StandardsLibrary standardsLibrary)
    {
        _standardsLibrary = standardsLibrary;
    }

    /// <summary>
    /// İki boru arasındaki bağlantı için Dirsek (Elbow) seçer.
    /// </summary>
    public MechanicalEntity SelectElbow(PipeEntity pipe1, PipeEntity pipe2)
    {
        // 1. AÇI HESABI
        var v1 = (pipe1.EndPoint - pipe1.StartPoint).Normalize();
        var v2 = (pipe2.EndPoint - pipe2.StartPoint).Normalize();
        
        // Dot Product ile açı bulma
        double dot = v1.Dot(v2);
        double angleRad = Math.Acos(dot);
        double angleDeg = angleRad * (180.0 / Math.PI);
        
        // 2. TİP SEÇİMİ (90° mi 45° mi?)
        // Tolerans: ±1 derece (Mühendislik Hassasiyeti)
        bool is90 = Math.Abs(angleDeg - 90) <= 1.0 || Math.Abs(angleDeg - 270) <= 1.0;
        bool is45 = Math.Abs(angleDeg - 45) <= 1.0 || Math.Abs(angleDeg - 315) <= 1.0;

        // 3. FITTINGS OLUŞTURMA
        // Şimdilik jenerik bir Elbow dönüyoruz ama ileride StandardsLibrary'den "PPRC 90 Dirsek" nesnesi döneceğiz.
        var elbow = new ElbowEntity(pipe1.EndPoint, pipe1.InnerDiameter, v1, v2)
        {
            Color = pipe1.Color, // Boru rengini alır
            SystemType = pipe1.SystemType,
            PipeMaterialType = pipe1.PipeMaterialType
        };

        if (!is90 && !is45)
        {
            // Standart dışı açı: hazır (45°/90°) bir dirsek bu açıyı karşılayamaz.
            // "Mühendislik Modu": burada hata fırlatıp akışı durdurmak yerine, özel üretim
            // fitting veya esnek boru gerektiğini nesne üzerinde işaretleyip kullanıcıya bırakıyoruz.
            elbow.EngineeringWarning =
                $"Standart dışı açı ({angleDeg:F2}°) — özel üretim fittings veya esnek boru gerekebilir.";
            System.Diagnostics.Debug.WriteLine($"[UYARI] {elbow.EngineeringWarning}");
        }

        return elbow;
    }

    /// <summary>
    /// Ana borudan ayrılan hat için T-Parçası (Tee) seçer.
    /// </summary>
    public MechanicalEntity SelectTee(PipeEntity mainPipe, PipeEntity branchPipe)
    {
        // İleride "İnegal T" vs "Eşit T" kontrolü burada yapılacak.
        return new TeeEntity(
            branchPipe.StartPoint, 
            mainPipe.InnerDiameter, 
            branchPipe.InnerDiameter,
            (mainPipe.EndPoint - mainPipe.StartPoint).Normalize(),
            (branchPipe.EndPoint - branchPipe.StartPoint).Normalize())
        {
             Color = mainPipe.Color,
             SystemType = mainPipe.SystemType,
             PipeMaterialType = mainPipe.PipeMaterialType
        };
    }

    /// <summary>
    /// Farklı çaplardaki iki boru arasına Redüksiyon seçer.
    /// </summary>
    public MechanicalEntity? SelectReducer(PipeEntity pipe1, PipeEntity pipe2)
    {
        if (Math.Abs(pipe1.InnerDiameter - pipe2.InnerDiameter) < 1.0) 
            return null; // Çaplar aynı, redüksiyon gerekmez.

        return new ReducerEntity(pipe1.EndPoint, pipe1.InnerDiameter, pipe2.InnerDiameter)
        {
             Color = pipe1.Color,
             SystemType = pipe1.SystemType,
             PipeMaterialType = pipe1.PipeMaterialType
        };
    }
}
