# FFmpeg / GStreamer Integration Boundary

FFmpeg is the preferred video processing dependency. GStreamer can be used where pipeline control is more important.

Expected provider:

- `FfmpegVideoProvider : IVideoProvider`

Responsibilities:

- Decode camera streams.
- Record video.
- Extract snapshots.
- Prepare replay assets.

The installer should bundle known-good binaries.
