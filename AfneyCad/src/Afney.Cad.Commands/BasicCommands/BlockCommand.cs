using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
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
    private readonly Action<BlockCommand> _onRequestName;
    
    // Durum
    private int _step = 0;
    private List<CadEntity> _selectedEntities = new();
    private Vector3D _basePoint;

    public string CommandName => "BLOCK";
    public Vector3D? ActivePoint => _basePoint;
    public List<CadEntity> SelectedEntities => _selectedEntities;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public BlockCommand(CadDatabase database, Action<BlockCommand> onRequestName)
    {
        _database = database;
        _onRequestName = onRequestName;
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Blok oluşturma döngüsünü tetikleyerek seçili nesne olup olmadığını kontrol etmek ve kullanıcıyı nesne seçimine veya referans noktası belirlemeye yönlendirmek için.
    */
    public void Start()
    {
        // 1. Önce seçim var mı kontrol et
        _selectedEntities = _database.GetSelectedEntities().ToList();
        
        if (_selectedEntities.Count > 0)
        {
            // Seçim varsa doğrudan nokta iste
            OnFeedback?.Invoke("BLOCK: Referans noktası (Base Point) seçin...");
            _step = 2;
        }
        else
        {
            // Seçim yoksa önce nesne seçtir
            OnFeedback?.Invoke("BLOCK: Blok yapılacak nesneleri seçin (Enter ile bitirin)...");
            _step = 1;
        }
    }

    /*
       NE: TÄ±klama OlayÄ± (OnPointerPressed)
       NEDEN: AdÄ±m 1'de nesne seÃ§im tÄ±klamalarÄ±nÄ±, AdÄ±m 2'de ise bloÄŸun referans (baz) noktasÄ±nÄ± belirlemek iÃ§in.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_step == 1)
        {
            // Nesne seçimi (PickBox ile tek tek veya pencere ile yapılabilir ama burada basit nokta seçimi varsayalım)
            // Aslında nesne seçimi genellikle Mouse Up/Down ile Selection Manager üzerinden yapılır.
            // Bu komut aktifken Selection çalışmayabilir mi?
            // Genelde komut içindeyken seçim yapmak için "PickEntityCommand" alt komutu kullanılır.
            // Ama şimdilik basitlik adına: Kullanıcı nesneleri seçip komuta girmeli varsayımı yapabiliriz.
            // Veya burada basit bir nokta kaydı alıp veritabanından seçim yapabiliriz.
            
            // Eğer adım 1 ise, seçim bekliyoruz. Kullanıcı seçim yapıp ENTER'a basmalı.
            // Fare tıklamasıyla seçim mantığı CadEngine/MainWindow'da yönetiliyor olabilir.
        }
        else if (_step == 2)
        {
            _basePoint = point;
            _step = 3;
            OnFeedback?.Invoke("BLOCK: Blok ismini girin...");
            
            // İsim isteme diyaloğunu tetikle
            _onRequestName?.Invoke(this);
        }
    }

    /*
       NE: Fare Hareket OlayÄ± (OnPointerMoved)
       NEDEN: Blok komutu sÄ±rasÄ±nda herhangi bir dinamik Ã¶nizleme (ghost) gerekirse kullanmak iÃ§in.
    */
    public void OnPointerMoved(Vector3D point) { }

    /*
       NE: Klavye GiriÅŸ OlayÄ± (OnKeyDown)
       NEDEN: ENTER tuÅŸu ile nesne seÃ§imini onaylamak veya ESC ile komutu iptal etmek iÃ§in.
    */
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter && _step == 1)
        {
            // Seçimi tamamla
            _selectedEntities = _database.GetSelectedEntities().ToList();
            if (_selectedEntities.Count == 0)
            {
                OnFeedback?.Invoke("Uyarı: Hiç nesne seçilmedi. Lütfen nesne seçin.");
                return;
            }
            
            OnFeedback?.Invoke("BLOCK: Referans noktası (Base Point) seçin...");
            _step = 2;
        }
        else if (key == InputKey.Escape)
        {
            Cancel();
        }
    }

    // UI'dan çağrılır
    /*
        NE: Blok Tanımını Kapat (FinalizeBlock)
        NEDEN: Kullanıcının girdiği isimle bir blok kaydı oluşturmak, seçilen nesneleri bu kaydın yerel koordinat sistemine (LCS) klonlamak ve sahnede bunların yerine bir Block Reference (INSERT) yerleştirmek için.
    */
    public void FinalizeBlock(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            OnFeedback?.Invoke("Hata: Geçersiz blok ismi.");
            Cancel();
            return;
        }

        // 1. Blok Tanımı Oluştur
        var blockRecord = new CadBlockRecord(name)
        {
            BasePoint = _basePoint
        };

        // 2. Nesneleri Kopyala (Clone) ve Transform Et (BasePoint'e göre)
        // Orijinal nesnelerin koordinatları WCS (World)
        // Blok içindeki koordinatlar LCS (Local) -> P_local = P_world - BasePoint
        var transformToLocal = Matrix4x4.TranslationMatrix(-_basePoint.X, -_basePoint.Y, -_basePoint.Z);

        foreach (var ent in _selectedEntities)
        {
            var clone = ent.Clone();
            clone.Transform(transformToLocal); // Yerel koordinata çek
            // ParentBlock vs ayarla?
            // clone.ParentBlockId = ... Guid?
            blockRecord.Entities.Add(clone);
        }

        // 3. Veritabanına Ekle
        _database.AddBlock(blockRecord);

        // 4. Orijinal Nesneleri Sil ve Yerine Insert Koy (Convert to Block)
        // Transaction başlatılabilir
        foreach (var ent in _selectedEntities)
        {
            _database.RemoveEntity(ent.Id);
        }

        var insert = new BlockReferenceEntity(name, _basePoint);
        _database.AddEntity(insert);

        OnFeedback?.Invoke($"Blok '{name}' oluşturuldu.");
        OnCompleted?.Invoke();
    }

    /*
       NE: Komutu Ä°ptal Et (Cancel)
       NEDEN: KullanÄ±cÄ± vazgeÃ§tiÄŸinde seÃ§imleri temizlemek ve komutu sonlandÄ±rmak iÃ§in.
    */
    public void Cancel()
    {
        _database.ClearSelection();
        OnCompleted?.Invoke();
    }

    /*
       NE: YardÄ±mcÄ± Ã‡izim (Draw)
       NEDEN: Blok oluÅŸturma sÄ±rasÄ±nda kullanÄ±cÄ±ya gÃ¶rsel referanslar (Ã¶rn: baz noktadan mouse'a Ã§izgi) sunmak iÃ§in.
    */
    public void Draw(IRenderContext context)
    {
        // Görsel ipucu yok
    }
}
