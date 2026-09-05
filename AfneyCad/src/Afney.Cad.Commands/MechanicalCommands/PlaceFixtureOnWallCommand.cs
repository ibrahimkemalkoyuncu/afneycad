using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Duvara Bağlı Cihaz Yerleşim Komutu (PlaceFixtureOnWallCommand)
    NEDEN: Fine SANI tarzı, duvarla ilişkilendirilmiş (associative) vitrifiye yerleşimi yapmak için.
*/
public class PlaceFixtureOnWallCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly MechanicalKernel _kernel;
    private int _step = 0;
    private ArchitecturalObstacle? _selectedWall;

    public string CommandName => "Place Fixture on Wall";
    public Vector3D? ActivePoint { get; private set; }

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public PlaceFixtureOnWallCommand(CadDatabase database, MechanicalKernel kernel, TransactionManager transactionManager)
    {
        _database = database;
        _kernel = kernel;
        _transactionManager = transactionManager;
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Kullanıcıyı duvar seçimi aşamasına geçirmek ve yönlendirici mesaj göstermek için.
    */
    public void Start()
    {
        _step = 0;

        bool hasRecognizedWalls = _kernel.ArchitecturalObstacles.Any(o => o.Type == ObstacleType.Wall);
        if (!hasRecognizedWalls)
        {
            OnFeedback?.Invoke("HATA: Tanınmış duvar yok. Önce AutoBLD → Eleman Tanı ile mimariyi tanıtın.");
            OnCompleted?.Invoke();
            return;
        }

        OnFeedback?.Invoke("Yerleşim yapılacak DUVARI seçin.");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: Önce duvarı seçmek, ardından duvar üzerindeki yerleşim noktasını belirleyerek cihazı (vitrifiye) duvara associative (bağlı) olarak eklemek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        if (_step == 0)
        {
            // 1. Duvar Seçimi
            _selectedWall = _kernel.ArchitecturalObstacles
                .FirstOrDefault(o => o.Type == ObstacleType.Wall && IsPointNearObstacle(point, o));

            if (_selectedWall != null)
            {
                _step = 1;
                OnFeedback?.Invoke("Duvar üzerinde yerleşim NOKTASINI seçin.");
            }
            else
            {
                OnFeedback?.Invoke("HATA: Yakında tanınmış duvar yok. Tanınmayan bir çizgiye mi tıkladınız? AutoBLD → Eleman Tanı ile mimariyi tanıtıp tekrar deneyin.");
            }
        }
        else if (_step == 1 && _selectedWall != null)
        {
            // 2. Nokta Seçimi ve Yerleşim
            PlaceFixtureAt(_selectedWall, point);
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point)
    {
        ActivePoint = point;
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) Cancel();
    }

    public void Draw(IRenderContext context)
    {
        // Önizleme çizimi (Örn: Cihaz hayaleti)
        if (_step == 1 && ActivePoint.HasValue)
        {
             // Basit bir daire çizelim önizleme olarak
             context.DrawCircle(ActivePoint.Value, 100, 0x8800FFFF, 1.0);
        }
    }

    private void PlaceFixtureAt(ArchitecturalObstacle wall, Vector3D pickPoint)
    {
        if (wall.Boundary.Count < 2) return;

        var pStart = wall.Boundary[0];
        var pEnd = wall.Boundary[1];
        var wallDir = (pEnd - pStart).Normalize();
        
        // PickPoint'in duvar doğrusu üzerindeki izdüşümü
        double t = (pickPoint - pStart).Dot(wallDir);
        var projectPos = pStart + (wallDir * t);

        var fixture = new SanitaryFixtureEntity(projectPos, "Lavabo", 1.0);
        fixture.AttachedObstacleId = wall.Id;
        fixture.WallOffset = t; 
        fixture.WallDistance = 0;

        var normal = new Vector3D(-wallDir.Y, wallDir.X, 0);
        fixture.Rotation = Math.Atan2(normal.Y, normal.X);

        _transactionManager.Submit(new AddEntityOperation(_database, fixture));
    }

    private bool IsPointNearObstacle(Vector3D p, ArchitecturalObstacle obs)
    {
        return obs.GetBoundingBox().Expand(100).Contains(p);
    }

    public void Cancel() => OnCompleted?.Invoke();
}
