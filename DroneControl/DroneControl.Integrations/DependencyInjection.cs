using DroneControl.Core.Abstractions;
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
    public static IServiceCollection AddDevelopmentMockProviders(this IServiceCollection services)
    {
        // Removed TemporaryMockDroneProvider from Mock Providers
        services.AddSingleton<ISimulatorProvider, TemporaryMockSimulatorProvider>();
        services.AddSingleton<IVisionProvider, TemporaryMockVisionProvider>();
        services.AddSingleton<ITrackingProvider, TemporaryMockTrackingProvider>();
        services.AddSingleton<IVideoProvider, DroneControl.Integrations.FFmpeg.FfmpegVideoProvider>();
        services.AddSingleton<IVideoRecorderService, VideoRecorderService>();
        return services;
    }

    public static IServiceCollection AddVisionTrackingProviders(this IServiceCollection services)
    {
        services.AddSingleton<IVisionProvider, YoloVisionProvider>();
        services.AddSingleton<ITrackingProvider, IouTrackingProvider>();
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
