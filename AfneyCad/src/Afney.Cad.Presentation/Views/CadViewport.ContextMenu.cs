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

    // ===== CONTEXT MENU EVENT HANDLERS =====
    
    /*
        NE: Sağ Tıklama Menüsü - Pan Modu
        NEDEN: AutoCAD'de sağ tık → Pan yaygın kullanımdır
    */
    private void OnContextMenu_Move(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.MoveCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Mirror(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.MirrorCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Rotate(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.RotateCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Scale(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.ScaleCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Stretch(object sender, RoutedEventArgs e)
    {
        OnFeedback?.Invoke("STRETCH: Grip noktalarını sürükleyerek esnetin.");
    }

    private void OnContextMenu_GripPoint(object sender, RoutedEventArgs e)
    {
        OnFeedback?.Invoke("Tutma noktası: Nesne seçin, mavi grip kutularını sürükleyin.");
    }

    private void OnContextMenu_Copy(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _database == null || _selectionManager.SelectedCount == 0) return;
        var selected = _selectionManager.GetSelectedEntities();
        var cmd = new Afney.Cad.Commands.BasicCommands.CopyCommand(_database, _database.TransactionManager, selected);
        cmd.OnFeedback += msg => OnFeedback?.Invoke(msg);
        cmd.OnCompleted += () => SetActiveCommand(null);
        SetActiveCommand(cmd);
        cmd.Start();
    }

    private void OnContextMenu_Pan(object sender, RoutedEventArgs e)
    {
        _isPanning = true;
        CadCanvas.Cursor = System.Windows.Input.Cursors.Hand;
        OnFeedback?.Invoke("PAN modu aktif - Fareyi hareket ettirin");
    }

    /*
       NE: Sağ Tıklama Menüsü - Ekrana Sığdır
       NEDEN: Kullanıcının menü üzerinden Zoom Extents yapabilmesi için.
    */
    private void OnContextMenu_ZoomExtents(object sender, RoutedEventArgs e)
    {
        ZoomExtents();
    }

    /*
       NE: Sağ Tıklama Menüsü - Tümünü Seç
       NEDEN: Çizimdeki tüm nesneleri tek seferde seçebilmek için.
    */
    private void OnContextMenu_SelectAll(object sender, RoutedEventArgs e)
    {
        if (_selectionManager != null && _database != null)
        {
            var allEntities = _database.GetAllEntities();
            foreach (var entity in allEntities)
            {
                _selectionManager.ToggleEntity(entity.Id);
            }
            OnFeedback?.Invoke($"Tüm entityler seçildi: {_selectionManager.SelectedCount} adet");
            InvalidateViewport();
        }
    }

    /*
       NE: Sağ Tıklama Menüsü - Seçimi Temizle
       NEDEN: Seçili olan tüm nesnelerin seçim durumunu iptal etmek için.
    */
    private void OnContextMenu_ClearSelection(object sender, RoutedEventArgs e)
    {
        _selectionManager?.ClearSelection();
        OnFeedback?.Invoke("Seçim temizlendi");
        InvalidateViewport();
    }

    /*
       NE: Sağ Tıklama Menüsü - Geri Al (Undo)
       NEDEN: Son yapılan işlemi geri almak için (Placeholder).
    */
    private void OnContextMenu_Undo(object sender, RoutedEventArgs e)
    {
        OnUndoRequested?.Invoke();
    }

    private void OnContextMenu_Redo(object sender, RoutedEventArgs e)
    {
        OnRedoRequested?.Invoke();
    }

    /*
       NE: Sağ Tıklama Menüsü - Özellikler
       NEDEN: Seçili nesnelerin teknik detaylarını ve özelliklerini görmek için ilgili paneli tetikler.
    */
    private void OnContextMenu_Properties(object sender, RoutedEventArgs e)
    {
        if (_selectionManager != null && _selectionManager.SelectedCount > 0)
        {
            var selected = _selectionManager.GetSelectedEntities();
            OnFeedback?.Invoke($"Properties: {selected.Count} entity seçili");
        }
    }

    /*
       NE: Sağ Tıklama Menüsü - Sil
       NEDEN: Seçili nesneleri veritabanından kalıcı olarak silmek için.
    */
    private void OnContextMenu_Delete(object sender, RoutedEventArgs e)
    {
        if (_selectionManager == null || _selectionManager.SelectedCount == 0) return;
        DeleteEntities(_selectionManager.GetSelectedEntities().ToList());
    }

    /*
       NE: Merkezi, Undo Destekli Nesne Silme (DeleteEntities)
       NEDEN: Delete tuşu, sağ-tık menüsü ve Ctrl+X (Kes) — üç ayrı silme yolu — aynı davranışı
              (TransactionManager üzerinden undoable silme) garanti etsin diye tek noktadan yönetilir.
              Önceden Ctrl+X bu yolu atlayıp doğrudan RemoveEntity çağırıyordu ve Ctrl+Z ile geri alınamıyordu.
    */
    public void DeleteEntities(IReadOnlyCollection<CadEntity> toDelete)
    {
        if (_database == null || toDelete == null || toDelete.Count == 0) return;

        _selectionManager?.ClearSelection();

        var composite = new Afney.Cad.Database.Transactions.CompositeOperation($"{toDelete.Count} nesne silindi");
        foreach (var ent in toDelete)
            composite.Add(new Afney.Cad.Database.Transactions.Operations.RemoveEntityOperation(_database, ent));
        _database.TransactionManager.Submit(composite);

        SelectionChanged?.Invoke(System.Linq.Enumerable.Empty<CadEntity>());
        InvalidateViewport();
        OnFeedback?.Invoke($"{toDelete.Count} obje silindi. (Ctrl+Z ile geri alınabilir)");
    }

    /*
       NE: Dinamik Context Menu Açılışı (ContextMenuOpening)
       NEDEN: Menü açılmadan hemen önce çalışarak gereksiz/aktif olmayan işlemleri (Sil, Özellikler vb.) devre dışı bırakmak veya yeni dinamik bilgiler eklemek için.
    */
    private void CadCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // 1. Sağ tık aktif bir komutu iptal ettiyse menüyü açma (Örn: Çizgi komutundan çıkarken menü çıkmasın)
        if (_rightClickCanceledCommand)
        {
            e.Handled = true;
            _rightClickCanceledCommand = false;
            return;
        }

        bool hasSelection = _selectionManager != null && _selectionManager.SelectedCount > 0;

        // 2. Statik menü elemanlarını etkinleştir/devre dışı bırak
        if (this.FindName("CtxMenu_ClearSelection") is MenuItem clearMenu) clearMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Properties") is MenuItem propsMenu) propsMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Delete") is MenuItem deleteMenu) deleteMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Move") is MenuItem moveMenu) moveMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Mirror") is MenuItem mirrorMenu) mirrorMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Rotate") is MenuItem rotateMenu) rotateMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Scale") is MenuItem scaleMenu) scaleMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Stretch") is MenuItem stretchMenu) stretchMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_GripPoint") is MenuItem gripMenu) gripMenu.IsEnabled = hasSelection;
        if (this.FindName("CtxMenu_Copy") is MenuItem copyMenu) copyMenu.IsEnabled = hasSelection;

        // Geri Al / Yinele durumu + dinamik başlık
        bool canUndo = _database?.TransactionManager.CanUndo == true;
        bool canRedo = _database?.TransactionManager.CanRedo == true;
        if (this.FindName("CtxMenu_Undo") is MenuItem undoMenu)
        {
            undoMenu.IsEnabled = canUndo;
            string? undoName = _database?.TransactionManager.PeekUndoName();
            undoMenu.Header = undoName != null ? $"Geri Al: {undoName}" : "Geri Al";
        }
        if (this.FindName("CtxMenu_Redo") is MenuItem redoMenu)
        {
            redoMenu.IsEnabled = canRedo;
            string? redoName = _database?.TransactionManager.PeekRedoName();
            redoMenu.Header = redoName != null ? $"Yinele: {redoName}" : "Yinele";
        }

        var ctx = CadCanvas.ContextMenu;
        if (ctx != null)
        {
            // 3. Eski dinamik eklemeleri (Örn: Boru Çapı textleri) temizle
            var toRemove = new System.Collections.Generic.List<object>();
            foreach (var item in ctx.Items)
            {
                if (item is FrameworkElement fe && fe.Tag is string tg && tg == "Dynamic")
                {
                    toRemove.Add(item);
                }
            }
            foreach (var item in toRemove) ctx.Items.Remove(item);

            // 3b. Aktif bir komut varsa (ör. Manuel Mahal) — kullanıcı isteği: sağ tık
            // menüsünün EN ÜSTÜNDE "Tamam" (komutu bitir) ve hemen altında "İptal" olmalı,
            // sessizce otomatik onaylamak yerine kullanıcı bilinçli şekilde tıklamalı.
            if (_activeCommand != null)
            {
                var cmdRef = _activeCommand;
                var confirmItem = new MenuItem
                {
                    Header = "Tamam",
                    FontWeight = FontWeights.Bold,
                    Tag = "Dynamic",
                    InputGestureText = "Enter",
                };
                confirmItem.Click += (_, __) =>
                {
                    Serilog.Log.Information("[Viewport] Context menü 'Tamam' → aktif komuta OnKeyDown(Enter) gönderiliyor.");
                    cmdRef.OnKeyDown(InputKey.Enter);
                    InvalidateViewport();
                };

                var cancelItem = new MenuItem
                {
                    Header = "İptal",
                    Tag = "Dynamic",
                    InputGestureText = "Esc",
                };
                cancelItem.Click += (_, __) =>
                {
                    Serilog.Log.Information("[Viewport] Context menü 'İptal' → aktif komut iptal ediliyor.");
                    cmdRef.Cancel();
                    InvalidateViewport();
                };

                ctx.Items.Insert(0, new Separator { Tag = "Dynamic" });
                ctx.Items.Insert(0, cancelItem);
                ctx.Items.Insert(0, confirmItem);
            }

            // 4. Sadece tek bir boru seçiliyse debi/çap bilgisini direkt menüye ekle (Bonus Bilgi)
            if (hasSelection && _selectionManager != null && _selectionManager.SelectedCount == 1)
            {
                var ent = _selectionManager.GetSelectedEntities().First();
                if (ent is Mechanical.Entities.PipeEntity pipe)
                {
                    ctx.Items.Add(new Separator { Tag = "Dynamic" });
                    ctx.Items.Add(new MenuItem 
                    { 
                        Header = $"Boru Çapı: DN {pipe.InnerDiameter:F1}", 
                        IsEnabled = false, 
                        Tag = "Dynamic", 
                        Foreground = System.Windows.Media.Brushes.Cyan 
                    });
                }
            }
        }
    }
}
