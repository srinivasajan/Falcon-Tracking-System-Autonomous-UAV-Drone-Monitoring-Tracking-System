namespace DroneControl.UI.ViewModels;

public sealed class RuntimeValidationItemViewModel : ObservableObject
{
    private string _installed = "Unknown";
    private string _version = "Unknown";
    private string _status = "Not checked";

    public RuntimeValidationItemViewModel(string dependency, string size)
    {
        Dependency = dependency;
        Size = size;
    }

    public string Dependency { get; }
    public string Size { get; }

    public string Installed
    {
        get => _installed;
        set => SetProperty(ref _installed, value);
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}
