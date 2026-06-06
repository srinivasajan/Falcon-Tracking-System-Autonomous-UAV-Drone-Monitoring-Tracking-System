# DroneControl Installer Strategy

The installer target is a single Windows MSI or Setup.exe that installs DroneControl and all required managed/native runtime components.

Bundled or internally managed components:

- .NET desktop runtime, preferably via self-contained publish.
- MAVSDK native dependencies.
- Gazebo runtime and selected worlds/models.
- PX4 simulation runtime assets where applicable.
- YOLO model/runtime assets.
- ByteTrack or DeepSORT runtime assets.
- FFmpeg or GStreamer binaries.
- SQLite database initialization.

End users must not manually install Python, OpenCV, PX4, MAVSDK, Gazebo, ROS, FFmpeg, CUDA, .NET, or any SDK.

The installer subsystem should be implemented after runtime packaging decisions are validated in development builds.
