namespace Afney.Cad.Commands.History;

/*
   NE: Geri Alınabilir Aksiyon Arayüzü (IReversibleAction)
   NEDEN: Kullanıcı işlemlerinin (Undo/Redo) bir komut geçmişi (History) üzerinden yönetilebilmesi için.

   NASIL (Mühendislik Detayı):
   - Command Pattern tasarım deseninin temelini oluşturur.
   - Her somut aksiyon (Action), kendi verisini geri yükleme (Undo) veya tekrar uygulama (Redo) mantığını barındırır.
*/
public interface IReversibleAction
{
    string DisplayName { get; } // "Create Line", "Delete Pipe"
    
    void Undo();
    void Redo();
}
