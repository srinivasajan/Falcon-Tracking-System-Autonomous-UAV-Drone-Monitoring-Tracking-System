using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface ISafetyEventRepository
{
    Task SaveSafetyEventAsync(string sessionId, SafetyEvent safetyEvent);
    Task<IEnumerable<SafetyEvent>> LoadSafetyEventsAsync(string sessionId, DateTimeOffset start, DateTimeOffset end);
}
