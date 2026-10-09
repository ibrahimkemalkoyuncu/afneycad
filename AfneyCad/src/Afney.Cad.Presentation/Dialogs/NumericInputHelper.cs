using System.Collections.Generic;
using System.Globalization;

namespace Afney.Cad.Presentation.Dialogs;

/*
   NE: Sayısal Girdi Okuma + Sessiz-Varsayılan Uyarısı (NumericInputHelper) — TB-07
   NEDEN: Çoğu mühendislik hesap diyaloğu kendi `GetDouble(string, double)` yardımcısını
          taşıyor — boş/geçersiz girdide SESSİZCE bir varsayılana düşüyor, kullanıcı kendi
          değerinin kullanılmadığını fark etmeden (örn. yangın/gaz/septik gibi can/mal
          güvenliği etkileyen hesaplarda) sonucu "kendi girdisiyle hesaplanmış" sanabiliyor.
          Bu yardımcı AYNI varsayılana-düşme davranışını (hesap SONUCU hiç değişmez) korur,
          sadece HANGİ alanların varsayılana düştüğünü bir listede toplar — çağıran taraf
          bunu tek bir uyarı MessageBox'ında kullanıcıya gösterebilir.
   KAPSAM: SADECE en kritik (can/mal güvenliği) hesap diyaloglarında (yangın, gaz, septik,
          atık su) kullanılması için eklendi — TÜM diyaloglara yaygınlaştırma kapsam dışı
          (ayrı, daha büyük bir iş, bkz. docs/08-TEKNIK-BORC.md TB-07).
*/
public static class NumericInputHelper
{
    public static double GetDoubleOrWarn(string raw, double fallback, string fieldLabel, List<string> warnings)
    {
        string trimmed = raw?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            warnings.Add($"{fieldLabel}: boş bırakıldı → varsayılan {fallback} kullanıldı");
            return fallback;
        }
        if (!double.TryParse(trimmed.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
        {
            warnings.Add($"{fieldLabel}: '{raw}' geçersiz sayı → varsayılan {fallback} kullanıldı");
            return fallback;
        }
        return v;
    }

    public static int GetIntOrWarn(string raw, int fallback, string fieldLabel, List<string> warnings)
    {
        string trimmed = raw?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            warnings.Add($"{fieldLabel}: boş bırakıldı → varsayılan {fallback} kullanıldı");
            return fallback;
        }
        if (!int.TryParse(trimmed, out int v))
        {
            warnings.Add($"{fieldLabel}: '{raw}' geçersiz sayı → varsayılan {fallback} kullanıldı");
            return fallback;
        }
        return v;
    }
}
