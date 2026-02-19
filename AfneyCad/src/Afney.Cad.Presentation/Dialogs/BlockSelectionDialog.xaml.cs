using System.Collections.Generic;
using System.Windows;
using Afney.Cad.Domain.Blocks;

namespace Afney.Cad.Presentation.Dialogs;

public partial class BlockSelectionDialog : Window
{
    public string SelectedBlockName { get; private set; } = string.Empty;

    public BlockSelectionDialog(IEnumerable<CadBlockRecord> blocks)
    {
        InitializeComponent();
        BlockList.ItemsSource = blocks;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void BlockList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void ConfirmSelection()
    {
        if (BlockList.SelectedItem is CadBlockRecord record)
        {
            SelectedBlockName = record.Name;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Lütfen bir blok seçin.");
        }
    }
}
