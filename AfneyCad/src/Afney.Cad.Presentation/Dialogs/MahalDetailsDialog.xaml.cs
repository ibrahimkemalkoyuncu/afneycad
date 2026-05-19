using System.Windows;
using System.Windows.Input;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Mahal Tanımı Dialog (MahalDetailsDialog Code-Behind)
   NEDEN: Fine Sani standardında mahal adı + tip girişi,
          alan/çevre hesabı gösterimi, vitrifiye tablosu ve Kaydet/İptal.

   ÇIKTILAR:
   - DialogResult = true  → Kullanıcı kaydet'e bastı; Mahal.MahalName + MahalType güncellendi
   - DialogResult = false → İptal
*/
public partial class MahalDetailsDialog : Window
{
    // Düzenlenen mahal entity'si (caller tarafından verilir, kaydet ile güncellenir)
    private readonly MahalEntity _mahal;

    // ─── Constructor ───────────────────────────────────────────────────────────
    public MahalDetailsDialog(MahalEntity mahal)
    {
        InitializeComponent();
        _mahal = mahal;

        Loaded += (_, _) => PopulateDialog();
    }

    // ─── Veri Doldurma ─────────────────────────────────────────────────────────

    /*
       NE: Dialog Alanlarını Doldur (PopulateDialog)
       NEDEN: MahalEntity'den gelen mevcut verileri UI'a yansıtmak için.
    */
    private void PopulateDialog()
    {
        // Üst başlık
        MahalSubHeaderText.Text = $"Alan: {_mahal.Area:F2} m²  ·  Çevre: {_mahal.Perimeter:F2} m  ·  {_mahal.Fixtures.Count} cihaz";

        // Giriş alanları — daha önceden kaydedilmiş değerler (düzenleme durumu)
        MahalNameInput.Text    = string.IsNullOrWhiteSpace(_mahal.MahalName) || _mahal.MahalName == "Yeni Mahal"
                                   ? string.Empty
                                   : _mahal.MahalName;
        MahalNameInput.Focus();
        MahalNameInput.SelectAll();

        // Tip combobox — mevcut tip ile eşleştir
        SetComboSelection(_mahal.MahalType);

        // Alan & Çevre (hesaplanan, salt okunur)
        AreaText.Text      = $"{_mahal.Area:F2} m²";
        PerimeterText.Text = $"{_mahal.Perimeter:F2} m";

        // Vitrifiye listesi
        FixtureGrid.ItemsSource = _mahal.Fixtures;

        // Toplam LU
        TotalFUText.Text = _mahal.TotalLoadUnits.ToString("F2");
    }

    /*
       NE: ComboBox'ı Mahal Tipine Göre Seç
       NEDEN: MahalEntity'de kaydedilmiş tip, ComboBox içindeki item ile eşleştirilmeli.
    */
    private void SetComboSelection(string? mahalType)
    {
        if (string.IsNullOrWhiteSpace(mahalType)) return;

        foreach (var item in MahalTypeCombo.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem cbi &&
                cbi.Content?.ToString() == mahalType)
            {
                MahalTypeCombo.SelectedItem = item;
                return;
            }
        }
    }

    // ─── Event Handlers ────────────────────────────────────────────────────────

    /*
       NE: Kaydet Butonu (OnSaveClick)
       NEDEN: MahalEntity'yi kullanıcının girdiği ad ve tip ile günceller.
              Zorunlu alan kontrolü burada yapılır.
    */
    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ValidationMsg.Text = string.Empty;

        string name = MahalNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ValidationMsg.Text = "Mahal adı boş olamaz.";
            MahalNameInput.Focus();
            return;
        }

        string selectedType = (MahalTypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)
                              ?.Content?.ToString()
                              ?? "Diğer";

        // MahalEntity güncelle
        _mahal.MahalName = name;
        _mahal.MahalType = selectedType;

        Serilog.Log.Information("[MahalDialog] Kaydedildi: '{Name}' / {Type} — {Area:F2}m²",
            _mahal.MahalName, _mahal.MahalType, _mahal.Area);

        DialogResult = true;
        Close();
    }

    /*
       NE: İptal Butonu (OnCancelClick)
    */
    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /*
       NE: Enter → Kaydet, Escape → İptal (Window_KeyDown)
    */
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
