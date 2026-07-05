using System;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions; 
using Afney.Cad.Database.Transactions.Operations; 
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.BasicCommands;

/*
NE:
Çizgi Çizme Komutu.
...
*/
public class LineCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager; // Added dependency
    private Vector3D? _startPoint;
    private LineEntity? _ghostLine; 

    public string CommandName => "LINE";
    public Vector3D? ActivePoint => _startPoint;
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public LineCommand(CadDatabase database, TransactionManager transactionManager)
    {
        _database = database;
        _transactionManager = transactionManager;
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Çizgi komutunun ilk aşamasına geçmek ve kullanıcıdan başlangıç noktasını istemek için.
    */
    public void Start()
    {
        OnFeedback?.Invoke("LINE: İlk noktayı belirtin.");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: İlk tıklamada başlangıç noktasını saptamak, ikinci tıklamada ise Transaction Manager üzerinden kalıcı çizgiyi veritabanına eklemek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_startPoint == null)
        {
            _startPoint = point;
            _ghostLine = new LineEntity(point, point) { Color = 0xFFAAAAAA }; 
            
            OnFeedback?.Invoke("LINE: Sonraki noktayı belirtin.");
        }
        else
        {
            // İkinci nokta tıklandı -> Çizgiyi Kalıcı Yap (TRANSACTION İLE)
            var permanentLine = new LineEntity(_startPoint.Value, point) 
            { 
                Layer = _database.ActiveLayerName,
                Color = _database.GetLayer(_database.ActiveLayerName)?.Color ?? 0xFFFFFFFF
            };
            
            // _database.AddEntity(permanentLine); // ESKİ (Bad practice for Undo)
            // YENİ: Transaction Manager üzerinden gönder
            _transactionManager.Submit(new AddEntityOperation(_database, permanentLine));

            _startPoint = point;
            _ghostLine = new LineEntity(point, point) { Color = 0xFFAAAAAA };

            OnFeedback?.Invoke("LINE: Sonraki noktayı belirtin (Bitirmek için ESC).");
        }
    }


    /*
       NE: Fare Hareket Olayı (OnPointerMoved)
       NEDEN: Başlangıç noktası belli olan çizginin bitiş ucunu fare imleciyle birlikte hareket ettirerek (Rubber band) kullanıcıya dinamik önizleme sunmak için.
    */
    public void OnPointerMoved(Vector3D point)
    {
        if (_startPoint != null && _ghostLine != null)
        {
            // Lastik bant efekti: Bitiş noktasını mouse'un olduğu yere taşı
            _ghostLine.EndPoint = point;
        }
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter || key == InputKey.Space)
        {
            _ghostLine = null;
            _startPoint = null;
            OnFeedback?.Invoke("LINE: Tamamlandı.");
            OnCompleted?.Invoke();
        }
    }

    /*
       NE: Hayalet Çizim (Draw)
       NEDEN: Çizim henüz tamamlanmadan, kullanıcının tıklayacağı ikinci noktanın nereye düştüğünü (Lastik bant önizlemesi) görmesini sağlamak için.
    */
    public void Draw(IRenderContext context)
    {
        if (_ghostLine != null)
        {
            _ghostLine.Draw(context);
        }
    }

    /*
       NE: Komutu İptal Et (Cancel)
       NEDEN: ESC tuşuna basıldığında veya komut yarıda kesildiğinde; henüz tamamlanmamış (hayalet) çizgiyi ve başlangıç noktasını bellekten temizlemek için.
    */
    public void Cancel()
    {
        _ghostLine = null;
        _startPoint = null;
    }
}
