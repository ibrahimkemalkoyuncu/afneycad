namespace Afney.Cad.Commands.Abstractions;

/*
   NE: Ölçü Değeri Geçersiz Kılma Arayüzü (IDimensionOverridable)
   NEDEN: AutoCAD'in "Dinamik Girdi" özelliği gibi, kullanıcının ölçü komutları sırasında
          otomatik hesaplanan ölçü metnini klavyeden girilen bir değerle değiştirebilmesi
          için (örn. gerçek mesafe 1150mm ama şema amaçlı "1200" yazdırmak istemesi).
          Komut satırına "=1200" yazılınca CadViewport bu arayüzü uygulayan aktif komutu bulup
          SetTextOverride çağırır.
*/
public interface IDimensionOverridable
{
    void SetTextOverride(string? text);
}
