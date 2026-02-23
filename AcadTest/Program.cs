using System;
using System.Linq;
using System.Collections.Generic;
using ACadSharp.Entities;
using ACadSharp.Tables;

class Program {
    static void Main() {
        var hPaths = typeof(Hatch).GetProperty("Paths").PropertyType.GetGenericArguments()[0];
        Console.WriteLine($"Path Type: {hPaths.Name}");
        
        var asm = typeof(Hatch).Assembly;
        var types = asm.GetTypes().Where(t => hPaths.IsAssignableFrom(t) || t.Name.Contains("HatchBoundaryPath"));
        foreach(var t in types) {
            Console.WriteLine($"- {t.Name}");
            foreach(var p in t.GetProperties()) {
                Console.WriteLine($"  {p.PropertyType.Name} {p.Name}");
            }
        }
    }
}
