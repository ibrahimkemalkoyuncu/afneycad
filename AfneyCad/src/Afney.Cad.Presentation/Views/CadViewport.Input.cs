using Afney.Cad.Application.Services;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Render.Engines;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Input;
using Serilog;
using Afney.Cad.Mechanical.Entities; // Eklendi

namespace Afney.Cad.Presentation.Views;

    /*
       NE: CAD Görüntüleyici (Viewport)
       NEDEN: 2B ve 3B Çizimlerin, Mühendislik donanımlarının SkiaSharp kütüphanesi kullanılarak yüksek performansla ekranda gösterilmesi.
    */
    public partial class CadViewport : UserControl, IDisposable
    {

        /*
           NE: Fare Basılma Olayı (MouseDown)
           NEDEN: Farenin hangi tuşuna basıldığına göre (Sol: Seçim/Çizim, Orta: Pan, Sağ: İptal) ilgili işlemi başlatmak için.
        */
        private void CadCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePosition = e.GetPosition(CadCanvas);
            CadCanvas.Focus();

            if (e.ChangedButton == MouseButton.Middle)
            {
                if (e.ClickCount == 2) { ZoomExtents(); return; }
                _isPanning = true;
                CadCanvas.CaptureMouse();
                CadCanvas.Cursor = System.Windows.Input.Cursors.Hand;
                return;
            }

            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2 && _activeCommand == null
                && _database != null && _lastMouseWorldPos.HasValue)
            {
                // Çift tıkla → Entity Properties (QuadTree ile bölgesel sorgu — bkz. hover hit-test notu)
                double ht = 12.0 / Math.Max(0.001, _zoom);
                var wp = _lastMouseWorldPos.Value;
                var queryBox = new CadBoundingBox(
                    new Vector3D(wp.X - ht, wp.Y - ht, -1e9),
                    new Vector3D(wp.X + ht, wp.Y + ht, 1e9));

                CadEntity? bestEnt = null;
                double bestDist = ht;
                foreach (var ent in _database.QueryEntities(queryBox))
                {
                    if (HiddenLayers.Contains(ent.Layer)) continue;
                    double dist = ent.DistanceTo(wp);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestEnt = ent;
                    }
                }

                if (bestEnt != null)
                {
                    EntityDoubleClicked?.Invoke(bestEnt);
                    e.Handled = true;
                    return;
                }
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                // MÜHENDİSLİK: Eğer BlockCommand obje seçimi bekliyorsa (step = 2) OnPointerPressed göndermek yerine
                // doğrudan Viewport'un normal seçim kutusu (selection box) mantığına geçiş yap.
                bool isBmakeSelecting = _activeCommand is Afney.Cad.Commands.BasicCommands.BlockCommand bc && bc.IsSelectingObjects;

                if (_activeCommand != null && _lastMouseWorldPos.HasValue && !isBmakeSelecting)
                    _activeCommand.OnPointerPressed(_lastMouseWorldPos.Value);
                else 
                { 
                    // MÜHENDİSLİK: Grip point üzerine tıklandı mı diye kontrol et (Stretch işlemi başlangıcı)
                    if (_selectionManager != null && _selectionManager.SelectedCount > 0 && _lastMouseWorldPos.HasValue)
                    {
                        var worldPos = _lastMouseWorldPos.Value;
                        double selectionThreshold = 10.0 / Math.Max(0.001, _zoom); // Ekranda yaklaşık 10px mesafe töleransı
                        
                        foreach (var entity in _selectionManager.GetSelectedEntities())
                        {
                            var grips = entity.GetGripPoints().ToList();
                            for (int i = 0; i < grips.Count; i++)
                            {
                                if (grips[i].DistanceTo(worldPos) < selectionThreshold)
                                {
                                    _activeGripEntity = entity;
                                    _activeGripIndex = i;
                                    _isStretching = true;
                                    CadCanvas.CaptureMouse();
                                    return; // Seçim kutusu oluşturma
                                }
                            }
                        }
                    }
                    
                    _isSelecting = true; 
                    _selectionStartPoint = _lastMousePosition; 
                    _selectionCurrentPoint = _lastMousePosition; 
                    CadCanvas.CaptureMouse(); 
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                /*
                   MÜHENDİSLİK: Komut aktifken sağ tık artık OTOMATİK onaylamıyor.
                   NEDEN (kullanıcı isteği): Sağ tık işlemi sessizce bitirmek yerine, bir
                   context menü açılmalı ve en üstte "Tamam" seçeneği olmalı — kullanıcı
                   bunu tıklayarak işlemi bilinçli şekilde tamamlar (AutoCAD'in sağ-tık
                   menüsündeki "Enter" davranışıyla tutarlı). Menü içeriği
                   `CadCanvas_ContextMenuOpening`'de `_activeCommand != null` durumuna göre
                   dinamik olarak "Tamam"/"İptal" içerecek şekilde kuruluyor — burada hiçbir
                   şey yapmadan MouseUp+ContextMenuOpening akışının doğal şekilde çalışmasına
                   izin veriliyor.
                */
                _rightClickCanceledCommand = false;
            }
            InvalidateViewport();
        }

        /*
           NE: Fare Hareket Olayı (MouseMove)
           NEDEN: Farenin pozisyonuna göre koordinatları güncellemek, Snap noktalarını yakalamak ve Pan/Seçim kutusunu güncellemek için.
        */
        private void CadCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var currentPos = e.GetPosition(CadCanvas);
            var worldPos = ScreenToWorld(currentPos);
            _lastMouseWorldPos = worldPos;

            /*
               MÜHENDİSLİK: OSNAP hesaplaması ARTIK grip sürükleme (Stretch) bloğundan ÖNCE yapılıyor.
               NEDEN: Önceden bu hesap fonksiyonun sonunda yapılıyordu — grip her zaman ham (snapsiz)
                      fare konumuyla taşınıyordu, çünkü _lastMouseWorldPos bir sonraki çağrıda hemen
                      worldPos ile eziliyordu (satır 950). Yani hesaplanan snap noktası hiçbir zaman
                      grip'e uygulanmıyordu. Artık snap önce hesaplanıp _lastMouseWorldPos'a yazılıyor,
                      grip sürükleme onu doğrudan kullanıyor.
            */
            if (_snapEngine != null)
                _activeSnap = _snapEngine.FindSnapPoint(worldPos, _zoom, _activeCommand?.ActivePoint);

            if (_activeSnap.HasValue)
                _lastMouseWorldPos = _activeSnap.Value.Position;

            if (_isPanning)
            {
                var delta = currentPos - _lastMousePosition;
                // Pan hızı: 1:1 pixel takip — AutoCAD standardı (zoom bağımsız).
                _offset = new Vector3D(_offset.X + delta.X, _offset.Y + delta.Y, 0);
                _targetOffset = _offset;
                _lastMousePosition = currentPos;
                InvalidateViewport();
            }
            else if (_isSelecting)
            {
                _selectionCurrentPoint = currentPos;
            }
            
            // Grip Stretching Aktifse nesneyi hareket ettir
            else if (_isStretching && _activeGripEntity != null && _activeGripIndex.HasValue && _lastMouseWorldPos.HasValue)
            {
                var oldPos = _activeGripEntity.GetGripPoints().ElementAt(_activeGripIndex.Value);
                var newPos = _lastMouseWorldPos.Value;
                var delta = newPos - oldPos;

                _activeGripEntity.MoveGripPointAt(_activeGripIndex.Value, newPos);

                // MÜHENDİSLİK: Faz 23 - Stretch (Bağlı Elemanların Otomatik Sündürülmesi)
                // Eğer borunun bir ucu çekiliyorsa, ucundaki Dirsek/T-Parçası da hareket etmeli,
                // Ve o Dirsek/T-Parçasına bağlı diğer boruların DA o ucu sündürülmeli!
                if (_activeGripEntity is PipeEntity pipe && (_activeGripIndex == 0 || _activeGripIndex == 1))
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    var fieldInfo = mainWindow?.GetType().GetField("_mechanicalKernel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                   ?? mainWindow?.GetType().GetField("MechanicalKernel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                   
                    var kernel = fieldInfo?.GetValue(mainWindow);
                    var graph = kernel?.GetType().GetProperty("TopologyGraph")?.GetValue(kernel) as Afney.Cad.Mechanical.Engine.MechanicalTopologyGraph;

                    if (graph == null)
                    {
                        // Fallback to searching Database manually if graph is null
                    }
                    else
                    {
                        var node = graph.GetNode(pipe.Id);
                        if (node != null)
                        {
                            var portName = _activeGripIndex == 0 ? "Start" : "End";
                            var port = node.Ports.FirstOrDefault(p => p.Name == portName);
                            if (port != null && port.IsConnected && port.ConnectedEntityId.HasValue)
                            {
                                var connectedEntityId = port.ConnectedEntityId.Value;
                                var connectedEntity = _database?.GetEntity(connectedEntityId);

                                if (connectedEntity != null)
                                {
                                    // Dirseği veya T-Parçasını taşı (Tüm nesne kayar)
                                    connectedEntity.Move(delta);

                                    // Dirsek/T-Parçasının DİĞER portlarına bağlı boruları bul ve onların ilgili uçlarını esnet
                                    var connectedNode = graph.GetNode(connectedEntityId);
                                    if (connectedNode != null)
                                    {
                                        foreach (var cPort in connectedNode.Ports)
                                        {
                                            if (cPort.IsConnected && cPort.ConnectedEntityId.HasValue && cPort.ConnectedEntityId.Value != pipe.Id)
                                            {
                                                var otherPipe = _database?.GetEntity(cPort.ConnectedEntityId.Value) as PipeEntity;
                                                if (otherPipe != null)
                                                {
                                                    // Hangi ucu bu port'a bağlı?
                                                    var otherNode = graph.GetNode(otherPipe.Id);
                                                    if (otherNode != null)
                                                    {
                                                        var otherPort = otherNode.Ports.FirstOrDefault(p => p.ConnectedEntityId == connectedEntityId);
                                                        if (otherPort != null)
                                                        {
                                                            if (otherPort.Name == "Start")
                                                                otherPipe.MoveGripPointAt(0, newPos);
                                                            else if (otherPort.Name == "End")
                                                                otherPipe.MoveGripPointAt(1, newPos);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!_activeSnap.HasValue && IsOrthoEnabled && _activeCommand != null && _activeCommand.ActivePoint.HasValue)
            {
                // MÜHENDİSLİK: AutoCAD standartlarına göre OSNAP yoksa ve ORTHO açıksa, koordinatları kısıtla
                var basePoint = _activeCommand.ActivePoint.Value;
                var current = _lastMouseWorldPos.Value;
                
                double dx = Math.Abs(current.X - basePoint.X);
                double dy = Math.Abs(current.Y - basePoint.Y);

                if (dx > dy)
                {
                    // Yatay kilit
                    _lastMouseWorldPos = new Vector3D(current.X, basePoint.Y, current.Z);
                }
                else
                {
                    // Dikey kilit
                    _lastMouseWorldPos = new Vector3D(basePoint.X, current.Y, current.Z);
                }
            }

            if (_activeCommand != null && _lastMouseWorldPos.HasValue)
                _activeCommand.OnPointerMoved(_lastMouseWorldPos.Value);

            // UI Güncelleme (CoordinateText XAML İsmi ile Uyumlu)
            if (CoordinateText != null)
                CoordinateText.Text = $"X: {worldPos.X:F2}, Y: {worldPos.Y:F2}";

            if ((DateTime.Now - _lastMouseMoveTime).TotalMilliseconds > 16)
            {
                _lastMouseMoveTime = DateTime.Now;
                
                // MÜHENDİSLİK: Faz 26 - Hover detection (Hit-Testing)
                // NOT: Önceden tüm veritabanını (_database.GetAllEntities()) doğrusal taranıyordu —
                // 10.000+ nesneli çizimlerde her fare hareketinde O(n) maliyet yaratıyordu.
                // Artık QuadTree'den (aynı indeks render/box-select'te de kullanılıyor) sadece imlecin
                // etrafındaki küçük bölgeyi sorguluyoruz; en yakın nesne "en üstteki" olarak seçilir.
                if (!_isPanning && !_isSelecting && _database != null && _activeCommand == null)
                {
                    double hitTolerance = 10.0 / Math.Max(0.001, _zoom); // Ekranda 10px tolerans
                    CadEntity? foundHit = null;
                    double bestDistance = hitTolerance;

                    var queryBox = new CadBoundingBox(
                        new Vector3D(worldPos.X - hitTolerance, worldPos.Y - hitTolerance, -1e9),
                        new Vector3D(worldPos.X + hitTolerance, worldPos.Y + hitTolerance, 1e9));

                    foreach (var ent in _database.QueryEntities(queryBox))
                    {
                        if (HiddenLayers.Contains(ent.Layer)) continue;

                        double dist = ent.DistanceTo(worldPos);
                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            foundHit = ent;
                        }
                    }

                    if (foundHit != _hoveredEntity)
                    {
                        _hoveredEntity = foundHit;
                        UpdateHoverTooltip(_hoveredEntity);
                    }
                }
                
                InvalidateViewport();
            }
        }

        /*
           NE: ToolTip Güncelleme ve Gösterme
           NEDEN: Fare bir nesnenin üzerine geldiğinde o nesnenin tipine (Pipe, Line vb.) göre detaylı mühendislik bilgisini oluşturup ekranda göstermek için.
        */
        private void UpdateHoverTooltip(CadEntity? entity)
        {
            if (entity == null)
            {
                if (this.FindName("EntityToolTip") is ToolTip toolTipHiding)
                {
                    toolTipHiding.IsOpen = false;
                    toolTipHiding.Visibility = Visibility.Collapsed;
                }
                return;
            }

            var toolTip = this.FindName("EntityToolTip") as ToolTip;
            var contentPanel = this.FindName("EntityToolTipContent") as StackPanel;

            if (toolTip == null || contentPanel == null) return;

            contentPanel.Children.Clear();

            // Başlık (Entity Tipi)
            var titleText = new TextBlock 
            { 
                Text = entity.GetType().Name.Replace("Entity", ""), 
                FontWeight = FontWeights.Bold, 
                Foreground = System.Windows.Media.Brushes.Gold,
                Margin = new Thickness(0,0,0,5)
            };
            contentPanel.Children.Add(titleText);

            // Temel Özellikler
            contentPanel.Children.Add(new TextBlock { Text = $"Katman: {entity.Layer}", Foreground = System.Windows.Media.Brushes.LightGray });

            // Geometrik bilgiler (Line, Circle, Polyline)
            if (entity is Afney.Cad.Domain.Entities.Basic.LineEntity line)
            {
                double lenMm = line.GetLength();
                string lenStr = lenMm >= 1000
                    ? $"{lenMm / 1000.0:F3} m  ({lenMm:F0} mm)"
                    : $"{lenMm:F1} mm";
                contentPanel.Children.Add(new TextBlock { Text = $"Uzunluk: {lenStr}", FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White });
                contentPanel.Children.Add(new TextBlock { Text = $"Başlangıç: ({line.StartPoint.X:F0}, {line.StartPoint.Y:F0})", Foreground = System.Windows.Media.Brushes.LightGray, FontSize = 10 });
                contentPanel.Children.Add(new TextBlock { Text = $"Bitiş:      ({line.EndPoint.X:F0}, {line.EndPoint.Y:F0})", Foreground = System.Windows.Media.Brushes.LightGray, FontSize = 10 });
            }
            else if (entity is Afney.Cad.Domain.Entities.Basic.CircleEntity circle)
            {
                contentPanel.Children.Add(new TextBlock { Text = $"Yarıçap: {circle.Radius / 1000.0:F3} m  ({circle.Radius:F0} mm)", FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White });
                contentPanel.Children.Add(new TextBlock { Text = $"Çap: {circle.Radius * 2 / 1000.0:F3} m", Foreground = System.Windows.Media.Brushes.LightGray });
                contentPanel.Children.Add(new TextBlock { Text = $"Çevre: {2 * Math.PI * circle.Radius / 1000.0:F3} m", Foreground = System.Windows.Media.Brushes.LightGray });
            }
            else if (entity is Afney.Cad.Domain.Entities.Basic.LwPolylineEntity poly)
            {
                double totalLen = 0;
                var verts = poly.Vertices;
                for (int i = 1; i < verts.Count; i++)
                    totalLen += (verts[i] - verts[i-1]).Length();
                contentPanel.Children.Add(new TextBlock { Text = $"Toplam uzunluk: {totalLen / 1000.0:F3} m", FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White });
                contentPanel.Children.Add(new TextBlock { Text = $"Köşe sayısı: {verts.Count}", Foreground = System.Windows.Media.Brushes.LightGray });
            }

            // Mekanik Nesnelere (Domain/Mechanical) özel bilgiler
            if (entity is MechanicalEntity mech)
            {
                contentPanel.Children.Add(new TextBlock { Text = $"Sistem: {mech.SystemType}", Foreground = System.Windows.Media.Brushes.Cyan });
                
                if (mech is PipeEntity pipe)
                {
                    contentPanel.Children.Add(new TextBlock { Text = $"Malzeme: {pipe.PipeMaterialType}", Foreground = System.Windows.Media.Brushes.White });
                    contentPanel.Children.Add(new TextBlock { Text = $"Çap: DN {pipe.InnerDiameter:F1}", FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White });
                    contentPanel.Children.Add(new TextBlock { Text = $"Debi: {pipe.FlowRate:F2} l/s", Foreground = System.Windows.Media.Brushes.LightGreen });
                    contentPanel.Children.Add(new TextBlock { Text = $"Hız: {pipe.Velocity:F2} m/s", Foreground = System.Windows.Media.Brushes.LightGreen });
                    contentPanel.Children.Add(new TextBlock { Text = $"Kayıp: {pipe.PressureDrop:F2} Pa/m", Foreground = System.Windows.Media.Brushes.Salmon });
                }
                // (Gelecekte buraya FittingEntity veya FixtureEntity gibi eklemeler yapılabilir)
            }
            
            toolTip.Visibility = Visibility.Visible;
            toolTip.IsOpen = true;
        }

        /*
           NE: Fare Bırakılma Olayı (MouseUp)
           NEDEN: Pan işlemini bitirmek veya fareyle çekilen seçim kutusuna (Window/Crossing) giren nesneleri seçmek için.
        */
        private void CadCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // MIDDLE BUTTON: Pan sonu
                if (e.ChangedButton == MouseButton.Middle)
                {
                    _isPanning = false;
                    CadCanvas.ReleaseMouseCapture();
                    CadCanvas.Cursor = System.Windows.Input.Cursors.Cross;
                    Serilog.Log.Information("🖱️ Pan modu kapandı");
                    return;
                }
                
                // RIGHT BUTTON: WPF ContextMenuOpening otomatik çalışacak
                if (e.ChangedButton == MouseButton.Right)
                {
                    // e.Handled işlemlerini ContextMenuOpening eventi içerisinde yapacağız (komut iptal edildiyse açılmasını engelledik)
                    return;
                }
                
                // LEFT BUTTON: Seçim veya Komut veya Grip Release
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (_isStretching)
                    {
                        _isStretching = false;
                        _activeGripEntity = null;
                        _activeGripIndex = null;
                        CadCanvas.ReleaseMouseCapture();
                        Serilog.Log.Information("✍️ Grip Stretch işlemi bitirildi.");
                        return; // Seçim kutusu hesaplamasına girme
                    }

                    if (_isSelecting)
                    {
                        _isSelecting = false;

                    if (_selectionManager != null)
                    {
                        try
                        {
                            double dragDist = Math.Sqrt(
                                Math.Pow(_selectionCurrentPoint.X - _selectionStartPoint.X, 2) +
                                Math.Pow(_selectionCurrentPoint.Y - _selectionStartPoint.Y, 2));

                            if (dragDist < 5.0)
                            {
                                // TEK TIK — en yakın entity'yi seç (AutoCAD pick)
                                var wp = ScreenToWorld(_selectionStartPoint);
                                double pickTol = 12.0 / Math.Max(0.001, _zoom);

                                if (Keyboard.Modifiers != ModifierKeys.Shift)
                                    _selectionManager.ClearSelection();

                                /*
                                   MÜHENDİSLİK: Önceden burada _database.GetAllEntities() ile TÜM veritabanı
                                   lineer taranıyordu (her tek tık O(n), polyline'larda vertex sayısıyla daha
                                   da ağır) — büyük DWG'lerde (binlerce entity) en sık kullanılan etkileşim
                                   (tek tık seçim) gözle görülür şekilde yavaşlıyordu. QuadTree zaten var
                                   (double-click hit-test'te ve SnapEngine'de kullanılıyor) — burada da
                                   pickTol yarıçaplı bir kutu ile önce aday listesi daraltılıyor.
                                */
                                double ht = pickTol;
                                var queryBox = new CadBoundingBox(
                                    new Vector3D(wp.X - ht, wp.Y - ht, -1e9),
                                    new Vector3D(wp.X + ht, wp.Y + ht, 1e9));

                                CadEntity? best = null;
                                double bestDist = pickTol;
                                foreach (var ent in _database!.QueryEntities(queryBox))
                                {
                                    if (HiddenLayers.Contains(ent.Layer)) continue;
                                    double d = ent.DistanceTo(wp);
                                    if (d < bestDist) { bestDist = d; best = ent; }
                                }

                                if (best != null)
                                {
                                    _selectionManager.ToggleEntity(best.Id);
                                    OnFeedback?.Invoke($"Seçildi: {best.GetType().Name} — Katman: {best.Layer}");
                                }

                                SelectionChanged?.Invoke(_selectionManager.GetSelectedEntities());
                            }
                            else
                            {
                                // PENCERE SEÇİMİ — drag ile seçim kutusu
                                var p1 = ScreenToWorld(_selectionStartPoint);
                                var p2 = ScreenToWorld(_selectionCurrentPoint);
                                p1 = new Vector3D(p1.X, p1.Y, -1000000);
                                p2 = new Vector3D(p2.X, p2.Y, 1000000);

                                var rect = new CadBoundingBox(p1, p2);
                                bool isCrossing = _selectionCurrentPoint.X < _selectionStartPoint.X;

                                if (Keyboard.Modifiers != ModifierKeys.Shift)
                                    _selectionManager.ClearSelection();

                                if (isCrossing)
                                    _selectionManager.SelectByCrossing(rect);
                                else
                                    _selectionManager.SelectByWindow(rect);

                                SelectionChanged?.Invoke(_selectionManager.GetSelectedEntities());
                            }
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, "Seçim sırasında hata!");
                        }
                    }
                }
                } // Added to close the e.ChangedButton == MouseButton.Left block
            } // Closes outer try block
            finally
            {
                // GARANTİ: Her zaman mouse capture'ı release et!
                if (CadCanvas.IsMouseCaptured)
                {
                    CadCanvas.ReleaseMouseCapture();
                    Serilog.Log.Information("🔓 Mouse capture release edildi (finally)");
                }
                
                InvalidateViewport();
            }
        }

        /*
           NE: Fare Tekerleği Olayı (MouseWheel) — AutoCAD 2026 Standardı
           NEDEN: Fare tekerleği ile çizime yakınlaşmak veya uzaklaşmak için.
           STANDART: AutoCAD instant zoom kullanır — fare imlecine doğru/dosyandan anında zoom.
           Faktör: 1.15x (çevrim başına %15)
        */
        /*
           NE: Fare Tekerleği — AutoCAD Standardı
           NASIL:
           1. Delta-aware: e.Delta / 120 → kaç notch → Math.Pow(1.12, notches)
              → hızlı scroll = büyük adım, yavaş = küçük adım (kümülatif)
           2. Anında (instant) zoom — AutoCAD animasyon kullanmaz, lerp YOK
           3. Pivot = fare imleci konumu (dünya koordinatı sabit kalır)
        */
        private void CadCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var mousePos = e.GetPosition(CadCanvas);

            // Kaç notch? (1 notch = 120 delta birimi; bazı fareler kesirli verir)
            double notches = e.Delta / 120.0;
            // Her ±1 notch ~%25 zoom — AutoCAD 2026 standardı (eski: 1.12 = %12, çok yavaş).
            double factor = Math.Pow(1.25, notches);

            // Yeni zoom değeri (klamp: aşırı in/out'u engelle)
            double newZoom = Math.Clamp(_zoom * factor, 1e-6, 1e6);

            // Pivot hesabı: fare imlecinin dünya koordinatı (worldPivot) sabit kalmalı.
            // worldPivot = (mousePos - offset) / zoom
            // Yeni offset = mousePos - worldPivot * newZoom
            double worldPivotX = (mousePos.X - _offset.X) / _zoom;
            double worldPivotY = (mousePos.Y - _offset.Y) / _zoom;

            _zoom   = newZoom;
            _offset = new Vector3D(
                mousePos.X - worldPivotX * newZoom,
                mousePos.Y - worldPivotY * newZoom, 0);

            // Target'ları da güncelle (ZoomExtents vb. ile tutarlılık)
            _targetZoom   = _zoom;
            _targetOffset = _offset;

            if (ZoomText != null)
                ZoomText.Text = $"Z: {_zoom:F4}x";

            InvalidateViewport();
        }


        /*
           NE: Klavye Olay Yöneticisi (KeyDown)
           NEDEN: ESC (İptal), DELETE (Sil) ve ENTER (Onay) gibi AutoCAD standart klavye etkileşimlerini handle etmek için.
        */
        private void CadCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            /*
               NE: Enter / Space → aktif komuta "onayla" sinyali gönder
               NEDEN: Manuel Mahal gibi komutlar Enter ile polygon oluşturur.
                      Viewport'un bu tuşları yutmaması için e.Handled = true yapılır.
            */
            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Space)
            {
                if (_activeCommand != null)
                {
                    Serilog.Log.Information("[Viewport] Enter/Space → _activeCommand.OnKeyDown(Enter)");
                    _activeCommand.OnKeyDown(InputKey.Enter);
                    e.Handled = true;
                    InvalidateViewport();
                    return;
                }
            }

            // ESC TUŞU: HER ŞEYİ İPTAL ET!
            if (e.Key == Key.Escape)
            {
                Serilog.Log.Information("⛔ ESC tuşuna basıldı - İşlemler iptal ediliyor");

                // 1. Aktif komut varsa iptal et
                if (_activeCommand != null)
                {
                    _activeCommand.Cancel();
                    SetActiveCommand(null);
                    OnFeedback?.Invoke("Komut iptal edildi (ESC)");
                    Serilog.Log.Information("❌ Aktif komut iptal edildi");
                }

                // 2. Seçim modu aktifse kapat
                if (_isSelecting)
                {
                    _isSelecting = false;
                    _selectionStartPoint = new Point();
                    _selectionCurrentPoint = new Point();
                    if (CadCanvas.IsMouseCaptured) CadCanvas.ReleaseMouseCapture();
                    InvalidateViewport();
                }

                // 3. Seçimleri iptal et
                if (_selectionManager != null && _selectionManager.SelectedCount > 0)
                {
                    _selectionManager.ClearSelection();
                    SelectionChanged?.Invoke(System.Linq.Enumerable.Empty<CadEntity>());
                    InvalidateViewport();
                }
            }
            else if (e.Key == Key.Delete)
            {
                if (_selectionManager != null && _selectionManager.SelectedCount > 0)
                    DeleteEntities(_selectionManager.GetSelectedEntities().ToList());
            }
            else if (e.Key == Key.F8)
            {
                ToggleOrtho();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && _activeCommand == null)
            {
                CycleOverlappingEntity();
                e.Handled = true;
            }
        }

        /*
           NE: Üst Üste Binen Nesneler Arası Geçiş (CycleOverlappingEntity)
           NEDEN: Yoğun çizimlerde birden fazla nesne aynı noktada üst üste durabilir (örn. duvar
                  üzerindeki bir boru). AutoCAD'de imleç sabit tutulup Tab'a basılınca sırayla her
                  biri seçilir. Önceden bu özellik hiç yoktu (hit-test altyapısı sadece "en yakın
                  tek nesneyi" buluyordu) — artık imlecin etrafındaki TÜM adaylar toplanıp sırayla
                  gezilebiliyor.
        */
        private void CycleOverlappingEntity()
        {
            if (_database == null || !_lastMouseWorldPos.HasValue) return;

            var cursor = _lastMouseWorldPos.Value;
            double hitTolerance = 10.0 / Math.Max(0.001, _zoom);

            // İmleç önemli ölçüde hareket ettiyse (yeni bir nokta) aday listesini sıfırla.
            bool sameSpot = _tabCycleOrigin.HasValue &&
                Math.Abs(_tabCycleOrigin.Value.X - cursor.X) < hitTolerance &&
                Math.Abs(_tabCycleOrigin.Value.Y - cursor.Y) < hitTolerance;

            if (!sameSpot || _tabCycleCandidates == null)
            {
                var queryBox = new CadBoundingBox(
                    new Vector3D(cursor.X - hitTolerance, cursor.Y - hitTolerance, -1e9),
                    new Vector3D(cursor.X + hitTolerance, cursor.Y + hitTolerance, 1e9));

                _tabCycleCandidates = _database.QueryEntities(queryBox)
                    .Where(ent => !HiddenLayers.Contains(ent.Layer) && ent.DistanceTo(cursor) < hitTolerance)
                    .OrderBy(ent => ent.DistanceTo(cursor))
                    .ToList();
                _tabCycleOrigin = cursor;
                _tabCycleIndex = -1;
            }

            if (_tabCycleCandidates == null || _tabCycleCandidates.Count == 0)
            {
                OnFeedback?.Invoke("Bu noktada nesne bulunamadı.");
                return;
            }

            _tabCycleIndex = (_tabCycleIndex + 1) % _tabCycleCandidates.Count;
            var picked = _tabCycleCandidates[_tabCycleIndex];

            _selectionManager?.ClearSelection();
            _selectionManager?.AddToSelection(picked);
            SelectionChanged?.Invoke(new[] { picked });

            OnFeedback?.Invoke($"{_tabCycleIndex + 1}/{_tabCycleCandidates.Count} — {picked.GetType().Name} (Katman: {picked.Layer})");
            InvalidateViewport();
        }

        public void ToggleOrtho()
        {
            ToggleOrthoMode(!IsOrthoEnabled);
        }

        public void ToggleOrthoMode(bool isEnabled)
        {
            if (IsOrthoEnabled == isEnabled) return;
            IsOrthoEnabled = isEnabled;
            Serilog.Log.Information("📐 Ortho Mode Set to: {Status}", IsOrthoEnabled);
            OrthoToggled?.Invoke(IsOrthoEnabled);
            OnFeedback?.Invoke(IsOrthoEnabled ? "Ortho Mode: AÇIK" : "Ortho Mode: KAPALI");
            InvalidateViewport();
        }


    /*
       NE: Ekrandan Dünyaya Dönüşüm (ScreenToWorld)
       NEDEN: Ekrandaki piksel koordinatlarını (ör: farenin tıkladığı yer) çizimdeki gerçek dünya koordinatlarına çevirmek için.
    */
    public Vector3D ScreenToWorld(Point screen)
    {
        return new Vector3D((screen.X - _offset.X) / _zoom, (screen.Y - _offset.Y) / _zoom, 0);
    }

    /*
       NE: Dünyadan Ekrana Dönüşüm (WorldToScreen)
       NEDEN: Çizimdeki gerçek dünya koordinatlarını ekranın piksellerine çevirerek nesneleri doğru yere çizmek için.
    */
    public Point WorldToScreen(Vector3D world)
    {
        return new Point(world.X * _zoom + _offset.X, world.Y * _zoom + _offset.Y);
    }
    }
