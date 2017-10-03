param($installPath, $toolsPath, $package, $project)
$project.Object.References | Where-Object { $_.Name -eq 'Optimization.Framework.Contracts' } | ForEach-Object { $_.Remove() }

# Always copy the Mip CL C++ wrapper to \bin\debug
$mipclDll = $project.ProjectItems.Item("MipCL131WrapperCpp.dll")
if ($mipclDll)
{
    $copyToOutput = $mipclDll.Properties.Item("CopyToOutputDirectory")
    $copyToOutput.Value = 2
    $buildAction = $mipclDll.Properties.Item("BuildAction")
    $buildAction.Value = 2
}

# Always copy the Mip CL C++ wrapper to \bin\debug
$mipclDll = $project.ProjectItems.Item("MipCL140WrapperCpp.dll")
if ($mipclDll)
{
    $copyToOutput = $mipclDll.Properties.Item("CopyToOutputDirectory")
    $copyToOutput.Value = 2
    $buildAction = $mipclDll.Properties.Item("BuildAction")
    $buildAction.Value = 2
}