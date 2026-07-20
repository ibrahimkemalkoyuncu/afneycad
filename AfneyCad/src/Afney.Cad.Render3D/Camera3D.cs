using System.Numerics;

namespace Afney.Cad.Render3D;

/*
   NE: Orbit Kamera (Camera3D)
   NEDEN: Sıfırdan yazılan D3D11 motorunun kamerası — WPF Viewport3D'nin PerspectiveCamera'sı
          KULLANILMIYOR, view/projection matrisleri elle hesaplanıyor (System.Numerics,
          harici bağımlılık yok).
*/
public sealed class Camera3D
{
    public Vector3 Target { get; set; } = Vector3.Zero;
    public float Distance { get; set; } = 5000f;
    public float YawRadians { get; set; } = MathF.PI / 4;
    public float PitchRadians { get; set; } = MathF.PI / 6;
    public float FieldOfViewRadians { get; set; } = MathF.PI / 4;
    public float NearPlane { get; set; } = 1f;
    public float FarPlane { get; set; } = 100000f;

    public Vector3 EyePosition
    {
        get
        {
            float cosPitch = MathF.Cos(PitchRadians);
            var offset = new Vector3(
                Distance * cosPitch * MathF.Cos(YawRadians),
                Distance * cosPitch * MathF.Sin(YawRadians),
                Distance * MathF.Sin(PitchRadians));
            return Target + offset;
        }
    }

    public Matrix4x4 GetViewMatrix() =>
        Matrix4x4.CreateLookAt(EyePosition, Target, Vector3.UnitZ);

    public Matrix4x4 GetProjectionMatrix(float aspectRatio) =>
        Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewRadians, aspectRatio, NearPlane, FarPlane);

    public void Orbit(float deltaYaw, float deltaPitch)
    {
        YawRadians += deltaYaw;
        PitchRadians = Math.Clamp(PitchRadians + deltaPitch, -MathF.PI / 2 + 0.01f, MathF.PI / 2 - 0.01f);
    }

    public void Pan(Vector3 worldDelta) => Target += worldDelta;

    public void Zoom(float factor) => Distance = Math.Clamp(Distance * factor, NearPlane * 2, FarPlane / 2);
}
