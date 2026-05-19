using System.Windows;

namespace Afney.Cad.Presentation.Dialogs;

/// <summary>
/// Tek satır metin girişi için minimal dialog.
/// LayerManagerPanel'daki "Yeni Katman" ve "Yeniden Adlandır" işlemleri için kullanılır.
/// </summary>
public partial class InputDialog : Window
{
    public string InputText => InputBox.Text;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        InputBox.Text = defaultValue;
        InputBox.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            DialogResult = true;
            Close();
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
