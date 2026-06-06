using System;
using System.Reflection;
using Compunet.YoloV8;

public class Program
{
    public static void Main()
    {
        var type = typeof(YoloPredictorOptions);
        foreach (var prop in type.GetProperties())
        {
            Console.WriteLine(prop.PropertyType.Name + " " + prop.Name);
        }
        Console.WriteLine(\"Metadata constructors:\");
        foreach(var c in typeof(Compunet.YoloV8.Metadata.YoloMetadata).GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine(c.ToString());
        }
    }
}
