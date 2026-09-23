using System.Runtime.CompilerServices;

/*
   NE: LicenseManager.GenerateKey (internal) — Afney.Cad.LicenseTool'a Görünürlük
   NEDEN: Anahtar üretme algoritması, müşteriye sevk edilen bu assembly'de PUBLIC bir API
          olarak durmasın diye internal yapıldı; sadece bu satırla yetkilendirilen ayrı,
          sevk edilmeyen konsol aracı (tools/Afney.Cad.LicenseTool) erişebiliyor.
*/
[assembly: InternalsVisibleTo("Afney.Cad.LicenseTool")]
