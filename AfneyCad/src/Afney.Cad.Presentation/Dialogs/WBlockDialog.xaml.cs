using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Blocks;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Presentation.Views;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class WBlockDialog : Window
    {
        private readonly CadViewport _viewport;
        
        // Output properties
        public Vector3D BasePoint { get; private set; } = Vector3D.Zero;
        public List<CadEntity> SelectedEntities { get; private set; } = new();
        public string FinalPath { get; private set; } = "";
        public string FloorName { get; private set; } = "";

        /*
           NE: WBlockDialog Yapıcı Metodu
           NEDEN: Blok kaydetme arayüzünü başlatır ve varsa mevcut seçimi içeri aktarır.
        */
        public WBlockDialog(CadViewport viewport, string defaultPath = "")
        {
            InitializeComponent();
            _viewport = viewport;
            FilePathBox.Text = defaultPath;
            
            // Set initial UI state if viewport already has selection
            var selection = _viewport.GetSelectedEntities().ToList();
            if (selection.Any())
            {
                SetEntities(selection);
            }
        }

        /*
           NE: Temel Noktayı Ayarla (SetBasePoint)
           NEDEN: Kaydedilecek bloğun 0,0,0 noktası olacak referans koordinatı belirlemek için.
        */
        public void SetBasePoint(Vector3D point)
        {
            BasePoint = point;
            TxtX.Text = point.X.ToString("F2");
            TxtY.Text = point.Y.ToString("F2");
            TxtZ.Text = point.Z.ToString("F2");
            PointStatusText.Text = $"Hizalama Noktası Seçildi";
            PointStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
        }

        /*
           NE: Varlıkları Ayarla (SetEntities)
           NEDEN: Bloğa dahil edilecek seçili nesnelerin listesini güncellemek için.
        */
        public void SetEntities(List<CadEntity> entities)
        {
            SelectedEntities = entities;
            SelectionStatusText.Text = $"{entities.Count} nesne seçildi";
            SelectionStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
        }

        public event Action? RequestPickPoint;
        public event Action? RequestSelectObjects;

        private void SelectPoint_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            RequestPickPoint?.Invoke();
        }

        private void SelectEntities_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            RequestSelectObjects?.Invoke();
        }

        /*
           NE: Gözat (Browse_Click)
           NEDEN: Kaydedilecek bloğun (dwg/json/afney) dosya yolunu seçmek için.
        */
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Afney CAD Planı (*.afney)|*.afney|Autocad DXF (*.dxf)|*.dxf",
                Title = "Mimari Planı Kaydet",
                FileName = "YeniPlan"
            };

            if (saveDialog.ShowDialog() == true)
            {
                FilePathBox.Text = saveDialog.FileName;
            }
        }

        /*
           NE: Tamam/Kaydet (Save_Click)
           NEDEN: Girilen temel nokta, seçilen nesneler ve dosya yoluyla WBlock işlemini onaylamak için.
        */
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEntities.Count == 0)
            {
                MessageBox.Show("Lütfen kaydedilecek nesneleri seçin.", "Hata");
                return;
            }

            if (string.IsNullOrWhiteSpace(FilePathBox.Text))
            {
                MessageBox.Show("Lütfen bir dosya adı ve kayıt yolu belirtin.", "Hata");
                return;
            }

            FinalPath = FilePathBox.Text;
            FloorName = System.IO.Path.GetFileNameWithoutExtension(FinalPath);
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
