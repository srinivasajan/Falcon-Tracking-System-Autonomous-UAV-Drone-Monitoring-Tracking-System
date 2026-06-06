# DroneControl Plugins

Plugins extend DroneControl through provider interfaces in `DroneControl.Core`.

Initial plugin candidates:

- Vehicle providers: PX4, ArduPilot, DJI.
- Vision providers: YOLO model variants.
- Tracking providers: ByteTrack, DeepSORT.
- Video providers: FFmpeg, GStreamer.
- Map providers: OpenStreetMap and offline tile packs.

Plugins must not create UI dependencies on vendor-specific SDKs. The WPF layer consumes only provider contracts.
