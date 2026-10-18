param([long]$WindowHandle)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$element = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]::new($WindowHandle))
$name = $element.Current.Name
$children = $element.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
@{name=$name; childCount=$children.Count; handle=$WindowHandle; interacted=$true} | ConvertTo-Json
