using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Enums;

namespace Afney.Cad.Presentation.Dialogs;

/*
    NE: Mahal Tanımla (Room Tag Dialog)
    NEDEN: Mimari plandan otomatik bulunan kapalı alanın özelliklerini belirlemek için.
*/
public partial class RoomTagDialog : Window
{
    private readonly RoomEntity _room;
    public Dictionary<string, RoomType> RoomTypes { get; } = new Dictionary<string, RoomType>();

    public RoomTagDialog(RoomEntity room)
    {
        InitializeComponent();
        _room = room;

        // Initialize Room Types
        RoomTypes["Standart Oda (Yatak/Oturma)"] = RoomType.StandardRoom;
        RoomTypes["Mutfak"] = RoomType.Kitchen;
        RoomTypes["Banyo (Islak Hacim)"] = RoomType.Bathroom;
        RoomTypes["Tuvalet (WC)"] = RoomType.Toilet;
        RoomTypes["Teknik Hacim"] = RoomType.UtilityRoom;

        // Populate Controls
        RoomNameText.Text = _room.RoomName;
        RoomTypeCombo.ItemsSource = RoomTypes;
        if (RoomTypes.ContainsValue(_room.Type))
            RoomTypeCombo.SelectedValue = _room.Type;
        else
            RoomTypeCombo.SelectedIndex = 0;

        AreaText.Text = $"{_room.Area:F2} m²";
        
        // Tespit edilen cihazları listele
        DetectedFixturesList.ItemsSource = _room.Fixtures;

        // DataContext
        DataContext = this;
    }

    private void RoomType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_room != null && RoomTypeCombo.SelectedValue is RoomType type)
        {
            _room.Type = type;
        }
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        // Kaydederken değerleri güncelle
        _room.RoomName = RoomNameText.Text;
        if (RoomTypeCombo.SelectedValue is RoomType type)
            _room.Type = type;

        DialogResult = true; // Pencerenin başarılı kapandığını belirt
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
