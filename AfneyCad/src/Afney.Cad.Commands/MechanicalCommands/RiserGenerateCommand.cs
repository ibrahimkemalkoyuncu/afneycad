using System;
using System.Collections.Generic;
using System.Linq;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical; // EKLENDİ (Kernel)
using Afney.Cad.Mechanical.Services;
using Afney.Cad.Mechanical.Entities;
using Afney.Cad.Mechanical.Engine; // EKLENDİ

namespace Afney.Cad.Commands.MechanicalCommands;

/*
    NE: Kolon Şeması Oluşturma Komutu (RiserGenerateCommand)
    NEDEN: Tanımlanan mahaller ve veritabanındaki tesisat verilerini kullanarak otomatik kolon şeması (Riser Diagram) üretmek için.
*/
public class RiserGenerateCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly RiserDiagramService _service;
    
    private readonly MechanicalKernel _kernel;

    public string CommandName => "KOLON_SEMA";
    public Vector3D? ActivePoint { get; private set; }

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public RiserGenerateCommand(CadDatabase database)
    {
        _database = database;
        _service = new RiserDiagramService(database);
        // Kernel'i geçici olarak oluşturuyoruz (Normalde Dependency Injection ile gelmeliydi)
        // Ancak MainWindow.xaml.cs içinde zaten Kernel var, komut constructor'ını değiştirmemiz gerekebilir.
        // Şimdilik yeni bir instance oluşturursak Topoloji boş gelir!
        // BU NEDENLE Constructor'ı değiştirmeliyiz. Ama Interface (ICadCommand) kısıtlaması yok.
        // Geçici çözüm: Kernel'siz çalışmaz. MainWindow'da Command oluştururken Kernel verilmeli.
        _kernel = new MechanicalKernel(); // HATA: Boş Kernel işe yaramaz.
    }

    // Constructor Overload (Doğrusu bu)
    public RiserGenerateCommand(CadDatabase database, MechanicalKernel kernel)
    {
        _database = database;
        _service = new RiserDiagramService(database);
        _kernel = kernel;
    }

    /*
       NE: Komutu Başlat (Start)
       NEDEN: Kolon şeması üretimini başlatmak ve şemanın yerleştirileceği boşluktaki noktayı seçmek için rehber metin göstermek için.
    */
    public void Start()
    {
        OnFeedback?.Invoke("KOLON ŞEMASI: Şemanın yerleştirileceği noktayı seçin.");
    }

    /*
       NE: Tıklama Olayı (OnPointerPressed)
       NEDEN: Tıklanan noktayı baz alarak, şemayı (mahallerden izometrik projeksiyonla üretilen 2D hatlar) veritabanına eklemek ve sonucu bildirmek için.
    */
    public void OnPointerPressed(Vector3D point)
    {
        try
        {
            ActivePoint = point;
            
            // 1. Mühendislik Motorundan Verileri Çek (Topology Analysis)
            var allEntities = _database.GetAllEntities().OfType<MechanicalEntity>();
            if (!allEntities.Any())
            {
                 OnFeedback?.Invoke("UYARI: Çizimde hiç mekanik tesisat nesnesi bulunamadı.");
                 OnCompleted?.Invoke();
                 return;
            }

            // Kernel üzerindeki topolojiyi güncelle (Emin olmak için)
            // _kernel.RecalculateProject(allEntities); // Opsiyonel (Pahalı olabilir)

            var schemas = _kernel.GetRiserSchemas(allEntities);

            if (!schemas.Any())
            {
                OnFeedback?.Invoke("UYARI: Kolon (Riser) hattı tespit edilemedi. Dikey boruları kontrol edin.");
                OnCompleted?.Invoke();
                return;
            }

            // 2. Görselleştirme Servisi ile Çizim Üret
            var schemaEntities = _service.GenerateRiserDiagram(schemas, point);
            
            foreach(var ent in schemaEntities)
            {
                _database.AddEntity(ent);
            }
            
            OnFeedback?.Invoke($"BAŞARILI: {schemas.Count} adet kolon şeması oluşturuldu.");
        }
        catch (Exception ex)
        {
            OnFeedback?.Invoke($"HATA: Şema oluşturulurken bir sorun oluştu. {ex.Message}");
        }
        finally
        {
            OnCompleted?.Invoke();
        }
    }

    public void OnPointerMoved(Vector3D point) { }
    public void OnKeyDown(InputKey key) { if (key == InputKey.Escape) Cancel(); }
    public void Draw(IRenderContext context) { }
    public void Cancel() { OnCompleted?.Invoke(); }
}
