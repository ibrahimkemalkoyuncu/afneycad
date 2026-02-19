using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Profesyonel Mimari Blok Komutu (WBlock)
    NEDEN: Kemal Bey'in belirttiği gibi nesneleri seçip, bir kat ismi vererek projeye kaydetmek için.
*/
public class ArchitecturalBlockCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly ArchitecturalScaleService _scaleService;
    private readonly Action<ArchitecturalBlockCommand> _onRequestFloorInfo;
    
    public enum WBlockState
    {
        Idle,
        PickingPoint,
        SelectingEntities
    }

    private WBlockState _state = WBlockState.Idle;
    private Vector3D _basePoint;
    private List<CadEntity> _selectedEntities = new();

    public string CommandName => "WBLOCK";
    public Vector3D? ActivePoint => _basePoint;
    public List<CadEntity> SelectedEntities => _selectedEntities;
    public Vector3D BasePoint => _basePoint;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;
    
    // UI Notification Events
    public event Action? OnPointPicked;
    public event Action? OnEntitiesSelected;

    public ArchitecturalBlockCommand(CadDatabase database, Action<ArchitecturalBlockCommand> onRequestFloorInfo)
    {
        _database = database;
        _scaleService = new ArchitecturalScaleService();
        _onRequestFloorInfo = onRequestFloorInfo;
    }

    public void Start()
    {
         OnFeedback?.Invoke("WBLOCK: Komut başlatıldı (Start)");
    }

    /*
       NE: Nokta Seçimi Başlat
       NEDEN: Diyalogdan "Nokta Seç" butonuna basıldığında komutu bu moda geçirmek için.
    */
    public void StartPickPoint()
    {
        Serilog.Log.Information("WBLOCK: StartPickPoint called. State changing to PickingPoint.");
        _state = WBlockState.PickingPoint;
        OnFeedback?.Invoke("WBLOCK: Referans noktasını seçin (Lütfen ekrana tıklayın)...");
    }

    /*
       NE: Nesne Seçimi Başlat
       NEDEN: Diyalogdan "Nesne Seç" butonuna basıldığında komutu seçim moduna geçirmek için.
    */
    public void StartSelection()
    {
        Serilog.Log.Information("WBLOCK: StartSelection called.");
        _state = WBlockState.SelectingEntities;
        OnFeedback?.Invoke("WBLOCK: Kaydedilecek nesneleri seçin (Bitince Enter)...");
        // Mevcut seçimi temizle veya koru? Genelde kullanıcı yeni seçim yapmak ister.
        _selectedEntities.Clear();
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: Aktif duruma göre (Nokta seçimi veya Nesne seçimi) işlemi yürütmek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        Serilog.Log.Information($"WBLOCK: OnPointerPressed at {point}. Current State: {_state}");

        if (_state == WBlockState.PickingPoint)
        {
            _basePoint = point;
            _state = WBlockState.Idle; // İşlem bitti, bekleme moduna dön
            
            Serilog.Log.Information("WBLOCK: Point picked. Invoking OnPointPicked event.");
            OnFeedback?.Invoke($"Nokta Seçildi: {_basePoint}");
            OnPointPicked?.Invoke(); // UI'ya haber ver (Diyaloğu geri aç)
        }
        else if (_state == WBlockState.SelectingEntities)
        {
            // Tekli seçim mantığı (Veya pencere seçimi SelectionManager halleder)
            // Burada basitçe tıklanan noktadaki entity'i bulup ekleyelim
            // Ama genelde SelectionManager kullanılır.
            // Bu metod "Tıklama" anıdır. SelectionManager zaten seçim yapıyorsa buraya gerek kalmayabilir.
            // Ancak "Enter" ile bitirme mantığı OnKeyDown'da.
        }
    }

    public void OnPointerMoved(Vector3D point) { }
    
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter && _state == WBlockState.SelectingEntities)
        {
            _state = WBlockState.Idle;
            OnFeedback?.Invoke($"Seçim Tamamlandı: {_selectedEntities.Count} nesne.");
            OnEntitiesSelected?.Invoke(); // UI'ya haber ver
        }
        else if (key == InputKey.Escape)
        {
            // İptal edilirse ne olacak? Diyaloğa geri mi dönsek yoksa komple mi çıksak?
            // Kullanıcı ESC bastıysa komple iptal etmek daha doğal.
            Cancel();
        }
    }

    // Seçim yöneticisinden gelen güncellemeleri almak için
    public void SetSelectedEntities(IEnumerable<CadEntity> entities)
    {
        _selectedEntities = entities.ToList();
        if (_state == WBlockState.SelectingEntities)
        {
             OnFeedback?.Invoke($"Seçili: {_selectedEntities.Count} nesne (Bitince Enter)");
        }
    }

    public void Draw(IRenderContext context)
    {
        // Base Point görselleştirme
        context.DrawCircle(_basePoint, 0.5, 0xFF00FFFF, 1.0, false);
        // Crosshair
        context.DrawLine(_basePoint + new Vector3D(-1, 0, 0), _basePoint + new Vector3D(1, 0, 0), 0xFF00FFFF, 1.0);
        context.DrawLine(_basePoint + new Vector3D(0, -1, 0), _basePoint + new Vector3D(0, 1, 0), 0xFF00FFFF, 1.0);
    }

    public void Cancel() 
    {
        _state = WBlockState.Idle;
        OnCompleted?.Invoke();
    }

    /*
        NE: Dışa Aktarımı Başlat
        NOT: Bu metod dışarıdan (UI) çağrılır.
    */
    /*
        NE: Dışa Aktarımı Başlat (FinalizeExport)
        NEDEN: Seçilen kat nesnelerini, kat ismi ve proje klasörü bazında .afney (json) formatında saklayarak, 3D bina montajı için hazır hale getirmek için.
    */
    public void FinalizeExport(string floorName, string projectPath, string serializedData)
    {
        if (string.IsNullOrEmpty(serializedData)) return;

        string filePath = System.IO.Path.Combine(projectPath, $"{floorName}.afney");
        System.IO.File.WriteAllText(filePath, serializedData);

        OnCompleted?.Invoke();
        OnFeedback?.Invoke($"{floorName} başarıyla kaydedildi.");
    }
}
