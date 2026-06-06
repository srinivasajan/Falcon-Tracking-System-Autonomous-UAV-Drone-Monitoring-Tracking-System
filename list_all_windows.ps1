Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$children = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)

foreach ($child in $children) {
    Write-Host "Name: '$($child.Current.Name)' | Class: '$($child.Current.ClassName)' | PID: $($child.Current.ProcessId)"
}
