using Afney.Cad.Application.Licensing;

if (args.Length < 1)
{
    Console.WriteLine("Kullanım: Afney.Cad.LicenseTool <MüşteriKodu> [Seri]");
    Console.WriteLine("Örnek:    Afney.Cad.LicenseTool ACME");
    return 1;
}

string customerId = args[0];
string? serial = args.Length > 1 ? args[1] : null;

string key = LicenseManager.GenerateKey(customerId, serial);
Console.WriteLine(key);
return 0;
