using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Pafta Seti Yöneticisi Diyaloğu (SheetSetManagerDialog)
   NEDEN (Session #74): Kullanıcı isteği — SheetIndexService artık kalıcı (bkz. sınıfın kendi
          açıklaması ve SheetSetPersistenceService), ama bunu yöneten görsel bir arayüz yoktu;
          pafta kayıtları sadece TitleBlockDialog üzerinden dolaylı olarak ekleniyordu.
          Bu diyalog, AutoCAD'in Sheet Set Manager'ına benzer şekilde:
          - Projedeki TÜM paftaları (otomatik + elle eklenen) tek bir tabloda listeler,
          - Elle yeni pafta ekleme, silme ve yeniden sıralama (yukarı/aşağı) sağlar,
          - Çift tıklamayla Pafta İndeksi (HTML) raporunu açar.

   KALICILIK: Bu diyalog SheetIndexService örneği üzerinde doğrudan çalışır (CadDocumentContext.
          SheetIndex, bkz. MainWindow.OnSheetSetManager) — yapılan her değişiklik, proje
          kaydedildiğinde (OnSave/OnSaveAs → SaveSheetSetState) otomatik olarak sidecar dosyaya
          yazılır. Ayrıca burada da "Kapat" sırasında ekstra bir kaydetme adımına gerek yoktur;
          servis referans tipte olduğu için CadDocumentContext üzerinden hep aynı canlı örnek kullanılır.
*/
public partial class SheetSetManagerDialog : Window
{
    private readonly SheetIndexService _sheetIndex;
    private readonly string _projectName;

    public SheetSetManagerDialog(SheetIndexService sheetIndex, string projectName = "AfneyCAD Projesi")
    {
        InitializeComponent();
        _sheetIndex  = sheetIndex;
        _projectName = string.IsNullOrWhiteSpace(projectName) ? "AfneyCAD Projesi" : projectName;
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        SheetGrid.ItemsSource = null;
        SheetGrid.ItemsSource = _sheetIndex.Sheets;
        StatusText.Text = $"Toplam {_sheetIndex.Sheets.Count} pafta · Sonraki öneri: {_sheetIndex.PeekNextNumber(TxtDiscipline.Text)}";
    }

    private void SheetGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Seçili satırı forma taşımaya gerek yok — sil/sırala işlemleri doğrudan SelectedItem üzerinden çalışır.
    }

    // ── Pafta Ekle ───────────────────────────────────────────────────────────────

    private void AddSheet_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusText.Text = "⚠ Çizim adı boş olamaz.";
            return;
        }

        string number = TxtNumber.Text.Trim();
        string discipline = string.IsNullOrWhiteSpace(TxtDiscipline.Text) ? "M" : TxtDiscipline.Text.Trim();
        string description = TxtDescription.Text.Trim();
        string status = string.IsNullOrWhiteSpace(TxtStatus.Text) ? "Taslak" : TxtStatus.Text.Trim();

        SheetIndexService.SheetEntry entry;
        if (string.IsNullOrEmpty(number))
        {
            // Otomatik seri numara — RegisterSheet, sayaçları ilerletecek şekilde kaydeder.
            entry = _sheetIndex.RegisterSheet(null, name, description, discipline);
            entry.Status = status;
        }
        else
        {
            // Kullanıcının kendi belirlediği numara — sayaçlara/otomatik numaralandırmaya dokunmaz.
            entry = _sheetIndex.AddManualEntry(number, name, description, discipline, status);
        }

        TxtNumber.Text = "";
        TxtName.Text = "";
        TxtDescription.Text = "";
        TxtStatus.Text = "Taslak";

        RefreshGrid();
        StatusText.Text = $"✓ {entry.Number} eklendi.";
    }

    // ── Silme ────────────────────────────────────────────────────────────────────

    private void DeleteSheet_Click(object sender, RoutedEventArgs e)
    {
        if (SheetGrid.SelectedItem is not SheetIndexService.SheetEntry entry) return;

        if (MessageBox.Show($"'{entry.Number} — {entry.Name}' silinsin mi?", "Onay",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _sheetIndex.RemoveSheet(entry);
        RefreshGrid();
        StatusText.Text = $"✓ {entry.Number} silindi.";
    }

    // ── Yeniden Sıralama ─────────────────────────────────────────────────────────

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (SheetGrid.SelectedItem is not SheetIndexService.SheetEntry entry) return;
        if (_sheetIndex.MoveUp(entry))
        {
            RefreshGrid();
            SheetGrid.SelectedItem = entry;
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (SheetGrid.SelectedItem is not SheetIndexService.SheetEntry entry) return;
        if (_sheetIndex.MoveDown(entry))
        {
            RefreshGrid();
            SheetGrid.SelectedItem = entry;
        }
    }

    // ── Pafta İndeksi (HTML) ─────────────────────────────────────────────────────

    private void ExportIndex_Click(object sender, RoutedEventArgs e) => OpenIndexHtml();

    private void SheetGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SheetGrid.SelectedItem is SheetIndexService.SheetEntry) OpenIndexHtml();
    }

    private void OpenIndexHtml()
    {
        try
        {
            string html = _sheetIndex.BuildIndexHtml(_projectName);
            string tempPath = Path.Combine(Path.GetTempPath(),
                $"AfneyCAD_PaftaIndeksi_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            File.WriteAllText(tempPath, html, Encoding.UTF8);
            Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Pafta indeksi açılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
