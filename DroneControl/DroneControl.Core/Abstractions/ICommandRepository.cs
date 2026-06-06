using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface ICommandRepository
{
    Task SaveCommandAsync(string sessionId, DroneCommand command);
    Task<IEnumerable<DroneCommand>> LoadCommandsAsync(string sessionId, DateTimeOffset start, DateTimeOffset end);
    Task<IEnumerable<DroneCommand>> LoadAllCommandsAsync(string sessionId);
}
