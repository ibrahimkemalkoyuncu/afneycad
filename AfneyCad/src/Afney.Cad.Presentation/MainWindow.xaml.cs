using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Entities.Basic;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;
using Afney.Cad.Mechanical.Models;
using Afney.Cad.Mechanical.Engine;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Commands.MechanicalCommands;
using Afney.Cad.Commands.BasicCommands;
using Afney.Cad.Presentation.Dialogs;
using Afney.Cad.Domain.Blocks;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Serilog;
using System.Diagnostics;
using Afney.Cad.Infrastructure.Import;
using System.IO;
using ACadSharp.IO;
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Infrastructure.Export;

namespace Afney.Cad.Presentation
{

    /*
       NE: Ana Pencere (MainWindow)
       NEDEN: Uygulama kabuğu ve komut koordinasyon merkezidir.
    */
    public partial class MainWindow : Window
    {
        // MDI (Multi Document Interface) - Çoklu Sekme Yönetimi
        private System.Collections.ObjectModel.ObservableCollection<CadDocumentContext> _documents = new System.Collections.ObjectModel.ObservableCollection<CadDocumentContext>();
        private CadDocumentContext? _activeContext;

        public CadDocumentContext ActiveContext
        {
            get
            {
                if (_activeContext == null && _documents.Count > 0) _activeContext = _documents[0];
                if (_activeContext == null) throw new InvalidOperationException("Aktif doküman yok. Lütfen yeni bir sekme açın.");
                return _activeContext;
            }
        }

        // Dinamik Erişimciler (Geriye Dönük Uyumluluk için)
        private CadDatabase _database => ActiveContext.Database;
        private Afney.Cad.Commands.History.CommandHistory _history => ActiveContext.History;
        private MechanicalKernel _mechanicalKernel => ActiveContext.MechanicalKernel;
        private Afney.Cad.Application.Services.SnapEngine _snapEngine => ActiveContext.SnapEngine;
        private Afney.Cad.Presentation.Views.CadViewport Viewport => ActiveContext.Viewport;

        /*
           NE: MainWindow Yapıcı Metodu
           NEDEN: Bileşenleri başlatır ve uygulama açıldığında otomatik olarak boş bir proje açılmasını sağlar.
        */
        public MainWindow()
        {
            InitializeComponent();

            // IntelligencePanel üzerinden gelen canlı özellikleri Database'e kaydet ve sistemi yeniden hesapla
            RightPanel.EntityModified += OnEntityModifiedFromRightPanel;

            // Katman Görünürlük (Layer Visibility) Tuşlarına basıldıkça Viewport Engine'e haber ver
            ProjectNavigatorPanel.LayerVisibilityChanged += OnLayerVisibilityChanged;

            // İlk sekmeyi (Boş Proje) oluştur
            CreateNewDocument("Boş Proje");
        }

        /*
           NE: Yeni Belge Oluşturma (CreateNewDocument)
           NEDEN: Kullanıcı "Yeni" dediğinde veya uygulama başladığında izole bir CAD bağlamı (Database, Kernel, History) oluşturmak için.
           PARAMETRELER: title (Sekme başlığı), filePath (Dosya yolu - isteğe bağlı)
        */
        private void CreateNewDocument(string title, string? filePath = null)
        {
            // 1. Yeni Context Oluştur
            var ctx = new CadDocumentContext
            {
                Database = new CadDatabase(),
                ProjectName = title,
                FilePath = filePath ?? string.Empty,
                MechanicalKernel = new MechanicalKernel()
            };


            ctx.History = new Afney.Cad.Commands.History.CommandHistory(ctx.Database.TransactionManager);
            ctx.SnapEngine = new Afney.Cad.Application.Services.SnapEngine(ctx.Database);
            ctx.SelectionManager = new Afney.Cad.Application.Services.SelectionManager(ctx.Database);

            // 2. Kernel Setup
            ctx.MechanicalKernel.SetDatabase(ctx.Database);

            // Event Bağlantıları (Context Bazlı)
            ctx.Database.EntityAdded += ctx.MechanicalKernel.OnEntityAddedToDatabase;
            ctx.Database.EntityRemoved += ctx.MechanicalKernel.OnEntityRemovedFromDatabase;
            ctx.Database.EntityUpdated += ctx.MechanicalKernel.OnEntityUpdatedInDatabase;

            // Kernel -> DB Entegrasyonu
            ctx.MechanicalKernel.OnRequestAddEntity += (entity) => ctx.History.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(ctx.Database, entity));
            ctx.MechanicalKernel.OnRequestDeleteEntity += (entity) => ctx.History.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.RemoveEntityOperation(ctx.Database, entity));

            // 3. Viewport Oluştur
            var viewport = new Afney.Cad.Presentation.Views.CadViewport();
            viewport.Initialize(ctx.Database, ctx.SnapEngine, ctx.SelectionManager);
            viewport.OnFeedback += (msg) => StatusText.Text = msg;
            viewport.SelectionChanged += (items) => RightPanel.UpdateEntityInfo(items.FirstOrDefault());

            ctx.Viewport = viewport;

            // 4. Tab Item Oluştur
            var tab = new TabItem
            {
                Header = title,
                Content = CreateTabContent(viewport),
                Tag = ctx
            };

            _documents.Add(ctx);
            DocumentTabs.Items.Add(tab);
            DocumentTabs.SelectedItem = tab; // Bu tetikleme OnTabChanged çağırır
        }

        /*
           NE: Sekme İçeriği Oluşturma
           NEDEN: Viewport'un TabItem içine nasıl yerleştirileceğini (Border, Padding vb.) merkezi olarak yönetmek için.
        */
        private FrameworkElement CreateTabContent(Control viewport)
        {
            // Viewport'u çerçevele (Border vs eklenebilir)
            return viewport;
        }

        private void OnEntityModifiedFromRightPanel(object? sender, Afney.Cad.Domain.Abstractions.CadEntity e)
        {
            try 
            {
                if (_activeContext == null) return;
                
                // 1. Veritabanını Güncelle
                _database.UpdateEntity(e);

                // 2. İş Akışını (IsoSync, Debi ve Çap) Yeniden Tetikle
                // Not: Olay sahte (RoutedEventArgs) olarak gönderilip menü butonuna basılmış gibi davranılıyor
                OnCalculateFlowCommand(sender, new RoutedEventArgs());
                
                // 3. Panelin kendisini yeni verilerle donat
                RightPanel.UpdateEntityInfo(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Değişiklik uygulanırken hata: {ex.Message}");
            }
        }

        /*
           NE: Katman Görünürlük Yöneticisi (OnLayerVisibilityChanged)
           NEDEN: Sol paneldeki katman checkboxlarına basıldıkça (örn: Mimariyi gizle) bunu ilgili (geçerli) Viewport'un render mekanizmasına bildirmek için.
        */
        private void OnLayerVisibilityChanged(string layerName, bool isVisible)
        {
            if (_activeContext?.Viewport == null) return;

            if (isVisible)
            {
                // Katman Görünür Dedi (Gizli Listeden Çıkart)
                _activeContext.Viewport.HiddenLayers.Remove(layerName);
            }
            else
            {
                // Katmanı Gizle Dedi (Gizli Listeye Ekle)
                _activeContext.Viewport.HiddenLayers.Add(layerName);
            }

            // Çizim motoruna kendini yeniden çizmesini söyle
            _activeContext.Viewport.InvalidateViewport();
        }

        /*
           NE: Sekme Değişimi Event Handler (OnTabChanged)
           NEDEN: Kullanıcı başka bir projeye tıkladığında _activeContext'i ve UI başlığını (Title) güncellemek için.
        */
        private void OnTabChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DocumentTabs.SelectedItem is TabItem tab && tab.Tag is CadDocumentContext ctx)
            {
                _activeContext = ctx;

                // UI Güncellemeleri
                Title = $"AfneyCAD - {ctx.ProjectName} [{(string.IsNullOrEmpty(ctx.FilePath) ? "Kaydedilmemiş" : ctx.FilePath)}]";

                // Undo/Redo butonlarını güncelle
                UpdateUndoLabels(); // _history artık ActiveContext.History'ye bakıyor

                // Navigator ve Info Panel'i güncelle (Context değiştiği için)
                // (ProjectNavigator code-behind kullanıyorsa orayı da bağlamalıyız, şimdilik dursun)

                // Viewport.InvalidateVisual(); // Gerek yok, zaten yeni viewport ekrana geldi.
            }
        }

        /*
           NE: Sekme Kapatma Event Handler (OnCloseTab_Click)
           NEDEN: Seçilen sekmeyi kapatmak, doküman listesinden kaldırmak ve eğer son sekme ise yeni boş bir doküman açmak için.
        */
        private void OnCloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TabItem tab)
            {
                var ctx = tab.Tag as CadDocumentContext;

                // Eğer değiştirilmişse sor (IsModified takibi henüz yok ama ekledim)
                // ...

                DocumentTabs.Items.Remove(tab);
                if (ctx != null) _documents.Remove(ctx);

                if (DocumentTabs.Items.Count == 0)
                {
                    // Son sekme kapandıysa -> Yeni boş aç veya uygulamayı kapat?
                    // AutoCAD mantığı: Boş gri ekran. Biz yeni boş açalım.
                    CreateNewDocument("Boş Proje");
                }
            }
        }

        // private void LoadInitialTestData() { ... } Removed

        #region -- KOMUTLAR (COMMANDS) --

        /*
           NE: Çizgi Komutu (OnLineCommand)
           NEDEN: Kullanıcı butona bastığında LineCommand'ı aktif viewport'a yüklemek için.
        */
        private void OnLineCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new Afney.Cad.Commands.BasicCommands.LineCommand(_database, _history.TransactionManager);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
        }

        /*
           NE: Daire Komutu (OnCircleCommand)
           NEDEN: Kullanıcı butona bastığında CircleCommand'ı aktif viewport'a yüklemek için.
        */
        private void OnCircleCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new Afney.Cad.Commands.BasicCommands.CircleCommand(_database, _history.TransactionManager);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);
            Viewport.SetActiveCommand(cmd);
        }

        /*
           NE: Boru Çizme Komutu (OnDrawPipeCommand)
           NEDEN: Mekanik tesisat borusu çizimini başlatmak ve Kernel/Settings entegrasyonunu sağlamak için.
        */
        private void OnDrawPipeCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new RoutePipeCommand(_database, _mechanicalKernel);
            SyncMechanicalSettings(cmd);

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnEntityPlaced += entity => _history.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, entity));
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); StatusText.Text = "Ready"; };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        /*
           NE: Duvara Vitrifiye Yerleştirme Komutu (OnPlaceFixtureOnWall)
           NEDEN: Lavabo, WC gibi cihazları duvar çizgileri üzerinde otomatik hizalayarak yerleştirmek için.
        */
        private void OnPlaceFixtureOnWall(object sender, RoutedEventArgs e)
        {
            var cmd = new Afney.Cad.Commands.MechanicalCommands.PlaceFixtureOnWallCommand(_database, _mechanicalKernel);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); StatusText.Text = "Ready"; };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        /*
           NE: Mekanik Ayarları Senkronize Et
           NEDEN: Toolbar üzerindeki Malzeme, Çap ve Sistem Tipi seçimlerini aktif çizim komutuna aktarmak için.
        */
        private void SyncMechanicalSettings(RoutePipeCommand? cmd = null)
        {
            if (Viewport == null) return;

            ICadCommand? target = cmd ?? (Viewport.GetType().GetField("_activeCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(Viewport) as ICadCommand);

            if (target is RoutePipeCommand pipeCmd)
            {
                string material = (MaterialCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "PVC";
                double size = double.Parse((PipeSizeCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "100");

                var systemTypeStr = (SystemTypeCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                Enum.TryParse<MechanicalSystemType>(systemTypeStr, out var sys);
                pipeCmd.SetSettings(size, sys);
            }
        }

        /*
           NE: Mekanik Ayar Değişimi Event Handler
           NEDEN: ComboBox'lar değiştirildiğinde anlık olarak aktif komutun ayarlarını güncellemek için.
        */
        private void OnMechanicalSettingsChanged(object sender, SelectionChangedEventArgs e)
        {
            // Başlangıçta henüz context yokken event tetiklenirse engelle
            if (_activeContext == null) return;

            SyncMechanicalSettings();
        }

        /*
           NE: Mimari Kat Hazırlama (WBlock) Komutu
           NEDEN: Ham bir mimari projeyi AfneyCAD standartlarına (ölçek, merkezleme) getirmek ve şaft/kat bazlı kaydetmek için.
        */
        /*
           NE: Mimari Kat Hazırlama (WBlock) Komutu
           NEDEN: Ham bir mimari projeyi AfneyCAD standartlarına (ölçek, merkezleme) getirmek ve şaft/kat bazlı kaydetmek için.
        */
        private void OnWBlockCommand(object sender, RoutedEventArgs e)
        {
            string projectPath = _mechanicalKernel.Metadata.ProjectName != null
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CALC", $"{_mechanicalKernel.Metadata.ProjectName}.bld")
                : AppDomain.CurrentDomain.BaseDirectory;

            if (!System.IO.Directory.Exists(projectPath)) System.IO.Directory.CreateDirectory(projectPath);

            // 1. Yeni Mimarileri Bloklama (WBlock) SihirbazÄ±nÄ± (Wizard) baÅŸlat
            // Dosya Dialog'u default aÃ§mak yerine kullanÄ±cÄ±nÄ±n adÄ±m 1'de kendi girmesini isteyelim
            var tempDefPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "YeniPlan.afney");
            var wizard = new Afney.Cad.Presentation.Dialogs.WBlockWizard(Viewport, tempDefPath);
            var cmd = new Afney.Cad.Commands.MechanicalCommands.ArchitecturalBlockCommand(_database, null!);
            wizard.Owner = this;

            // --- Event BaÄŸlantÄ±larÄ± ---

            // A) Diyalog -> Komut (KullanÄ±cÄ± butona bastÄ±)
            wizard.RequestPickPoint += () =>
            {
                Viewport.SetActiveCommand(cmd); // Komutu aktifleÅŸtir (Mouse dinlesin)
                Viewport.Focus(); // Ensure functionality
                Serilog.Log.Information("MAINWINDOW: RequestPickPoint. Viewport focused.");
                cmd.StartPickPoint();
            };

            wizard.RequestSelectObjects += () =>
            {
                Viewport.SetActiveCommand(cmd);
                cmd.StartSelection();
            };

            // B) Komut -> Diyalog (Ä°ÅŸlem bitti, diyaloÄŸu geri getir)
            cmd.OnPointPicked += () =>
            {
                Serilog.Log.Information("MAINWINDOW: OnPointPicked received from WBlockCommand.");
                // UI Thread check
                Dispatcher.Invoke(() =>
                {
                    wizard.SetBasePoint(cmd.BasePoint);
                    wizard.Show(); // Sihirbaz tekrar ekranda belirir
                    Serilog.Log.Information("MAINWINDOW: Wizard shown after point pick.");
                });
            };

            cmd.OnEntitiesSelected += () =>
            {
                cmd.SetSelectedEntities(Viewport.GetSelectedEntities());
                wizard.SetEntities(cmd.SelectedEntities);
                wizard.Show(); // Sihirbaz tekrar ekranda belirir
            };

            // Selection Changed (Global seÃ§im deÄŸiÅŸirse komut gÃ¼ncellensin)
            Viewport.SelectionChanged += (selection) =>
            {
                if (Viewport.ActiveCommand == cmd)
                {
                    cmd.SetSelectedEntities(selection);
                }
            };

            // C) Feedback -> Status Bar
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () =>
            {
                Viewport.SetActiveCommand(null);
                StatusText.Text = "WBlock HazÄ±r.";
            };

            // 3. BaÅŸlangÄ±Ã§ Durumu
            cmd.SetSelectedEntities(Viewport.GetSelectedEntities());
            wizard.SetEntities(cmd.SelectedEntities); // Sihirbaz ilk aÃ§Ä±ldÄ±ÄŸÄ±nda varsa Ã¶nceki seÃ§imleri de alsÄ±n

            // Sihirbaz Modeline geÃ§iyoruz
            if (wizard.ShowDialog() == true)
            {
                string finalPath = wizard.FinalPath;
                string floorName = wizard.FloorName;

                var entitiesToSave = wizard.SelectedEntities;
                var basePoint = wizard.BasePoint;

                // KayÄ±t Ä°ÅŸlemi (Logic ArchitecturalBlockCommand.FinalizeExport iÃ§inde)
                var cloned = entitiesToSave.Select(x => x.Clone()).ToList();
                var scaleService = new Afney.Cad.Mechanical.Services.ArchitecturalScaleService();
                var (_, factor) = scaleService.DetectScale(cloned);

                var moveMat = Matrix4x4.TranslationMatrix(-basePoint.X, -basePoint.Y, -basePoint.Z);
                var scaleMat = Matrix4x4.Scaling(factor, factor, factor);
                var final = scaleMat * moveMat;

                foreach (var ent in cloned) ent.Transform(final);

                var serializer = new Afney.Cad.Database.Persistence.CadSerializer();
                var json = serializer.Serialize(new Afney.Cad.Database.Persistence.ProjectData
                {
                    Entities = cloned,
                    Layers = _database.GetLayers().ToList()
                });

                cmd.FinalizeExport(floorName, System.IO.Path.GetDirectoryName(finalPath) ?? projectPath, json);
            }

            // Temizlik
            Viewport.SetActiveCommand(null);
        }

        /*
           NE: Blok Tanımlama Komutu
           NEDEN: Seçilen nesneleri bir araya getirerek bir sembol (Block Definition) oluşturmak için.
        */
        private void OnBlockCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new BlockCommand(_database, (bc) =>
            {
                var dialog = new BlockNameDialog();
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    bc.FinalizeBlock(dialog.BlockName);
                }
            });

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        /*
           NE: Blok Ekleme (INSERT) Komutu
           NEDEN: Kütüphanedeki sembolleri (Blokları) seçip çizime yerleştirmek için.
        */
        private void OnInsertCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new InsertCommand(_database, (ic) =>
            {
                var dialog = new BlockSelectionDialog(_database.GetBlocks());
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    ic.SetBlock(dialog.SelectedBlockName);
                }
                else
                {
                    ic.Cancel();
                }
            });

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        /*
            NE: Mahal Tanımlama Komutu (OnDefineMahalCommand)
            NEDEN: Menu Bar'dan veya kısayol ile mahal tanımlamak için.
            NASIL: OnSelectRoom ile aynı mantık - RoomTagDialog, Highlight, Fixture Detection
        */
        private void OnDefineMahalCommand(object sender, RoutedEventArgs e)
        {
            // OnSelectRoom ile aynı komut kullanılıyor (kod tekrarını önle)
            OnSelectRoom(sender, e);
        }

        /*
           NE: Mahal İnceleme Komutu
           NEDEN: Çizimdeki bir mahal üzerine tıklayarak içindeki cihazları ve alan detaylarını görmek için.
        */
        private void OnInspectMahalCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new MahalInspectCommand(_database, _mechanicalKernel, (mahal, fixtures) =>
            {
                var dialog = new MahalDetailsDialog(mahal); // Fixtures artık mahal içinden okunuyor
                dialog.Owner = this;
                dialog.ShowDialog();
            });

            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        /*
           NE: Tesisat Yeniden Hesapla (RecalculatePlumbing)
           NEDEN: Boru bağlantıları veya cihazlar değiştiğinde debi ve çapları güncellemek, çakışmaları kontrol etmek için.
        */
        private void OnRecalculatePlumbing(object sender, RoutedEventArgs e)
        {
            _mechanicalKernel.RecalculateProject(_database.GetAllEntities());
            _mechanicalKernel.ResolveAllClashes(_database.GetAllEntities()); // EKLENDİ (Suggestion 20)
            Viewport.InvalidateViewport();
            StatusText.Text = "Tesisat hesaplamaları ve otomatik çakışma giderme tamamlandı.";
        }

        /*
           NE: Kolon Şeması Üretme Komutu
           NEDEN: Bina katlarındaki boru hatlarını analiz ederek teknik bir kolon (Riser) şeması çizimi üretmek için.
        */
        private void OnRiserGenerateCommand(object sender, RoutedEventArgs e)
        {
            // Önce katların tanımlı olduğundan emin olalım (Engine için gerekli)
            if (!_mechanicalKernel.LevelManager.GetLevels().Any())
            {
                _mechanicalKernel.LevelManager.AddLevel(new MepLevel("Zemin Kat", 0, 3000));
                _mechanicalKernel.LevelManager.AddLevel(new MepLevel("1. Kat", 3000, 3000));
            }

            var cmd = new RiserGenerateCommand(_database, _mechanicalKernel); // Kernel bağımlılığı eklendi
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        /*
           NE: Akıllı Etiketleme Komutu (Smart Label)
           NEDEN: Çizimdeki boru hatlarının üzerine çap ve tipi belirten dinamik etiketler eklemek için.
        */
        /*
           NE: Akıllı Etiketleme (OnSmartLabelCommand)
           NEDEN: Boruların üzerine çap, sistem tipi ve akış yönü verilerini içeren dinamik metin etiketlerini otomatik olarak yerleştirmek için.
        */
        private void OnSmartLabelCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new SmartLabelCommand(_database);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        /*
           NE: Metraj (BOM) Listesi Komutu
           NEDEN: Projedeki tüm mekanik öğelerin (boru, fitting, vana vb.) malzeme listesini çıkarmak için.
        */
        private void OnBOMCommand(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Metraj listesini dışa aktarmak ister misiniz?\nEvet: HTML Raporu Al\nHayır: Ekranda Göster", "Metraj Raporu", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                var bomService = new BOMExportService();
                string html = bomService.GenerateHtmlReport(_database.GetAllEntities(), _activeContext?.ProjectName ?? "AfneyProject");
                
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"BOM_{DateTime.Now:yyyyMMdd_HHmm}.html");
                System.IO.File.WriteAllText(path, html);
                
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                StatusText.Text = $"Metraj raporu masaüstüne kaydedildi: {path}";
            }
            else if (result == MessageBoxResult.No)
            {
                var dialog = new BOMDialog(_database.GetAllEntities());
                dialog.Owner = this;
                dialog.ShowDialog();
            }
        }

        /*
           NE: IFC Export Komutu
           NEDEN: Projeyi BIM formatında dışa aktarmak için.
        */
        private void OnIfcExportCommand(object sender, RoutedEventArgs e)
        {
            try 
            {
                var service = new IfcExportService();
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Project_{DateTime.Now:yyyyMMdd_HHmm}.ifc");
                
                service.ExportToIfc(_database.GetAllEntities(), path);
                
                MessageBox.Show($"IFC dosyası başarıyla oluşturuldu:\n{path}", "BIM Export", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = "IFC dışa aktarımı tamamlandı.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}");
            }
        }

        /*
           NE: Lejant Üretme Komutu
           NEDEN: Projede kullanılan sembollerin anlamlarını içeren teknik bir tablo oluşturmak için.
        */
        private void OnLegendCommand(object sender, RoutedEventArgs e)
        {
            var cmd = new LegendGenerateCommand(_database);
            cmd.OnFeedback += msg => StatusText.Text = msg;
            cmd.OnCompleted += () => { Viewport.SetActiveCommand(null); };
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
        }

        /*
           NE: Bina Tanımlama (Define Building) Metodu
           NEDEN: Çok katlı projelerde kat yüksekliklerini tanımlamak, mimari dosyaları sekmelere bağlamak ve 3D bina montajı yapmak için.
        */
        /*
           NE: Katman ve Bina Özellikleri (OnDefineBuilding)
           NEDEN: Projenin kaç katlı olduğu, kat yükseklikleri ve ilgili mimari DWG dosyalarının koordinat eşleşmelerini tanımlamak için.
        */
        private void OnDefineBuilding(object sender, RoutedEventArgs e)
        {
            string projectPath = _mechanicalKernel.Metadata.ProjectName != null
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CALC", $"{_mechanicalKernel.Metadata.ProjectName}.bld")
                : AppDomain.CurrentDomain.BaseDirectory;

            if (!System.IO.Directory.Exists(projectPath)) System.IO.Directory.CreateDirectory(projectPath);

            var dialog = new Afney.Cad.Presentation.Dialogs.DefineBuildingDialog(projectPath);
            dialog.Owner = this;

            // Handle "Set Active" (Kullanılan Yap)
            dialog.OnLevelActivated += (filePath) =>
            {
                try
                {
                    if (System.IO.File.Exists(filePath))
                    {
                        _database.Clear();

                        var serializer = new Afney.Cad.Database.Persistence.CadSerializer();
                        string json = System.IO.File.ReadAllText(filePath);

                        var data = serializer.Deserialize(json);

                        if (data?.Entities != null)
                        {
                            var layers = data.Layers ?? new System.Collections.Generic.List<Afney.Cad.Domain.Tables.CadLayer>();
                            foreach (var layer in layers)
                            {
                                if (_database.GetLayer(layer.Name) == null) _database.AddLayer(layer);
                            }

                            foreach (var ent in data.Entities)
                            {
                                _database.AddEntity(ent);
                            }
                        }

                        Viewport.SetViewMode(false); // 2D Plan View
                        Viewport.InvalidateViewport();
                        Viewport.ZoomExtents();
                        StatusText.Text = $"Kat Yüklendi: {System.IO.Path.GetFileName(filePath)}";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kat yüklenirken hata: {ex.Message}");
                }
            };

            // Handle "Show 3D" (Tüm Binayı 3D Göster)
            dialog.OnShow3D += (levels) =>
            {
                try
                {
                    StatusText.Text = "Bina montajı yapılıyor (BIM Alignment)...";

                    var assemblyService = new Afney.Cad.Mechanical.Services.BuildingAssemblyService(_database, _mechanicalKernel);

                    var regs = levels.Select(l => new Afney.Cad.Mechanical.Services.LevelFileRegistration
                    {
                        FilePath = l.FilePath,
                        Elevation = l.Elevation,
                        LevelName = l.LevelName
                    });

                    assemblyService.AssembleBuilding(regs);

                    Viewport.SetViewMode(true); // Enable Isometric View
                    Viewport.InvalidateViewport();
                    Viewport.ZoomExtents();

                    // MÜHENDİSLİK GÜNCELLEMESİ: Katlar arası bağlantılardan sonra debi ve çapları tazele
                    _mechanicalKernel.RecalculateProject(_database.GetAllEntities());

                    StatusText.Text = "3D Bina Modeli ve Tesisat Ağı Oluşturuldu.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"3D Model oluşturulurken hata: {ex.Message}");
                }
            };

            dialog.ShowDialog();
        }

        /*
           NE: Komut Satırı KeyDown Handler
           NEDEN: AutoCAD benzeri hızlı komut girişlerini (L: Line, C: Circle, WBLOCK vb.) işlemek için.
        */
        private void CommandInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                string cmdText = CommandInput.Text.Trim().ToLower();
                CommandInput.Clear();

                switch (cmdText)
                {
                    case "wblock":
                        OnWBlockCommand(this, new RoutedEventArgs());
                        break;
                    case "l":
                    case "line":
                        OnLineCommand(this, new RoutedEventArgs());
                        break;
                    case "c":
                    case "circle":
                        OnCircleCommand(this, new RoutedEventArgs());
                        break;
                    case "p":
                    case "pipe":
                        OnDrawPipeCommand(this, new RoutedEventArgs());
                        break;
                    case "rec":
                        OnRecognizeArchitecture(this, new RoutedEventArgs());
                        break;
                    case "block":
                    case "b":
                        OnBlockCommand(this, new RoutedEventArgs());
                        break;
                    case "insert":
                    case "i":
                        OnInsertCommand(this, new RoutedEventArgs());
                        break;
                    case "mahal":
                    case "ma":
                        OnDefineMahalCommand(this, new RoutedEventArgs());
                        break;
                    case "mahalanaliz":
                    case "man":
                        OnInspectMahalCommand(this, new RoutedEventArgs());
                        break;
                    case "kolonsema":
                    case "ks":
                        OnRiserGenerateCommand(this, new RoutedEventArgs());
                        break;
                    case "etiket":
                    case "label":
                        OnSmartLabelCommand(this, new RoutedEventArgs());
                        break;
                    case "metraj":
                    case "bom":
                        OnBOMCommand(this, new RoutedEventArgs());
                        break;
                    case "lejant":
                    case "legana": // Alias
                    case "legend": // Alias
                    case "leg":
                        OnLegendCommand(this, new RoutedEventArgs());
                        break;
                    case "ifc":
                    case "export":
                    case "bim":
                        OnIfcExportCommand(this, new RoutedEventArgs());
                        break;
                    default:
                        StatusText.Text = $"Geçersiz Komut: {cmdText}";
                        break;
                }
            }
        }

        /*
           NE: Komut Çalıştır (ExecuteCommand)
           NEDEN: Kullanıcıdan gelen komut isteğini (Çizgi, Boru, Hesapla vb.) viewport üzerinden aktif hale getirerek çizim döngüsünü başlatmak için.
        */
        private void ExecuteCommand(string commandName)
        {
            if (ActiveContext?.Viewport == null) return;

            // Mevcut komutu iptal et
            ActiveContext.Viewport.CancelCurrentCommand();

            switch (commandName.ToUpper())
            {
                case "LINE":
                    ActiveContext.Viewport.SetCommand(new LineCommand(ActiveContext.Database, ActiveContext.History.TransactionManager));
                    break;
                case "PIPING":
                    ActiveContext.Viewport.SetCommand(new RoutePipeCommand(ActiveContext.Database, ActiveContext.MechanicalKernel));
                    break;
                case "MAHAL":
                    ActiveContext.Viewport.SetCommand(new SelectRoomCommand(ActiveContext.Database, (room) =>
                    {
                        // Oda seçildiğinde yapılacak işlem
                    }));
                    break;
                    // ... diğer komutlar
            }
        }

        #endregion

        #region -- GÖRÜNÜM KONTROLLERİ (VIEW) --

        /*
           NE: Zoom Extents (Ekrana Sığdır)
           NEDEN: Çizimdeki tüm nesnelerin ekrana tam olarak sığacak şekilde ölçeklenmesini ve ortalanmasını sağlar.
        */
        private void OnZoomExtents(object sender, RoutedEventArgs e) => Viewport.ZoomExtents();

        /*
           NE: Proje Gezgini Görünürlük Kontrolü
           NEDEN: Sol taraftaki proje ağacının gösterilip gizlenmesini yönetmek için.
        */
        private void OnToggleProjectNavigator(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && ProjectNavigatorPanel != null)
            {
                ProjectNavigatorPanel.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /*
           NE: Özellik/Zeka Paneli Görünürlük Kontrolü
           NEDEN: Sağ taraftaki nesne özellik ve analiz panelinin gösterilip gizlenmesini yönetmek için.
        */
        private void OnToggleIntelligencePanel(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && RightPanel != null)
            {
                RightPanel.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            }
        }


        /*
           NE: 2D Görünüm Modu
           NEDEN: Çizimi üst bakış (Plan) moduna getirir ve görsel stilleri 2D'ye uygun ayarlar.
        */
        private void OnToggle2DView(object sender, RoutedEventArgs e)
        {

            Viewport.SetViewMode(false);

            var view2DBtn = this.FindName("View2DBtn") as Button;
            var view3DBtn = this.FindName("View3DBtn") as Button;

            if (view2DBtn != null)
            {
                view2DBtn.Background = System.Windows.Media.Brushes.Cyan;
                view2DBtn.Foreground = System.Windows.Media.Brushes.Black;
            }
            if (view3DBtn != null)
            {
                view3DBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
                view3DBtn.Foreground = System.Windows.Media.Brushes.White;
            }
            StatusText.Text = "Çizim Modu: 2D Plan";
        }

        /*
           NE: 3D Görünüm Modu (İzometrik)
           NEDEN: Tesisatın 3. boyutunu görmek için izometrik izdüşüm ve 3D görselleştirme modunu aktif eder.
        */
        private void OnToggle3DView(object sender, RoutedEventArgs e)
        {

            Viewport.SetViewMode(true);

            var view2DBtn = this.FindName("View2DBtn") as Button;
            var view3DBtn = this.FindName("View3DBtn") as Button;

            if (view3DBtn != null)
            {
                view3DBtn.Background = System.Windows.Media.Brushes.Orange;
                view3DBtn.Foreground = System.Windows.Media.Brushes.Black;
            }
            if (view2DBtn != null)
            {
                view2DBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
                view2DBtn.Foreground = System.Windows.Media.Brushes.White;
            }
            StatusText.Text = "Çizim Modu: 3D İzometrik";
        }

        #endregion

        #region -- MÜHENDİSLİK KOMUTLARI (ENGINEERING) --

        /*
           NE: Sistemi Yeniden Hesapla (Mühendislik Analizi)
           NEDEN: Projedeki tüm boru hatlarını analiz ederek debi, hız ve çap optimizasyonunu TS 1258 standartlarında gerçekleştirmek için.
        */
        private void OnRecalculateSystem(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Hidrolik analiz yapılıyor (TS 1258)...";
                _mechanicalKernel.RecalculateProject(_database.GetAllEntities());

                // Viewport'u yenile (Çaplar değişmiş olabilir)
                Viewport.InvalidateVisual();

                MessageBox.Show("Tüm sistem analizi tamamlandı.\n" +
                                "- Akış yükleri (FU) hesaplandı.\n" +
                                "- Boru çapları otomatik optimize edildi.\n" +
                                "- Kritik hat basınç kayıpları güncellendi.",
                                "Mühendislik Analizi");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Analiz hatası: {ex.Message}");
            }
        }

        #endregion

        private void OnUndo(object sender, RoutedEventArgs e) => _history.Undo();
        private void OnRedo(object sender, RoutedEventArgs e) => _history.Redo();
        private void UpdateUndoLabels() { }

        /*
           NE: Yeni Proje Oluşturma
           NEDEN: Yeni bir bina projesi başlatmak, mimari dosyaları set etmek ve kalsifikasyon (BLD) yapısını kurmak için.
        */
        private void OnNewProject(object sender, RoutedEventArgs e)
        {
            var dialog = new NewProjectDialog();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // MDI Update: Veritabanını temizlemek yerine yeni sekme aç
                CreateNewDocument(dialog.ProjectName);

                // 2. Mimariyi Yükle
                string archPath = dialog.ArchitectPath;
                if (!string.IsNullOrEmpty(archPath))
                {
                    string targetPath = System.IO.Path.Combine(dialog.FinalProjectFolder, System.IO.Path.GetFileName(archPath));
                    LoadDwgInternal(targetPath);
                }

                StatusText.Text = $"Yeni Proje Sekmesi: {dialog.ProjectName}";
                Log.Information("Yeni proje sekmesi oluşturuldu: {ProjectName}", dialog.ProjectName);
            }
        }

        private void OnNewFile(object sender, RoutedEventArgs e)
        {
            // Standart "Yeni Dosya" -> "Drawing1.dwg" gibi
            string name = $"Drawing{_documents.Count + 1}";
            CreateNewDocument(name);
        }

        private void OnNewWindow(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Environment.ProcessPath!);
        }

        /*
           NE: Dosya Aç (DWG/DXF)
           NEDEN: Mevcut bir CAD dosyasını seçmek ve yeni bir sekmede yüklemek için.
        */
        private void OnOpenFile(object sender, RoutedEventArgs e)
        {
            Log.Information("Dosya açma diyaloğu açılıyor...");
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Autocad DWG (*.dwg)|*.dwg|Autocad DXF (*.dxf)|*.dxf|Tüm Dosyalar (*.*)|*.*",
                Title = "AfneyCAD - Proje Aç"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var info = new System.IO.FileInfo(openFileDialog.FileName);
                string name = System.IO.Path.GetFileNameWithoutExtension(info.Name);

                // Yeni sekme oluştur ve yükle
                CreateNewDocument(name, info.FullName);
                LoadDwgInternal(openFileDialog.FileName);
            }
        }

        /*
           NE: DWG İçeriği Yükleme (İç Metod)
           NEDEN: Seçilen dosyadaki CAD nesnelerini okur, uzak nesneleri filtreler ve veritabanına aktarır.
        */
        private void LoadDwgInternal(string filePath)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                Log.Information("[MAIN] Dosya yükleniyor: {Path}", filePath);

                // 1. IMPORT (CadImporter Kullan)
                var importer = new Afney.Cad.Infrastructure.Import.CadImporter();
                Log.Information("[MAIN] importer.Import({Path}) çağrılıyor...", filePath);
                var entities = importer.Import(filePath);
                Log.Information("[MAIN] importer.Import(...) başarıyla tamamlandı.");

                stopwatch.Stop();
                Log.Information("[MAIN] Dosya yüklendi. Nesne: {Count}, Süre: {Duration}ms", entities.Count, stopwatch.ElapsedMilliseconds);

                if (entities.Count == 0)
                {
                    Log.Warning("[MAIN] Dosya boş veya nesne okunamadı: {Path}", filePath);
                    MessageBox.Show("Dosyada okunabilir nesne bulunamadı.");
                    return;
                }

                // AKILLI OPTİMİZASYON VE YÜKLEME

                // ÖNCE ESKİLERİ TEMİZLE
                _database.Clear();
                Log.Information("Veritabanı temizlendi (önceki çizim silindi).");

                // Layerları Çıkar ve Ekle (Entity'lerden)
                // CadImporter sadece Entity listesi döndüğü için layerları entity özelliklerinden topluyoruz
                var distinctLayers = entities.Select(e => e.Layer).Distinct().ToList();
                foreach (var layerName in distinctLayers)
                {
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        _database.AddLayer(new Afney.Cad.Domain.Tables.CadLayer(layerName));
                    }
                }
                Log.Information("{Count} adet layer çıkarıldı ve eklendi.", distinctLayers.Count);

                // 2. Yoğunluk Analizi (Outlier Removal)
                var activeEntities = entities;

                var centers = entities.Select(e => e.GetBoundingBox().Center).ToList();
                if (centers.Count > 0)
                {
                    double avgX = centers.Average(c => c.X);
                    double avgY = centers.Average(c => c.Y);

                    // Basit mesafe kontrolü
                    // (Karmaşık LINQ hatalarından kaçınmak için döngü ile yapalım)
                    var filtered = new System.Collections.Generic.List<Afney.Cad.Domain.Abstractions.CadEntity>();
                    int removedCount = 0;
                    double thresholdSq = 500000.0 * 500000.0; // 500km kare

                    foreach (var ent in entities)
                    {
                        var c = ent.GetBoundingBox().Center;
                        double distSq = Math.Pow(c.X - avgX, 2) + Math.Pow(c.Y - avgY, 2);
                        if (distSq < thresholdSq)
                            filtered.Add(ent);
                        else
                            removedCount++;
                    }

                    if (removedCount > 0)
                    {
                        activeEntities = filtered;
                        Log.Information("Otomatik Temizleme: {Count} adet uzak nesne silindi.", removedCount);
                    }
                }

                // 3. İstatistik ve Temizleme
                var finalEntities = new System.Collections.Generic.List<Afney.Cad.Domain.Abstractions.CadEntity>();
                double totalLen = 0;
                int lineCount = 0;

                foreach (var ent in activeEntities)
                {
                    if (ent is Afney.Cad.Domain.Entities.Basic.LineEntity l)
                    {
                        // Z Flattening (Sıfırlama)
                        l.StartPoint = new Afney.Cad.Geometry.Primitives.Vector3D(l.StartPoint.X, l.StartPoint.Y, 0);
                        l.EndPoint = new Afney.Cad.Geometry.Primitives.Vector3D(l.EndPoint.X, l.EndPoint.Y, 0);

                        double dx = l.EndPoint.X - l.StartPoint.X;
                        double dy = l.EndPoint.Y - l.StartPoint.Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);

                        if (len > 0.01) // Çok kısa çizgileri at
                        {
                            totalLen += len;
                            lineCount++;
                            finalEntities.Add(l);
                        }
                    }
                    else
                    {
                        finalEntities.Add(ent);
                    }
                }

                activeEntities = finalEntities;
                double avgLen = lineCount > 0 ? totalLen / lineCount : 0;
                Log.Information("Yüklenen temiz nesne sayısı: {Count}. Ort çizgi uz.: {AvgLen:F2}", activeEntities.Count, avgLen);

                // 4. Veritabanına Ekle
                foreach (var ent in activeEntities)
                {
                    _database.AddEntity(ent);
                }

                // --- FAZ 11: Otomatik Mahal Analizi (Import Sonrası) ---
                var mahalService = new Afney.Cad.Application.Services.MahalExportService(_database);
                string mahalPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mahal.txt");
                try { mahalService.ExportMahalDataToJson(mahalPath); } catch (Exception ex) { Log.Warning("Otomatik Mahal Analizi tamamlanamadı: " + ex.Message); }

                Viewport.ZoomExtents();

                string statusMsg = $"Proje yüklendi: {activeEntities.Count} nesne.";
                StatusText.Text = statusMsg;
                MessageBox.Show($"{statusMsg}\nOrtalama çizgi: {avgLen:F2} birim.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Dosya yükleme hatası");
                MessageBox.Show($"Dosya yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            // Çalışma klasörünü aç (Şimdilik executable path)
            Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
        }

        /*
           NE: Mahal Analizi Dışa Aktar (Faz 11)
           NEDEN: DWG içindeki text'leri parçalayıp JSON formatında Mahal verisi üretmek.
        */
        private void OnExportMahalData(object sender, RoutedEventArgs e)
        {
            if (_database == null) return;
            try
            {
                var service = new Afney.Cad.Application.Services.MahalExportService(_database);
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mahal.txt");
                string result = service.ExportMahalDataToJson(path);
                MessageBox.Show(result, "Mahal Analizi (JSON)", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Klasörü otomatik aç
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Mahal analizi çıkarılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Kaydetme özelliği yakında eklenecektir.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnSaveAs(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Farklı Kaydet özelliği yakında eklenecektir.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnAutoSave(object sender, RoutedEventArgs e)
        {
            // Checkbox durumu otomatik değişir
            if (sender is MenuItem item)
            {
                StatusText.Text = item.IsChecked ? "Otomatik Kaydet: AÇIK" : "Otomatik Kaydet: KAPALI";
            }
        }

        private void OnCloseEditor(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Editör kapatıldı (Simülasyon).", "Bilgi");
        }

        private void OnCloseFolder(object sender, RoutedEventArgs e)
        {
            // İşlevsiz
        }

        /*
           NE: Uygulamayı Kapat
           NEDEN: Ana pencereyi kapatarak tüm oturumu ve kaynakları sonlandırmak için.
        */
        private void OnCloseWindow(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }


        /*
           NE: Kritik Hat Basınç Kaybı Analizi
           NEDEN: Tesisat ağındaki en uzak/kritik noktayı bularak toplam basınç kaybını ve pompa basma yüksekliğini hesaplamak için.
        */
        private void OnPressureDropCalc(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Önce akış hesaplarını tazele (Güncel veri için)
                OnCalculateFlowCommand(sender, e);

                // 2. Sistemdeki "Sinks" (Riser/Giriş) noktalarını bul
                var entities = _database.GetAllEntities().ToList();
                var pipes = entities.OfType<Afney.Cad.Mechanical.Entities.PipeEntity>().ToList();
                var sinks = pipes.Where(p =>
                    Math.Abs((p.EndPoint - p.StartPoint).Normalize().Z) > 0.8 // Dikey borular
                ).ToList();

                if (!sinks.Any())
                {
                    MessageBox.Show("Rapor üretilecek bir kolon (Riser) veya giriş noktası bulunamadı.", "Hata");
                    return;
                }

                // 3. Her Sink için rapor üret (Şimdilik ilkini gösterelim veya listeden seçtirelim)
                var pressureService = new Afney.Cad.Mechanical.Services.PressureDropService(
                    _mechanicalKernel.TopologyGraph,
                    _mechanicalKernel.ProjectSettings,
                    _database);

                // Örnek: İlk riser için kritik hat raporu
                var report = pressureService.GenerateReport(sinks.First().Id);

                if (report != null && report.Segments.Any())
                {
                    var reportWindow = new PressureDropReportWindow(report);
                    reportWindow.Owner = this;
                    reportWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Bu kolon için kritik hat tespit edilemedi. Bağlantıların tam olduğundan emin olun.", "Uyarı");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Kritik hat raporu oluşturulurken hata");
                MessageBox.Show($"Rapor oluşturulamadı: {ex.Message}", "Hata");
            }
        }

        /*
        METOD ADI: OnAutoPipeSizing
        AMACI: FINE SANI / DIN 1988'e göre tüm sistemin debi ve çaplarını tek tıkla güncellemek.
        */
        private void OnAutoPipeSizing(object sender, RoutedEventArgs e)
        {
            try
            {
                var progress = new MessageBoxResult();
                var flowService = new Afney.Cad.Mechanical.Services.FlowCalculationService(_mechanicalKernel.TopologyGraph);
                var mechEntities = _database.GetAllEntities().OfType<Afney.Cad.Mechanical.Entities.MechanicalEntity>().ToList();

                flowService.CalculateSystemFlow(mechEntities);
                flowService.AutoSizePipes(mechEntities);

                Viewport.InvalidateVisual();
                StatusText.Text = "AKILLI ÇAPLANDIRMA TAMAMLANDI: Borular standartlara göre güncellendi.";
                MessageBox.Show("Tesisat ağındaki tüm borular TS 1258 standartlarına göre otomatik olarak çaplandırıldı.", "Mühendislik Modu");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Çaplandırma hatası: {ex.Message}");
            }
        }

        /*
        METOD ADI: OnGenerateLegend
        AMACI: Projede kullanılan sembolleri içeren teknik lejantı veritabanına eklemek.
        */
        private void OnGenerateLegend(object sender, RoutedEventArgs e)
        {
            try
            {
                var legendService = new Afney.Cad.Mechanical.Services.LegendService(_database.GetAllEntities());
                // Tabloyu viewport'un merkezine veya sol altına yerleştir
                var pos = Viewport.GetCameraCenter();
                var legendTable = legendService.GenerateLegend(pos);

                foreach (var ent in legendTable)
                    _database.AddEntity(ent);
                Viewport.InvalidateVisual();
                StatusText.Text = "LEJANT OLUŞTURULDU: Kullanılan semboller tabloya eklendi.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lejant oluşturulurken hata: {ex.Message}");
            }
        }

        private void OnCalculateFlowCommand(object sender, RoutedEventArgs e)
        {
            OnAutoPipeSizing(sender, e);
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Viewport.SetActiveCommand(null);
                StatusText.Text = "Ready";
            }
        }

        // -- YENİ ÖZELLİKLER --

        /*
           NE: Kat Yöneticisi (Level Manager)
           NEDEN: Projedeki tüm katların yükseklik, kot ve isim bilgilerini yönetmek için.
        */
        private void OnLevelManager(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.LevelManagerDialog(_mechanicalKernel.LevelManager);
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void OnBuildingProperties(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.BuildingPropertiesDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        /*
           NE: Mahal Tanımlama (Oda Seçme)
           NEDEN: Kullanıcının tıkladığı noktadan sınırı bulmak, etiket basmak ve opsiyonel olarak vitrifiyeleri otomatik yerleştirmek için.
        */
        private void OnSelectRoom(object sender, RoutedEventArgs e)
        {
            // 1. Gelişmiş Mahal Tanımlama Komutunu Başlat (MahalDefineCommand)
            // MahalDefineCommand, akıllı ray casting ile oda sınırlarını bulur.
            var cmd = new Afney.Cad.Commands.MechanicalCommands.MahalDefineCommand(_database, (Afney.Cad.Mechanical.Entities.RoomEntity mahal) =>
            {
                try
                {
                    Serilog.Log.Information("🏠 MAHAL BULUNDU: {Name}, Alan: {Area:F2}, Fixture: {FixCount}",
                        mahal.RoomName, mahal.Area, mahal.Fixtures.Count);

                    // 2. Görselleştirme (Turuncu Highlight - Kullanıcı odayı görsün)
                    Viewport.ShowHighlight(mahal.BoundaryPoints);

                    // 3. Callback: Oda bulunduğunda RoomTagDialog aç
                    Serilog.Log.Information("📋 DIALOG AÇILIYOR...");
                    var dialog = new Dialogs.RoomTagDialog(mahal);
                    dialog.Owner = this;

                    Serilog.Log.Information("✅ Dialog oluşturuldu, ShowDialog() çağrılıyor...");
                    var dialogResult = dialog.ShowDialog();

                    Serilog.Log.Information("🔚 Dialog kapandı: Result = {Result}", dialogResult);

                    if (dialogResult == true)
                    {
                        Serilog.Log.Information("💾 Kullanıcı KAYDET'e bastı, mahal kaydediliyor...");

                        // Mahal veritabanına ekle (Transaction ile geri alınabilir)
                        _database.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, mahal));

                        // FINE SANI: Eğer mekanik kernel varsa oraya da ekle
                        if (_mechanicalKernel != null)
                            _mechanicalKernel.TopologyGraph.AddRoom(mahal);

                        // --- FINE SANI TARZI OTOMATİK YERLEŞİM (Auto Layout) ---
                        if (MessageBox.Show("Odaya uygun vitrifiyeler otomatik yerleştirilsin mi? (Lavabo, Klozet, Duş)", "Akıllı Tesisat", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            try
                            {
                                var layoutService = new Afney.Cad.Mechanical.Services.AutoLayoutService(
                                    _mechanicalKernel.TopologyGraph,
                                    _mechanicalKernel.ArchitecturalObstacles);

                                var fixtures = layoutService.AutoFurnishRoom(mahal);

                                if (fixtures.Count > 0)
                                {
                                    foreach (var fix in fixtures)
                                    {
                                        // TransactionManager ile ekle ki Undo yapılabilsin
                                        _database.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, fix));
                                        _mechanicalKernel.TopologyGraph.AddEntity(fix);
                                    }
                                    StatusText.Text = $"{fixtures.Count} parça vitrifiye yerleştirildi.";
                                    Log.Information("{Count} vitrifiye odaya yerleştirildi: {Room}", fixtures.Count, mahal.RoomName);
                                }
                                else
                                {
                                    MessageBox.Show("Bu odaya sığacak vitrifiye bulunamadı veya yerleşim başarısız.", "Bilgi");
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Otomatik yerleşim hatası: {ex.Message}", "Hata");
                                Log.Error(ex, "Otomatik yerleşim hatası");
                            }
                        }

                        StatusText.Text = $"MAHAL TANIMLANDI: {mahal.RoomName} ({mahal.TotalLoadUnits:F2} LU)";
                    }
                    else
                    {
                        Serilog.Log.Information("❌ Kullanıcı İPTAL etti");
                        StatusText.Text = "Mahal tanımlama iptal edildi.";
                    }

                    // 4. Görselleştirmeyi Temizle (İptal edilse bile temizlensin)
                    Viewport.ClearHighlight();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "🔥 KRITIK HATA: Dialog açılırken exception!");
                    MessageBox.Show($"Mahal tanımlama sırasında hata:\n{ex.Message}\n\n{ex.StackTrace}", "HATA", MessageBoxButton.OK, MessageBoxImage.Error);
                    Viewport.ClearHighlight();
                }
            });

            cmd.OnFeedback += (msg) => StatusText.Text = msg;
            cmd.OnCompleted += () => Viewport.SetActiveCommand(null);

            // 5. Komutu aktif et ve kullanıcıya bildir
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
            StatusText.Text = "MAHAL ANALİZİ: Odanın ortasına boş bir noktaya tıklayın...";
        }

        private void OnShowBOMReport(object sender, RoutedEventArgs e)
        {
            try
            {
                var res = MessageBox.Show("Metraj raporunu çizimin içine TABLO olarak eklemek ister misiniz?",
                                        "Mühendislik Raporu", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    // İnteraktif Tablo Ekleme
                    var cmd = new Afney.Cad.Commands.MechanicalCommands.InsertBOMTableCommand(_database);
                    Viewport.SetActiveCommand(cmd);
                    cmd.Start();
                    StatusText.Text = "Tablo yerleşimi için bir noktaya tıklayın...";
                }
                else if (res == MessageBoxResult.No)
                {
                    // Sadece Metin Raporu (Eski mantık ama optimize edildi)
                    var bomService = new Afney.Cad.Mechanical.Services.BillOfMaterialsService(_database);
                    var table = bomService.GenerateTable(Vector3D.Zero); // Sadece veri çekmek için
                                                                         // Not: GenerateTable'dan text üretme mantığı eklenebilir ama şimdilik tablo yeterli.
                    MessageBox.Show("Tablo modunu seçerek çizime ekleyebilirsiniz.", "Bilgi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor hatası: {ex.Message}", "Hata");
            }
        }

        private void OnSmartDetectRoomClick(object sender, RoutedEventArgs e)
        {
            // ...
            var cmd = new Afney.Cad.Commands.MechanicalCommands.DetectRoomCommand(_database, (room) =>
            {
                var dialog = new Dialogs.RoomTagDialog(room);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    _database.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, room));
                    _mechanicalKernel.TopologyGraph.AddRoom(room);

                    // --- YENİ: MEVCUT VİTRİFİYE TARAMASI (Room Definitions) ---
                    var roomDefService = new Afney.Cad.Mechanical.Services.RoomDefinitionService(_database);
                    var foundFixtures = roomDefService.IdentifyFixturesInRoom(room.BoundaryPoints);

                    if (foundFixtures.Any())
                    {
                        double totalFU = foundFixtures.Sum(f => f.LoadUnits);
                        // --- YENİ: MAHAL ETİKETİ (ROOM TAG) OLUŞTUR ---
                        // FINE SANI benzeri profesyonel görünüm için odaya kalıcı etiket basıyoruz.
                        var tagPos = room.GetBoundingBox().Center;
                        string tagText = $"{room.RoomName}\nA: {room.Area:F2} m²\nFU: {totalFU:F2}"; // Fix: RoomName
                        var roomTag = new Afney.Cad.Domain.Entities.Basic.TextEntity(tagText, tagPos, 200) // 20cm yazı
                        {
                            Color = 0xFF00FF00, // Yeşil
                            Layer = "Space_Tags"
                        };
                        _database.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, roomTag));

                        string msg = $"Mimari planda {foundFixtures.Count} adet vitrifiye (blok) tespit edildi.\n\n" +
                                     $"Toplam Yük Birimi (FU): {totalFU:F2}\n" +
                                     string.Join("\n", foundFixtures.GroupBy(f => f.FixtureType).Select(g => $"- {g.Count()}x {g.Key}")) +
                                     "\n\nBu cihazlar akıllı tesisat nesnelerine (MEP) dönüştürülsün mü?";

                        if (MessageBox.Show(msg, "Mahal Analizi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            var ops = new Afney.Cad.Database.Transactions.CompositeOperation("Convert Fixtures");
                            foreach (var fix in foundFixtures)
                            {
                                ops.Add(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, fix));
                                _mechanicalKernel.TopologyGraph.AddEntity(fix);
                            }
                            _history.TransactionManager.Submit(ops);
                            StatusText.Text = $"{foundFixtures.Count} cihaz tanıtıldı.";

                            // --- OTOMATİK BORULAMA (AUTO-PIPING) ---
                            if (MessageBox.Show("Atık su tesisat hatlarını (Kolektör) otomatik oluşturmak ister misiniz?",
                                "Otomatik Tesisat", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                var autoPipeService = new Afney.Cad.Mechanical.Services.AutoBranchingService(_database, _mechanicalKernel);
                                var pipes = autoPipeService.CreateSmartCollector(foundFixtures, room.GetBoundingBox().Center, Afney.Cad.Mechanical.Enums.MechanicalSystemType.WasteWater);

                                if (pipes.Any())
                                {
                                    var pipeOps = new Afney.Cad.Database.Transactions.CompositeOperation("Auto Piping");
                                    foreach (var p in pipes)
                                    {
                                        pipeOps.Add(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, p));

                                        // Fix: Cast CadEntity to MechanicalEntity
                                        if (p is Afney.Cad.Mechanical.Entities.MechanicalEntity mechEntity)
                                        {
                                            _mechanicalKernel.TopologyGraph.AddEntity(mechEntity);
                                        }
                                    }
                                    _history.TransactionManager.Submit(pipeOps);
                                    StatusText.Text += " + Tesisat boruları çizildi.";

                                    // --- SİSTEM HESABI VE ÇAPLANDIRMA ---
                                    if (MessageBox.Show("Borular çizildi. Hidrolik hesap yapılıp boru çapları optimize edilsin mi?",
                                        "Mühendislik Analizi", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                    {
                                        OnRecalculateSystem(this, new RoutedEventArgs());
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // --- FALLBACK: OTOMATİK YERLEŞİM (Eğer çizim boşsa) ---
                        if (MessageBox.Show("Oda boş görünüyor. Standart vitrifiyeler otomatik yerleştirilsin mi?",
                            "Akıllı Tesisat", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            try
                            {
                                var layoutService = new Afney.Cad.Mechanical.Services.AutoLayoutService(
                                    _mechanicalKernel.TopologyGraph,
                                    _mechanicalKernel.ArchitecturalObstacles);

                                var fixtures = layoutService.AutoFurnishRoom(room);

                                foreach (var fix in fixtures)
                                {
                                    _database.AddEntity(fix);
                                    _mechanicalKernel.TopologyGraph.AddEntity(fix);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Otomatik yerleşim hatası: {ex.Message}", "Hata");
                            }
                        }
                    }

                    Viewport.InvalidateVisual();
                }
            });

            // 3. Komutu aktif et
            Viewport.SetActiveCommand(cmd);
            cmd.Start();
            StatusText.Text = "Mahal sınırlarını belirlemek için kapalı bir alan bak noktasını tıklayın...";
        }

        /*
           NE: Cihazları Boruya Bağla (Auto Branching)
           NEDEN: Seçilen vitrifiyeleri (Lavabo vb.) en yakındaki ana boruya teknik kurallara göre otomatik bağlayan tesisat kollarını oluşturmak için.
        */
        private void OnAutoBranchingClick(object sender, RoutedEventArgs e)
        {
            // Check if we are already in the middle of a pick
            if (Viewport.GetActiveCommand() is Afney.Cad.Commands.BasicCommands.PickEntityCommand)
            {
                return;
            }

            // 1. Kullanıcıdan Cihazları Seçmesini İste
            StatusText.Text = "BAĞLANACAK CİHAZLARI SEÇİN (Önce Seçim)";

            var selected = Viewport.GetSelectedEntities();
            var fixtures = selected.OfType<Afney.Cad.Mechanical.Entities.SanitaryFixtureEntity>().ToList();

            if (!fixtures.Any())
            {
                MessageBox.Show("Lütfen önce bağlanacak cihazları (Vitrifiye) seçip komuta tıklayın.", "Uyarı");
                return;
            }

            // 2. Ana Boruyu Seçmesini İste
            StatusText.Text = "ANA BORUYU SEÇİN (Tıklayın)...";

            var pickCmd = new Afney.Cad.Commands.BasicCommands.PickEntityCommand(_database);
            pickCmd.OnEntityPicked += (ent) =>
            {
                if (ent is Afney.Cad.Mechanical.Entities.PipeEntity mainPipe)
                {
                    try
                    {
                        var service = new Afney.Cad.Mechanical.Services.AutoBranchingService(_database, _mechanicalKernel);
                        var newEntities = service.ConnectFixturesToPipe(fixtures, mainPipe);

                        if (newEntities.Any())
                        {
                            var ops = new Afney.Cad.Database.Transactions.CompositeOperation("Auto Branching");

                            // 1. Yeni Parçaları Ekle
                            foreach (var item in newEntities)
                            {
                                ops.Add(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, item));
                                if (item is Afney.Cad.Mechanical.Entities.MechanicalEntity mechItem)
                                {
                                    _mechanicalKernel.TopologyGraph.AddEntity(mechItem); // Update Graph
                                }
                            }

                            // 2. Eski Ana Boruyu Sil
                            ops.Add(new Afney.Cad.Database.Transactions.Operations.RemoveEntityOperation(_database, mainPipe));
                            _mechanicalKernel.TopologyGraph.RemoveEntity(mainPipe.Id); // Update Graph

                            _history.TransactionManager.Submit(ops);

                            StatusText.Text = $"{newEntities.Count} parça (boru/fittings) ile bağlantı sağlandı.";
                            Viewport.InvalidateVisual();
                        }
                        else
                        {
                            MessageBox.Show("Uygun bağlantı noktası veya sistem tipi eşleşmesi bulunamadı.\n(Örn: Lavabo sıcak su portu -> Soğuk su borusuna bağlanmaz)", "Bilgi");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Otomatik bağlantı hatası: {ex.Message}");
                    }
                    finally
                    {
                        Viewport.SetActiveCommand(null);
                    }
                }
                else
                {
                    MessageBox.Show("Lütfen bir BORU seçin.");
                    Viewport.SetActiveCommand(null);
                }
            };

            Viewport.SetActiveCommand(pickCmd);
            pickCmd.Start();
        }

        /*
           NE: Kolon Bağlantısı (Riser Connection)
           NEDEN: Kat içindeki yatay ana boru hatlarını dikey şaft/kolon (Riser) borularına bağlayarak tüm bina tesisatını birleştirmek için.
        */
        private void OnRiserConnection(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "KOLONA BAĞLANACAK YATAY BORUYU SEÇİN...";

            var pickCmd1 = new Afney.Cad.Commands.BasicCommands.PickEntityCommand(_database);
            pickCmd1.OnEntityPicked += (horizontalEnt) =>
            {
                if (horizontalEnt is Afney.Cad.Mechanical.Entities.PipeEntity horizontalPipe)
                {
                    // UI Thread senkronizasyonu için Dispatcher kullanalım
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        StatusText.Text = "ŞİMDİ DİKEY KOLONU (RISER) SEÇİN...";
                        var pickCmd2 = new Afney.Cad.Commands.BasicCommands.PickEntityCommand(_database);

                        pickCmd2.OnEntityPicked += (riserEnt) =>
                        {
                            if (riserEnt is Afney.Cad.Mechanical.Entities.PipeEntity riserPipe)
                            {
                                try
                                {
                                    var service = new Afney.Cad.Mechanical.Services.AutoBranchingService(_database);
                                    var result = service.ConnectToRiser(horizontalPipe, riserPipe);

                                    if (result.NewEntities.Any())
                                    {
                                        var ops = new Afney.Cad.Database.Transactions.CompositeOperation("Riser Connection");

                                        // 1. Yeni Parçaları Ekle
                                        foreach (var item in result.NewEntities)
                                        {
                                            ops.Add(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, item));
                                            if (item is Afney.Cad.Mechanical.Entities.MechanicalEntity mechItem)
                                            {
                                                _mechanicalKernel.TopologyGraph.AddEntity(mechItem); // Update Graph
                                            }
                                        }

                                        // 2. Eski Riser'ı Sil
                                        foreach (var oldItem in result.RemovedEntities)
                                        {
                                            ops.Add(new Afney.Cad.Database.Transactions.Operations.RemoveEntityOperation(_database, oldItem));
                                            _mechanicalKernel.TopologyGraph.RemoveEntity(oldItem.Id); // Update Graph
                                        }

                                        _history.TransactionManager.Submit(ops);
                                        StatusText.Text = "Kolon bağlantısı başarıyla yapıldı.";
                                        Viewport.InvalidateVisual();
                                    }
                                    else
                                    {
                                        StatusText.Text = "Uyarı: Bağlantı yapılamadı (Sistem tipi veya mesafe sorunu).";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Hata: {ex.Message}");
                                }
                                finally
                                {
                                    Viewport.SetActiveCommand(null);
                                }
                            }
                        };

                        Viewport.SetActiveCommand(pickCmd2);
                        pickCmd2.Start();
                    }));
                }
            };

            Viewport.SetActiveCommand(pickCmd1);
            pickCmd1.Start();
        }

        /*
           NE: Mimari Tanıma (Architectural Recognition)
           NEDEN: Ham çizim verisindeki katman isimlerini analiz ederek hangilerinin duvar, kapı veya pencere olduğunu semantik olarak ayırt etmek için.
        */
        private void OnRecognizeArchitecture(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("Mimari Tanıma İşlemi Başlatıldı...");
                var recognitionService = new Afney.Cad.Mechanical.Services.ArchitecturalRecognitionService(_database);
                var result = recognitionService.RecognizeObstacles();

                _mechanicalKernel.ArchitecturalObstacles.Clear();
                _mechanicalKernel.ArchitecturalObstacles.AddRange(result);

                StatusText.Text = $"MİMARİ ANALİZ: {result.Count} adet engel (Duvar/Kapı/Pencere) tanımlandı.";

                int walls = result.Count(o => o.Type == Afney.Cad.Mechanical.Models.ObstacleType.Wall);
                int doors = result.Count(o => o.Type == Afney.Cad.Mechanical.Models.ObstacleType.Door);
                int windows = result.Count(o => o.Type == Afney.Cad.Mechanical.Models.ObstacleType.Window);

                MessageBox.Show($"Mimari kat planı başarıyla analiz edildi.\n\n" +
                                $"Tespit Edilen Unsurlar:\n" +
                                $"-----------------------\n" +
                                $"- Duvarlar: {walls}\n" +
                                $"- Kapılar: {doors}\n" +
                                $"- Pencereler: {windows}\n" +
                                $"- Kolonlar: {result.Count - (walls + doors + windows)}\n\n" +
                                $"Bu veriler 'Akıllı Yerleşim' (AutoLayout) ve 'Otomatik Rotalama' sırasında engel olarak dikkate alınacaktır.",
                                "Mimari Tanıma (Sematik Analiz)", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Mimari tanıma hatası");
                MessageBox.Show($"Mimari analiz hatası: {ex.Message}", "Hata");
            }
        }


        /*
           NE: Metraj Tablosu Üret (Generate BOM)
           NEDEN: Projedeki tüm boru ve ekipmanların listesini çıkartarak ekrana tablo şeklinde basmak için.
        */
        private void OnGenerateBOM(object sender, RoutedEventArgs e)
        {
            try
            {
                var bomService = new Afney.Cad.Mechanical.Services.BillOfMaterialsService(_database);
                var tablePos = Viewport.GetCameraCenter();
                var table = bomService.GenerateTable(tablePos);

                _history.TransactionManager.Submit(new Afney.Cad.Database.Transactions.Operations.AddEntityOperation(_database, table));

                StatusText.Text = "Metraj ve Malzeme Listesi (BOM) ekranın ortasına tablo olarak eklendi.";
                Viewport.InvalidateVisual();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BOM hatası: {ex.Message}");
            }
        }

        /*
           NE: Detaylı Hidrolik Hesap Raporu Üret
           NEDEN: Sistemdeki tüm boru segmentlerinin hız, debi, yük birimi ve sürtünme kaybı bilgilerini içeren HTML tablosu oluşturup mühendise sunmak için. (Faz 7)
        */
        private void OnGenerateHydraulicReport(object sender, RoutedEventArgs e)
        {
            try
            {
                var pipes = _database.GetAllEntities().OfType<Afney.Cad.Mechanical.Entities.PipeEntity>().ToList();
                if (!pipes.Any())
                {
                    MessageBox.Show("Raporlanacak boru bulunamadı. Lütfen önce tesisatı çizin.", "Uyarı");
                    return;
                }

                // Ön hesaplamaları tetikleyelim
                OnCalculateFlowCommand(sender, e);
                var pressureService = new Afney.Cad.Mechanical.Services.PressureDropService(_mechanicalKernel.TopologyGraph, _mechanicalKernel.ProjectSettings, _database);
                
                string projectName = "Aktif_Proje";
                var reportService = new Afney.Cad.Mechanical.Services.HydraulicReportService(pressureService);
                string htmlContent = reportService.GenerateHtmlReport(pipes, projectName);

                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HydraulicReport_" + Guid.NewGuid().ToString() + ".html");
                System.IO.File.WriteAllText(tempPath, htmlContent, System.Text.Encoding.UTF8);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });

                StatusText.Text = "Hidrolik analiz raporu oluşturuldu ve tarayıcıda açıldı.";
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Hidrolik Rapor Hatası: {ex.Message}", "Hata");
            }
        }

        /*
           NE: Pompa ve Hidrofor Seçimi
           NEDEN: Kritik hattaki toplam basınç kaybını ve debiyi analiz ederek veritabanındaki uygun pompa modellerini önermek için.
        */
        private void OnPumpSelection(object sender, RoutedEventArgs e)
        {
            try
            {
                var pipes = _database.GetAllEntities().OfType<PipeEntity>().ToList();
                if (!pipes.Any())
                {
                    MessageBox.Show("Sistemde boru bulunamadı. Lütfen önce tesisatı çizin ve akış analizi yapın.", "Uyarı");
                    return;
                }

                // 1. Akış hesaplarını tazele
                OnCalculateFlowCommand(sender, e);

                // 2. Kritik Hattı Bul (En yüksek debili borudan başlayarak veya Sink üzerinden)
                double maxFlow = pipes.Max(p => p.FlowRate);

                // Tahmini Head yerine gerçek hesap yapmaya çalış
                double requiredHead = 6.0; // Default fallback
                var pressureService = new Afney.Cad.Mechanical.Services.PressureDropService(
                    _mechanicalKernel.TopologyGraph, _mechanicalKernel.ProjectSettings, _database);

                // Riser/Sink bul
                var sink = pipes.OrderByDescending(p => p.FlowRate).FirstOrDefault();
                if (sink != null)
                {
                    var report = pressureService.GenerateReport(sink.Id);
                    if (report != null)
                    {
                        requiredHead = report.TotalPressureRequired;
                    }
                }

                var pumpService = new Afney.Cad.Mechanical.Services.PumpSelectionService();
                var recommendations = pumpService.RecommendPumps(maxFlow, requiredHead);

                if (recommendations.Any())
                {
                    string msg = $"HESAPLANAN SİSTEM İHTİYACI:\n" +
                                 $"---------------------------\n" +
                                 $"Maksimum Debi (Q): {maxFlow:F2} m³/h\n" +
                                 $"Gerekli Basınç (Hm): {requiredHead:F2} mSS (Kritik Hat)\n\n" +
                                 $"MÜHENDİSLİK ÖNERİLERİ (Uygun Pompalar):\n";

                    foreach (var p in recommendations)
                    {
                        msg += $"> {p.Brand} {p.ModelName} (Güç: {p.Power}, Verim: %{p.Efficiency * 100:F0})\n";
                    }

                    MessageBox.Show(msg, "Pompa ve Hidrofor Seçim Modülü", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Q={maxFlow:F2}, Hm={requiredHead:F2} değerlerine uygun pompa bulunamadı.", "Uyarı");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Seçim modülü hatası: {ex.Message}");
            }
        }

        /*
           NE: Keşif ve Teknik Şartname Analizi (Auto-Spec)
           NEDEN: Projedeki tüm malzemeleri kodlarıyla birlikte listeleyen metraj raporu ve teknik şartname metni oluşturmak için.
        */
        private void OnAnalyzeSpecClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var specService = new Afney.Cad.Mechanical.Services.AutoSpecService(_database.GetAllEntities());
                var bomItems = specService.GenerateBoMReport();
                var specText = specService.GenerateSpecificationText();

                string report = "KEŞİF ÖZETİ (PROJE METRAJI)\n";
                report += "---------------------------------\n";
                foreach (var item in bomItems)
                {
                    report += $"[{item.Code}] {item.Description}: {item.Quantity:F2} {item.Unit}\n";
                }

                report += "\n\nTEKNİK ŞARTNAME TASLAĞI (ÖN İZLEME)\n";
                report += "---------------------------------\n";
                report += specText.Substring(0, Math.Min(300, specText.Length)) + "...";

                MessageBox.Show(report, "AfneyCAD - Mühendislik Raporu (Auto-Spec)", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rapor üretilirken hata oluştu: {ex.Message}");
            }
        }

        /*
           NE: İzometrik Şemayı Göster
           NEDEN: Tesisat ağını 30-30 kuralına göre izometrik izdüşümle görselleştirerek Live ISO-Sync durumunu kontrol etmek için.
        */
        private void OnShowIsometricScheme(object sender, RoutedEventArgs e)
        {
            try
            {
                var isoEntities = _mechanicalKernel.IsoSync.GenerateIsometricScheme();

                // Gerçek projede bu nesneler ayrı bir Viewport veya Layer'da çizilir.
                // Şimdilik sadece başarılı projeksiyonu raporluyoruz.
                MessageBox.Show(
                    $"{isoEntities.Count} adet tesisat bileşeni 30-30 projeksiyon kuralına göre izometrik düzleme düşürüldü.\n\n" +
                    $"Sistem: Temsili İzometrik Şema Verisi Hazır.",
                    "AfneyCAD Live ISO-Sync",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzometrik şema hatası: {ex.Message}");
            }
        }
    }
}
