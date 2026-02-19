using System.Windows;

namespace Afney.Cad.Presentation.Dialogs;

public partial class BlockNameDialog : Window
{
    public string BlockName { get; private set; } = string.Empty;

    public BlockNameDialog()
    {
        InitializeComponent();
        NameInput.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameInput.Text))
        {
            MessageBox.Show("Lütfen geçerli bir isim girin.");
            return;
        }

        BlockName = NameInput.Text.Trim();
        DialogResult = true;
        Close();
    }
}
