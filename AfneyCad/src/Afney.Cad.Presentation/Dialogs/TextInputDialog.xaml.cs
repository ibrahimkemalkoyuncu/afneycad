using System.Windows;

namespace Afney.Cad.Presentation.Dialogs;

public partial class TextInputDialog : Window
{
    public string InputText => InputBox.Text;

    public TextInputDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        Loaded += (_, _) => InputBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(InputBox.Text))
            DialogResult = true;
    }
}
