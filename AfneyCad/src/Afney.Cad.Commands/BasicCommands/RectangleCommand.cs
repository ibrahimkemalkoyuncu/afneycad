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
NE:
Dikdörtgen Çizme Komutu.
*/
public class RectangleCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private Vector3D? _startCorner;
    private LwPolylineEntity? _ghostRect;

    public string CommandName => "RECTANGLE";
    public Vector3D? ActivePoint => _startCorner;
    public event Action<string>? OnFeedback;
#pragma warning disable CS0067
    public event Action? OnCompleted;
#pragma warning restore CS0067

    public RectangleCommand(CadDatabase database, TransactionManager transactionManager)
    {
        _database = database;
        _transactionManager = transactionManager;
    }

    public void Start()
    {
        OnFeedback?.Invoke("RECTANGLE: İlk köşeyi belirtin.");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: İlk tıklamada dikdörtgenin başlangıç köşesini sabitlemek, ikinci tıklamada ise karşı köşe koordinatlarını kullanarak 4 köşeli bir kapalı polyline oluşturup veritabanına eklemek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_startCorner == null)
        {
            _startCorner = point;
            _ghostRect = CreateRectFromTwoPoints(point, point, 0xFFAAAAAA);
            
            OnFeedback?.Invoke("RECTANGLE: Karşı köşeyi belirtin.");
        }
        else
        {
            // İkinci köşe -> Kalıcı yap
            var permanentRect = CreateRectFromTwoPoints(_startCorner.Value, point, _database.GetLayer(_database.ActiveLayerName)?.Color ?? 0xFFFFFFFF);
            permanentRect.Layer = _database.ActiveLayerName;
            
            _transactionManager.Submit(new AddEntityOperation(_database, permanentRect));

            _startCorner = null; 
            _ghostRect = null;
            
            OnFeedback?.Invoke("RECTANGLE: İlk köşeyi belirtin."); 
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        if (_startCorner != null)
        {
            // Ghost güncelle
            _ghostRect = CreateRectFromTwoPoints(_startCorner.Value, point, 0xFFAAAAAA);
        }
    }

    public void OnKeyDown(InputKey key) { }

    public void Draw(IRenderContext context)
    {
        if (_ghostRect != null)
        {
            _ghostRect.Draw(context);
        }
    }

    public void Cancel()
    {
        _ghostRect = null;
        _startCorner = null;
    }

    /*
       NE: Dikdörtgen Poligonu Oluştur (CreateRectFromTwoPoints)
       NEDEN: İki çapraz köşe noktasından 4 adet köşe koordinatı türeterek, kapalı bir LwPolylineEntity (dikdörtgen) yapısı kurmak için.
    */
    private LwPolylineEntity CreateRectFromTwoPoints(Vector3D p1, Vector3D p2, uint color)
    {
        // P1 ve P2 köşe noktaları
        
        var v1 = new Vector3D(p1.X, p1.Y, 0);
        var v2 = new Vector3D(p2.X, p1.Y, 0); 
        var v3 = new Vector3D(p2.X, p2.Y, 0); 
        var v4 = new Vector3D(p1.X, p2.Y, 0);

        var rect = new LwPolylineEntity(new List<Vector3D> { v1, v2, v3, v4 }, isClosed: true)
        {
            Color = color
        };
        return rect;
    }
}
