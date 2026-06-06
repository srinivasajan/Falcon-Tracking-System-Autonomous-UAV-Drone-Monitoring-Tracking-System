$proc = Start-Process -FilePath "C:\Personal Coding\Projects\Falcon Drone System\DroneControl\DroneControl.UI\bin\Release\net8.0-windows\win-x64\publish\DroneControl.UI.exe" -PassThru
Start-Sleep -Seconds 5

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)

Write-Host "Windows found: $($windows.Count)"
foreach ($w in $windows) {
    Write-Host "Window Name: $($w.Current.Name)"
}

Stop-Process -Id $proc.Id -Force
