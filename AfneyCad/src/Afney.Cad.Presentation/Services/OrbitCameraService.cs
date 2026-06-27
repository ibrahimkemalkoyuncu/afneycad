namespace Afney.Cad.Presentation.Services;

public class OrbitCameraService
{
    public double RotationX { get; set; } = 30.0;
    public double RotationY { get; set; } = -45.0;
    public double RotationZ { get; set; } = 0.0;
    public double Distance  { get; set; } = 100.0;
    public double FieldOfView { get; set; } = 60.0;
    public bool   IsPerspective { get; set; } = false;

    public enum ViewPreset { Top, Front, Right, Left, Back, Bottom, Isometric, SE_Isometric, NE_Isometric, SW_Isometric }

    public void SetPreset(ViewPreset preset)
    {
        (RotationX, RotationY) = preset switch
        {
            ViewPreset.Top           => (90.0, 0.0),
            ViewPreset.Front         => (0.0, 0.0),
            ViewPreset.Right         => (0.0, -90.0),
            ViewPreset.Left          => (0.0, 90.0),
            ViewPreset.Back          => (0.0, 180.0),
            ViewPreset.Bottom        => (-90.0, 0.0),
            ViewPreset.Isometric     => (35.264, -45.0),
            ViewPreset.SE_Isometric  => (35.264, -135.0),
            ViewPreset.NE_Isometric  => (35.264, -45.0),
            ViewPreset.SW_Isometric  => (35.264, 135.0),
            _                        => (30.0, -45.0)
        };
    }

    public void Orbit(double deltaX, double deltaY)
    {
        RotationY += deltaX * 0.5;
        RotationX += deltaY * 0.5;
        RotationX = Math.Clamp(RotationX, -90.0, 90.0);
        if (RotationY > 360) RotationY -= 360;
        if (RotationY < -360) RotationY += 360;
    }

    public void ZoomCamera(double factor)
    {
        Distance *= factor;
        Distance = Math.Clamp(Distance, 1.0, 100000.0);
    }

    public void TogglePerspective()
    {
        IsPerspective = !IsPerspective;
    }

    public (double azimuth, double elevation, double dist, bool persp) GetCameraState()
        => (RotationY, RotationX, Distance, IsPerspective);

    public string GetViewName()
    {
        if (Math.Abs(RotationX - 90) < 1) return "Ust (Top)";
        if (Math.Abs(RotationX) < 1 && Math.Abs(RotationY) < 1) return "On (Front)";
        if (Math.Abs(RotationX) < 1 && Math.Abs(RotationY + 90) < 1) return "Sag (Right)";
        if (Math.Abs(RotationX) < 1 && Math.Abs(RotationY - 90) < 1) return "Sol (Left)";
        if (Math.Abs(RotationX) < 1 && Math.Abs(Math.Abs(RotationY) - 180) < 1) return "Arka (Back)";
        if (Math.Abs(RotationX - 35.264) < 2) return "Izometrik";
        return $"Serbest ({RotationX:F0}, {RotationY:F0})";
    }
}

public class ViewCubeService
{
    private readonly OrbitCameraService _camera;

    public ViewCubeService(OrbitCameraService camera)
    {
        _camera = camera;
    }

    public static readonly (string Name, OrbitCameraService.ViewPreset Preset)[] Faces = new[]
    {
        ("UST",   OrbitCameraService.ViewPreset.Top),
        ("ON",    OrbitCameraService.ViewPreset.Front),
        ("SAG",   OrbitCameraService.ViewPreset.Right),
        ("SOL",   OrbitCameraService.ViewPreset.Left),
        ("ARKA",  OrbitCameraService.ViewPreset.Back),
        ("ALT",   OrbitCameraService.ViewPreset.Bottom),
    };

    public void ClickFace(string faceName)
    {
        var face = Faces.FirstOrDefault(f => f.Name == faceName);
        if (face != default)
            _camera.SetPreset(face.Preset);
    }
}
