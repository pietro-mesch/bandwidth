param($installPath, $toolsPath, $package, $project)
# Always copy the gurobi files to \bin\debug
#win64

$grbDll = $project.ProjectItems.Item("gurobi75.dll")
if ($grbDll)
{
	$copyToOutput = $grbDll.Properties.Item("CopyToOutputDirectory")
	$copyToOutput.Value = 2
	$buildAction = $grbDll.Properties.Item("BuildAction")
	$buildAction.Value = 2
}

#.net
$grbDll = $project.ProjectItems.Item("Gurobi75.NET.XML")
if ($grbDll)
{
	$copyToOutput = $grbDll.Properties.Item("CopyToOutputDirectory")
	$copyToOutput.Value = 2
	$buildAction = $grbDll.Properties.Item("BuildAction")
	$buildAction.Value = 2
}

$grbDll = $project.ProjectItems.Item("gurobi.lic")
if ($grbDll)
{
	$copyToOutput = $grbDll.Properties.Item("CopyToOutputDirectory")
	$copyToOutput.Value = 2
	$buildAction = $grbDll.Properties.Item("BuildAction")
	$buildAction.Value = 2
}
# open landing page
[System.Diagnostics.Process]::Start("https://www.gurobi.com/partners/optano")