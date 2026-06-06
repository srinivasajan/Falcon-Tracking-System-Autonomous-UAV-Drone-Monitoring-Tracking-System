# Map Integration Boundary

OpenStreetMap is the preferred map data source.

Expected provider:

- `OpenStreetMapProvider : IMapProvider`

Responsibilities:

- Provide map tile source metadata.
- Manage attribution.
- Support future offline tile cache policy.

The WPF map view should consume provider output rather than hard-code tile URLs.
