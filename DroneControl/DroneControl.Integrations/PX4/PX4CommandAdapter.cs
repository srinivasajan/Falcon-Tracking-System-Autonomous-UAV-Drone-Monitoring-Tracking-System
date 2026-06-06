using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Integrations.PX4;

public interface IPX4CommandAdapter
{
    Task ExecuteCommandAsync(DroneCommand command);
}

public class PX4CommandAdapter : IPX4CommandAdapter
{
    private readonly ILogger<PX4CommandAdapter> _logger;
    private readonly IDroneProvider _droneProvider;

    public PX4CommandAdapter(ILogger<PX4CommandAdapter> logger, IDroneProvider droneProvider)
    {
        _logger = logger;
        _droneProvider = droneProvider;
    }

    public async Task ExecuteCommandAsync(DroneCommand command)
    {
        // In a real MAVSDK adapter, this maps the internal DroneCommand abstraction
        // directly to the respective MAVSDK gRPC action (Action.GotoLocationAsync, Action.HoldAsync).
        
        switch (command)
        {
            case DroneCommand.Goto cmdGoto:
                _logger.LogInformation("MAVSDK Adapter: Action.GotoLocationAsync({Lat}, {Lon}, {Alt})", cmdGoto.Latitude, cmdGoto.Longitude, cmdGoto.Altitude);
                // await _droneProvider.GotoLocationAsync(cmdGoto.Latitude, cmdGoto.Longitude, cmdGoto.Altitude, cmdGoto.Speed);
                break;
            case DroneCommand.Hold cmdHold:
                _logger.LogInformation("MAVSDK Adapter: Action.HoldAsync()");
                // await _droneProvider.HoldAsync();
                break;
            case DroneCommand.Orbit cmdOrbit:
                _logger.LogInformation("MAVSDK Adapter: Action.OrbitAsync({Lat}, {Lon}, {Radius})", cmdOrbit.Latitude, cmdOrbit.Longitude, cmdOrbit.Radius);
                break;
            case DroneCommand.Yaw cmdYaw:
                _logger.LogInformation("MAVSDK Adapter: Action.SetActuatorAsync(...) -> Yaw {Heading}", cmdYaw.HeadingDegrees);
                break;
            case DroneCommand.TrackTarget cmdTrack:
                _logger.LogInformation("MAVSDK Adapter: Gimbal.SetPitchAndYawAsync({Pitch}, {Yaw})", cmdTrack.CameraPitch, cmdTrack.CameraYaw);
                break;
            default:
                _logger.LogWarning("Unknown command received by PX4CommandAdapter.");
                break;
        }

        await Task.CompletedTask;
    }
}
