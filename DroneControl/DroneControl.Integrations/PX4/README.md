# PX4 Integration Boundary

PX4 is the target open-source flight stack for simulator and future real-vehicle workflows.

DroneControl should not embed PX4-specific concepts in the WPF UI. PX4 support should appear through `IDroneProvider` and `ISimulatorProvider` implementations that communicate through MAVSDK/MAVLink and Gazebo.

Installer/runtime responsibilities:

- Bundle a supported PX4 runtime or manage a versioned internal install.
- Hide all command-line setup from the end user.
- Expose health checks and clear error messages through DroneControl.
