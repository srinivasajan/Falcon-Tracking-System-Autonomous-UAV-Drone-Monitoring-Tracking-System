Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process DroneControl.UI -ErrorAction SilentlyContinue
if (!$proc) {
    Write-Host "DroneControl.UI is not running."
    exit
}

$processId = $proc.Id
$desktop = [System.Windows.Automation.AutomationElement]::RootElement

$condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
$windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)

Write-Host "Windows for PID $processId : $($windows.Count)"
foreach ($w in $windows) {
    Write-Host "Window: $($w.Current.Name)"
    
    function DumpTree($element, $indent) {
        try {
            $name = $element.Current.Name
            $type = $element.Current.LocalizedControlType
            Write-Host "$indent- $type : $name"
            
            $children = $element.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
            foreach ($c in $children) {
                DumpTree $c "$indent  "
            }
        } catch {}
    }
    
    DumpTree $w ""
}
