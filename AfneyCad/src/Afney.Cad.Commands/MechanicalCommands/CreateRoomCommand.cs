using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Mahal Tanımlama Komutu (Create Room)
    NEDEN: Kullanıcının ekrana tıklayarak oda sınırlarını (poligon) belirlemesi için.
    
    NASIL:
    1. Kullanıcı noktaları tıklar.
    2. Her tıklamada poligon güncellenir.
    3. Enter veya 'C' tuşu ile poligon kapatılır.
    4. RoomEntity oluşur ve OnRoomDefined callback'i çağrılır (UI Dialog için).
    
    MÜHENDİSLİK DETAYI:
    - En az 3 nokta kontrolü yapar (Triangle).
    - Poligonu kapalı (Closed) olarak işaretler.
    - Alan hesabı RoomEntity içinde otomatik yapılır.
*/
public class CreateRoomCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly Action<RoomEntity> _onRoomDefined; // UI Callback
    
    private List<Vector3D> _points = new();
    private LwPolylineEntity? _ghostPoly;
    private Vector3D _currentMousePos;

    public string CommandName => "ROOM";
    
    // Aktif nokta (Son tıklanan)
    public Vector3D? ActivePoint => _points.Count > 0 ? _points.Last() : null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public CreateRoomCommand(CadDatabase database, Action<RoomEntity> onRoomDefined)
    {
        _database = database;
        _onRoomDefined = onRoomDefined;
    }

    public void Start()
    {
        OnFeedback?.Invoke("MAHAL: Başlangıç noktasını seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        _points.Add(point);
        UpdateGhost();
        OnFeedback?.Invoke($"MAHAL: {(_points.Count + 1)}. noktayı seçin (Bitirmek için ENTER veya C, İptal için ESC).");
    }

    public void OnPointerMoved(Vector3D point)
    {
        _currentMousePos = point;
        UpdateGhost();
    }

    private void UpdateGhost()
    {
        if (_points.Count == 0) return;
        
        // Mevcut noktalar + Fare ucu
        var ghostPoints = new List<Vector3D>(_points);
        ghostPoints.Add(_currentMousePos);
        
        // Poligonu oluştur (fare ucuyla birlikte)
        _ghostPoly = new LwPolylineEntity(ghostPoints, isClosed: true) // Kapalı göster ki alan belli olsun
        { 
            Color = 0xFF00FF00 // Yeşil (Onay rengi)
        };
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter || key == InputKey.Space)
        {
            if (_points.Count >= 3)
            {
                Finish();
            }
            else
            {
                OnFeedback?.Invoke("UYARI: Mahal oluşturmak için en az 3 nokta gereklidir.");
            }
        }
        else if (key == InputKey.Escape)
        {
            Cancel();
        }
    }

    private void Finish()
    {
        // Odayı Oluştur (Klonlanmış liste ile)
        var room = new RoomEntity(new List<Vector3D>(_points), "Yeni Mahal");
        room.Layer = "0"; // Varsayılan katman
        room.Color = 0xFF00FF00; // Yeşil sınır
        
        // UI Dialog'u Tetikle (Mebrure Hanım devralıyor)
        // Dialog sonucunda eğer onaylanırsa veritabanına ekleme işlemi Presentation katmanında yapılacak.
        // Komutun görevi sadece geometrik tanımı yapmaktır.
        _onRoomDefined?.Invoke(room);
        
        // Komutu bitir
        OnCompleted?.Invoke();
        
        // Temizlik
        _points.Clear();
        _ghostPoly = null;
    }

    public void Cancel()
    {
        _points.Clear();
        _ghostPoly = null;
        OnFeedback?.Invoke("İşlem iptal edildi.");
        OnCompleted?.Invoke(); // Komut sonlandı
    }

    public void Draw(IRenderContext context)
    {
        if (_ghostPoly != null)
        {
            _ghostPoly.Draw(context);
        }
        
        // Tıklanan köşe noktalarını belirginleştir
        foreach(var p in _points)
        {
             // Basit bir daire veya nokta çizilebilir ama şimdilik polyline yeterli.
        }
    }
}
