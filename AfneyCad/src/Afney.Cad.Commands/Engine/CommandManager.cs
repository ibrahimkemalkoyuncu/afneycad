using System;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Commands.Engine;

/*
NE:
Komut Yöneticisi (Command Processor).

NE İÇİN:
Kullanıcıdan gelen girdileri (Input) aktif komuta ("LINE", "CIRCLE" vb.) yönlendirmek için.
Ayrıca aktif komutun yaşam döngüsünü (Start, Cancel, Complete) yönetir.

NEREDE:
Engine / Commands Katmanında.

NE ZAMAN:
Her zaman aktiftir. Bir komut yoksa "Idle" durumdadır.

AMAÇ:
Stateless Architecture. UI, hangi komutun çalıştığını bilmez, sadece Input gönderir.
*/
public class CommandManager
{
    private ICadCommand? _activeCommand;

    public bool IsCommandActive => _activeCommand != null;
    
    public event Action<string>? CommandFeedback;

    /*
       NE: Komutu Başlat (StartCommand)
       NEDEN: Yeni bir çizim veya düzenleme komutunu (Çizgi, Taşı vb.) aktif hale getirip, önceki komutu temizleyerek yaşam döngüsünü başlatmak için.
    */
    public void StartCommand(ICadCommand command)
    {
        if (_activeCommand != null)
        {
            CancelCommand();
        }

        _activeCommand = command;
        _activeCommand.OnFeedback += HandleFeedback;
        _activeCommand.OnCompleted += HandleCompletion;
        
        _activeCommand.Start();
        CommandFeedback?.Invoke($"Komut başlatıldı: {_activeCommand.CommandName}");
    }

    /*
       NE: Tıklama Girişini İşle (ProcessPointerPressed)
       NEDEN: Kullanıcının ekranda tıkladığı noktayı aktif komuta ileterek (Örn: Çizginin başlangıç noktası) komutun kendi aşamalarını yürütmesini sağlamak için.
    */
    public void ProcessPointerPressed(Vector3D location)
    {
        _activeCommand?.OnPointerPressed(location);
    }

    /*
       NE: Mouse Hareketini İşle (ProcessPointerMoved)
       NEDEN: İmleç konumunu aktif komuta ileterek dinamik "Hayalet" (Ghost) çizimlerin (Örn: Çekilen çizginin anlık görünümü) güncellenmesini sağlamak için.
    */
    public void ProcessPointerMoved(Vector3D location)
    {
        _activeCommand?.OnPointerMoved(location);
    }

    /*
       NE: Klavye Girişini İşle (ProcessKeyDown)
       NEDEN: ESC ile komut iptali veya ENTER/SPACE gibi özel tuşların komut özelinde (Örn: Çoklu çizimi bitirme) işlenmesini sağlamak için.
    */
    public void ProcessKeyDown(InputKey key)
    {
        if (_activeCommand != null)
        {
            if (key == InputKey.Escape || key == InputKey.Cancel)
            {
                CancelCommand();
            }
            else
            {
                _activeCommand.OnKeyDown(key);
            }
        }
    }

    /*
       NE: Hayalet Çizimleri Yap (DrawGhost)
       NEDEN: Henüz veritabanına kaydedilmemiş ancak aktif olarak çizilen yardımcı çizgileri/nesneleri render döngüsünde ekrana basmak için.
    */
    public void DrawGhost(IRenderContext context)
    {
        _activeCommand?.Draw(context);
    }

    public void CancelCommand()
    {
        if (_activeCommand != null)
        {
            _activeCommand.Cancel();
            CommandFeedback?.Invoke("Komut iptal edildi.");
            Cleanup();
        }
    }

    private void HandleFeedback(string message)
    {
        CommandFeedback?.Invoke(message);
    }

    private void HandleCompletion()
    {
        CommandFeedback?.Invoke("Komut tamamlandı.");
        Cleanup();
    }

    private void Cleanup()
    {
        if (_activeCommand != null)
        {
            _activeCommand.OnFeedback -= HandleFeedback;
            _activeCommand.OnCompleted -= HandleCompletion;
            _activeCommand = null;
        }
    }
}
