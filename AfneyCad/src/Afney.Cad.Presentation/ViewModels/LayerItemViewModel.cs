using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Afney.Cad.Presentation.ViewModels
{
    /*
       NE: Katman Seçici Popup (LayerPickerPopup) için ListBox Öğesi ViewModel'i
       NEDEN: ListBox.ItemTemplate'in bağlanabileceği reaktif bir veri nesnesi sağlamak için.
              Görünürlük/Dondur/Kilitle simgeleri ve renk burada tutulur.
    */
    public class LayerItemViewModel : INotifyPropertyChanged
    {
        // ── Sabit Veri ─────────────────────────────────────────────────────────
        public string Name { get; set; } = string.Empty;
        public string ColorBrush { get; set; } = "#FFFFFF";

        // ── Değişken Durumlar (Property-changed ile UI'ı anlık günceller) ────
        private bool _isVisible = true;
        private bool _isFrozen  = false;
        private bool _isLocked  = false;

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; Notify(); Notify(nameof(VisibilityIcon)); }
        }

        public bool IsFrozen
        {
            get => _isFrozen;
            set { _isFrozen = value; Notify(); Notify(nameof(FreezeIcon)); }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; Notify(); Notify(nameof(LockIcon)); }
        }

        // ── Hesaplanan İkon Özellikleri ─────────────────────────────────────
        /// Görünürlük ikonu: açık göz 👁 / kapalı göz 🚫
        public string VisibilityIcon => _isVisible ? "👁" : "🚫";

        /// Dondurma ikonu: dondurulmuş ❄ / aktif ○
        public string FreezeIcon => _isFrozen ? "❄" : "○";

        /// Kilit ikonu: kilitli 🔒 / açık 🔓
        public string LockIcon => _isLocked ? "🔒" : "🔓";

        // ── INotifyPropertyChanged ──────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
