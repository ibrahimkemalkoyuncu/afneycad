using Afney.Cad.Database.Core;
using Afney.Cad.Mechanical;
using Afney.Cad.Presentation.Views;
using Afney.Cad.Commands.History;

namespace Afney.Cad.Presentation
{
    /*
        NE: CAD Doküman Bağlamı (CadDocumentContext)
        NEDEN: Her bir sekme (Tab) için bağımsız veritabanı, çekirdek ve viewport yönetimi sağlamak.
        NASIL: AutoCAD'deki her DWG dosyasının kendi bellek alanına sahip olması gibi, 
               burada da her sekme kendi "Dünyasını" barındırır.
    */
    public class CadDocumentContext
    {
        public CadDatabase Database { get; set; } = null!;
        public MechanicalKernel MechanicalKernel { get; set; } = null!;
        public CommandHistory History { get; set; } = null!;
        public Afney.Cad.Application.Services.SnapEngine SnapEngine { get; set; } = null!;
        public Afney.Cad.Application.Services.SelectionManager SelectionManager { get; set; } = null!;
        public CadViewport Viewport { get; set; } = null!;
        
        public string FilePath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public bool IsModified { get; set; }

        /*
           NE: CadDocumentContext Yapıcı Metodu
           NEDEN: Sekme bazlı doküman verilerini tutan sınıfı başlatır.
        */
        public CadDocumentContext()
        {
        }
    }
}
