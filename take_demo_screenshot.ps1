Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

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
        Write-Host "Waiting for demo to fully initialize..."
        Start-Sleep -Seconds 8
        
        $rect = $window.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
            $graphics = [System.Drawing.Graphics]::FromImage($bmp)
            $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
            $bmp.Save("C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\demo_mode_evidence.png", [System.Drawing.Imaging.ImageFormat]::Png)
            $graphics.Dispose()
            $bmp.Dispose()
            Write-Host "Saved demo_mode_evidence.png"
        }
    }
}

Stop-Process -Name DroneControl.UI -Force -ErrorAction SilentlyContinue
