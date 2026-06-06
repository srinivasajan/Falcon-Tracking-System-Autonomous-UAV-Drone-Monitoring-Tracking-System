# MAVSDK Integration Boundary

MAVSDK is the preferred high-level communication layer for MAVLink vehicles.

Expected provider:

- `MavsdkDroneProvider : IDroneProvider`

Responsibilities:

- Connection lifecycle.
- Telemetry subscription.
- Command dispatch.
- Error normalization for UI display.

The UI must only consume `IDroneProvider`.
