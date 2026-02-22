using System.Reflection; using ACadSharp.Entities; class P { static void Main() { foreach(var p in typeof(Block).GetProperties()) System.Console.WriteLine(p.Name + " : " + p.PropertyType.Name); } }
