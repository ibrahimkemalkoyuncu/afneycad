using System;
using System.Windows;
using Afney.Cad.Mechanical.Services;
using static Afney.Cad.Mechanical.Services.ValveLibraryService;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Vana Kütüphanesi Diyaloğu (ValveLibraryDialog)
   NEDEN: Kullanıcının katalogdan vana seçip projeye yerleştirmesini sağlamak için.
*/
public partial class ValveLibraryDialog : Window
{
    private readonly ValveLibraryService _service;
    public ValveDefinition? SelectedValve { get; private set; }

    public ValveLibraryDialog()
    {
        InitializeComponent();
        _service = new ValveLibraryService();
        LoadValves();
    }

    private void LoadValves()
    {
        var valves = _service.GetAll();
        ValveGrid.ItemsSource = valves;
        if (valves.Count > 0)
        {
            ValveGrid.SelectedIndex = 0;
        }
    }

    private void Place_Click(object sender, RoutedEventArgs e)
    {
        if (ValveGrid.SelectedItem is ValveDefinition valve)
        {
            SelectedValve = valve;
            this.DialogResult = true;
            this.Close();
        }
        else
        {
            MessageBox.Show("Lütfen yerleştirmek için bir vana seçin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }
}
