# Gazebo Integration Boundary

Gazebo is the primary simulator strategy.

Expected provider:

- `GazeboSimulatorProvider : ISimulatorProvider`

Responsibilities:

- Launch and stop bundled Gazebo.
- Select worlds/models.
- Bridge virtual camera and telemetry streams into DroneControl contracts.
- Surface runtime health and missing asset diagnostics.

Temporary mocks exist only to keep the WPF shell testable before Gazebo packaging is added.
