using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Presentation.Views;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class WBlockWizard : Window
    {
        private int _currentStep = 1;
        private readonly int _totalSteps = 4;
        private readonly CadViewport _viewport;

        // Output properties
        public Vector3D BasePoint { get; private set; } = Vector3D.Zero;
        public List<CadEntity> SelectedEntities { get; private set; } = new();
        public string FinalPath { get; private set; } = "";
        public string FloorName { get; private set; } = "";
        
        // Flags
        private bool _isPointPicked = false;
        private bool _isSelectionPicked = false;

        public event Action? RequestPickPoint;
        public event Action? RequestSelectObjects;
        public event Action<string, string, List<CadEntity>, Vector3D>? OnExportConfirmed;

        public WBlockWizard(CadViewport viewport, string defaultPath = "")
        {
            InitializeComponent();
            _viewport = viewport;
            TxtFilePath.Text = defaultPath;
            UpdateUI();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "AutoCAD DWG (*.dwg)|*.dwg|AutoCAD DXF (*.dxf)|*.dxf",
                Title = "Mimari Planı DWG Olarak Kaydet",
                FileName = "BodrumKat"
            };

            if (saveDialog.ShowDialog() == true)
            {
                TxtFilePath.Text = saveDialog.FileName;
            }
        }

        public void SetBasePoint(Vector3D point)
        {
            BasePoint = point;
            _isPointPicked = true;
            TxtPointStatus.Text = "Hizalama Noktası Kaydedildi.";
            TxtPointStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
            TxtCoordinates.Text = $"(X: {point.X:F2}, Y: {point.Y:F2}, Z: {point.Z:F2})";
            TxtCoordinates.Visibility = Visibility.Visible;
            UpdateUI();
        }

        public void SetEntities(List<CadEntity> entities)
        {
            SelectedEntities = entities ?? new List<CadEntity>();
            _isSelectionPicked = SelectedEntities.Count > 0;
            TxtSelectionStatus.Text = $"{SelectedEntities.Count} mimari nesne seçildi.";
            TxtSelectionStatus.Foreground = _isSelectionPicked ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Orange;
            UpdateUI();
        }

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

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateUI();
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            // Validations before moving next
            if (_currentStep == 1)
            {
                if (string.IsNullOrWhiteSpace(TxtFilePath.Text))
                {
                    MessageBox.Show("Lütfen kayıt yolu ve dosya adı belirtiniz.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                FinalPath = TxtFilePath.Text;
                FloorName = System.IO.Path.GetFileNameWithoutExtension(FinalPath);
            }
            else if (_currentStep == 2)
            {
                if (!_isPointPicked)
                {
                    MessageBox.Show("Sonraki adıma geçmeden önce bir Sabit Referans Noktası seçmelisiniz.", "Zorunlu Adım", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (_currentStep == 3)
            {
                if (!_isSelectionPicked)
                {
                    MessageBox.Show("Lütfen dışa aktarılacak çizimleri seçiniz.", "Zorunlu Adım", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (_currentStep < _totalSteps)
            {
                _currentStep++;
                UpdateUI();
            }
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            OnExportConfirmed?.Invoke(FinalPath, FloorName, SelectedEntities, BasePoint);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateUI()
        {
            // Panel Visibilities
            Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

            // Indicator
            string[] stepTitles = { "Kat İşimlendirme ve Kayıt", "Sabit Referans (Orijin)", "Objeleri Seçin", "Tamamlandı" };
            TxtStepIndicator.Text = $"Adım {_currentStep} / {_totalSteps} • {stepTitles[_currentStep - 1]}";

            // Controls
            BtnBack.IsEnabled = _currentStep > 1;
            
            if (_currentStep == _totalSteps)
            {
                BtnNext.Visibility = Visibility.Collapsed;
                BtnFinish.Visibility = Visibility.Visible;
                
                // Final Summary Update
                SummaryFloor.Text = $"Kat Adı: {FloorName}";
                SummaryPoint.Text = $"Özel Orijin: (X: {BasePoint.X:F1}, Y: {BasePoint.Y:F1})";
                SummaryEntities.Text = $"Transfer: {SelectedEntities.Count} mimari nesne";
            }
            else
            {
                BtnNext.Visibility = Visibility.Visible;
                BtnFinish.Visibility = Visibility.Collapsed;
            }
        }
    }
}
