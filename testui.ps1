Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$children = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)

foreach ($child in $children) {
    if ($child.Current.Name -eq "Provider error") {
        Write-Host "Found Provider error window! PID: $($child.Current.ProcessId)"
        
        $textCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
        $textElements = $child.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCondition)
        foreach ($text in $textElements) {
            Write-Host "Text: $($text.Current.Name)"
        }
        
        $rect = $child.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
            $graphics = [System.Drawing.Graphics]::FromImage($bmp)
            $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
            $bmp.Save("C:\Personal Coding\Projects\Falcon Drone System\error_screenshot.png", [System.Drawing.Imaging.ImageFormat]::Png)
            $graphics.Dispose()
            $bmp.Dispose()
            Write-Host "Saved error_screenshot.png"
        }
    }
}
