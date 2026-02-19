using System;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Render.Viewport;

public class Camera
{
    public Vector3D Position { get; set; } = Vector3D.Zero;
    public double Zoom { get; set; } = 1.0;
    
    // Viewport size in pixels
    public double ViewportWidth { get; set; }
    public double ViewportHeight { get; set; }

    public Camera()
    {
    }

    /*
       NE: Kaydır (Pan)
       NEDEN: Mouse tekerleğine basılı tutarak ekranı sürükleme (pan) işlemi yapıldığında, dünya koordinatlarının ekrandaki izdüşümünü (ofsetini) kaydırmak için.
    */
    public void Pan(double dx, double dy)
    {
        // Pan: Move the offset directly (Screen space shift)
        // Position represents visual offset of (0,0) point
        Position = new Vector3D(Position.X + dx, Position.Y + dy, Position.Z);
    }

    /*
       NE: Belirli Noktaya Zoom Yap (ZoomToPoint)
       NEDEN: Mouse tekerleğiyle yaklaşıp uzaklaşırken, mouse imlecinin altındaki noktanın ekrandaki yerinin değişmemesini (sabit kalmasını) sağlayacak şekilde kamera ofsetini ve ölçeğini ayarlamak için.
    */
    public void ZoomToPoint(double zoomDelta, double pivotX, double pivotY)
    {
        // Pivot noktası ekran koordinatlarında. 
        // Pivot'un dünya koordinatı sabit kalmalı.
        // S = W * Z + P
        // Old: S = W * Z1 + P1
        // New: S = W * Z2 + P2
        // P2 = S - W * Z2 = S - ((S - P1) / Z1) * Z2
        
        var worldPivot = ScreenToWorld(pivotX, pivotY);

        Zoom *= zoomDelta;
        
        // Limit zoom
        if (Zoom < 0.001) Zoom = 0.001;
        if (Zoom > 10000) Zoom = 10000;
        
        // Adjust Position (P2) so worldPivot stays at pivotX, pivotY
        // S = W * Z + P => P = S - W * Z
        Position = new Vector3D( 
            pivotX - worldPivot.X * Zoom, 
            pivotY - worldPivot.Y * Zoom, 
            Position.Z
        );
    }

    /*
       NE: Kamerayı Sıfırla (Reset)
       NEDEN: Çizimin merkezine dönmek ve ölçeği (Zoom) başlangıç değerine (1.0) getirmek için.
    */
    public void Reset()
    {
        Position = Vector3D.Zero;
        Zoom = 1.0;
    }

    /*
       NE: Dünyadan Ekrana Dönüştür (WorldToScreen)
       NEDEN: Çizimdeki (mimarideki) gerçek 3D koordinatları, kameranın mevcut konumu ve zoom seviyesine göre ekran üzerindeki piksel (X, Y) değerlerine dönüştürmek için.
    */
    public Vector3D WorldToScreen(Vector3D worldPoint)
    {
        // Model: Offset
        // Screen = World * Zoom + Position
        
        return new Vector3D(
            worldPoint.X * Zoom + Position.X,
            worldPoint.Y * Zoom + Position.Y, 
            0
        );
    }

    /*
       NE: Ekrandan Dünyaya Dönüştür (ScreenToWorld)
       NEDEN: Mouse imlecinin tıkladığı piksel konumunu, çizimdeki gerçek mimari (dünya) koordinatlarına çevirmek için.
    */
    public Vector3D ScreenToWorld(double screenX, double screenY)
    {
        // Inverse of WorldToScreen
        // World = (Screen - Position) / Zoom
        
        // Avoid division by zero
        double z = Zoom == 0 ? 0.0001 : Zoom;

        return new Vector3D(
            (screenX - Position.X) / z,
            (screenY - Position.Y) / z,
            0
        );
    }
}
