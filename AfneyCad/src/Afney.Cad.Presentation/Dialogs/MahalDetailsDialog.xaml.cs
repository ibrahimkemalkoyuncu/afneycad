using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Afney.Cad.Mechanical.Entities;

namespace Afney.Cad.Presentation.Dialogs;

public partial class MahalDetailsDialog : Window
{
    // Constructor artık sadece MahalEntity alıyor
    public MahalDetailsDialog(MahalEntity mahal)
    {
        InitializeComponent();
        
        // Mahal bilgilerini göster (Alias kullanımı yerine güvenli property'ler)
        MahalTitle.Text = mahal.MahalName.ToUpper();
        MahalSubTitle.Text = $"{mahal.MahalType} - {mahal.Area:F2} m² - {mahal.Perimeter:F2} m Çevre";
        
        // Vitrifiye listesini mahal içinden al
        FixtureGrid.ItemsSource = mahal.Fixtures;
        
        // Toplam Yük Birimi (LU) Gösterimi
        TotalFUText.Text = mahal.TotalLoadUnits.ToString("F2");
    }
}
