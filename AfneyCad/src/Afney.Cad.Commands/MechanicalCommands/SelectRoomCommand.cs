using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
NE: Mahal Seçim Komutu (SelectRoomCommand)
NEDEN: Mimari plandaki kapalı bir alanı tıklayarak 'Oda' (Room) olarak tanımlamak için.
*/
public class SelectRoomCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly ClosedAreaDetector _detector;
    private readonly Action<RoomEntity> _onRoomCreated;

    // Interface Implementation
    public string CommandName => "Select Room";
    public Vector3D? ActivePoint { get; private set; }
    
    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public SelectRoomCommand(CadDatabase database, Action<RoomEntity> onRoomCreated)
    {
        _database = database;
        _detector = new ClosedAreaDetector(); // Servis oluştur
        _onRoomCreated = onRoomCreated;
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Mahal tanımlama sürecini başlatarak kullanıcıdan odanın içinde bir noktaya tıklamasını istemek için.
    */
    public void Start()
    {
        OnFeedback?.Invoke("Bir mahal (kapalı alan) içine tıklayın...");
    }

    public void OnPointerMoved(Vector3D position)
    {
        ActivePoint = position;
        // Opsiyonel: İmleç altındaki odayı highlight etme logic'i buraya gelebilir.
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: Tıklanan koordinattaki kapalı alanı (mimari çizgilerle çevrili bölgeyi) otomatik tespit etmek, alanı hesaplamak ve içindeki vitrifiyeleri odaya dahil etmek için.
    */
    public void OnPointerPressed(Vector3D position)
    {
        // 1. Tüm çizgileri al
        var allEntities = _database.GetAllEntities();
        
        // 2. Kapalı alanları bul
        var areas = _detector.FindClosedAreas(allEntities);
        
        // 3. Tıklanan noktanın içinde olduğu alanı bul
        var selectedArea = areas.FirstOrDefault(polygon => IsPointInPolygon(position, polygon));
        
        if (selectedArea != null)
        {
            // 4. Odayı oluştur
            var room = new RoomEntity(selectedArea, "Yeni Mahal");
            
            // 5. Mahal içindeki cihazları bul (FINE SANI Zekası)
            var fixturesInRoom = _database.GetAllEntities()
                .OfType<SanitaryFixtureEntity>()
                .Where(f => IsPointInPolygon(f.Position, selectedArea))
                .ToList();

            foreach (var fEntity in fixturesInRoom)
            {
                room.Fixtures.Add(new SanitaryFixtureEntity(fEntity.Position, ConvertToFixtureType(fEntity.FixtureType).ToString(), fEntity.FixtureUnit)
                {
                   BlockName = fEntity.FixtureType,
                   // Location = Position (ctor)
                   // LoadUnit = FixtureUnit (ctor)
                   // Count = 1 (N/A in Entity)
                });
            }

            // Gebze/Mete Bey: Kullanıcıya özet bilgisi ver
            string fixtureList = string.Join(", ", fixturesInRoom.Select(f => f.FixtureType).Distinct());
            OnFeedback?.Invoke($"Mahal oluşturuldu: {room.Area:F2}m². İçindeki cihazlar: {fixtureList}");

            // Call callback
            _onRoomCreated?.Invoke(room);
            OnCompleted?.Invoke();
        }
        else
        {
            OnFeedback?.Invoke("Tıklanan noktada kapalı bir alan bulunamadı. Mimari hatların (Line) uç uca birleştiğinden emin olun.");
        }
    }

    private SanitaryFixtureType ConvertToFixtureType(string typeName)
    {
        if (typeName.Contains("WC", StringComparison.OrdinalIgnoreCase)) return SanitaryFixtureType.WC;
        if (typeName.Contains("Lavabo", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Basin", StringComparison.OrdinalIgnoreCase)) return SanitaryFixtureType.Lavatory;
        if (typeName.Contains("Eviye", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Sink", StringComparison.OrdinalIgnoreCase)) return SanitaryFixtureType.Sink;
        if (typeName.Contains("Duş", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Shower", StringComparison.OrdinalIgnoreCase)) return SanitaryFixtureType.Shower;
        return SanitaryFixtureType.Unknown;
    }
    
    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) Cancel();
    }

    public void Cancel()
    {
        OnFeedback?.Invoke("İptal edildi.");
        OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context)
    {
        // Ghost drawing (henüz gerek yok)
    }

    // Ray Casting Algorithm (Point in Polygon)
    private bool IsPointInPolygon(Vector3D point, List<Vector3D> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            if (polygon[i].Y < point.Y && polygon[j].Y >= point.Y || 
                polygon[j].Y < point.Y && polygon[i].Y >= point.Y)
            {
                if (polygon[i].X + (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < point.X)
                {
                    inside = !inside;
                }
            }
            j = i;
        }
        return inside;
    }
}
