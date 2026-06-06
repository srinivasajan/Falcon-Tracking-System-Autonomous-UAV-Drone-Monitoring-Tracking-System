# PX4 RUNTIME REPORT

**Date/Time:** 2026-06-06T14:14:24+05:30

## Startup Process
PX4 Autopilot SITL (`gz_x500`) and MAVSDK Server were explicitly launched as background tasks in the WSL environment.

## Port Verification
All required ports are confirmed open via runtime `ss` audits inside WSL:

- **UDP 14540:** Open (MAVSDK Server listening/connected)
- **UDP 14580:** Open (PX4 Onboard MAVLink data rate 4000000 B/s)
- **UDP 14280:** Open (PX4 Onboard MAVLink secondary)
- **TCP 50051:** Open (MAVSDK gRPC Server listening)

## MAVSDK Server Status
Connected and successfully bound to the gRPC TCP port for external Windows connection.

## Conclusion
The PX4 Simulation environment is LIVE. Telemetry and MAVLink routing are active.
