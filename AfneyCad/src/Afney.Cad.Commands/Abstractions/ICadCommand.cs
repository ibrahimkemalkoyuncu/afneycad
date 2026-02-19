using System;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Domain.Abstractions;

namespace Afney.Cad.Commands.Abstractions;

/*
   NE: CAD Komut Arayüzü (ICadCommand)
   NEDEN: Tüm çizim ve düzenleme komutlarının (Line, Move, Circle vb.) CommandManager tarafından aynı arayüz üzerinden standart bir şekilde yönetilmesini sağlamak için.
*/
public interface ICadCommand
{
    string CommandName { get; }
    Vector3D? ActivePoint { get; }
    
    /*
       NE: Komutu Başlat (Start)
       NEDEN: Komut ilk tetiklendiğinde gerekli ön ayarları yapmak ve kullanıcıya ilk adımı (Örn: "Başlangıç noktasını seç") bildirmek için.
    */
    void Start();

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: Kullanıcının ekrandaki tıklamalarını komutun aşamalarına (Nokta seçimi, nesne belirleme) aktarmak için.
    */
    void OnPointerPressed(Vector3D point);

    /*
       NE: Hareket Olayı (OnPointerMoved)
       NEDEN: Mouse imlecinin anlık konumuna göre çizilecek "Hayalet" (Ghost) nesnenin konumunu güncellemek için.
    */
    void OnPointerMoved(Vector3D point);

    /*
       NE: Klavye Olayı (OnKeyDown)
       NEDEN: ENTER, SPACE veya diğer tuşlarla komutun akışını (Örn: Çoklu çizimi bitirme) kontrol etmek için.
    */
    void OnKeyDown(InputKey key);

    /*
       NE: Hayalet Çizim (Draw)
       NEDEN: Komut henüz tamamlanmadan, kullanıcının ne çizdiğini (dinamik önizleme) görmesini sağlamak için.
    */
    void Draw(IRenderContext context); // Ghost drawing

    /*
       NE: İptal Et (Cancel)
       NEDEN: ESC tuşuna basıldığında komutun geçici verilerini temizleyip sonlandırmak için.
    */
    void Cancel();

    event Action<string> OnFeedback; // Kullanıcıya mesaj: "İlk noktayı seçin"
    event Action OnCompleted;        // Komut bitti
}
