# MAVSDK Connectivity Verification Report

**Date:** June 5, 2026
**Target:** `mavsdk_server` v3.17.1 (Linux x64 musl)
**Status:** ✅ Connection Established

## Connectivity Architecture
The PX4 SITL environment runs inside WSL, binding to multiple local UDP ports for MAVLink communication. The connection sequence is as follows:

1. **PX4 SITL** runs internally, communicating with Gazebo physics on UDP `18570`.
2. **PX4 MAVLink modules** open endpoints for external communication:
   - `UDP 14280`: MAVLink Ground Control Station (GCS) port.
   - `UDP 14580`: MAVLink Onboard / Companion computer port.
3. **`mavsdk_server`** connects to the UDP endpoint to discover the vehicle.
4. **`mavsdk_server`** exposes a gRPC server on TCP `50051`.
5. **DroneControl (`MavsdkProvider.cs`)** acts as a gRPC client, connecting to TCP `50051` to send commands.

## Discovery Results
During verification, the `mavsdk_server` was launched targeting the MAVLink port with the following command:
`nohup mavsdk_server udp://:14540 -p 50051`

**Console Output:**
```text
[Info ] MAVSDK version: v3.17.1 (mavsdk_impl.cpp:35)
[Info ] Waiting to discover system on udp://:14540...
[Info ] New system on: 127.0.0.1:14580 (system ID: 1)
[Debug] Component Autopilot (component ID: 1) added.
[Debug] Discovered 1 component
[Info ] System discovered
[Info ] Server started
[Info ] Server set to listen on 0.0.0.0:50051
```

## Conclusion
The `mavsdk_server` successfully discovered the simulated PX4 vehicle (System ID 1) via the `14580` internal routing and is actively listening for gRPC requests on `0.0.0.0:50051`. This confirms the backend pipeline is fully ready for `DroneControl` integration.
