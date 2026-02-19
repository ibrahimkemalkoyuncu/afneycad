using System.Windows;
using System.Windows.Controls;

namespace Afney.Cad.Presentation.Dialogs;

public partial class MahalInfoDialog : Window
{
    public string MahalName { get; private set; } = "";
    public string MahalType { get; private set; } = "";

    public MahalInfoDialog(string defaultName = "")
    {
        InitializeComponent();
        MahalNameBox.Text = defaultName;
        MahalNameBox.Focus();
        MahalNameBox.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MahalNameBox.Text))
        {
            MessageBox.Show("Lütfen bir mahal ismi girin.");
            return;
        }

        MahalName = MahalNameBox.Text;
        MahalType = (TypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Genel";
        
        DialogResult = true;
        Close();
    }
}
