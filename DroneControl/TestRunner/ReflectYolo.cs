using System;
using System.Reflection;
using Compunet.YoloV8;
using System.Threading.Tasks;

namespace TestRunner;

public static class ReflectYolo
{
    public static Task RunAsync()
    {
        Console.WriteLine("Methods in YoloPredictor:");
        foreach (var method in typeof(YoloPredictor).GetMethods())
        {
            if (method.Name.Contains("Detect"))
            {
                Console.WriteLine($"{method.Name} -> {method.ReturnType.Name}");
                if (method.ReturnType.IsGenericType)
                {
                    var inner = method.ReturnType.GetGenericArguments()[0];
                    Console.WriteLine($"  Inner: {inner.Name}");
                    foreach (var prop in inner.GetProperties())
                    {
                        Console.WriteLine($"    Prop: {prop.Name} ({prop.PropertyType.Name})");
                        if (prop.Name == "Boxes")
                        {
                            foreach (var bProp in prop.PropertyType.GetProperties())
                                Console.WriteLine($"      Box Prop: {bProp.Name}");
                        }
                    }
                }
            }
        }
        return Task.CompletedTask;
    }
}
