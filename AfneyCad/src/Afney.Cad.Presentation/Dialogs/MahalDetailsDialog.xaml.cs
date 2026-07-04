using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Afney.Cad.Geometry.Primitives;
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
    private readonly MahalEntity _mahal;

    // ─── TS 1258 Mahal Tipi Kataloğu ──────────────────────────────────────────
    // (tip adı → (açıklama, önerilen vitrifiye listesi [(ad, LU)]))
    private static readonly Dictionary<string, (string Hint, (string Name, double LU)[] Fixtures)> s_typeMap = new()
    {
        ["Banyo"]             = ("1 küvet/duş + 1 lavabo", [("Küvet", 3.0), ("Lavabo", 1.5)]),
        ["WC"]                = ("1 klozet + 1 lavabo", [("Klozet", 3.0), ("Lavabo", 1.5)]),
        ["Mutfak"]            = ("1 eviye + 1 bulaşık makinesi", [("Eviye (tek)", 2.0), ("Bulaşık Makinesi", 1.5)]),
        ["Mutfak + Banyo"]    = ("Kombine: eviye + küvet/duş + lavabo", [("Eviye (tek)", 2.0), ("Küvet", 3.0), ("Lavabo", 1.5)]),
        ["Oturma Odası"]      = ("Vitrifiye yok — temiz su ihtiyacı yok", []),
        ["Yatak Odası"]       = ("Vitrifiye yok", []),
        ["Çocuk Odası"]       = ("Vitrifiye yok", []),
        ["Çamaşır Odası"]     = ("1 çamaşır makinesi bağlantısı", [("Çamaşır Makinesi", 1.5)]),
        ["Koridor"]           = ("Vitrifiye yok", []),
        ["Antre"]             = ("Vitrifiye yok", []),
        ["Balkon"]            = ("İsteğe bağlı: 1 döşeme süzgeci", [("Döşeme Süzgeci", 0.5)]),
        ["Depo"]              = ("Vitrifiye yok", []),
        ["Ofis"]              = ("Paylaşımlı WC — ayrı mahal", []),
        ["Ofis (Açık Plan)"]  = ("Paylaşımlı WC — ayrı mahal", []),
        ["Ofis WC (Erkek)"]   = ("2 pisuvar + 2 klozet + 1 lavabo (10 kişi/WC)", [("Pisuvar", 2.0), ("Pisuvar", 2.0), ("Klozet", 3.0), ("Lavabo", 1.5)]),
        ["Ofis WC (Kadın)"]   = ("2 klozet + 1 lavabo (10 kişi/WC)", [("Klozet", 3.0), ("Klozet", 3.0), ("Lavabo", 1.5)]),
        ["Toplantı Odası"]    = ("Vitrifiye yok", []),
        ["Restoran Mutfak"]   = ("2 eviye + 1 bulaşıkhane", [("Eviye (tek)", 2.0), ("Eviye (tek)", 2.0), ("Bulaşık Makinesi", 1.5)]),
        ["Otel Odası Banyo"]  = ("1 küvet + 1 lavabo + 1 klozet", [("Küvet", 3.0), ("Lavabo", 1.5), ("Klozet", 3.0)]),
        ["Hasta Odası"]       = ("1 lavabo (hastane tipi)", [("Lavabo", 1.5)]),
        ["Ameliyathane"]      = ("2 cerrahi lavabo + 1 döşeme süzgeci", [("Lavabo", 1.5), ("Lavabo", 1.5), ("Döşeme Süzgeci", 0.5)]),
        ["Hastane WC"]        = ("2 klozet + 2 lavabo (engelli erişimli)", [("Klozet", 3.0), ("Klozet", 3.0), ("Lavabo", 1.5), ("Lavabo", 1.5)]),
        ["Sınıf"]             = ("Vitrifiye yok", []),
        ["Okul WC"]           = ("3 klozet + 2 lavabo (30 öğrenci)", [("Klozet", 3.0), ("Klozet", 3.0), ("Klozet", 3.0), ("Lavabo", 1.5), ("Lavabo", 1.5)]),
        ["Laboratuvar"]       = ("2 lavabo + 1 döşeme süzgeci", [("Lavabo", 1.5), ("Lavabo", 1.5), ("Döşeme Süzgeci", 0.5)]),
        ["Soyunma/Duş"]       = ("4 duş + 1 lavabo (soyunma odası başına)", [("Duş Teknesi", 2.0), ("Duş Teknesi", 2.0), ("Duş Teknesi", 2.0), ("Duş Teknesi", 2.0), ("Lavabo", 1.5)]),
        ["Diğer"]             = ("Manuel vitrifiye tanımı yapın", []),
    };

    // ─── Constructor ───────────────────────────────────────────────────────────
    public MahalDetailsDialog(MahalEntity mahal)
    {
        _mahal = mahal;
        InitializeComponent();

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

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; Close(); }
    }

    // ─── Mahal Tipi Değişti ────────────────────────────────────────────────────
    private void OnMahalTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        string? selected = (MahalTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(selected) || !s_typeMap.TryGetValue(selected, out var info))
        {
            TypeHintPanel.Visibility = Visibility.Collapsed;
            return;
        }

        double expectedLU = info.Fixtures.Sum(f => f.LU);
        string fixtureList = info.Fixtures.Length > 0
            ? string.Join(" + ", info.Fixtures.Select(f => $"{f.Name} ({f.LU} LU)"))
            : "—";

        TypeHintTitle.Text = $"Standart set: ∑LU = {expectedLU:F1}  (TS 1258)";
        TypeHintDetail.Text = $"{info.Hint}\n{fixtureList}";
        TypeHintPanel.Visibility = Visibility.Visible;

        // Mahalde henüz vitrifiye yoksa standart set butonunu göster
        bool hasNoFixtures = _mahal.Fixtures.Count == 0;
        bool hasFixtures = info.Fixtures.Length > 0;
        AddStandardSetBtn.Visibility = (hasNoFixtures && hasFixtures) ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── Standart Vitrifiye Seti Ekle ─────────────────────────────────────────
    private void OnAddStandardFixtureSet(object sender, RoutedEventArgs e)
    {
        string? selected = (MahalTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(selected) || !s_typeMap.TryGetValue(selected, out var info)) return;
        if (info.Fixtures.Length == 0) return;

        // Mahalin merkez pozisyonunu merkez olarak al
        var center = _mahal.BoundaryPoints.Count > 0
            ? _mahal.BoundaryPoints.Aggregate((a, b) => new Vector3D((a.X + b.X) / 2, (a.Y + b.Y) / 2, 0))
            : new Vector3D(0, 0, 0);

        double offsetX = 0;
        foreach (var (name, lu) in info.Fixtures)
        {
            var pos = new Vector3D(center.X + offsetX, center.Y, 0);
            var fixture = new SanitaryFixtureEntity(pos, name, lu);
            _mahal.Fixtures.Add(fixture);
            offsetX += 500; // 500mm aralık
        }

        // UI güncelle
        FixtureGrid.ItemsSource = null;
        FixtureGrid.ItemsSource = _mahal.Fixtures;
        TotalFUText.Text = _mahal.TotalLoadUnits.ToString("F2");
        MahalSubHeaderText.Text = $"Alan: {_mahal.Area:F2} m²  ·  Çevre: {_mahal.Perimeter:F2} m  ·  {_mahal.Fixtures.Count} cihaz";
        AddStandardSetBtn.Visibility = Visibility.Collapsed;

        Serilog.Log.Information("[MahalDialog] Standart set eklendi: {Type} — {Count} vitrifiye, ∑LU={LU:F1}",
            selected, _mahal.Fixtures.Count, _mahal.TotalLoadUnits);
    }
}
