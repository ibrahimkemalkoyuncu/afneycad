using System;
using System.Collections.Generic;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
    NE: Blok Ekleme Komutu (INSERT)
    NEDEN: Model Space'e daha önce tanımlanmış bir bloğu (BlockRecord) yerleştirmek (BlockReference) için.
*/
public class InsertCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly Action<InsertCommand> _onRequestBlock;

    private int _step = 0;
    private string _blockName = string.Empty;
    private Vector3D _position;

    // Geçici görselleştirme için
    private BlockReferenceEntity? _ghostEntity;

    public string CommandName => "INSERT";
    public Vector3D? ActivePoint => _position;
    public List<CadEntity> SelectedEntities => new(); // Insert yaparken seçim yok

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public InsertCommand(CadDatabase database, TransactionManager transactionManager, Action<InsertCommand> onRequestBlock)
    {
        _database = database;
        _transactionManager = transactionManager;
        _onRequestBlock = onRequestBlock;
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Veritabanındaki blok kütüphanesini kontrol etmek ve kullanıcıdan bir blok seçmesini istemek için.
    */
    public void Start()
    {
        // 1. Veritabanında hiç blok var mı bak
        var blocks = _database.GetBlocks();
        if (!blocks.Any())
        {
            OnFeedback?.Invoke("Hata: Çizimde hiç blok tanımı yok. Önce BLOCK komutunu kullanın.");
            OnCompleted?.Invoke();
            return;
        }

        // 2. Blok seçimi iste
        OnFeedback?.Invoke("INSERT: Yerleştirilecek bloğu seçin...");
        _step = 1;
        _onRequestBlock?.Invoke(this);
    }
    
    // UI'dan çağrılır
    /*
       NE: Blok Tipini Seç (SetBlock)
       NEDEN: Kullanıcının diyalog üzerinden seçtiği bloğu komuta atamak ve hayalet (ghost) önizlemesini başlatmak için.
    */
    public void SetBlock(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            OnCompleted?.Invoke(); // İptal
            return;
        }
        
        var block = _database.GetBlock(name);
        if (block == null)
        {
            OnFeedback?.Invoke($"Hata: '{name}' bloğu bulunamadı.");
            OnCompleted?.Invoke();
            return;
        }

        _blockName = name;
        
        // Ghost entity oluştur
        _ghostEntity = new BlockReferenceEntity(_blockName, Vector3D.Zero)
        {
            Definition = block // Çizim için gerekli
        };

        OnFeedback?.Invoke($"INSERT: '{name}' bloğunu yerleştirmek için nokta seçin...");
        _step = 2;
    }

    public void OnPointerPressed(Vector3D point)
    {
        if (_step == 2)
        {
            _position = point;
            
            // Bloğu oluştur ve veritabanına ekle
            var entity = new BlockReferenceEntity(_blockName, _position);
            
            // Runtime reference'ı bağla (Önemli!)
            // Normalde bu işlem AddEntity sırasında veya Load sırasında otomatik yapılmalı.
            // Ama şimdilik manual bağlıyoruz.
            entity.Definition = _database.GetBlock(_blockName);

            _transactionManager.Submit(new AddEntityOperation(_database, entity));

            OnFeedback?.Invoke($"Blok yerleştirildi: {_blockName}");
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_step == 2 && _ghostEntity != null)
        {
            _ghostEntity.Position = point;
            // Ghost'un bounding box'ını invalidate etmek gerekebilir ama basit property set yeterli.
        }
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape)
        {
            Cancel();
        }
    }

    public void Draw(IRenderContext context)
    {
        if (_step == 2 && _ghostEntity != null)
        {
            // Hayalet çizim
            _ghostEntity.Draw(context);
        }
    }
    
    public void Cancel() => OnCompleted?.Invoke();
}
