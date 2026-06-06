# ByteTrack / DeepSORT Integration Boundary

ByteTrack is the preferred target tracking provider. DeepSORT remains a compatible alternative.

Expected provider:

- `ByteTrackProvider : ITrackingProvider`

Responsibilities:

- Maintain object identities across frames.
- Report selected target state and confidence.
- Handle temporary occlusion according to provider capabilities.

DroneControl owns target selection and mission actions, not the tracking algorithm.
