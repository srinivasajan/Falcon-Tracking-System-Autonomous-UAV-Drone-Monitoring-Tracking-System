using DroneControl.Core.Abstractions;
using DroneControl.Core.Services;
using DroneControl.Integrations.MAVSDK;
using DroneControl.Integrations.Mocks;
using DroneControl.Integrations.PX4;
using DroneControl.Integrations.FFmpeg;
using DroneControl.Integrations.Tracking;
using DroneControl.Integrations.Vision;
using Microsoft.Extensions.DependencyInjection;

namespace DroneControl.Integrations;

public static class DependencyInjection
{
    /// <summary>
    /// Mock providers for simulation / development with no real camera.
    /// Vision and tracking are synthetic — no real image bytes required.
    /// </summary>
    public static IServiceCollection AddDevelopmentMockProviders(this IServiceCollection services)
    {
        services.AddSingleton<ISimulatorProvider, TemporaryMockSimulatorProvider>();
        services.AddSingleton<IVisionProvider,    TemporaryMockVisionProvider>();
        services.AddSingleton<ITrackingProvider,  TemporaryMockTrackingProvider>();
        services.AddSingleton<IVideoProvider,     FfmpegVideoProvider>();
        services.AddSingleton<IVideoRecorderService, VideoRecorderService>();
        return services;
    }

    /// <summary>
    /// Real YOLO + IoU tracker — only used when a live camera feed provides image bytes.
    /// </summary>
    public static IServiceCollection AddVisionTrackingProviders(this IServiceCollection services)
    {
        services.AddSingleton<IVisionProvider,   YoloVisionProvider>();
        services.AddSingleton<ITrackingProvider, IouTrackingProvider>();
        return services;
    }

    /// <summary>
    /// IoU tracker over mock detections (best for simulation without camera).
    /// Call after AddDevelopmentMockProviders to upgrade the tracker only.
    /// </summary>
    public static IServiceCollection AddIouTrackerOnly(this IServiceCollection services)
    {
        // Last-registration wins in MS DI — overrides TemporaryMockTrackingProvider.
        services.AddSingleton<ITrackingProvider, IouTrackingProvider>();
        return services;
    }

    /// <summary>
    /// Registers the CommandPlanner so the UI can invoke mission planning.
    /// </summary>
    public static IServiceCollection AddCommandPlanner(this IServiceCollection services)
    {
        services.AddSingleton<ICommandPlanner, CommandPlanner>();
        return services;
    }

    public static IServiceCollection AddPx4MavsdkProviders(this IServiceCollection services)
    {
        services.AddOptions<Px4ProviderOptions>();
        services.AddOptions<MavsdkProviderOptions>();
        services.AddSingleton<IDroneProvider, MavsdkProvider>();
        services.AddSingleton<IVideoRecorderService, VideoRecorderService>();
        return services;
    }
}
