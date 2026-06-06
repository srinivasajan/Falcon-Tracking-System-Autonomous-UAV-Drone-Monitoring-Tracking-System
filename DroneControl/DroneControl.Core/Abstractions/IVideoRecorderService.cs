using System;
using System.Threading.Tasks;

namespace DroneControl.Core.Abstractions;

public interface IVideoRecorderService
{
    void StartRecording(Uri streamUri, string outputPath);
    Task StopRecordingAsync();
}
