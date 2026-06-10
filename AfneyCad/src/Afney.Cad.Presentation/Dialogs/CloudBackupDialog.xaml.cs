using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Afney.Cad.Presentation.Services;

namespace Afney.Cad.Presentation.Dialogs;

public partial class CloudBackupDialog
{
    private readonly CloudBackupService _svc;
    private readonly string _projectName;
    private readonly string _sourceFile;

    public CloudBackupDialog(CloudBackupService service, string projectName, string sourceFile)
    {
        InitializeComponent();
        _svc         = service;
        _projectName = projectName;
        _sourceFile  = sourceFile;

        TxtBackupDir.Text = _svc.BackupDirectory;
        ChkAutoBackup.IsChecked = _svc.AutoEnabled;
        UpdateAutoStatus();
        RefreshGrid();
    }

    private void BrowseDir_Click(object sender, RoutedEventArgs e)
    {
        // WPF'de klasör seçimi için OpenFileDialog trick kullanılır
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title            = "Yedekleme hedef klasörünü seçin — Bu Klasörü Seç düğmesine basın",
            InitialDirectory = Directory.Exists(_svc.BackupDirectory) ? _svc.BackupDirectory
                                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            CheckFileExists  = false,
            CheckPathExists  = true,
            FileName         = "Bu Klasörü Seç",
            Filter           = "Klasör|*.",
            ValidateNames    = false
        };
        if (dlg.ShowDialog() == true)
        {
            string folder = System.IO.Path.GetDirectoryName(dlg.FileName) ?? dlg.FileName;
            _svc.SetBackupDirectory(folder);
            TxtBackupDir.Text = folder;
            RefreshGrid();
        }
    }

    private void AutoBackup_Changed(object sender, RoutedEventArgs e)
    {
        if (ChkAutoBackup.IsChecked == true)
        {
            int mins = GetIntervalMinutes();
            _svc.StartAuto(_sourceFile, _projectName, mins);
        }
        else
        {
            _svc.StopAuto();
        }
        UpdateAutoStatus();
    }

    private void Interval_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_svc.AutoEnabled)
        {
            _svc.StartAuto(_sourceFile, _projectName, GetIntervalMinutes());
            UpdateAutoStatus();
        }
    }

    private async void BackupNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dest = await _svc.BackupAsync(_sourceFile, _projectName);
            TxtLastBackup.Text = $"Son yedek: {Path.GetFileName(dest)} ({DateTime.Now:HH:mm})";
            RefreshGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yedekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshList_Click(object sender, RoutedEventArgs e) => RefreshGrid();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_svc.BackupDirectory))
            Process.Start("explorer.exe", _svc.BackupDirectory);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshGrid() => BackupGrid.ItemsSource = _svc.ListBackups(_projectName);

    private void UpdateAutoStatus()
    {
        TxtAutoStatus.Text = _svc.AutoEnabled
            ? $"Durum: Aktif — her {_svc.AutoIntervalMin} dakikada bir yedekleniyor → {_svc.BackupDirectory}"
            : "Durum: Devre dışı";
    }

    private int GetIntervalMinutes() => CmbInterval.SelectedIndex switch
    {
        0 => 5,
        2 => 30,
        3 => 60,
        _ => 15
    };
}
