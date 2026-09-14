using System;
using Afney.Cad.Commands.Abstractions;
using Afney.Cad.Database.Core;
using Afney.Cad.Database.Transactions;
using Afney.Cad.Database.Transactions.Operations;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using Afney.Cad.Mechanical.Services;

namespace Afney.Cad.Commands.MechanicalCommands;

/*
   NE: Kesit Görünümü Oluşturma Komutu (GenerateSectionViewCommand)
   NEDEN: `MultiStoryEnhancementService.GenerateSectionView` test edilmiş (`MultiStoryEnhancementServiceTests`)
          ama hiçbir ekrana bağlanmamış bir yetenekti — Session #75 denetiminde bunun nedeni
          gerçek bir çoklu-nokta viewport seçim akışı gerektirmesiydi. Bu komut üç tıklamayla
          (kesit başlangıcı, kesit bitişi, çizimin ekleneceği nokta) bu akışı sağlar ve
          üretilen kat çizgisi/etiket/boru-kesit-dairesi nesnelerini TransactionManager
          üzerinden (Undo/Redo destekli) çizime ekler.
*/
public class GenerateSectionViewCommand : ICadCommand
{
    private readonly CadDatabase _database;
    private readonly TransactionManager _transactionManager;
    private readonly MultiStoryEnhancementService _enhancementService;

    private int _step;
    private Vector3D _sectionStart;
    private Vector3D _sectionEnd;

    public string CommandName => "SECTIONVIEW";
    public Vector3D? ActivePoint { get; private set; }

    public event Action<string>? OnFeedback;
    public event Action? OnCompleted;

    public GenerateSectionViewCommand(CadDatabase database, TransactionManager transactionManager, MultiStoryEnhancementService enhancementService)
    {
        _database = database;
        _transactionManager = transactionManager;
        _enhancementService = enhancementService;
    }

    public void Start()
    {
        _step = 0;
        OnFeedback?.Invoke("KESİT GÖRÜNÜMÜ: Kesit BAŞLANGIÇ noktasını seçin.");
    }

    public void OnPointerPressed(Vector3D point)
    {
        switch (_step)
        {
            case 0:
                _sectionStart = point;
                _step = 1;
                OnFeedback?.Invoke("Kesit BİTİŞ noktasını seçin.");
                break;

            case 1:
                _sectionEnd = point;
                _step = 2;
                OnFeedback?.Invoke("Kesitin ÇİZİME EKLENECEĞİ noktayı seçin.");
                break;

            case 2:
                var entities = _enhancementService.GenerateSectionView(_sectionStart, _sectionEnd, point);
                if (entities.Count > 0)
                {
                    var ops = new CompositeOperation("Kesit Görünümü");
                    foreach (var ent in entities)
                        ops.Add(new AddEntityOperation(_database, ent));
                    _transactionManager.Submit(ops);
                    OnFeedback?.Invoke($"Kesit görünümü eklendi ({entities.Count} nesne).");
                }
                else
                {
                    OnFeedback?.Invoke("Kesit görünümü oluşturulamadı — önce en az bir kat tanımlayın (Kat Yöneticisi).");
                }
                OnCompleted?.Invoke();
                break;
        }
    }

    public void OnPointerMoved(Vector3D point) => ActivePoint = point;

    public void OnKeyDown(InputKey key)
    {
        if (key == InputKey.Escape) Cancel();
    }

    public void Draw(IRenderContext context)
    {
        // Basit bir rehber çizgi: seçilen kesit hattı henüz tamamlanmadıysa önizlenir.
        if (_step == 1 && ActivePoint.HasValue)
        {
            context.DrawLine(_sectionStart, ActivePoint.Value, 0xAAFFFF00, 1.0);
        }
    }

    public void Cancel() => OnCompleted?.Invoke();
}
