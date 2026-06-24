using System.Windows;
using Afney.Cad.Database.Core;

namespace Afney.Cad.Presentation.Dialogs;

public partial class ProjectInfoDialog : Window
{
    public ProjectInfoDialog(CadDatabase database, string? filePath)
    {
        InitializeComponent();

        TxtProjeName.Text = System.IO.Path.GetFileNameWithoutExtension(filePath ?? "Yeni Proje");
        TxtFilePath.Text = filePath ?? "(Kaydedilmedi)";
        TxtCreatedDate.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        TxtModifiedDate.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        var entities = database.GetAllEntities().ToList();
        TxtEntityCount.Text = entities.Count.ToString("N0");
        TxtLayerCount.Text = database.GetLayers().Count().ToString();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
