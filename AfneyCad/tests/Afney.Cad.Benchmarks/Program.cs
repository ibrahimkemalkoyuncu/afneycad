using BenchmarkDotNet.Running;

namespace Afney.Cad.Benchmarks;

/*
   NE: Gerçek Performans Ölçüm Girişi (BenchmarkDotNet Entry Point)
   NEDEN: docs/Kullanici_kitabi.md madde 43'te belirtilen eksiklik — bugüne kadar
          "O(n²) -> O(n log n) oldu", "spatial index'e bağlandı" gibi performans
          iddiaları HİÇBİR gerçek ölçüm aracı olmadan, tahminle/elle yapılmıştı.
          Bu proje BenchmarkDotNet ile gerçek, tekrarlanabilir sayılar üretir.

   NASIL: `dotnet run -c Release --project tests/Afney.Cad.Benchmarks` ile çalıştırılır.
          BenchmarkSwitcher tüm [MemoryDiagnoser] işaretli sınıfları otomatik keşfeder.
*/
public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
