using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Mahal Analiz Komutu (INSPECT)
    NEDEN: Mevcut bir mahale tıklayarak onun içindeki verileri (FU, Alan vb.) raporlamak için.
*/
public class MahalInspectCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly MechanicalKernel _kernel;
    private readonly Action<MahalEntity, List<SanitaryFixtureEntity>> _onSelected;

    public string CommandName => "INSPECT_MAHAL";
    public Vector3D? ActivePoint => null;
    public List<CadEntity> SelectedEntities => new();

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public MahalInspectCommand(CadDatabase database, MechanicalKernel kernel, Action<MahalEntity, List<SanitaryFixtureEntity>> onSelected)
    {
        _database = database;
        _kernel = kernel;
        _onSelected = onSelected;
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Mahal analiz işleminin başladığını bildirmek ve kullanıcıdan hedef mahali seçmesini istemek için.
    */
    public void Start()
    {
        OnFeedback?.Invoke("ANALİZ: Bilgilerini görmek istediğiniz mahale (oda sınırına) tıklayın...");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: Tıklanan noktadaki mahali tespit ederek içindeki vitrifiye listesini, toplam yükünü ve alanını içeren bir teknik tabloyu koordinatına yerleştirmek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        // Tıklanan noktadaki mahali bul
        var candidates = _database.GetAllEntities().OfType<MahalEntity>();
        var archService = new Afney.Cad.Mechanical.Services.ArchitecturalRecognitionService(_database);
        
        foreach (var mahal in candidates)
        {
            // MÜHENDİSLİK HASSASİYETİ: BoundingBox yerine poligon içi (PIP) testi
            if (archService.IsPointInPolygon(point, mahal.BoundaryPoints))
            {
                // Mahal içindeki vitrifiyeleri topla
                var fixtures = _database.GetAllEntities()
                    .OfType<SanitaryFixtureEntity>()
                    .Where(f => mahal.FixtureIds.Contains(f.Id))
                    .ToList();
                
                // MÜHENDİSLİK HESABI TETİKLE (FINE SANI USULÜ)
                var engService = new Afney.Cad.Mechanical.Services.PlumbingEngineeringService(_database, 
                    _kernel.TopologyGraph);
                
                engService.CalculateByMahal(mahal.Id);

                // --- YENİ: TEKNİK TABLO OLUŞTUR VE YERLEŞTİR ---
                var scheduleService = new Afney.Cad.Mechanical.Services.MahalScheduleService();
                var tableEntities = scheduleService.GenerateRoomTable(mahal, fixtures, point + new Vector3D(1000, 1000, 0)); // Tıklanan yerin yanına

                foreach (var ent in tableEntities)
                    _database.AddEntity(ent);

                _onSelected?.Invoke(mahal, fixtures);
                OnCompleted?.Invoke();
                return;
            }
        }
        
        OnFeedback?.Invoke("Hata: Tıklanan noktada bir mahal bulunamadı.");
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) 
    {
        if (key == InputKey.Escape) Cancel();
    }

    public void Draw(IRenderContext context) { }
    public void Cancel() => OnCompleted?.Invoke();
}
