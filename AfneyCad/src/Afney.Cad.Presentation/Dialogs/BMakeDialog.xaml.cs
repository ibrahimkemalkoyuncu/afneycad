using System;
using System.Linq;
using System.Windows;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;

namespace Afney.Cad.Presentation.Dialogs
{
    /*
        NE: Gelişmiş Blok Tanımlama (BMAKE) Penceresi Arkası Kodları
        NEDEN: WPF formunda girilen koordinatları okumak, ekrandan nesne veya referans noktası seçme eylemlerini (BlockCommand'a veya diğer komutlara) aktarmak ve nihai bloğu yaratmak için.
    */
    public partial class BMakeDialog : Window
    {
    private readonly BlockCommand _command;
    private readonly CadDatabase _database;

    public string BlockName => TxtBlockName.Text.Trim();
    public Vector3D BasePoint { get; private set; }
    
    // Retain (0), Convert (1), Delete (2)
    public int ObjectBehavior => RbDelete.IsChecked == true ? 2 : (RbRetain.IsChecked == true ? 0 : 1);

    public BMakeDialog(BlockCommand command, CadDatabase database)
    {
        InitializeComponent();
        _command = command;
        _database = database;

        UpdateSelectionCount();
        
        // Command form'u kapatmak istediğinde:
        _command.RegisterCloseCallback(() => 
        {
            this.Close();
        });
    }

    public void UpdateSelectionCount()
    {
        int count = _database.GetSelectedEntities().Count();
        LblObjectsSelected.Text = $"{count} objects selected";
    }

    public void SetBasePoint(Vector3D point)
    {
        BasePoint = point;
        TxtBaseX.Text = point.X.ToString("F3");
        TxtBaseY.Text = point.Y.ToString("F3");
        TxtBaseZ.Text = point.Z.ToString("F3");
    }

    private void BtnPickPoint_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
        // Callbackleri veriyoruz: Biri noktayı formda günceller, diğeri formu geri açar.
        _command.RequestPickPoint(
            point => Dispatcher.Invoke(() => SetBasePoint(point)),
            () => Dispatcher.Invoke(() => this.Show())
        );
    }

    private void BtnSelectObjects_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
        _command.RequestSelectObjects(
            () => Dispatcher.Invoke(() => UpdateSelectionCount()),
            () => Dispatcher.Invoke(() => this.Show())
        );
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtBlockName.Text))
        {
            MessageBox.Show("Please enter a block name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_database.GetSelectedEntities().Count() == 0)
        {
            MessageBox.Show("No objects selected. Please select objects to define the block.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (double.TryParse(TxtBaseX.Text, out double x) &&
            double.TryParse(TxtBaseY.Text, out double y) &&
            double.TryParse(TxtBaseZ.Text, out double z))
        {
            BasePoint = new Vector3D(x, y, z);
        }
        else
        {
            MessageBox.Show("Invalid Base Point coordinates.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _command.FinalizeBlock(BlockName, BasePoint, ObjectBehavior);
    }

    private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Bloğu Kaydet",
            Filter = "DWG Dosyası (*.dwg)|*.dwg|DXF Dosyası (*.dxf)|*.dxf",
            DefaultExt = ".dwg",
            FileName = TxtBlockName?.Text ?? "BLOCK"
        };
        if (dlg.ShowDialog() == true)
        {
            TxtFilePath.Text = dlg.FileName;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _command.Cancel();
        this.Close();
    }
}
}
