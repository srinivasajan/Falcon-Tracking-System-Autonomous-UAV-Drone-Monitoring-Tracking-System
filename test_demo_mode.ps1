Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$exePath = "C:\Personal Coding\Projects\Falcon Drone System\DroneControl\DroneControl.UI\bin\Release\net8.0-windows\win-x64\publish\DroneControl.UI.exe"
$proc = Start-Process -FilePath $exePath -PassThru

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$windowCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "DroneControl")

$window = $null
for ($i = 0; $i -lt 10; $i++) {
    Start-Sleep -Seconds 2
    $window = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children, $windowCond)
    if ($window) { break }
}

if ($window) {
    Write-Host "Window found. Clicking Run Demo Mode..."
    $btnCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "Run Demo Mode")
    $btn = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
    if ($btn) {
        $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        $invokePattern.Invoke()
        Write-Host "Demo Mode started! Waiting 12 seconds for events to populate..."
        Start-Sleep -Seconds 12
        
        Write-Host "`n--- UI Text Dump ---"
        $textCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
        $texts = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCond)
        foreach ($t in $texts) {
            $name = $t.Current.Name
            if ($name -match "Demo:|Locked|Target|Telemetry|Pursuit") {
                Write-Host $name
            }
        }
        Write-Host "--------------------"
    } else {
        Write-Host "Run Demo Mode button not found!"
    }
} else {
    Write-Host "Main window not found!"
}

Stop-Process -Name DroneControl.UI -Force -ErrorAction SilentlyContinue
