using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        try {
            var asm = Assembly.LoadFrom("c:\\Users\\afney\\.nuget\\packages\\acadsharp\\1.6.4\\lib\\netstandard2.0\\ACadSharp.dll");
            var colorType = asm.GetType("ACadSharp.Color") ?? asm.GetType("ACadSharp.CadColor");
            if(colorType != null) {
                var methods = colorType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic).Select(m => m.Name);
                Console.WriteLine("Methods: " + string.Join(", ", methods));
                var fields = colorType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic).Select(f => f.Name);
                Console.WriteLine("Fields: " + string.Join(", ", fields));
            } else { Console.WriteLine("Class not found"); }
        } catch(Exception ex) { Console.WriteLine(ex.Message); }
    }
}
