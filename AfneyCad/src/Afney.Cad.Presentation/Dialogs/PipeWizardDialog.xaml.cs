using System;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class PipeWizardDialog : Window
    {
        // ── Sonuç ────────────────────────────────────────────────────────────────
        // NE/NEDEN — Session #75 iş akışı denetiminde bulunan hatanın düzeltmesi: bu diyalog
        // artık ENTITY OLUŞTURMUYOR/EKLEMİYOR (veritabanına ihtiyacı yok) — sadece kullanıcının
        // seçtiği şablon+sistem tipini dışa açıyor. Gerçek yerleştirme (tıklama noktası +
        // TransactionManager) PlacePipeWizardTemplateCommand'da yapılıyor (bkz. OnPipeWizard çağıran).
        public PipeWizardService.TemplateType SelectedTemplateType { get; private set; }
        public MechanicalSystemType SelectedSystemType { get; private set; }

        public PipeWizardDialog()
        {
            InitializeComponent();
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            var templates = PipeWizardService.GetAvailableTemplates();
            foreach (var t in templates)
            {
                TemplateList.Items.Add(new ListBoxItem
                {
                    Content = $"{t.Name}  ({t.FixtureCount} cihaz)",
                    Tag = t.Type,
                    FontSize = 14,
                    Padding = new Thickness(5, 8, 5, 8)
                });
            }
            if (TemplateList.Items.Count > 0) TemplateList.SelectedIndex = 0;
        }

        private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateList.SelectedItem is ListBoxItem item && item.Tag is PipeWizardService.TemplateType type)
            {
                var templates = PipeWizardService.GetAvailableTemplates();
                var selected = templates.Find(t => t.Type == type);
                DescriptionText.Text = selected.Description;
                FixtureCountText.Text = $"Otomatik oluşturulacak vitrifiye sayısı: {selected.FixtureCount}";
            }
        }

        private void Place_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateList.SelectedItem is not ListBoxItem item || item.Tag is not PipeWizardService.TemplateType type)
            {
                MessageBox.Show("Lütfen bir şablon seçin.", "Uyarı"); return;
            }

            SelectedTemplateType = type;
            SelectedSystemType = SystemTypeCombo.SelectedIndex switch
            {
                0 => MechanicalSystemType.DomesticColdWater,
                1 => MechanicalSystemType.DomesticHotWater,
                2 => MechanicalSystemType.WasteWater,
                _ => MechanicalSystemType.DomesticColdWater
            };

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
