using System;
using System.IO;
using Afney.Cad.Mechanical.Models;

namespace Afney.Cad.Mechanical.ProjectManagement;

/*
    NE: Proje Yöneticisi (ProjectManager)
    NEDEN: FineSANI iş akışına uygun olarak proje dosyalarını ve mimari altlıkları yönetmek için.
    
    KURALLAR:
    1. Projeler ana dizindeki 'CALC' klasörü altında tutulur.
    2. Her proje için '[ProjeAdı].bld' isimli bir klasör oluşturulur.
    3. Mimari çizimler bu klasörün içine kopyalanır.
*/
public class ProjectManager
{
    private readonly string _basePath;
    private const string CalcFolderName = "CALC";

    public ProjectManager(string? customPath = null)
    {
        // Sabit Klasör Yapısı (Step Id: 10529)
        _basePath = @"C:\AFNEY_SANI";
    }

    public string GetCalcPath() => Path.Combine(_basePath, CalcFolderName);

    /*
        NE: Yeni Proje Oluştur (Step 1.1)
        NEDEN: Kemal Bey'in belirttiği gibi klasör yapısını kurmak için.
    */
    public string CreateProject(string projectName)
    {
        // Türkçe karakter temizliği (Opsiyonel ama önerilen)
        string safeName = SanitizeProjectName(projectName);
        
        string calcPath = GetCalcPath(); // C:\AFNEY_SANI\CALC
        if (!Directory.Exists(calcPath)) Directory.CreateDirectory(calcPath);

        string projectFolderPath = Path.Combine(calcPath, $"{safeName}.bld");

        if (!Directory.Exists(projectFolderPath))
        {
            Directory.CreateDirectory(projectFolderPath);
        }

        // Proje Dosyasını Oluştur (Placeholder)
        // User Request: "içerisinde ORNEKPROJE.dwg oluşacak"
        string projectFilePath = Path.Combine(projectFolderPath, $"{safeName}.dwg");
        if (!File.Exists(projectFilePath)) 
        {
            File.Create(projectFilePath).Close(); // Boş dosya oluştur
        }

        return projectFolderPath;
    }

    /*
        NE: Mimari Çizimi İçeri Al (Step 1.2)
        NEDEN: Autocad mimarisini proje klasörüne kopyalamak için.
        USER: "Manuel yapıştıracağız" dediği için bu adım opsiyonel kaldı.
    */
    public string ImportArchitectural(string sourceFilePath, string projectFolderPath)
    {
        if (!File.Exists(sourceFilePath)) return string.Empty; // Sessizce geç

        string fileName = Path.GetFileName(sourceFilePath);
        string destinationPath = Path.Combine(projectFolderPath, fileName);

        // Kopyala (Varsa üzerine yaz)
        File.Copy(sourceFilePath, destinationPath, true);

        return destinationPath;
    }

    private string SanitizeProjectName(string name)
    {
        // Türkçe karakterleri temizle (Mete Bey'in isteği)
        return name.Replace("ı", "i").Replace("İ", "I")
                   .Replace("ğ", "g").Replace("Ğ", "G")
                   .Replace("ü", "u").Replace("Ü", "U")
                   .Replace("ş", "s").Replace("Ş", "S")
                   .Replace("ö", "o").Replace("Ö", "O")
                   .Replace("ç", "c").Replace("Ç", "C")
                   .Replace(" ", "_");
    }
}
