using System.Collections.Generic;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Algorithms;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   NE: Uç-Yakala Mahal Tanımlama Komutu (EdgeCaptureMahalCommand)
   NEDEN: Kullanıcı isteği (bkz. Kullanici_kitabi.md #12 sonrası): "duvarın bir ucunu
          yakalayıp fareyi diğer ucuna götürüp yakalayınca o kenarı hesaba alacak
          şekilde ölçmeli" — ManualMahalCommand'ın greedy zincirleme modeli YERİNE
          DEĞİL, ONA EK, AYRI bir buton/komut olarak eklendi ("2. Uç Noktalar" ribbon).

   KULLANICI AKIŞI:
   1. Komut başlar → OSNAP (uç noktası) açıkken duvar köşelerini SIRAYLA tıklayın
   2. Her tıklama → CadViewport zaten OSnap ile en yakın uç noktaya snap eder
      (bkz. CadViewport.xaml.cs: _snapEngine.FindSnapPoint → OnPointerPressed'e
      snap'lenmiş nokta gönderilir) — bu komut ham tıklama sırasını AYNEN kaydeder.
   3. Enter/Sağ Tık (Tamam) → sıradaki noktalardan kapalı polygon oluşturulur
   4. ESC → iptal

   NEDEN WallChainBuilder KULLANILMIYOR: Kullanıcı köşeleri zaten DOĞRU SIRADA
   tıkladığı için greedy yeniden-sıralamaya (ve onun getirdiği self-intersection
   riskine) hiç gerek yok — polygon doğrudan tıklama sırasından kurulur.
*/
public class EdgeCaptureMahalCommand : ICadCommand
{
    private readonly System.Action<MahalEntity> _onRoomCreated;
    private readonly List<Vector3D> _points = new();
    private Vector3D _cursor;

    public string CommandName => "EDGE_CAPTURE_MAHAL";
    public Vector3D? ActivePoint => _cursor;

    public event System.Action<string>? OnFeedback;
    public event System.Action? OnCompleted;

    public EdgeCaptureMahalCommand(System.Action<MahalEntity> onRoomCreated)
    {
        _onRoomCreated = onRoomCreated;
    }

    public void Start()
    {
        _points.Clear();
        Serilog.Log.Information("[EdgeCaptureMahal] Komut başlatıldı.");
        OnFeedback?.Invoke("Duvar köşelerini SIRAYLA tıklayın (OSNAP açık olmalı) → [Enter] Mahal oluştur | [ESC] İptal");
    }

    public void OnPointerPressed(Vector3D position)
    {
        _points.Add(new Vector3D(position.X, position.Y, 0));
        Serilog.Log.Information("[EdgeCaptureMahal] Köşe {Index}: ({X:F0}, {Y:F0})", _points.Count, position.X, position.Y);
        OnFeedback?.Invoke($"{_points.Count} köşe yakalandı. Devam edin veya [Enter] ile kapatın.");
    }

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Enter || key == InputKey.Space)
            FinalizeRoom();
        else if (key == InputKey.Escape)
            Cancel();
    }

    private void FinalizeRoom()
    {
        if (_points.Count < 3)
        {
            OnFeedback?.Invoke("En az 3 köşe yakalanmalı. Devam edin.");
            return;
        }

        var polygon = new List<Vector3D>(_points);

        /*
           NE: Kendi Kendini Kesme Kontrolü
           NEDEN: Kullanıcı köşeleri sırayla tıklasa da, aradaki bir tıklama yanlış/sıra dışı
                  bir OSNAP noktasını yakalarsa (ör. yanlış duvar ucuna kayma) poligon çaprazlaşır
                  ve Shoelace alan formülü sessizce çok küçük/yanlış bir alan üretir — aynı kök
                  neden ManualMahal'da da yaşandı (bkz. GeomUtils.HasSelfIntersection — iki
                  komutun da paylaştığı ortak geometri katmanı).
                  Sessizce yanlış sonuç kaydetmek yerine burada da açıkça reddediliyor.
        */
        if (GeomUtils.HasSelfIntersection(polygon))
        {
            OnFeedback?.Invoke("HATA: Yakalanan köşeler kendini kesen (çapraz) bir sınır oluşturuyor — " +
                                "muhtemelen bir köşe yanlış noktaya yakalandı. [ESC] ile iptal edip tekrar deneyin.");
            Serilog.Log.Warning("[EdgeCaptureMahal] Kendi kendini kesen poligon tespit edildi — mahal reddedildi.");
            return;
        }

        var mahal = new MahalEntity(polygon, "Yeni Mahal");
        Serilog.Log.Information("[EdgeCaptureMahal] MahalEntity oluşturuldu: {Count} köşe, Alan: {Area:F2}m²",
            polygon.Count, mahal.Area);

        OnFeedback?.Invoke($"Mahal oluşturuldu: {polygon.Count} köşe. Alan: {mahal.Area:F2} m²");
        _onRoomCreated?.Invoke(mahal);
        OnCompleted?.Invoke();
    }

    public void OnPointerMoved(Vector3D position)
    {
        _cursor = position;
    }

    public void Cancel()
    {
        _points.Clear();
        Serilog.Log.Information("[EdgeCaptureMahal] Komut ESC ile iptal edildi.");
        OnFeedback?.Invoke("Uç-yakala mahal tanımlama iptal edildi.");
        OnCompleted?.Invoke();
    }

    public void Draw(IRenderContext context)
    {
        const uint SegmentColor = 0xBB00CC44; // yeşil yarı saydam — yakalanan kenarlar
        const uint CornerColor  = 0xFFFF3300; // kırmızı — yakalanan köşe noktaları
        const uint GhostColor   = 0x662299FF; // mavi — imlece önizleme
        const uint CloseColor   = 0x66FFCC00; // sarı — kapanış önizlemesi
        const double CrossSize  = 120.0;      // mm

        for (int i = 0; i < _points.Count - 1; i++)
            context.DrawLine(_points[i], _points[i + 1], SegmentColor, 3.0);

        foreach (var p in _points)
        {
            context.DrawLine(new Vector3D(p.X - CrossSize, p.Y, 0), new Vector3D(p.X + CrossSize, p.Y, 0), CornerColor, 2.0);
            context.DrawLine(new Vector3D(p.X, p.Y - CrossSize, 0), new Vector3D(p.X, p.Y + CrossSize, 0), CornerColor, 2.0);
        }

        if (_points.Count > 0)
        {
            context.DrawLine(_points[_points.Count - 1], _cursor, GhostColor, 1.0);

            if (_points.Count >= 3)
                context.DrawLine(_cursor, _points[0], CloseColor, 1.0);
        }
    }
}
