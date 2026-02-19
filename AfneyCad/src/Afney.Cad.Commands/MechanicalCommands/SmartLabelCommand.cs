using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Akıllı Etiketleme Komutu (SMART_LABEL)
    NEDEN: Projedeki tüm borulara veya seçilen borulara otomatik olarak çap/eğim etiketlerini yerleştirmek için.
    
    NASIL:
    1. Veritabanındaki tüm PipeEntity'leri tara.
    2. Her biri için bir PipeLabelEntity oluştur.
    3. Mevcut etiket varsa mükerrerliği önle (opsiyonel).
*/
public class SmartLabelCommand : ICadCommand
{
    private readonly CadDatabase _database;

    public string CommandName => "ETİKETLE";
    public Vector3D? ActivePoint => null;
    public List<CadEntity> SelectedEntities => new();

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public SmartLabelCommand(CadDatabase database)
    {
        _database = database;
    }

    /*
       NE: Komutu Çalıştır (Start)
       NEDEN: Veritabanındaki tüm boruları tarayarak, her birinin orta noktasına otomatik olarak çap ve sistem bilgisi içeren akıllı etiketler (Smart Labels) serpiştirmek için.
    */
    public void Start()
    {
        int count = 0;
        var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
        
        foreach (var pipe in pipes)
        {
            // Eğer bu borunun zaten bir etiketi yoksa (basit kontrol)
            bool alreadyLabeled = _database.GetAllEntities()
                .OfType<PipeLabelEntity>()
                .Any(l => l.Position.DistanceTo((pipe.StartPoint + pipe.EndPoint) / 2.0) < 500);

            if (!alreadyLabeled)
            {
                var label = new PipeLabelEntity(pipe)
                {
                    Layer = "BORU_ETIKETLERI"
                };
                _database.AddEntity(label);
                count++;
            }
        }

        OnFeedback?.Invoke($"{count} adet akıllı etiket eklendi.");
        OnCompleted?.Invoke();
    }

    public void OnPointerPressed(Vector3D point) { }
    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) Cancel();
    }

    public void Draw(IRenderContext context) { }
    public void Cancel() => OnCompleted?.Invoke();
}
