using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Compunet.YoloV8;
using Compunet.YoloV8.Metadata;
using Microsoft.Extensions.Logging;

namespace DroneControl.Integrations.Vision;

public sealed class YoloVisionProvider : IVisionProvider, IDisposable
{
    public string ProviderName => "YOLOv8 Vision Provider (ONNX)";
    private YoloPredictor? _predictor;
    private readonly ILogger<YoloVisionProvider> _logger;

    public IReadOnlySet<DetectedObjectType> SupportedObjectTypes { get; } = new HashSet<DetectedObjectType>
    {
        DetectedObjectType.Person,
        DetectedObjectType.Car,
        DetectedObjectType.Truck,
        DetectedObjectType.Bicycle,
        DetectedObjectType.Motorcycle
    };

    public event EventHandler<VisionEvent>? VisionEventEmitted;

    public YoloVisionProvider(ILogger<YoloVisionProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<DetectionResult>> DetectAsync(CameraFrame frame, CancellationToken cancellationToken = default)
    {
        if (_predictor == null)
        {
            var modelPath = @"C:\temp\yolov8n_fixed.onnx";
            if (File.Exists(modelPath))
            {
                try
                {
                    _predictor = new YoloPredictor(modelPath);
                    _logger.LogInformation("YOLOv8 model loaded successfully from {ModelPath}.", modelPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FAILED TO LOAD PREDICTOR: {ex}");
                    _logger.LogError(ex, "Failed to load YOLOv8 model at {ModelPath}.", modelPath);
                }
            }
        }

        var results = new List<DetectionResult>();

        if (_predictor != null && frame.ImageBytes != null && frame.ImageBytes.Length > 0)
        {
            try
            {
                using var stream = new MemoryStream(frame.ImageBytes);
                using var image = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgb24>(stream, cancellationToken);
                
                var inferenceResult = await Task.Run(() => _predictor.Detect(image), cancellationToken);
                
                var boxes = inferenceResult.ToList();
                Console.WriteLine($"Total raw boxes from predictor: {boxes.Count}");
                foreach (var box in boxes)
                {
                    Console.WriteLine($"Found object: {box.Name.Name} with confidence {box.Confidence}");
                    if (box.Name.Name == "person" || box.Name.Name == "car" || box.Name.Name == "truck" || box.Name.Name == "bicycle" || box.Name.Name == "motorcycle" || box.Name.Name == "bus" || box.Name.Name == "stop sign")
                    {
                        var type = box.Name.Name switch
                        {
                            "person" => DetectedObjectType.Person,
                            "car" => DetectedObjectType.Car,
                            "truck" => DetectedObjectType.Truck,
                            "bicycle" => DetectedObjectType.Bicycle,
                            "motorcycle" => DetectedObjectType.Motorcycle,
                            "bus" => DetectedObjectType.Car, // map bus to car for now if bus isn't in enum
                            _ => DetectedObjectType.Unknown
                        };

                        var det = new DetectionResult(
                            $"yolo_{Guid.NewGuid():N}",
                            type,
                            box.Confidence,
                            box.Bounds.X,
                            box.Bounds.Y,
                            box.Bounds.Width,
                            box.Bounds.Height
                        );
                        results.Add(det);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"YOLO inference failed: {ex}");
                _logger.LogError(ex, "YOLO inference failed.");
            }
        }

        VisionEventEmitted?.Invoke(this, new VisionEvent(frame.Timestamp, results));
        return results;
    }

    public void Dispose()
    {
        _predictor?.Dispose();
    }
}
