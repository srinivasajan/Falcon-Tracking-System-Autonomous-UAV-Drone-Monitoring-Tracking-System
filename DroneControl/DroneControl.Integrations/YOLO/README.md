# YOLO Integration Boundary

Ultralytics YOLO is the intended object detection provider.

Expected provider:

- `YoloVisionProvider : IVisionProvider`

Responsibilities:

- Load bundled model files.
- Run inference through the managed runtime strategy selected for packaging.
- Return normalized `DetectionResult` values.

DroneControl should not implement detection algorithms.
