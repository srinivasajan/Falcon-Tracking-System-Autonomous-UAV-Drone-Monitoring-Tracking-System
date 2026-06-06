using System.Collections.Specialized;
using System.Windows;
using DroneControl.Core.Models;
using DroneControl.UI.ViewModels;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Wpf;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using NetTopologySuite.Geometries;

namespace DroneControl.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly MemoryLayer _droneLayer;
    private readonly MemoryLayer _waypointLayer;
    private readonly MemoryLayer _routeLayer;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Initialize Map
        MapControl.Map?.Layers.Add(OpenStreetMap.CreateTileLayer());
        
        // Add layers
        _routeLayer = new MemoryLayer { Name = "Route", Style = new VectorStyle { Line = new Pen(Color.FromArgb(255, 53, 208, 186), 4) } };
        _waypointLayer = new MemoryLayer { Name = "Waypoints", Style = null };
        _droneLayer = new MemoryLayer { Name = "Drone", Style = null };

        MapControl.Map?.Layers.Add(_routeLayer);
        MapControl.Map?.Layers.Add(_waypointLayer);
        MapControl.Map?.Layers.Add(_droneLayer);

        // Center map to a default location (e.g., Zurich where PX4 SITL defaults)
        var center = SphericalMercator.FromLonLat(8.5456, 47.3977);
        MapControl.Map?.Navigator.CenterOnAndZoomTo(new MPoint(center.x, center.y), 2);

        // Handle Map Clicks
        MapControl.Info += OnMapInfo;

        // Listen to Waypoint changes
        _viewModel.MissionWaypoints.CollectionChanged += OnWaypointsChanged;

        // Listen to Telemetry for drone movement
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Listen to Replay
        _viewModel.ReplayViewModel.PropertyChanged += OnReplayViewModelPropertyChanged;

        // Wire up replay video syncing
        _viewModel.ReplayViewModel.PlayRequested += async (s, e) => await ReplayVideo.Play();
        _viewModel.ReplayViewModel.PauseRequested += async (s, e) => await ReplayVideo.Pause();
        _viewModel.ReplayViewModel.SeekRequested += async (s, ts) => await ReplayVideo.Seek(ts);
    }

    private void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (e.WorldPosition != null)
        {
            var (lon, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
            _viewModel.AddWaypoint(lat, lon);
        }
    }

    private void OnWaypointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateRouteLayer();
        UpdateWaypointLayer();
        MapControl.Refresh();
    }

    private void UpdateRouteLayer()
    {
        if (_viewModel.MissionWaypoints.Count < 2)
        {
            _routeLayer.Features = [];
            return;
        }

        var coordinates = _viewModel.MissionWaypoints
            .Select(w => SphericalMercator.FromLonLat(w.Longitude, w.Latitude))
            .Select(p => new Coordinate(p.x, p.y))
            .ToArray();

        var lineString = new LineString(coordinates);
        var feature = new GeometryFeature(lineString);
        _routeLayer.Features = [feature];
    }

    private void UpdateWaypointLayer()
    {
        var features = new List<IFeature>();
        foreach (var wp in _viewModel.MissionWaypoints)
        {
            var sm = SphericalMercator.FromLonLat(wp.Longitude, wp.Latitude);
            var feature = new PointFeature(new MPoint(sm.x, sm.y));
            feature.Styles.Add(new SymbolStyle 
            { 
                Fill = new Brush(Color.FromArgb(255, 230, 57, 70)),
                Outline = new Pen(Color.White, 2),
                SymbolScale = 0.5
            });
            features.Add(feature);
        }
        _waypointLayer.Features = features;
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.Latitude) || e.PropertyName == nameof(_viewModel.Longitude))
        {
            if (double.TryParse(_viewModel.Latitude, out double lat) && double.TryParse(_viewModel.Longitude, out double lon))
            {
                if (lat != 0 && lon != 0) // Skip empty telemetry
                {
                    UpdateDroneMarker(lat, lon);
                }
            }
        }
        else if (e.PropertyName == nameof(_viewModel.VideoUri))
        {
            if (_viewModel.VideoUri != null)
            {
                await LiveVideo.Open(_viewModel.VideoUri);
            }
            else
            {
                await LiveVideo.Close();
            }
        }
    }

    private async void OnReplayViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.ReplayViewModel.ReplayVideoUri))
        {
            if (_viewModel.ReplayViewModel.ReplayVideoUri != null)
            {
                await ReplayVideo.Open(_viewModel.ReplayViewModel.ReplayVideoUri);
            }
            else
            {
                await ReplayVideo.Close();
            }
        }
    }

    private void UpdateDroneMarker(double lat, double lon)
    {
        var sm = SphericalMercator.FromLonLat(lon, lat);
        var feature = new PointFeature(new MPoint(sm.x, sm.y));
        feature.Styles.Add(new SymbolStyle 
        { 
            Fill = new Brush(Color.FromArgb(255, 53, 208, 186)),
            Outline = new Pen(Color.White, 3),
            SymbolScale = 0.7
        });
        
        _droneLayer.Features = [feature];
        MapControl.RefreshData();
    }
}
