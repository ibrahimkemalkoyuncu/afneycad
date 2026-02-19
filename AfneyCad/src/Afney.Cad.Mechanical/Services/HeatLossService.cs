using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Isı Kaybı Analiz Servisi (HeatLossService)
    NEDEN: Isıtma ve sıcak su tesisatında boru hattı boyunca suyun sıcaklık düşüşünü (Temperature Drop) hesaplamak için.
    
    ALGORİTMA: FDM (Finite Difference Method) - Sonlu Farklar Yöntemi
    1. Boruyu küçük segmentlere (Örn: 10cm) böler.
    2. Her segmentte çevreye olan ısı transferini (Q = U * A * ΔT) hesaplar.
    3. Suyun o segmentten çıkış sıcaklığını bir sonraki segmentin giriş sıcaklığı yapar.
    4. Tüm boru hattı boyunca sıcaklık profilini çıkarır.
*/
public class HeatLossService
{
    private const double WATER_HEAT_CAPACITY = 4186.0; // J/(kg·K)
    private const double WATER_DENSITY = 998.0; // kg/m³

    public class HeatLossResult
    {
        public double InletTemperature { get; set; }
        public double OutletTemperature { get; set; }
        public double TotalTemperatureDrop => InletTemperature - OutletTemperature;
        public double TotalHeatLossWatts { get; set; }
    }

    /*
        NE: Boru Isı Kaybını Hesapla (CalculatePipeHeatLoss)
        AMACI: Tek bir boru elemanı boyunca sıcaklık düşüşünü FDM ile simüle eder.
    */
    public HeatLossResult CalculatePipeHeatLoss(PipeEntity pipe, double ambientTemperature, double uValue = 0.5)
    {
        double lengthMm = pipe.GetLength();
        double lengthM = lengthMm / 1000.0;
        
        // FDM Parametreleri
        double dx = 0.1; // 10cm segmentler
        int steps = (int)(lengthM / dx);
        if (steps == 0) steps = 1;
        dx = lengthM / steps;

        double currentTemp = pipe.Temperature;
        double inletTemp = currentTemp;
        
        // Debiyi kg/s cinsine çevir ( m³/h -> kg/s )
        double massFlowRate = (pipe.FlowRate * WATER_DENSITY) / 3600.0;
        
        if (massFlowRate <= 0) return new HeatLossResult { InletTemperature = inletTemp, OutletTemperature = inletTemp };

        double innerRadiusM = (pipe.InnerDiameter / 1000.0) / 2.0;
        double surfaceAreaStep = 2 * Math.PI * innerRadiusM * dx;

        double totalHeatLoss = 0;

        // FDM Iteration
        for (int i = 0; i < steps; i++)
        {
            // Fourier Yasası + Enerji Dengesi
            // dQ = U * Area * (T_water - T_ambient)
            double dQ = uValue * surfaceAreaStep * (currentTemp - ambientTemperature);
            
            // dT = dQ / ( m_dot * Cp )
            double dT = dQ / (massFlowRate * WATER_HEAT_CAPACITY);
            
            currentTemp -= dT;
            totalHeatLoss += dQ;
        }

        return new HeatLossResult
        {
            InletTemperature = inletTemp,
            OutletTemperature = currentTemp,
            TotalHeatLossWatts = totalHeatLoss
        };
    }
}
