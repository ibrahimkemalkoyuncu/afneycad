using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;

namespace Afney.Cad.Presentation.Dialogs;

public partial class PrintPreviewDialog : Window
{
    private readonly CadDatabase _database;
    private string _paperSize = "A3";

    private static readonly Dictionary<string, (double W, double H)> PaperSizes = new()
    {
        ["A4"] = (297, 210),
        ["A3"] = (420, 297),
        ["A2"] = (594, 420),
        ["A1"] = (841, 594),
    };

    public PrintPreviewDialog(CadDatabase database)
    {
        InitializeComponent();
        _database = database;
        SetPaper("A3");
        UpdatePreviewInfo();
    }

    private void SetPaper(string size)
    {
        _paperSize = size;
        var (w, h) = PaperSizes[size];
        double scale = 600.0 / w;
        PaperBorder.Width = w * scale;
        PaperBorder.Height = h * scale;
        TxtPaperInfo.Text = $"{size} — {w} × {h} mm";
        UpdatePreviewInfo();
    }

    private void UpdatePreviewInfo()
    {
        int count = _database.GetAllEntities().Count();
        int layers = _database.GetLayers().Count();
        TxtPreviewContent.Text = $"Toplam {count} nesne\n{layers} katman\n\nKağıt: {_paperSize}";
    }

    private void OnA4_Click(object sender, RoutedEventArgs e) => SetPaper("A4");
    private void OnA3_Click(object sender, RoutedEventArgs e) => SetPaper("A3");
    private void OnA2_Click(object sender, RoutedEventArgs e) => SetPaper("A2");
    private void OnA1_Click(object sender, RoutedEventArgs e) => SetPaper("A1");

    private void OnPrint_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Controls.PrintDialog();
        if (dlg.ShowDialog() == true)
        {
            MessageBox.Show($"Yazdırma işlemi başlatıldı.\nKağıt: {_paperSize}", "Baskı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
