$ErrorActionPreference = "Stop"

# A. Uninstall existing if possible
Write-Host "Uninstalling old application..."
$uninstallPath = "$env:LOCALAPPDATA\Falcon Drone System\unins000.exe"
if (Test-Path $uninstallPath) {
    Start-Process -FilePath $uninstallPath -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES" -Wait
    Start-Sleep -Seconds 2
}

# B. Install the new setup
Write-Host "Installing new application..."
$installerPath = "C:\Personal Coding\Projects\Falcon Drone System\Installer\Output\FalconDroneSystem_v1.0_Setup.exe"
Start-Process -FilePath $installerPath -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES" -Wait
Start-Sleep -Seconds 2

# C. Launch the installed app and verify
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$installedExe = "$env:LOCALAPPDATA\Falcon Drone System\DroneControl.UI.exe"
Write-Host "Launching installed EXE: $installedExe"
Start-Process -FilePath $installedExe -PassThru

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$windowCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "Falcon Drone System")

$window = $null
for ($i = 0; $i -lt 15; $i++) {
    Start-Sleep -Seconds 2
    $window = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children, $windowCond)
    if ($window) { break }
}

if ($window) {
    Write-Host "Window found. Taking landing screen screenshot..."
    Start-Sleep -Seconds 2
    $rect = $window.Current.BoundingRectangle
    if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
        $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bmp)
        $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
        $bmp.Save("C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\installed_landing.png", [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bmp.Dispose()
        Write-Host "Saved installed_landing.png"
    }

    $btnCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "Run Demo")
    $btn = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
    if ($btn) {
        Write-Host "Clicking Run Demo..."
        $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        $invokePattern.Invoke()
        
        Write-Host "Waiting for demo to reach Pursuit stage (approx 8 seconds)..."
        Start-Sleep -Seconds 8
        
        $rect = $window.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
            $graphics = [System.Drawing.Graphics]::FromImage($bmp)
            $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
            $bmp.Save("C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\installed_demo_running.png", [System.Drawing.Imaging.ImageFormat]::Png)
            $graphics.Dispose()
            $bmp.Dispose()
            Write-Host "Saved installed_demo_running.png"
        }
        
        Write-Host "Waiting for demo to complete (approx 8 seconds)..."
        Start-Sleep -Seconds 8
        
        $rect = $window.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
            $graphics = [System.Drawing.Graphics]::FromImage($bmp)
            $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
            $bmp.Save("C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\installed_demo_complete.png", [System.Drawing.Imaging.ImageFormat]::Png)
            $graphics.Dispose()
            $bmp.Dispose()
            Write-Host "Saved installed_demo_complete.png"
        }
    } else {
        Write-Host "Could not find Run Demo button."
    }
} else {
    Write-Host "Main window not found!"
}

Stop-Process -Name DroneControl.UI -Force -ErrorAction SilentlyContinue
Write-Host "Test complete."
