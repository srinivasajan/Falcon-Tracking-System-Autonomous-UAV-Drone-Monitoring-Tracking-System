$ErrorActionPreference = "Stop"
$installedExePath = "$env:LOCALAPPDATA\Falcon Drone System\DroneControl.UI.exe"

Stop-Process -Name DroneControl.UI -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

$process = Start-Process -FilePath $installedExePath -PassThru
Start-Sleep -Seconds 5

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$windowCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "Falcon Drone System")
$window = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children, $windowCond)

if ($window) {
    $btnCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "Run Demo")
    $btn = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
    
    if ($btn) {
        $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        $invokePattern.Invoke()
        
        Write-Host "Waiting 5 seconds for target detection..."
        Start-Sleep -Seconds 5
        
        $rect = $window.Current.BoundingRectangle
        $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bmp)
        $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
        $bmp.Save("C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\trace_detect.png", [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bmp.Dispose()
        
        Write-Host "Waiting 5 seconds for target lock..."
        Start-Sleep -Seconds 5
        
        $rect = $window.Current.BoundingRectangle
        $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bmp)
        $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
        $bmp.Save("C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\trace_lock.png", [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bmp.Dispose()
        
        Write-Host "Waiting 5 seconds for auto pursuit..."
        Start-Sleep -Seconds 5
        
        $rect = $window.Current.BoundingRectangle
        $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bmp)
        $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
        $bmp.Save("C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\trace_pursuit.png", [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bmp.Dispose()
    }
}
Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
