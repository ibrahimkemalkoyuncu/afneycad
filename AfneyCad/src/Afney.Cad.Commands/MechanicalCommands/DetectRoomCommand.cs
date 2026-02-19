using Afney.Cad.Mechanical.Services;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using System;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Akıllı Mahal Algılama Komutu (DetectRoomCommand)
    NEDEN: Mimari plandaki kapalı alanları (duvarları) otomatik takip ederek oda poligonu oluşturmak için.
    
    NASIL:
    1. Kullanıcı odanın içinde bir yere tıklar.
    2. SpaceDetector servisi ray-casting ile en yakın duvarı bulur.
    3. Duvarı takip ederek (Wall Following) kapalı bir döngü oluşturur.
    4. Bulunan mahal RoomEntity'ye dönüştürülür ve UI'a bildirilir.
*/
public class DetectRoomCommand : ICadCommand
{
    private readonly SmartBoundaryService _boundaryService;
    private readonly Action<RoomEntity> _onRoomDetected;

    public string CommandName => "DETECT_ROOM";
    public Vector3D? ActivePoint => null;

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public DetectRoomCommand(CadDatabase database, Action<RoomEntity> onRoomDetected)
    {
        _boundaryService = new SmartBoundaryService(database);
        _onRoomDetected = onRoomDetected;
    }

    public void Start()
    {
        OnFeedback?.Invoke("AKILLI MAHAL: Odanın içinde bir noktaya tıklayın.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        try
        {
            var boundaryPoints = _boundaryService.FindBoundary(point);
            
            if (boundaryPoints != null && boundaryPoints.Count >= 3)
            {
                var room = new RoomEntity(boundaryPoints, "Yeni Mahal");
                _onRoomDetected?.Invoke(room);
                OnFeedback?.Invoke($"MAHAL TESPİT EDİLDİ: Alan = {room.Area:F2} m²");
                OnCompleted?.Invoke();
            }
            else
            {
                OnFeedback?.Invoke("HATA: Kapalı bir alan bulunamadı. Lütfen duvarların içinde bir noktaya tıklayın.");
            }
        }
        catch (Exception ex)
        {
            OnFeedback?.Invoke($"HATA: Algoritma başarısız oldu. {ex.Message}");
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        // Otomatik algılama olduğu için önizleme (ghost) şu anlık yok.
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape)
        {
            Cancel();
        }
    }

    public void Draw(Domain.Abstractions.IRenderContext context)
    {
        // İşlem anlık olduğu için çizime gerek yok.
    }

    public void Cancel()
    {
        OnFeedback?.Invoke("İşlem iptal edildi.");
        OnCompleted?.Invoke();
    }
}
