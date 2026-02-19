using Afney.Cad.Mechanical.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Engine.Hydraulics;

/*
   NE: Hardy-Cross Hidrolik Çözücü (HardyCrossSolver)
   NEDEN: Kapalı döngü (halkalı) boru şebekelerinde, basınç dengesini (Kirchhoff II. Yasası) sağlayacak debi dağılımını hesaplamak için.

   MÜHENDİSLİK DETAYI:
   - Iterative Correction (Ardışıl Yaklaşım) yöntemini kullanır.
   - Her halka için ΔQ = -Σ(hL) / Σ(n * hL / Q) formülü uygulanır.
   - n değeri Darcy-Weisbach akış rejimi için 2.0 (veya akışa göre 1.85-2.0 arası) kabul edilir.
   - Çözümün geçerliliği için şebekenin topolojik olarak "Düğüm Noktası Dengesi" (Kirchhoff I. Yasası) sağlanmış olmalıdır.
   
   KULLANIM:
   - Şebeke tasarımı tamamlandıktan sonra debi ve basınç analizi için tetiklenir.
*/
public class HardyCrossSolver
{
    private const int MaxIterations = 100;
    private const double Tolerance = 0.001; // 1 L/h hassasiyet (m³/h cinsinden)

    public void Solve(HydraulicNetwork network)
    {
        // ÖNŞART: Kirchhoff I. Kanunu (Debi Dengesi) sağlanmış olmalı.
        InitializeFlows(network);

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            bool converged = true;
            
            // TODO: Topolojik Rota Analizi ile Halka Tespiti (Loop Detection) eklenecek.
            // Şimdilik sistemin tek bir halkadan oluştuğu (Basic Loop) kabul ediliyor.
            
            double sumHeadLoss = 0;
            double sumDerivative = 0; // Σ(n * hL / Q)

            foreach (var pipe in network.Pipes)
            {
                double qAbs = Math.Abs(pipe.FlowRate);
                
                // Basınç düşümü hesabı (MechanicalCalculations servisi kullanılır)
                // Sonuç bar -> mSS (Metre Su Sütunu) çevrimi yapılır.
                double pressureDropBar = MechanicalCalculations.CalculatePressureDrop(
                    pipe.Length, 
                    pipe.InnerDiameter, // Düzeltme: Diameter -> InnerDiameter
                    qAbs, 
                    pipe.Material ?? "Steel", 
                    20.0 
                );
                
                double headLoss = pressureDropBar * 10.197; // 1 bar ≈ 10.197 mSS
                
                // Akış yönü işareti (Sign convention: Saat yönü pozitif)
                double sign = Math.Sign(pipe.FlowRate); 
                
                sumHeadLoss += sign * headLoss;
                
                if (qAbs > 1e-6)
                    sumDerivative += 2.0 * headLoss / qAbs; // Darcy-Weisbach için n=2
            }

            // Payda sıfır kontrolü (Mühendislik Emniyeti)
            if (Math.Abs(sumDerivative) < 1e-12) break;

            // Hardy-Cross Düzeltme Faktörü (Correction Factor)
            double deltaQ = -sumHeadLoss / sumDerivative;

            // Halka üzerindeki debileri güncelle
            foreach (var pipe in network.Pipes)
            {
                pipe.FlowRate += deltaQ;
            }

            // Yakınsama kontrolü
            if (Math.Abs(deltaQ) > Tolerance)
                converged = false;

            if (converged) break;
        }

        UpdateResults(network);
    }

    private void InitializeFlows(HydraulicNetwork network)
    {
        // TODO: Rastgele değer yerine 'Spanning Tree' tabanlı debi dağıtıcı yazılacak.
        foreach (var pipe in network.Pipes)
        {
            if (pipe.FlowRate == 0) pipe.FlowRate = 1.0; 
        }
    }

    private void UpdateResults(HydraulicNetwork network)
    {
        foreach (var pipe in network.Pipes)
        {
            double qAbs = Math.Abs(pipe.FlowRate);
            double pBar = MechanicalCalculations.CalculatePressureDrop(
                pipe.Length, pipe.InnerDiameter, qAbs, pipe.Material, 20.0);
            
            pipe.HeadLoss = pBar * 10.197;
        }
    }
}

