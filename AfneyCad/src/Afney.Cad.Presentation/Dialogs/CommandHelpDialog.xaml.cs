using System.Windows;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Komut Satırı Yardım Penceresi (CommandHelpDialog)
   NEDEN: Komut satırına HELP/? yazan kullanıcıya mevcut kısaltmaları ve klavye
          kısayollarını göstermek için. Önceden bilinmeyen komutta sadece
          "Bilinmeyen komut" mesajı gösteriliyordu; kullanıcı komutları keşfedemiyordu.
*/
public partial class CommandHelpDialog : Window
{
    public CommandHelpDialog()
    {
        InitializeComponent();
        TxtBody.Text = BuildHelpText();
    }

    private static string BuildHelpText()
    {
        return
@"ÇİZİM
  L, LINE          Çizgi
  C, CIRCLE        Daire
  PL, PLINE        Polyline
  RECT             Dikdörtgen
  H, HATCH, BH     Tarama
  MT, MTEXT, TEXT  Çok satırlı metin

DÜZENLEME
  TR, TRIM         Buda
  EX, EXTEND       Uzat
  MI, MIRROR       Aynala
  CO, COPY         Kopyala
  M, MOVE          Taşı
  O, OFFSET        Ofset
  X, EXPLODE       Patlat

ÖLÇÜLENDİRME
  DIM, DIML        Doğrusal ölçü
  DIMA             Hizalı ölçü
  DIMR             Yarıçap ölçüsü
  DIMANG           Açısal ölçü
  DCO, DIMCONT     Ardışık ölçü
  DIST, MESAFE     Uzaklık ölçümü

TESİSAT (MEP)
  P, PIPE          Boru çiz
  RISER, KOLON     Kolon borusu
  SOURCE, SP       Su kaynağı noktası
  CF, CONNECT      Armatür bağla
  DUCT, KANAL      Kanal döşe
  DC               Kanal bağla
  KABUL, VALIDATE  Tesisatı kabul et

MİMARİ / BLOK
  BLOCK, B         Blok tanımla
  INSERT, I        Blok yerleştir
  WBLOCK           Kat blok dışa aktar
  ARCHDETECT, AD   Mimari eleman tanı
  MAHAL, MA        Mahal tanımla
  MAN              Mahal analizi

ÇIKTI / RAPOR
  BOM, METRAJ      Metraj oluştur
  ARCHBOM, MB      Mimari metraj
  DUCTBOM          HVAC metraj
  SELBOM, SM       Seçim metrajı
  DXF, SAVEAS      DXF dışa aktar
  IFC, BIM         IFC dışa aktar
  IFCIMPORT        IFC içe aktar
  SPEC, TECHSPEC   Teknik şartname
  LEGEND, LEG      Lejant
  PRINT, PLOT      Yazdır

DİĞER
  ROUTE, AR        Otomatik boru rotası
  KS, KOLONSEMA    Kolon şeması
  LABEL, ETIKET    Akıllı etiket
  HELP, ?          Bu pencere

── KLAVYE KISAYOLLARI ──────────────────────────
  Ctrl+Z / Ctrl+Y     Geri al / Yinele
  Ctrl+C / X / V      Kopyala / Kes / Yapıştır
  Ctrl+S              Kaydet
  Ctrl+L              Sol panel aç/kapat
  Ctrl+F              Seçime yakınlaştır
  F2                  Seçili nesnenin özelliklerini düzenle
  F3                  OSNAP aç/kapat
  F8                  ORTHO aç/kapat
  F10                 Polar tracking aç/kapat
  Space               (komut yokken) Son komutu tekrarla
  Space / Enter       (komut aktifken) Onayla / Bitir
  Delete              Seçili nesneleri sil
  Esc                 Aktif komutu iptal et";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
