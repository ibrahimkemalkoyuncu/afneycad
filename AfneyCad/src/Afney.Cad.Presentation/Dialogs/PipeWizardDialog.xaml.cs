using System;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class PipeWizardDialog : Window
    {
        private readonly CadDatabase _database;

        public PipeWizardDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
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

            var systemType = SystemTypeCombo.SelectedIndex switch
            {
                0 => MechanicalSystemType.DomesticColdWater,
                1 => MechanicalSystemType.DomesticHotWater,
                2 => MechanicalSystemType.WasteWater,
                _ => MechanicalSystemType.DomesticColdWater
            };

            try
            {
                var wizard = new PipeWizardService(_database);
                var origin = new Vector3D(0, 0, 0);
                var riser = new Vector3D(-500, 0, 0);
                var entities = wizard.GenerateFromTemplate(type, origin, riser, systemType);

                foreach (var ent in entities) _database.AddEntity(ent);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon yerleştirme hatası: {ex.Message}", "Hata");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
