using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Mechanical.Services;

/*
    NE: Otomatik Boru Etiketleme Servisi (AutoPipeLabeler)
    NEDEN: FINE SANI standardında, hidrolik hesap sonuçlarını (Örn: DN 50) çizim üzerine otomatik metin olarak basmak için.
    
    NASIL (Mühendislik Modu):
    1. Sistemdeki tüm PipeEntity nesnelerini tarar.
    2. Borunun orta noktasına, boru açısına paralel olarak "DN [Çap]" metnini yerleştirir.
    3. Çakışmaları önlemek için hafif bir offset (öteleme) uygular.
*/
public class AutoPipeLabeler
{
    private readonly CadDatabase _database;

    public AutoPipeLabeler(CadDatabase database)
    {
        _database = database;
    }

    public void LabelAllPipes()
    {
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        string layerName = "M_PIPE_LABELS";

        // Varsa eski etiketleri temizle
        var existingLabels = _database.GetAllEntities()
            .Where(e => e.Layer == layerName)
            .ToList();
            
        foreach(var old in existingLabels) _database.RemoveEntity(old.Id);

        foreach (var pipe in pipes)
        {
            if (pipe.InnerDiameter <= 0) continue;

            // 1. Konum: Borunun orta noktası
            Vector3D mid = new Vector3D(
                (pipe.StartPoint.X + pipe.EndPoint.X) / 2.0,
                (pipe.StartPoint.Y + pipe.EndPoint.Y) / 2.0,
                0
            );

            // 2. Açı: Boru açısını hesapla
            double dx = pipe.EndPoint.X - pipe.StartPoint.X;
            double dy = pipe.EndPoint.Y - pipe.StartPoint.Y;
            double angleRad = Math.Atan2(dy, dx);
            double angleDeg = angleRad * (180.0 / Math.PI);

            // AutoCAD Standardı: Yazı okunabilir olmalı (Baş aşağı olmamalı)
            if (angleDeg > 90 || angleDeg < -90) angleDeg += 180;

            // 3. Etiket Oluştur (DN XX ve opsiyonel Debi Q)
            string labelText = $"DN {pipe.InnerDiameter}";
            if (pipe.FlowRate > 0) labelText += $" (Q:{pipe.FlowRate:F1})";

            var label = new TextEntity(labelText, mid, 120) // Yazı boyu 120mm yapıldı (daha kompakt)
            {
                Layer = layerName,
                Color = 0xFF00FFFF, // Cyan (Mekanik Etiket Rengi)
                Rotation = angleDeg
            };

            // Borunun tam üstüne binmesin diye az bir miktar dik yönde ötele
            Vector3D normal = new Vector3D(-Math.Sin(angleRad), Math.Cos(angleRad), 0);
            label.Position += normal * 80; // 80mm offset

            _database.AddEntity(label);
        }
    }
}
