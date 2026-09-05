using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Blocks;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
    NE: Blok Oluşturma Komutu (BLOCK)
    NEDEN: Kullanıcının seçtiği nesneleri bir isim altında gruplayıp blok tanımı (BlockRecord) oluşturmak için.
*/
public class BlockCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;

    // Command layer UI bilmez, o yüzden callback veya interface ile tetikleriz.
    private readonly Func<BlockCommand, bool?> _onOpenBMakeDialog;
    
    // Durum
    private int _step = 0;
    private List<CadEntity> _selectedEntities = new();
    private Vector3D _basePoint;
    
    // Geçici olarak UI tarafının sağladığı Action delegasyonları
    private Action<Vector3D>? _onBasePointPicked;
    private Action? _onObjectsSelected;

    public string CommandName => "BLOCK";
    public Vector3D? ActivePoint => _basePoint;
    public List<CadEntity> SelectedEntities => _selectedEntities;

    // NE: Dışarıdan okuyabilmek için, komut obje seçimi bekliyor mu?
    public bool IsSelectingObjects => _step == 2;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    // Presentation katmanı (MainWindow) Command'ı üretirken BMakeDialog açıcı Factory fonksiyonu geçmeli
    public BlockCommand(CadDatabase database, TransactionManager transactionManager, Func<BlockCommand, bool?> onOpenBMakeDialog)
    {
        _database = database;
        _transactionManager = transactionManager;
        _onOpenBMakeDialog = onOpenBMakeDialog;
    }

    public void Start()
    {
        // 1. Önce seçim var mı kontrol et
        _selectedEntities = _database.GetSelectedEntities().ToList();
        
        // UI Dialog'u aç - Dialog işlemi bitirdiğinde (OK) form zaten FinalizeBlock()'u çağırır.
        _onOpenBMakeDialog?.Invoke(this);
    }
    
    /*
       NE: BMakeDialog'dan Tetiklenen Pick Point
    */
    public void RequestPickPoint(Action<Vector3D> onPicked)
    {
        _step = 1;
        _onBasePointPicked = onPicked;
        OnFeedback?.Invoke("BLOCK: Referans noktası (Base Point) seçin...");
    }

    /*
       NE: BMakeDialog'dan Tetiklenen Select Objects
    */
    public void RequestSelectObjects(Action onSelected)
    {
        _step = 2;
        _onObjectsSelected = onSelected;
        OnFeedback?.Invoke("BLOCK: Blok yapılacak nesneleri seçin (Enter ile bitirin)...");
    }

    // UI tarafına (BMakeDialog) haber verecek delegasyonlar
    private Action<Vector3D>? _updateBasePointInDialog;
    private Action? _updateSelectionCountInDialog;
    private Action? _showDialogAction;
    private Action? _closeDialogAction;

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: BMAKE dialogundan "Ekranda Seç" butonuna basıldığında tıklanan noktayı bloğun yerel orijini olarak (BasePoint) kaydetmek veya obje seçtirmek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_step == 1) // Pick Base Point
        {
            _basePoint = point;
            OnFeedback?.Invoke($"Base point selected: {point.X:F2}, {point.Y:F2}");
            
            // UI tarafındaki dialoga noktayı gönder
            _updateBasePointInDialog?.Invoke(point);
            
            // Dialogu tekrar göster
            _showDialogAction?.Invoke();
            _step = 0; // Komut tekrar idle (dialog içinde) moda döner
        }
        else if (_step == 2) // Pick Objects
        {
            // Seçim işlemi (genelde SelectionManager ile halledilir)
        }
    }

    public void OnPointerMoved(Vector3D point) { }

    /*
       NE: Klavye Girişi (OnKeyDown)
    */
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter && _step == 2)
        {
            // Seçimi tamamla ve dialoga geri dön
            _selectedEntities = _database.GetSelectedEntities().ToList();
            if (_selectedEntities.Count == 0)
            {
                OnFeedback?.Invoke("Uyarı: Hiç nesne seçilmedi.");
            }
            
            _updateSelectionCountInDialog?.Invoke();
            _showDialogAction?.Invoke();
            _step = 0;
        }
        else if (key == InputKey.Escape)
        {
            if (_step == 1 || _step == 2)
            {
                // Seçim yapmaktan vazgeçip dialoga dön
                _showDialogAction?.Invoke();
                _step = 0;
            }
            else
            {
                Cancel();
            }
        }
    }

    /*
       NE: BMakeDialog'dan Tetiklenen Pick Point
    */
    public void RequestPickPoint(Action<Vector3D> updatePointCb, Action showDialogCb)
    {
        _step = 1;
        _updateBasePointInDialog = updatePointCb;
        _showDialogAction = showDialogCb;
        OnFeedback?.Invoke("BLOCK: Referans noktası (Base Point) seçin...");
    }

    /*
       NE: BMakeDialog'dan Tetiklenen Select Objects
    */
    public void RequestSelectObjects(Action updateSelectionCb, Action showDialogCb)
    {
        _step = 2;
        _updateSelectionCountInDialog = updateSelectionCb;
        _showDialogAction = showDialogCb;
        OnFeedback?.Invoke("BLOCK: Blok yapılacak nesneleri seçin (Enter ile bitirin)...");
    }

    public void RegisterCloseCallback(Action closeDialogCb)
    {
        _closeDialogAction = closeDialogCb;
    }

    public void FinalizeBlock(string name, Vector3D basePnt, int behavior)
    {
        _selectedEntities = _database.GetSelectedEntities().ToList();

        if (string.IsNullOrWhiteSpace(name))
        {
            OnFeedback?.Invoke("Hata: Geçersiz blok ismi.");
            Cancel();
            return;
        }

        if (_selectedEntities.Count == 0)
        {
            OnFeedback?.Invoke("Hata: Blok yapılacak nesne seçilmedi.");
            Cancel();
            return;
        }

        var blockRecord = new CadBlockRecord(name)
        {
            BasePoint = basePnt
        };

        var transformToLocal = Matrix4x4.TranslationMatrix(-basePnt.X, -basePnt.Y, -basePnt.Z);

        foreach (var ent in _selectedEntities)
        {
            var clone = ent.Clone();
            clone.Transform(transformToLocal); 
            blockRecord.Entities.Add(clone);
        }

        if (_database.GetBlock(name) == null)
        {
            _database.AddBlock(blockRecord);
        }
        else
        {
            OnFeedback?.Invoke($"Hata: '{name}' blok ismi zaten kullanılıyor.");
            _showDialogAction?.Invoke();
            return;
        }

        // NE/NEDEN — GERÇEK HATA (Session #75 mimari denetiminde bulundu): behavior 1/2'de
        // seçili nesnelerin silinmesi ve (behavior 1'de) yeni INSERT'in eklenmesi doğrudan
        // _database üzerinden yapılıyordu — Ctrl+Z bu adımı geri alamıyordu (blok tanımı
        // kalıcı olsa da, orijinal geometrinin silinmesi/INSERT'in eklenmesi undone olmuyordu).
        // Artık tek bir CompositeOperation'da toplanıp TransactionManager'a submit ediliyor.
        if (behavior == 1 || behavior == 2)
        {
            var composite = new CompositeOperation("Bloğa Dönüştür");
            foreach (var ent in _selectedEntities)
            {
                composite.Add(new RemoveEntityOperation(_database, ent));
            }

            if (behavior == 1)
            {
                var insert = new BlockReferenceEntity(name, basePnt);
                composite.Add(new AddEntityOperation(_database, insert));
            }

            _transactionManager.Submit(composite);
        }

        _database.ClearSelection();
        OnFeedback?.Invoke($"Blok '{name}' başarıyla oluşturuldu.");
        _closeDialogAction?.Invoke();
        OnCompleted?.Invoke();
    }

    public void Cancel()
    {
        _database.ClearSelection();
        _closeDialogAction?.Invoke();
        OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context)
    {
    }
}
