// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
using System; using System.Net.Http; using System.Text.RegularExpressions;
class Program { static void Main() { 
    var client = new HttpClient(); 
    var html = client.GetStringAsync("https://raw.githubusercontent.com/ixmilia/dxf/main/src/Ixmilia.Dxf/DxfColor.cs").Result;
    var matches = Regex.Matches(html, "0x([0-9a-fA-F]{6})");
    foreach(Match m in matches) { Console.WriteLine("0xFF" + m.Groups[1].Value.ToUpper() + ","); }
}}
