$ErrorActionPreference = "Stop"
$outputLog = "C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\verification_results.txt"

Function LogMessage($msg) {
    Write-Host $msg
    Add-Content -Path $outputLog -Value $msg
}

if (Test-Path $outputLog) { Remove-Item $outputLog }

$pubExePath = "C:\Personal Coding\Projects\Falcon Drone System\DroneControl\DroneControl.UI\bin\Release\net8.0-windows\win-x64\publish\DroneControl.UI.exe"
$installerPath = "C:\Personal Coding\Projects\Falcon Drone System\Installer\Output\FalconDroneSystem_v1.0_Setup.exe"
$installedExePath = "$env:LOCALAPPDATA\Falcon Drone System\DroneControl.UI.exe"

$pubExe = Get-Item $pubExePath
$instExe = Get-Item $installerPath
$installedExe = Get-Item $installedExePath

LogMessage "=== Filesystem Verification ==="
LogMessage "1. Latest published EXE: $($pubExe.FullName) | Timestamp: $($pubExe.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"
LogMessage "2. Latest installer: $($instExe.FullName) | Timestamp: $($instExe.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"
LogMessage "3. Installed EXE: $($installedExe.FullName) | Timestamp: $($installedExe.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExePath)
LogMessage "Installed EXE File Version: $($versionInfo.FileVersion)"

# Ensure no existing processes
Stop-Process -Name DroneControl.UI -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

LogMessage "`n=== Launching Application ==="
$process = Start-Process -FilePath $installedExePath -PassThru
Start-Sleep -Seconds 4

$procDetail = Get-Process -Id $process.Id
LogMessage "Running Process ID: $($procDetail.Id) | StartTime: $($procDetail.StartTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$windowCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "Falcon Drone System")
$window = $desktop.FindFirst([System.Windows.Automation.TreeScope]::Children, $windowCond)

if ($window) {
    $rect = $window.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
    
    $landingImgPath = "C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\fresh_landing.png"
    $bmp.Save($landingImgPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bmp.Dispose()
    
    $landingFile = Get-Item $landingImgPath
    LogMessage "Landing Screenshot Path: $($landingFile.FullName) | Timestamp: $($landingFile.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"
    
    $btnCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "Run Demo")
    $btn = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
    
    if ($btn) {
        $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
        $invokePattern.Invoke()
        
        Start-Sleep -Seconds 8
        
        $rect = $window.Current.BoundingRectangle
        $bmp2 = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
        $graphics2 = [System.Drawing.Graphics]::FromImage($bmp2)
        $graphics2.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp2.Size)
        
        $demoImgPath = "C:\Users\srini\.gemini\antigravity-ide\brain\aed7e014-82a3-40fd-afb6-9eece0cedb42\fresh_demo.png"
        $bmp2.Save($demoImgPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics2.Dispose()
        $bmp2.Dispose()
        
        $demoFile = Get-Item $demoImgPath
        LogMessage "Demo Screenshot Path: $($demoFile.FullName) | Timestamp: $($demoFile.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"
    } else {
        LogMessage "Could not find Run Demo button!"
    }
} else {
    LogMessage "Could not find application window!"
}

Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
LogMessage "Verification complete."
