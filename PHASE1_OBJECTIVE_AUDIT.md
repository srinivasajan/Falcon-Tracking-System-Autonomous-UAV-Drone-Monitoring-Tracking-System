# PHASE 1 ZERO-TRUST OBJECTIVE VERIFICATION AUDIT

**Date/Time:** 2026-06-06T19:47:52+05:30  
**Method:** Source inspection + runtime artifact verification  
**Trust level:** ZERO — no previous reports trusted

---

## Evidence Inventory (files read during this audit)

| File | Purpose |
|------|---------|
| `DroneControl.UI/App.xaml.cs` | App startup + DI registration |
| `DroneControl.UI/appsettings.json` | Runtime mode configuration |
| `DroneControl.UI/Views/MainWindow.xaml` | Full UI definition |
| `DroneControl.UI/ViewModels/MainWindowViewModel.cs` | Full ViewModel |
| `DroneControl.Core/Abstractions/IDroneProvider.cs` | Drone API surface |
| `DroneControl.Core/Abstractions/ICommandPlanner.cs` | Planner API |
| `DroneControl.Core/Services/CommandPlanner.cs` | Command planner implementation |
| `DroneControl.Core/Services/TargetLockService.cs` | Target lock implementation |
| `DroneControl.Integrations/Vision/YoloVisionProvider.cs` | YOLO detection |
| `DroneControl.Integrations/Tracking/IouTrackingProvider.cs` | IoU tracker |
| `DroneControl.Integrations/Mocks/TemporaryMockSimulatorProvider.cs` | Mock camera/simulator |
| `DroneControl.Integrations/Mocks/TemporaryMockVisionProvider.cs` | Mock vision |
| `DroneControl.Integrations/Mocks/TemporaryMockTrackingProvider.cs` | Mock tracker |
| `DroneControl.Integrations/DependencyInjection.cs` | DI wiring |
| `C:\temp\yolov8n_fixed.onnx` (filesystem check) | YOLO model binary |

---

## Question-by-Question Evidence

---

### Q1. Can the current system ingest a target?

**Code path:** `MainWindowViewModel.LockTarget()` (line 279–290) called by `LockTargetCommand`  
**UI wiring:** `MainWindow.xaml` line 67–78 — each detection bounding box is a `Button` bound to `LockTargetCommand` with the `DetectionResult` as `CommandParameter`.  
**What happens at click:**
```csharp
// MainWindowViewModel.cs:279
private void LockTarget(DetectionResult? detection)
{
    var dummyTrack = new TrackedObject(0, detection, false);
    _targetLockService.RequestLock(dummyTrack);
}
```
**Critical defect found:** `TrackId` is hardcoded to `0` (line 288). This means every lock request ignores the actual TrackId from the tracker. When `UpdateFromTracks()` runs next frame, it looks for `TrackId == 0` and will fail to find the real track.

**VERDICT — Target ingestion: PARTIAL**  
- The UI allows clicking a bounding box to designate a target ✅  
- Target is stored in `TargetLockService.CurrentLock` ✅  
- `TargetLockLabel` and `TargetConfidenceLabel` update in the UI ✅  
- TrackId is always 0, breaking cross-frame continuity ❌

---

### Q2. What forms of targets are supported?

**Code path:** `ITargetLockService`, `TargetLock` model, `DetectionResult` model  
**Supported input at lock-time:** bounding box coordinates (X, Y, Width, Height), object class (`DetectedObjectType`), confidence  
**Not supported:** raw image crop as target, GPS coordinate as target, manual string description  
**Declared supported types** (`YoloVisionProvider.cs` line 21–28):
```
Person, Car, Truck, Bicycle, Motorcycle
```
**VERDICT:** Bounding-box + object-class targeting only. No GPS-coordinate or image-crop ingestion path.

---

### Q3. Can the current system detect targets automatically?

**Detection pipeline in MainWindowViewModel.cs line 231:**
```csharp
var detections = await _visionProvider.DetectAsync(frame);
```
**Triggered by:** `OnFrameReady()` — fired every 500ms by `TemporaryMockSimulatorProvider.RunAsync()` via a `PeriodicTimer`.

**In production mode (appsettings.json `Mode: "Simulation"`):**  
`App.xaml.cs` line 66–69:
```csharp
services.AddDevelopmentMockProviders();  // registers TemporaryMockVisionProvider
services.AddVisionTrackingProviders();   // OVERRIDES with YoloVisionProvider (last-wins)
```
**Therefore the active `IVisionProvider` at runtime IS `YoloVisionProvider`.**

**Trigger path:**
```
TemporaryMockSimulatorProvider → VirtualCameraFrameReady event 
  → MainWindowViewModel.OnFrameReady() 
  → YoloVisionProvider.DetectAsync(frame)
```

**CRITICAL ISSUE — CameraFrame has no image bytes in mock mode:**  
`TemporaryMockSimulatorProvider.RunAsync()` line 68:
```csharp
VirtualCameraFrameReady?.Invoke(this, new CameraFrame($"MOCK-{_tick:000000}", DateTimeOffset.Now, 1280, 720));
```
`CameraFrame` constructor used here only sets FrameId, Timestamp, Width, Height — **no `ImageBytes`**.

In `YoloVisionProvider.DetectAsync()` line 59:
```csharp
if (_predictor != null && frame.ImageBytes != null && frame.ImageBytes.Length > 0)
```
**With mock simulator, `frame.ImageBytes` is always null → YOLO inference is NEVER invoked.**

The detections shown in the UI come from `TemporaryMockVisionProvider` still being registered first (but then overridden) — **actually no**, `AddVisionTrackingProviders()` overrides the mock, so `YoloVisionProvider` IS the active provider, and it always returns **0 detections** with the mock simulator.

**VERDICT — Automatic detection: FAIL in mock/simulation mode**  
- YOLO model loads from `C:\temp\yolov8n_fixed.onnx` if present ✅  
- Model IS present on this machine (12.23 MB, confirmed by filesystem check) ✅  
- YOLO is registered and wired as the active `IVisionProvider` ✅  
- `CameraFrame` from mock simulator carries NO image bytes ❌  
- YOLO inference is gated behind `frame.ImageBytes != null` check — never triggered ❌  
- End result: zero real detections in current runnable configuration ❌

---

### Q4. What detection model is being used?

**Source:** `YoloVisionProvider.cs` line 41:
```csharp
var modelPath = @"C:\temp\yolov8n_fixed.onnx";
```
**Model:** YOLOv8n (nano) — ONNX format  
**Library:** `Compunet.YoloV8` (NuGet)  
**Hardcoded absolute path** — will not work on any machine other than the developer's.

---

### Q5. Is the detection model actually loaded at runtime?

**Evidence:**
- `C:\temp\yolov8n_fixed.onnx` EXISTS on current machine: 12.23 MB (verified via `Get-Item`)
- Load happens lazily on first `DetectAsync()` call if the file exists
- Since `ImageBytes` is always null in mock mode, `DetectAsync()` exits before loading

**Therefore:** Model IS on disk ✅, but model is **never loaded** at runtime under current execution path because the image bytes guard fails first ❌

---

### Q6. Can the current system continuously track a detected target?

**Code path in `OnFrameReady()` (lines 231–233):**
```csharp
var detections = await _visionProvider.DetectAsync(frame);
var trackedObjects = await _trackingProvider.UpdateAsync(detections);
_targetLockService.UpdateFromTracks(trackedObjects);
```
**Active tracker:** `IouTrackingProvider` (registered via `AddVisionTrackingProviders()`, which overrides mock)  
**Algorithm:** IoU-based greedy matching with 5-frame memory (`_maxLostFrames = 5`)  
**UpdateFromTracks()** in `TargetLockService.cs` lines 39–56 correctly looks up the locked track by `TrackId` and updates its bounding box.

**But the pipeline only works if detections exist.**  
Since mock simulator provides no image bytes → YOLO gives 0 detections → tracker has 0 inputs → `UpdateFromTracks()` receives empty list → locked target is never updated.

**VERDICT — Continuous tracking: FAIL**  
- `IouTrackingProvider` implementation is correct and fully functional ✅  
- `TargetLockService.UpdateFromTracks()` correctly refreshes lock ✅  
- Pipeline is properly wired in `OnFrameReady()` ✅  
- No real detections are produced → tracker has nothing to track ❌  
- TrackId=0 hardcoding in `LockTarget()` would break continuity even if detections existed ❌

---

### Q7. What tracker is being used?

**Active at runtime:** `IouTrackingProvider` (IoU-based SORT-lite)  
**Source:** `DroneControl.Integrations/Tracking/IouTrackingProvider.cs`  
**Threshold:** IoU > 0.3 for match; tracks held for 5 missed frames  
**ByteTrack:** Referenced only in a README stub. `DroneControl.Integrations/ByteTrack/` contains only a README.

---

### Q8. Is tracker output connected to mission planning?

**Evidence from `MainWindowViewModel.cs`:**  
Tracker output feeds `_targetLockService.UpdateFromTracks()` → updates `CurrentLock`.

**`ICommandPlanner` / `CommandPlanner` — is it wired in UI?**  
```
grep "CommandPlanner" → DroneControl.UI: 0 results
grep "PlanCommands" → DroneControl.UI: 0 results
```
`CommandPlanner` is registered only in `TestRunner/MissionExecutionValidation.cs` (line 36) — a test harness — **not in `App.xaml.cs` or anywhere in the UI DI registration.**

**VERDICT — Tracker → Mission planning: FAIL**  
- `CommandPlanner` exists and correctly converts `TargetLock` to `DroneCommand.TrackTarget` ✅  
- `CommandPlanner` is **never registered in the running application** ❌  
- `PlanCommands()` is **never called from the UI** ❌

---

### Q9. Is tracker output connected to drone navigation?

**`GotoAsync` in UI:**
```
grep "GotoAsync" in DroneControl.UI → 0 results
```
No code in the UI ever calls `drone.GotoAsync()` in response to a track update or target lock.

**What the UI DOES have:**
- Manual "Arm", "Takeoff", "Land", "Disarm" buttons wired to drone commands
- Mission planner: map-click waypoints saved to SQLite, loadable, but NO "execute mission" button exists in the UI

**VERDICT — Tracker → Drone navigation: FAIL**  
- `GotoAsync` is fully implemented in `MavsdkProvider` and `PX4Provider` ✅  
- `CommandPlanner.PlanCommands()` correctly generates `DroneCommand.Goto` from waypoints ✅  
- **Neither `GotoAsync` nor `PlanCommands` is ever called from the UI** ❌  
- No autonomous navigation loop exists in the UI code ❌

---

### Q10. Does the drone automatically move toward the tracked target?

**Direct answer: NO.**

Evidence path from tracker output to drone movement:  
- Track update → `_targetLockService.UpdateFromTracks()` ✅  
- `CommandPlanner.PlanCommands(targetLock)` → `DroneCommand.TrackTarget` (pitch/yaw adjustment) — **NEVER CALLED** ❌  
- `drone.GotoAsync()` called in response to target — **NEVER CALLED** ❌  

The `DroneCommand.TrackTarget` model exists. The `CommandPlanner` generates it. But there is no consumer anywhere in the running application that receives these commands and sends them to the drone.

**VERDICT — Drone moves toward target: FAIL**

---

### Q11. Can this be demonstrated in PX4 SITL today?

**Pre-conditions:**
- PX4 SITL + MAVSDK previously verified working (Phase 5 of Zero-Trust audit)
- `appsettings.json` Mode = "Simulation" → MavsdkProvider IS activated

**What works:** Connect, Arm, Takeoff, Land, GotoAsync, RTL — all manually operable from UI buttons.  
**What does NOT work:** Automatic target-driven navigation. There is no autonomous loop.

**VERDICT — PX4 SITL demo of autonomous tracking: FAIL**  
Manual flight control via buttons: PASS ✅  
Autonomous target-following in SITL: FAIL ❌

---

### Q12. Can this be demonstrated without any drone hardware?

**Mock mode works:** `TemporaryMockSimulatorProvider` provides camera frames, `TemporaryMockVisionProvider` generates synthetic detections — BUT it is overridden by `YoloVisionProvider`.

**If YOLO were bypassed (mock vision):**  
`TemporaryMockVisionProvider` generates 5 moving bounding boxes every 500ms, fully functional tracking would run, and `_targetLockService.CurrentLock` would update. But drone navigation commands are still never issued.

**VERDICT — Demo without hardware: PARTIAL**  
- App runs with mocks, target lock UI updates, telemetry scrolls ✅  
- Target-following behavior does not execute ❌

---

### Q13. Can this be demonstrated with Replay mode?

**Evidence:** `ReplayDroneProvider` verified in Phase 7 — 5 snapshots replayed with correct timing.  
`ReplayViewModel` exists and is wired to UI (Telemetry Replay tab).  
Replay shows telemetry readouts (battery, speed, altitude, lat, lon).  
Replay does NOT re-execute vision/tracking — it replays stored telemetry only.

**VERDICT — Replay demo: PARTIAL**  
Telemetry replay works ✅. Vision/tracking replay (stored detections) is scaffolded in repository classes but no UI in replay tab shows stored detections ❌

---

### Q14. Steps to reproduce a demonstration today

**Working demo (no drone, mock data only):**
1. Run `DroneControl.UI.exe` (from `Publish\FalconDroneSystem\`)
2. Click "Start Providers" — mock simulator starts, mock detections appear (NOTE: these will be 0 since YOLO is active but has no image data)
3. Click "Arm", "Takeoff" — fires against mock drone provider
4. In Telemetry Replay tab: click "Refresh List", select a session, click "Load Session", drag timeline slider

**What is NOT reproducible today:**
- YOLO inference on real video frames
- Tracker-driven target lock with persistent TrackId
- Drone automatically moving toward a detected/tracked target

---

## Phase 1 Requirement Classification

| # | Requirement | Status | Key Blocking Issue |
|---|-------------|--------|--------------------|
| 1 | Accept a target | **PARTIAL** | UI click works; TrackId hardcoded to 0; lock breaks on next frame |
| 2 | Detect the target automatically | **FAIL** | Mock simulator emits no image bytes; YOLO receives null frames; 0 real detections |
| 3 | Continuously track the target | **FAIL** | Tracker pipeline wired but receives 0 detections; TrackId=0 bug |
| 4 | Navigate the drone toward the target | **FAIL** | `CommandPlanner` never registered in app; `GotoAsync` never called from UI |
| 5 | Operate with a real drone | **PARTIAL** | Manual flight control works; autonomous target navigation does not |
| 6 | Simulation mode equivalent functionality | **PARTIAL** | Simulation mode boots and accepts manual commands; target following absent |

---

## Final Verdict

> **"Phase 1 Not Complete"**

### Justification

Three of six core Phase 1 requirements **FAIL outright**:

1. **Detection is broken** — The `CameraFrame` emitted by the mock simulator carries no pixel data (`ImageBytes == null`). `YoloVisionProvider.DetectAsync()` explicitly guards against null frames and returns zero detections. No real detection pipeline is active in any runnable configuration today.

2. **Autonomous navigation does not exist** — `CommandPlanner` is implemented correctly but is never registered in `App.xaml.cs`. `GotoAsync()` is never called from the UI in response to any tracking or vision event. There is no autonomous loop.

3. **Target lock is broken across frames** — `LockTarget()` hardcodes `TrackId = 0`, meaning `UpdateFromTracks()` will never find the correct track. Even if detections existed, the lock would immediately go stale.

### What IS complete and working

- Full UI application boots and runs ✅
- Manual drone control (Arm/Takeoff/Land/GotoAsync) wired and tested against PX4 SITL ✅
- IoU tracker implementation is algorithmically correct ✅
- `CommandPlanner` generates correct commands when called ✅
- Target lock service architecture is correct ✅
- SQLite telemetry recording and replay verified working ✅
- Safety manager (RTL on battery/GPS/timeout) verified working ✅

### Minimum changes required to reach Phase 1 Complete

1. **Fix image bytes** — Mock simulator must embed image bytes in `CameraFrame`, OR add a real video source (RTSP/FFmpeg)
2. **Fix TrackId** — `LockTarget()` must look up the actual `TrackId` from the tracker's current output for the clicked `DetectionResult`
3. **Register and invoke CommandPlanner** — Add to DI, call `PlanCommands()` on every telemetry + frame cycle
4. **Add navigation loop** — After `PlanCommands()`, execute resulting `DroneCommand.Goto` via `drone.GotoAsync()`
