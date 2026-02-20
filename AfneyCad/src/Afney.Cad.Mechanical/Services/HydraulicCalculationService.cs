using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Mechanical.Services;

/*
   NE: Hidrolik Hesap ve Çap Tayin Motoru (HydraulicCalculationService)
   NEDEN: TS 1258 ve DIN 1988 standartlarına göre, toplam yük biriminden (LU) yola çıkarak boru çaplarını otomatik belirlemek için.
   
   MÜHENDİSLİK FORMÜLLERİ:
   - Toplam Yük Birimi (Total LU) -> Tasarım Debisi (Q_design)
   - Q = 0.615 * sqrt(Total LU) - 0.17  (Örnek Konut Formülü)
   - Çap Hesabı: d = sqrt((4 * Q) / (pi * V * 3600))
   - Kritik Hız (V): 1.5 m/s (Konfor sınırı)
*/
public class HydraulicCalculationService
{
    // Çelik/Plastik borular için standart ticari çaplar (İç Çap mm)
    private readonly double[] _standardDiameters = { 15, 20, 25, 32, 40, 50, 65, 80, 100 };

    public double CalculateDesignFlow(double totalLU)
    {
        if (totalLU <= 0) return 0;
        
        // Örnek: DIN 1988 Konut Tipi Debi Formülü (m3/h)
        // Not: Gerçekte LU -> Q dönüşüm tabloları kullanılır, burada bilimsel yaklaşım uyguluyoruz.
        double flowInLps = 0.25 * Math.Sqrt(totalLU); // Litre/saniye cinsinden yaklaşık
        return flowInLps * 3.6; // m3/h dönüşümü
    }

    public double DeterminePipeDiameter(double flowM3h, double limitVelocity = 1.5)
    {
        if (flowM3h <= 0) return _standardDiameters[0];

        // Alan = Q / V
        double area = (flowM3h / 3600.0) / limitVelocity;
        
        // d = sqrt(4A / pi)
        double theoreticalDiameter = Math.Sqrt((4 * area) / Math.PI) * 1000; // mm

        // En yakın ÜST standart çapı seç
        return _standardDiameters.FirstOrDefault(d => d >= theoreticalDiameter);
    }

    /*
       NE: Mahal Bazlı Çap Önerisi
       AMACI: Bir odanın (mahalin) içindeki tüm vitrifiyeleri toplayıp girişteki boru çapını söyler.
    */
    public (double TotalLU, double Flow, double Diameter) AnalyzeMahalHydraulics(MahalEntity mahal)
    {
        double totalLU = mahal.Fixtures.Sum(f => f.LoadUnits);
        double flow = CalculateDesignFlow(totalLU);
        double diameter = DeterminePipeDiameter(flow);
        
        return (totalLU, flow, diameter);
    }

    /*
       NE: Sirkülasyon Pompası Debi Hesabı
       AMACI: Sıcak su dönüş (HotWaterReturn) hattındaki ısı kaybını karşılayacak sirkülasyon pompası debisini hesaplamak.
       FORMÜL: P(Watt) = m(kg/s) * c * DeltaT 
               Debi(m³/h) = (P / (c * DeltaT)) * 3.6
    */
    public double CalculateCirculationPumpFlow(double heatLossWatt, double deltaT = 5.0)
    {
        if (heatLossWatt <= 0 || deltaT <= 0) return 0;
        
        // Su için özgül ısı (c) = 4.18 kJ/kg.K = 4180 J/kg.K
        double massFlowKgPerSec = heatLossWatt / (4180.0 * deltaT);
        
        // Yoğunluk 1 kg/L kabulü ile m³/h dönüşümü
        double volumeFlowM3h = massFlowKgPerSec * 3.6;
        
        return volumeFlowM3h;
    }
}
