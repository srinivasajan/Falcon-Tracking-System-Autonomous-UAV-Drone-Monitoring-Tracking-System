Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$processId$processId = 27160
$desktop = [System.Windows.Automation.AutomationElement]::RootElement

$condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
$windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)

Write-Host "Windows for PID $processId : $($windows.Count)"
foreach ($w in $windows) {
    Write-Host "Window: $($w.Current.Name)"
    
    # Dump tree
    function DumpTree($element, $indent) {
        $name = $element.Current.Name
        $type = $element.Current.LocalizedControlType
        Write-Host "$indent- $type : $name"
        
        $children = $element.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($c in $children) {
            DumpTree $c "$indent  "
        }
    }
    
    DumpTree $w ""
}
