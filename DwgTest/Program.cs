using System;
using Afney.Cad.Infrastructure.Import;

namespace DwgTest
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("DwgImportService başlatılıyor...");
                var service = new DwgImportService();
                var path = @"C:\Users\afney\Desktop\AutoCad Dosya\ornek_proje.dwg";
                Console.WriteLine($"Dosya: {path}");
                
                var entities = service.ImportDwg(path);
                Console.WriteLine($"Başarılı: {entities.Count} obje okundu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("HATA OLUŞTU:");
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
