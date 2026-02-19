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
Çember Çizme Komutu (Circle).

NE İÇİN:
Kullanıcının Merkez-Yarıçap yöntemiyle çember oluşturması için.

NEREDE:
Commands Katmanında.

AMAÇ:
Ghost çizimi ile dinamik yarıçap gösterimi.
*/
public class CircleCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private Vector3D? _centerPoint;
    private CircleEntity? _ghostCircle;

    public string CommandName => "CIRCLE";
    public Vector3D? ActivePoint => _centerPoint;
    public event Action<string>? OnFeedback;
#pragma warning disable CS0067
    public event Action? OnCompleted;
#pragma warning restore CS0067

    public CircleCommand(CadDatabase database, TransactionManager transactionManager)
    {
        _database = database;
        _transactionManager = transactionManager;
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Çember komutunu aktif ederek kullanıcıdan ilk giriş olan merkez noktasını istemek için.
    */
    public void Start()
    {
        OnFeedback?.Invoke("CIRCLE: Merkez noktasını belirtin.");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: İlk tıklamada çemberin merkezini sabitlemek, ikinci tıklamada ise merkezden uzaklığa göre yarıçapı belirleyip çemberi veritabanına kalıcı olarak eklemek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_centerPoint == null)
        {
            // 1. Merkez belirlendi
            _centerPoint = point;
            _ghostCircle = new CircleEntity(point, 0) { Color = 0xFFAAAAAA };
            OnFeedback?.Invoke("CIRCLE: Yarıçapı belirtin.");
        }
        else
        {
            // 2. Yarıçap belirlendi
            double radius = CalculateDistance(_centerPoint.Value, point);
            
            // Sıfır yarıçap kontrolü
            if (radius < 0.001) radius = 0.001; 

            var permanentCircle = new CircleEntity(_centerPoint.Value, radius) 
            { 
                Layer = _database.ActiveLayerName,
                Color = _database.GetLayer(_database.ActiveLayerName)?.Color ?? 0xFFFFFFFF
            };
            _transactionManager.Submit(new AddEntityOperation(_database, permanentCircle));

            // Komutu bitir veya tekrar et (AutoCAD'de Circle tek seferliktir ama biz tekrar edelim)
            _centerPoint = null;
            _ghostCircle = null;
            OnFeedback?.Invoke("CIRCLE: Merkez noktasını belirtin (Bitirmek için ESC).");
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_centerPoint != null && _ghostCircle != null)
        {
            // Yarıçapı güncelle
            _ghostCircle.Radius = CalculateDistance(_centerPoint.Value, point);
        }
    }

    public void OnKeyDown(InputKey key) { }

    public void Draw(IRenderContext context)
    {
        _ghostCircle?.Draw(context);
        
        // Merkezden mouse'a bir çizgi de çizelim (Yarıçap çizgisi)
        if (_centerPoint != null && _ghostCircle != null)
        {
            // Mouse pozisyonunu _ghostCircle'dan alamıyoruz, o yüzden sadece çemberi çiziyoruz.
            // Extra: context.DrawLine(_centerPoint.Value, (mousePos), ... ); // Mouse pos burada yok
        }
    }

    public void Cancel()
    {
        _ghostCircle = null;
        _centerPoint = null;
    }

    /*
       NE: Mesafe Hesapla (CalculateDistance)
       NEDEN: Merkez noktası ile fare imleci arasındaki Öklid mesafesini bularak çemberin güncel yarıçapını saptamak için.
    */
    private double CalculateDistance(Vector3D p1, Vector3D p2)
    {
        return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
    }
}
