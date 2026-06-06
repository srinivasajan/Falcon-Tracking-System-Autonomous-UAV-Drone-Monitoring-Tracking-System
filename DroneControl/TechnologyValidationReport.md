# DroneControl Technology Validation Report

Date: 2026-06-04

Purpose: validate whether DroneControl can realistically ship as a single Windows installer while bundling or internally managing PX4 SITL, Gazebo, MAVSDK, YOLO, ByteTrack, FFmpeg, and SQLite.

This is a deployment feasibility spike, not legal advice and not production integration work.

## Executive Summary

DroneControl can ship as a single Windows installer, but the runtime bundle strategy should be tiered rather than putting every heavyweight dependency into the base MSI.

Recommended packaging model:

- Base installer: DroneControl UI, .NET self-contained publish, SQLite, FFmpeg, MAVSDK native/runtime assets, runtime manager, license notices.
- Optional simulation pack: Gazebo + PX4 SITL assets. This may be very large and operationally complex on native Windows.
- Optional AI pack: YOLO runtime/model assets and ByteTrack runtime. Commercial YOLO use is the largest licensing blocker if using Ultralytics.
- First-run/runtime manager downloads: large models, sample Gazebo worlds, optional GPU/CUDA packages, and nonessential demos.

Key blockers:

- Ultralytics YOLO is AGPL-3.0 by default for code and trained models; proprietary commercial distribution likely requires an Ultralytics Enterprise license.
- PX4 SITL + Gazebo native Windows packaging is feasible in principle but high risk. PX4 supports Windows generally, but PX4/Gazebo SITL workflows are still more mature on Linux/WSL/Docker than native Windows.
- Gazebo has Windows support/build paths, but shipping a polished, GUI-capable Windows simulator bundle will require careful dependency curation, graphics testing, and size control.
- FFmpeg redistribution depends on build configuration. LGPL builds are easiest for commercial packaging; GPL-enabled builds impose GPL obligations.

## Runtime Bundle Strategy

Target layout:

```text
DroneControl/
  DroneControl.exe
  runtimes/
    px4/
    gazebo/
    mavsdk/
    yolo/
    bytetrack/
    ffmpeg/
    sqlite/
  licenses/
  models/
  missions/
```

Recommended installer assets:

- Ship with base installer:
  - DroneControl self-contained .NET app.
  - SQLite native/managed dependency.
  - FFmpeg LGPL-compatible Windows build.
  - MAVSDK C++ runtime libraries or generated .NET interop assets once selected.
  - Runtime registry manifests.
  - Third-party license texts and notices.

- Optional bundled feature packs:
  - Gazebo runtime, plugins, selected worlds, models, and rendering dependencies.
  - PX4 SITL binaries and required model/config assets.
  - ByteTrack runtime if shipped as native/ONNX/C++ package.

- Download on first run:
  - Large YOLO model weights.
  - Optional high-resolution Gazebo worlds/models.
  - Optional CUDA/GPU acceleration packages.
  - Any dependency whose redistribution terms or size make base-bundling unattractive.

- Optional only:
  - CUDA.
  - Python-based tooling.
  - Development headers, compilers, source trees, examples, test data, and training assets.

Estimated install footprint:

- Minimal operations install: 250 MB-700 MB.
- With FFmpeg + MAVSDK + SQLite + small CPU AI model: 700 MB-1.8 GB.
- With Gazebo + PX4 SITL + sample worlds: 2.5 GB-6+ GB.
- With GPU AI runtimes/CUDA: 6 GB-12+ GB.

## Dependency Findings

### PX4 SITL

Sources:

- [PX4 docs](https://docs.px4.io/)
- [PX4 GitHub](https://github.com/PX4/PX4-Autopilot)

Licensing summary:

- PX4 Autopilot is BSD-3-Clause.
- Commercial redistribution is generally compatible if license and copyright notices are preserved.

Windows 11 viability:

- PX4 project material states Windows support exists, but PX4 SITL workflows are much more established on Linux/macOS/WSL/Docker.
- Native Windows SITL with Gazebo should be treated as a high-risk packaging target until validated with exact PX4 release, Gazebo release, compiler/runtime, and model set.

Redistribution considerations:

- Include BSD-3-Clause license text.
- Respect PX4/Dronecode trademarks; do not imply endorsement.
- If distributing modified PX4 binaries, preserve notices and identify modifications.

Files to ship:

- PX4 SITL executable(s).
- Airframe config files.
- MAVLink config and startup scripts.
- Simulator bridge plugins/assets required by the chosen Gazebo version.
- License notices.

Runtime dependencies:

- Native C/C++ runtime dependencies.
- Gazebo-compatible bridge/runtime dependencies.
- Potential shell/script environment depending on build approach.

Python required:

- Should not be required at end-user runtime if prebuilt SITL binaries and assets are shipped.
- Python may still be needed during build/package preparation.

CUDA required:

- No.

CPU-only:

- Yes.

Approximate install size:

- 200-800 MB depending on whether source, build tools, models, and Gazebo bridge assets are included.

Startup strategy:

- DroneControl.Runtime launches PX4 SITL as a managed process.
- Capture stdout/stderr and exit code.
- Expose health status and diagnostics through runtime manager.
- Later PX4Provider should communicate through MAVSDK/MAVLink, not through direct UI dependencies.

Risks:

- Native Windows SITL packaging maturity.
- Gazebo/PX4 version coupling.
- Startup scripts may assume Unix-like paths/shells.
- Large dependency surface if bundled with simulator assets.

### Gazebo

Sources:

- [Gazebo installation docs](https://gazebosim.org/docs/latest/install/)
- [Gazebo Sim GitHub](https://github.com/gazebosim/gz-sim)
- [Gazebo Sim library docs](https://gazebosim.org/libs/sim/)

Licensing summary:

- Gazebo Sim is Apache-2.0.
- Apache-2.0 is commercially redistributable with license/notice compliance.

Windows 11 viability:

- Gazebo provides Windows source installation/build guidance and repository documentation references Windows operation.
- Practical packaging remains complex because Gazebo brings many native dependencies and rendering/physics libraries.

Redistribution considerations:

- Include Apache-2.0 license.
- Include NOTICE files where present.
- Track licenses for bundled dependencies such as rendering, physics, transport, and math libraries.

Files to ship:

- Gazebo server/client executables.
- Gazebo shared libraries.
- Rendering/physics libraries.
- Plugins.
- Worlds and models selected for DroneControl.
- Resource paths/config files.
- License/notice files.

Runtime dependencies:

- Native C++ runtime.
- Graphics/rendering stack.
- Gazebo library dependency chain.
- Possibly environment variables for resource/plugin paths.

Python required:

- No for end-user runtime if packaged as binaries.

CUDA required:

- No.

CPU-only:

- Yes, although rendering performance depends on GPU/graphics drivers.

Approximate install size:

- 1.5-3.0 GB for a curated Windows runtime; more with worlds/models.

Startup strategy:

- DroneControl.Runtime launches Gazebo server and optional GUI as managed processes.
- GazeboProvider should later build process requests and set resource/plugin environment variables.

Risks:

- Largest native dependency packaging risk.
- Graphics driver variability.
- Version compatibility with PX4 SITL.
- Installer size growth.

### MAVSDK

Sources:

- [MAVSDK guide](https://mavsdk.mavlink.io/)
- [MAVSDK Windows install notes](https://mavsdk.mavlink.io/main/en/cpp/quickstart.html)
- [MAVSDK GitHub](https://github.com/mavlink/MAVSDK)

Licensing summary:

- MAVSDK is BSD-3-Clause.
- Commercial bundling is compatible with notice preservation.

Windows 11 viability:

- MAVSDK provides Windows x64 release zip guidance.
- It is a strong candidate for bundling in the base installer.

Redistribution considerations:

- Include BSD-3-Clause license text.
- Include native library files and notices.
- Track any generated wrapper/runtime dependency license.

Files to ship:

- MAVSDK libraries.
- Any gRPC/server component if used.
- .NET binding/interoperability layer once selected.
- License files.

Runtime dependencies:

- Native C++ runtime.
- gRPC/protobuf libraries if using server/client wrapper path.

Python required:

- No if using C++/.NET interop or bundled native libraries.

CUDA required:

- No.

CPU-only:

- Yes.

Approximate install size:

- 20-100 MB.

Startup strategy:

- If using library mode, runtime manager validates library presence/version.
- If using `mavsdk_server`, runtime manager launches it as a managed process and captures output.

Risks:

- Official C# support may require careful binding strategy.
- MAVSDK version must match MAVLink/PX4 behavior expectations.

### Ultralytics YOLO

Sources:

- [Ultralytics licensing](https://www.ultralytics.com/license)
- [Ultralytics contribution/license docs](https://docs.ultralytics.com/help/contributing)

Licensing summary:

- Ultralytics YOLO is AGPL-3.0 by default.
- Ultralytics states that proprietary/commercial products require an Enterprise License if the project is not open-sourced under AGPL-compatible terms.
- This is the largest commercial licensing concern in the proposed stack.

Windows 11 viability:

- YOLO can run on Windows through Python/PyTorch/ONNX/OpenVINO/DirectML-style deployment paths, depending on selected export/runtime.
- For DroneControl, avoid requiring user-installed Python by shipping an exported model and a managed runtime such as ONNX Runtime if license strategy permits.

Redistribution considerations:

- AGPL obligations may apply to code and models.
- A closed-source commercial DroneControl distribution should either obtain an enterprise/commercial license, use an alternative permissively licensed detector, or isolate YOLO as a user-installed/open-source plugin with legal review.

Files to ship:

- Model weights or exported ONNX/TensorRT/OpenVINO model.
- Inference runtime libraries.
- Class labels/config.
- License/enterprise entitlement records.

Runtime dependencies:

- CPU inference runtime if CPU-only.
- Optional CUDA/cuDNN/TensorRT for GPU acceleration.
- Avoid Python at runtime if possible.

Python required:

- Not required if exporting and running through a packaged inference runtime.
- Required if using the stock Ultralytics Python package directly.

CUDA required:

- No.

CPU-only:

- Yes, at lower performance.

Approximate install size:

- 300 MB-3 GB depending on model, inference runtime, and whether Python/PyTorch/CUDA are included.

Startup strategy:

- Prefer library/asset validation in DroneControl.Runtime rather than a long-running process.
- YoloProvider later loads packaged model/runtime and reports health via runtime diagnostics.

Risks:

- Commercial licensing blocker.
- Python/PyTorch bundle can be huge.
- GPU acceleration increases installer size dramatically.
- Model provenance and license must be tracked.

### ByteTrack

Sources:

- [FoundationVision ByteTrack GitHub](https://github.com/FoundationVision/ByteTrack)

Licensing summary:

- ByteTrack is MIT licensed.
- Commercial redistribution is generally compatible with preserving license/copyright notices.

Windows 11 viability:

- Algorithm is portable, but the original implementation is Python/C++/research-oriented.
- Production packaging should avoid user-installed Python by using a C++ port, ONNX-adjacent implementation, or internal wrapper around bundled runtime assets.

Redistribution considerations:

- Include MIT license.
- Track licenses for dependencies such as NumPy/PyTorch/OpenCV if using Python implementation.

Files to ship:

- Tracking implementation library/script.
- Any native dependencies.
- License files.

Runtime dependencies:

- Depends on implementation choice.
- Python/PyTorch/OpenCV if using original Python path.
- Lighter native dependency set if implemented or wrapped in C++/.NET-friendly runtime.

Python required:

- Only if using Python implementation.
- Avoid for production bundle.

CUDA required:

- No. Tracking can run CPU-only.

CPU-only:

- Yes.

Approximate install size:

- 50-500 MB depending on dependency path.

Startup strategy:

- Prefer in-process provider or lightweight worker process.
- Runtime manager validates runtime assets and versions.

Risks:

- Original implementation may drag in Python/OpenCV/PyTorch.
- Need compatibility between detector output format and tracker input format.

### FFmpeg

Sources:

- [FFmpeg legal page](https://www.ffmpeg.org/legal.html)
- [FFmpeg license notes](https://ffmpeg.org/doxygen/trunk/md_LICENSE.html)

Licensing summary:

- FFmpeg is LGPL-2.1-or-later by default, but optional GPL components can make a build GPL.
- Some combinations can become non-redistributable.

Windows 11 viability:

- FFmpeg is widely available on Windows and is a strong candidate for base installer bundling.

Redistribution considerations:

- Use a known LGPL-compatible build unless DroneControl is prepared to comply with GPL obligations.
- Include license texts and build configuration details.
- Track external codec library licenses.

Files to ship:

- `ffmpeg.exe`.
- `ffprobe.exe` if diagnostics/probing are needed.
- Shared DLLs if using a shared build.
- License and build configuration notices.

Runtime dependencies:

- Native runtime DLLs depending on build.
- Codec libraries depending on configuration.

Python required:

- No.

CUDA required:

- No.

CPU-only:

- Yes.

Approximate install size:

- 80-200 MB depending on static/shared build and codecs.

Startup strategy:

- DroneControl.Runtime launches FFmpeg/FFprobe as managed processes.
- Capture stdout/stderr and exit code.
- Use FFmpeg for recording, snapshots, transcoding, and replay preparation.

Risks:

- Accidentally selecting GPL/non-redistributable build.
- Codec patent/licensing concerns in some jurisdictions.
- Need deterministic binary provenance.

### SQLite

Sources:

- [SQLite about](https://www.sqlite.org/about.html)
- [SQLite license](https://www.sqlite.org/src/doc/tip/LICENSE.md)
- [SQLite download page](https://www.sqlite.org/download.html)

Licensing summary:

- SQLite core is public domain and free for commercial/private use.
- Some organizations may still purchase a warranty/license from SQLite, but it is not generally required.

Windows 11 viability:

- Excellent. SQLite is already suitable for DroneControl local persistence.

Redistribution considerations:

- Minimal.
- If using SQLite extensions, separately validate extension licenses.

Files to ship:

- Managed package/native SQLite library used by `Microsoft.Data.Sqlite`.
- Optional CLI tools only for diagnostics; not required for normal users.

Runtime dependencies:

- Native SQLite library resolved by the .NET package/runtime.

Python required:

- No.

CUDA required:

- No.

CPU-only:

- Yes.

Approximate install size:

- 1-10 MB.

Startup strategy:

- No external process.
- Runtime manager validates library/package presence and version metadata.
- Storage subsystem uses SQLite directly.

Risks:

- Low.
- Ensure database files live under user-writable application data, not `Program Files`.

## Proof-of-Concept Implemented

Added a WPF `Runtime Validation` tab that displays:

- Dependency
- Installed
- Version
- Size
- Status

The screen uses:

- `IRuntimeRegistry` for dependency list.
- `IRuntimeHealthMonitor` for installed/status/version checks.
- Mock/estimated footprint data from this spike.

No GazeboProvider, PX4Provider, YoloProvider, or ByteTrackProvider was implemented.

## Success Criteria Assessment

Architecture deployable:

- Yes, with a tiered runtime bundle strategy.

Expected installer size:

- Base: 250 MB-700 MB.
- With simulation: 2.5 GB-6+ GB.
- With GPU AI: 6 GB-12+ GB.

Expected runtime footprint:

- Manage runtime assets under `runtimes/`.
- Store large optional assets outside the base installer when possible.

Licensing constraints:

- YOLO/Ultralytics requires legal/commercial decision before proprietary release.
- FFmpeg build configuration must be controlled.
- PX4/MAVSDK/ByteTrack/Gazebo/SQLite are generally compatible with commercial packaging when notices are handled.

Technical blockers:

- Native Windows Gazebo + PX4 SITL packaging.
- YOLO licensing.
- Avoiding Python/CUDA as end-user requirements.
- Selecting deterministic binary sources for FFmpeg and Gazebo.

Recommended next step:

- Build a packaging-only spike that downloads/unpacks known FFmpeg and SQLite artifacts into `runtimes/`, writes runtime manifests, and proves the validation screen changes from `Missing` to `Installed`.
