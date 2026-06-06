using DroneControl.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DroneControl.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddDroneControlStorage(this IServiceCollection services)
    {
        services.AddSingleton<SqliteDatabase>();
        services.AddSingleton<IMissionStore, SqliteMissionStore>();
        services.AddSingleton<ITelemetryRecorder, SqliteTelemetryRecorder>();
        services.AddSingleton<IDetectionRepository, SqliteDetectionRepository>();
        services.AddSingleton<ITrackingRepository, SqliteTrackingRepository>();
        services.AddSingleton<ITargetLockRepository, SqliteTargetLockRepository>();
        services.AddSingleton<IMissionStateRepository, DroneControl.Storage.Repositories.SqliteMissionStateRepository>();
        services.AddSingleton<ICommandRepository, DroneControl.Storage.Repositories.SqliteCommandRepository>();
        services.AddSingleton<ISafetyEventRepository, DroneControl.Storage.Repositories.SqliteSafetyEventRepository>();
        return services;
    }
}
