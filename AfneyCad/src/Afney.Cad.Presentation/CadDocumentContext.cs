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
    public class CadDocumentContext : System.IDisposable
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
           NE: Pafta Seti Yönetimi (Session #74)
           NEDEN: Her sekme/proje kendi pafta numaralandırmasına ve revizyon geçmişine sahip
                  olmalı — önceden statik (uygulama genelinde paylaşılan) SheetIndexService.Instance
                  ve her diyalog açılışında "new RevisionTrackingService()" ile sıfırlanan geçici
                  bir örnek kullanılıyordu. Artık bu iki servis doküman bazlı burada tutulur ve
                  proje dosyasıyla birlikte (sidecar JSON, bkz. SheetSetPersistenceService) kalıcı
                  olarak kaydedilip yüklenir.
        */
        public Afney.Cad.Mechanical.Services.SheetIndexService SheetIndex { get; set; } = new();
        public Afney.Cad.Mechanical.Services.RevisionTrackingService Revisions { get; set; } = new();

        /*
           NE: Katman Durumu Yöneticisi (Session #75)
           NEDEN: Denetim raporunda "Layer State Manager: isimlendirilmiş çoklu-state yönetimi
                  yok" olarak işaretlenmişti. SheetIndex/Revisions ile aynı desende — her doküman
                  kendi adlandırılmış katman state listesine sahip, proje dosyasıyla birlikte
                  (sidecar JSON, bkz. LayerStatePersistenceService) kalıcı kaydedilir/yüklenir.
        */
        public Afney.Cad.Mechanical.Services.LayerStateManagerService LayerStates { get; set; } = new();

        /*
           NE: CadDocumentContext Yapıcı Metodu
           NEDEN: Sekme bazlı doküman verilerini tutan sınıfı başlatır.
        */
        public CadDocumentContext()
        {
        }

        /*
           NE: Kaynakları Serbest Bırak (Dispose)
           NEDEN: Sekme tamamen kapandığında içerisindeki rendering (SKPaint) ve database işlemlerinin GC tarafından silinmesini zorlamak.
        */
        public void Dispose()
        {
            if (Viewport != null)
            {
                Viewport.Dispose();
            }
            
            if (Database is System.IDisposable dbDisposable)
            {
                dbDisposable.Dispose();
            }
            
            // Olası event handler koparmalarını vb. yapabiliriz.
            Serilog.Log.Information($"🧹 CadDocumentContext temizlendi: {ProjectName}");
        }
    }
}
