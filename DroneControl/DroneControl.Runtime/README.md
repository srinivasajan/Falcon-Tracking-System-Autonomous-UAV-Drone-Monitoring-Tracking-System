# DroneControl Runtime Management

`DroneControl.Runtime` manages external tool runtimes as installer-controlled assets. Gazebo, PX4, MAVSDK, YOLO, ByteTrack, FFmpeg, and future tools are treated as managed runtimes, not as DroneControl business logic.

## Architecture

- `Abstractions`: contracts for registry, path resolution, health monitoring, process management, package installation, and managed runtime lifecycle.
- `Models`: runtime identity, kind, status, diagnostics, version data, process output, exit records, and package manifests.
- `Registry`: metadata for supported runtimes and install path resolution.
- `Processes`: generic external process launcher with stdout, stderr, exit-code capture, stop, and restart.
- `Health`: runtime health snapshots and periodic monitoring.
- `Packaging`: generic installer/unpack support for zip or directory-based runtime payloads.
- `Managed`: generic `IManagedRuntime` implementation and factory.

## Runtime Registry

The registry contains metadata, not implementation logic:

- Runtime ID and display name.
- Runtime kind.
- Supported version.
- Relative install path.
- Optional executable path.
- Default process arguments.
- Whether the runtime runs as a process or is only a bundled library/model asset.
- Project URI for diagnostics and installer metadata.

Current registered runtimes:

- Gazebo
- PX4
- MAVSDK
- Ultralytics YOLO
- ByteTrack
- FFmpeg
- SQLite

## State Model

Runtime health uses `RuntimeStatus`:

- `Unknown`
- `Missing`
- `Installed`
- `Starting`
- `Running`
- `Stopping`
- `Stopped`
- `Failed`

`RuntimeHealthSnapshot` includes installed state, version info, process ID, last exit code, update time, and diagnostics.

## Process Management

`IRuntimeProcessManager` starts external processes from a normalized `RuntimeProcessStartRequest`.

It captures:

- stdout
- stderr
- process ID
- exit code
- process-exited events

This is intentionally not tied to Gazebo, PX4, or FFmpeg. Those integrations will build provider-specific launch requests and delegate process lifecycle here.

## Health Monitoring

`IRuntimeHealthMonitor` checks each runtime by:

- resolving install paths
- checking required executables
- reading installed version manifests
- asking the process manager whether a runtime is running
- emitting diagnostics suitable for UI display

`RuntimeHealthHostedService` periodically checks all registered runtimes and raises `HealthChanged` events.

## Logging Strategy

Runtime activity is logged through `Microsoft.Extensions.Logging`, which the WPF app routes to Serilog. The process manager logs runtime start/stop, stdout/stderr, exit codes, and forced termination.

## Packaging Strategy

`IRuntimePackageInstaller` accepts `RuntimePackageManifest` records from the future installer/runtime bootstrapper. The generic implementation can unpack zip files or copy prepared runtime directories into:

`%LOCALAPPDATA%\DroneControl\runtimes`

The MSI/Setup.exe can later pre-place assets, call installer registration, or write runtime manifests. Users should never install external SDKs manually.

## Example Generic Runtime Flow

1. UI or integration asks `IManagedRuntimeFactory.Create(RuntimeId.Ffmpeg)`.
2. Runtime checks whether `%LOCALAPPDATA%\DroneControl\runtimes\ffmpeg\bin\ffmpeg.exe` exists.
3. Health snapshot reports `Missing`, `Installed`, or `Running`.
4. `StartAsync` creates a generic process request using registry metadata.
5. Process manager captures output and exit codes.
6. Health monitor raises updates for the UI.

Future Gazebo, PX4, YOLO, and ByteTrack integrations should depend on this subsystem for runtime lifecycle, but keep their domain-specific provider logic in `DroneControl.Integrations`.
