using System;
using System.Reflection;
using Compunet.YoloV8;

var type = typeof(YoloResult<Detection>);
Console.WriteLine($"Type: {type.FullName}");
foreach (var prop in type.GetProperties())
{
    Console.WriteLine($"Property: {prop.Name} ({prop.PropertyType.Name})");
}
Console.WriteLine("Methods:");
foreach (var method in type.GetMethods())
{
    if (method.DeclaringType == type)
        Console.WriteLine($"Method: {method.Name}");
}
