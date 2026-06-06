# DroneControl Architecture

DroneControl is a Windows desktop orchestration and user-experience layer for drone operations. It is not a flight controller, simulator, computer vision framework, video framework, or mapping engine.

The product strategy is open-source first: integrate mature projects, normalize their behavior behind provider interfaces, and keep the WPF UI free of vendor or runtime-specific dependencies.

## Revised Folder Structure

- `DroneControl.UI`: WPF, MVVM, user workflows, view models, and composition root.
- `DroneControl.Core`: provider interfaces, shared domain models, mission models, telemetry models, and integration health contracts.
- `DroneControl.Integrations`: boundaries for PX4, MAVSDK, Gazebo, YOLO, ByteTrack, FFmpeg, and maps.
- `DroneControl.Integrations/Mocks`: temporary development mocks only, used until real packaged integrations are available.
- `DroneControl.Plugins`: plugin manifest and extension model for optional providers.
- `DroneControl.Runtime`: registry, installation/unpack hooks, health monitoring, process lifecycle, version tracking, diagnostics, and runtime logging for external tools.
- `DroneControl.Storage`: SQLite-backed persistence for missions, telemetry, replay metadata, settings, and local cache policy.
- `DroneControl.Installer`: MSI/Setup.exe strategy and future packaging assets.

Removed as product modules:

- `DroneControl.Simulator`: replaced by `ISimulatorProvider`, with Gazebo as the intended primary implementation.
- `DroneControl.Vision`: replaced by `IVisionProvider` and `ITrackingProvider`, with YOLO and ByteTrack/DeepSORT as intended implementations.
- `DroneControl.Telemetry`: telemetry recording now belongs to storage/replay persistence.
- `DroneControl.Missions`: mission storage now sits behind storage interfaces; mission workflow remains a UI/domain concern.

## Provider Interfaces

- `IDroneProvider`: vehicle connection, telemetry, and command boundary. Intended implementations include `PX4Provider`, `ArduPilotProvider`, and future `DJIProvider`.
- `ISimulatorProvider`: simulator lifecycle and virtual camera boundary. Intended implementation: `GazeboProvider`.
- `IVisionProvider`: detection boundary. Intended implementation: `YoloProvider`.
- `ITrackingProvider`: tracking and target lock boundary. Intended implementations: `ByteTrackProvider` or `DeepSortProvider`.
- `IVideoProvider`: video recording, snapshots, ingest, and replay media boundary. Intended implementation: `FfmpegVideoProvider` or `GStreamerVideoProvider`.
- `IMapProvider`: map tile/source boundary. Intended implementation: `OpenStreetMapProvider`.

The UI depends only on these interfaces and shared models.

## Custom Subsystems To Replace

- Custom simulator loop: replace with Gazebo integration. Keep only temporary mock provider for UI development.
- Custom detection logic: replace with Ultralytics YOLO. Keep only temporary mock provider for UI layout and wiring.
- Custom tracking logic: replace with ByteTrack or DeepSORT. DroneControl owns target selection, target lock state, visualization, and mission actions.
- Custom video processing: replace with FFmpeg or GStreamer.
- Custom map engine: use OpenStreetMap-compatible tile providers and cache policy.
- Ad-hoc in-memory persistence: replace with SQLite.
- Basic logging providers: use Serilog for durable file logging and diagnostics.

## Design Decisions

### UI Isolation

The WPF layer references `DroneControl.Core`, `DroneControl.Integrations`, and `DroneControl.Storage`, but it does not reference PX4, Gazebo, YOLO, MAVSDK, FFmpeg, or vendor SDKs directly. Provider selection and runtime wiring happen through dependency injection.

### External Runtime Management

DroneControl will bundle or internally manage external tools. End users should never install Python, OpenCV, PX4, MAVSDK, Gazebo, ROS, FFmpeg, CUDA, .NET, or any SDK manually.

`DroneControl.Runtime` owns runtime existence checks, package unpacking, health snapshots, process start/stop/restart, stdout/stderr capture, exit-code reporting, installed/supported version tracking, and UI-ready diagnostics. Integrations consume this framework rather than launching external tools themselves.

### Temporary Mocks

Mocks are permitted only under `DroneControl.Integrations/Mocks` and are explicitly named `TemporaryMock...`. They exist to keep the UI and storage layers testable while real integrations are packaged.

### SQLite Storage

SQLite is the default local persistence layer because it is embeddable, mature, portable, and installer-friendly. It supports mission storage, telemetry replay, settings, media metadata, and cache indexes without requiring a server.

### Plugins

Plugins extend provider capability without changing the UI. A future plugin loader can discover manifests and register provider implementations through dependency injection.

## Migration Plan

1. Replace engine abstractions with provider abstractions in `DroneControl.Core`.
2. Move temporary simulator and vision behavior into `DroneControl.Integrations/Mocks` and label it as development-only.
3. Remove product-level `DroneControl.Simulator` and `DroneControl.Vision` projects from the solution.
4. Move mission and telemetry persistence behind SQLite storage interfaces.
5. Add Serilog as the durable logging strategy.
6. Create integration boundary folders and documentation for PX4, MAVSDK, Gazebo, YOLO, ByteTrack, FFmpeg, and maps.
7. Add runtime management so all external tools are registered, installed/unpacked, health-checked, started, stopped, restarted, logged, and diagnosed consistently.
8. Implement real providers one at a time, starting with Gazebo + MAVSDK/PX4 simulation, then FFmpeg video, then YOLO detection, then ByteTrack tracking.
9. Add installer-managed runtime packaging for each provider before exposing it to end users.

## Current Status

Implemented in this refactor:

- Provider interfaces for drone, simulator, vision, tracking, video, maps, and health checks.
- Revised solution structure.
- WPF dashboard rewired to interfaces only.
- Temporary development mocks isolated under integrations.
- SQLite storage boundary and initial persistence implementation.
- Serilog file logging.
- Integration boundary documentation and installer strategy.
- Runtime management framework for external tools.

Pending:

- Installer-driven runtime asset packaging.
- Real Gazebo provider.
- Real MAVSDK/PX4 provider.
- Real YOLO provider.
- Real ByteTrack or DeepSORT provider.
- Real FFmpeg or GStreamer provider.
- OpenStreetMap WPF map view integration.
- MSI/Setup.exe runtime bundling.
