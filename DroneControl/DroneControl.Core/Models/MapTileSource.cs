namespace DroneControl.Core.Models;

public sealed record MapTileSource(
    string Name,
    Uri TemplateUri,
    string Attribution);
