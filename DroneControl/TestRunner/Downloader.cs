using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace TestRunner;

public static class Downloader
{
    public static async Task DownloadModelAsync()
    {
        var modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroneControl", "models", "yolov8n.onnx");
        if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 1000000)
        {
            Console.WriteLine("Model already exists and seems valid.");
            return;
        }

        Console.WriteLine("Downloading model...");
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        var response = await client.GetAsync("https://raw.githubusercontent.com/Hyuto/yolov8-onnxruntime-web/master/public/model/yolov8n.onnx");
        response.EnsureSuccessStatusCode();
        using var fs = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs);
        Console.WriteLine("Model downloaded successfully.");
    }
}
