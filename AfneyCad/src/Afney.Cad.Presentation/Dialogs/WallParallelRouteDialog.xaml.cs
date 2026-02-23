using System;
using System.Collections.Generic;
using System.Windows;
using Afney.Cad.Database.Core;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Presentation.Dialogs
{
    public partial class WallParallelRouteDialog : Window
    {
        private readonly CadDatabase _database;

        public WallParallelRouteDialog(CadDatabase database)
        {
            InitializeComponent();
            _database = database;
        }

        private void Route_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double offset = double.Parse(OffsetInput.Text);
                double diameter = double.Parse(DiameterInput.Text);

                var service = new WallParallelRoutingService(_database) { DefaultOffset = offset, DefaultDiameter = diameter };

                // Demo duvar segmenti
                var walls = new List<WallParallelRoutingService.WallSegment>
                {
                    new() { Start = new Vector3D(0, 0, 0), End = new Vector3D(5000, 0, 0), Thickness = 200 },
                    new() { Start = new Vector3D(5000, 0, 0), End = new Vector3D(5000, 4000, 0), Thickness = 200 }
                };

                var result = service.RouteAlongWalls(walls, offset, diameter);
                foreach (var p in result.Pipes) _database.AddEntity(p);
                foreach (var el in result.Elbows) _database.AddEntity(el);

                StatusText.Text = $"Sonuç: {result.Pipes.Count} boru | {result.Elbows.Count} dirsek | {result.TotalLength / 1000.0:F2} m";
                DialogResult = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Hata: {ex.Message}";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
