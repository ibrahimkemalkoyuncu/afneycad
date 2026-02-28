using System.IO;
using System.Text;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical.Services;
using Microsoft.Win32;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class BomReportWindow : Window
    {
        private readonly CadDatabase _database;
        private readonly BomService _bomService;

        public BomReportWindow(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
            _bomService = new BomService(_database);
            
            LoadBomData();
        }

        private void LoadBomData()
        {
            var bomData = _bomService.GenerateBom();
            BomGrid.ItemsSource = bomData;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var bomData = BomGrid.ItemsSource as System.Collections.Generic.List<BomItem>;
            if (bomData == null || bomData.Count == 0)
            {
                MessageBox.Show("Dışa aktarılacak metraj verisi bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Dosyası (*.csv)|*.csv",
                Title = "Metraj Raporunu Dışa Aktar",
                FileName = "AfneyCAD_Metraj_Raporu.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8))
                    {
                        writer.WriteLine("Kategori;Açıklama;Malzeme Tipi;Miktar;Birim");
                        foreach (var item in bomData)
                        {
                            writer.WriteLine($"{item.Category};{item.Description};{item.Material};{item.Quantity};{item.Unit}");
                        }
                    }
                    MessageBox.Show("Metraj raporu başarıyla kaydedildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Dosya kaydedilirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
