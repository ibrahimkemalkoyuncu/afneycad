using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.ProjectManagement;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class NewProjectDialog : Window
    {
        public string ProjectName { get; private set; } = string.Empty;
        public string FinalProjectFolder { get; private set; } = string.Empty;
        public string ArchitectPath { get; } = string.Empty; // Compat

        public ObservableCollection<ProjectFileItem> FileItems { get; set; } = new ObservableCollection<ProjectFileItem>();

        private string _basePath = @"C:\AFNEY_SANI\CALC";

        /*
           NE: NewProjectDialog Yapıcı Metodu
           NEDEN: Proje listesini göstermek için gerekli UI bileşenlerini hazırlar.
        */
        public NewProjectDialog()
        {
            InitializeComponent();
            ProjectListView.ItemsSource = FileItems;
        }

        /*
           NE: Pencere Yüklendi Olayı (Window_Loaded)
           NEDEN: Pencere açıldığında otomatik olarak mevcut projeleri listelemeye başlamak için.
        */
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Location is strictly C:\AFNEY_SANI\CALC and hardcoded in UI now.
            LoadFiles();
        }

        /*
           NE: Projeleri Yükle (LoadFiles)
           NEDEN: AFNEY_SANI dizini altındaki .bld uzantılı proje klasörlerini tarayıp listelemek için.
        */
        private void LoadFiles()
        {
            FileItems.Clear();
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }

            try
            {
                var dirs = Directory.GetDirectories(_basePath);
                foreach (var dir in dirs)
                {
                    var info = new DirectoryInfo(dir);
                    bool isProject = info.Extension.Equals(".bld", StringComparison.OrdinalIgnoreCase);
                    
                    FileItems.Add(new ProjectFileItem
                    {
                        Name = info.Name,
                        Date = info.LastWriteTime.ToString("dd-MM-yy [HH:mm]"),
                        Type = isProject ? "Proje Dosyası" : "Dosya klasörü",
                        Size = "", 
                        FullPath = dir
                    });
                }
            }
            catch { }
        }

        public string SelectedFilePath { get; private set; } = string.Empty;

        /*
           NE: Proje Kaydet/Oluştur (Save_Click)
           NEDEN: Girilen proje ismine göre yeni bir dizin yapısı oluşturmak ve proje başlangıcını yapmak için.
        */
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                string name = ProjectNameBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Lütfen bir proje ismi giriniz.");
                    return;
                }

                // ProjectManager will handle folder creation
                var mgr = new ProjectManager();
                FinalProjectFolder = mgr.CreateProject(name);
                ProjectName = name;
                SelectedFilePath = string.Empty;
                
                DialogResult = true;
                Close();
            }
            catch(Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /*
           NE: Proje Seçimi Değişti (ProjectListView_SelectionChanged)
           NEDEN: Listeden bir proje tıklandığında, ismini otomatik olarak giriş kutusuna yazmak için.
        */
        private void ProjectListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectListView.SelectedItem is ProjectFileItem item)
            {
                // Eğer proje ise ismini kutuya yaz (overwrite gibi) but strip extension
                string name = item.Name;
                if (name.EndsWith(".bld", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4);
                
                ProjectNameBox.Text = name;
            }
        }
    }

    public class ProjectFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }
}
