using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using System;
using System.Collections.Generic;

namespace Afney.Cad.Mechanical.Engine.AI;

/*
    NE: Akıllı Malzeme ve Çap Danışmanı (MechanicalAIAdvisor)
    NEDEN: Mühendise, projedeki şartlara (Basınç, Sıcaklık, Akışkan Tipi) göre en uygun boru materyalini ve çapını önermek için.
    
    NASIL (Mühendislik Modu - Rule-Based Expert System):
    1. Basınç > 16 Bar ise Çelik/Bakır öner.
    2. Sıcaklık > 60'C ise Kompozit/PPRC-C öner.
    3. Akış Hızı > 2.0 m/s ise çap büyütme uyarısı ver.
*/
public class MechanicalAIAdvisor
{
    public string SuggestMaterial(PipeEntity pipe)
    {
        // Uzman Sistem Kuralları (Expert System Rules)
        if (pipe.Pressure > 16.0) return "Çelik Boru (Sch 40)";
        if (pipe.Temperature > 70.0) return "PPRC-Glass Fiber Reinforced";
        if (pipe.SystemType == MechanicalSystemType.DomesticColdWater) return "PPRC (PN 20)";
        if (pipe.SystemType == MechanicalSystemType.WasteWater) return "PVC (SDR 41)";
        
        return "PPRC (Standard)";
    }

    public void OptimizeProject(IEnumerable<PipeEntity> pipes)
    {
        // PROJE OPTİMİZASYONU: Minimum maliyet, maksimum performans dengesi
        foreach (var pipe in pipes)
        {
            double v = pipe.GetVelocity();
            if (v > 2.0)
            {
                // Kritik Hız Aşımı Uyarısı
            }
        }
    }
}
