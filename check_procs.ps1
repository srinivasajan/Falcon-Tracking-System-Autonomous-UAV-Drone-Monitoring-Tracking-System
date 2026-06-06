$procs = Get-Process -Name "DroneControl.UI" -ErrorAction SilentlyContinue
foreach ($p in $procs) {
    Write-Host "PID: $($p.Id) | MainWindowTitle: $($p.MainWindowTitle)"
}
