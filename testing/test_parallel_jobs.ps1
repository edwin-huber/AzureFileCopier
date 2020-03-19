param(
[string]$target
)
Clear-Host
$stopwatch =  [system.diagnostics.stopwatch]::StartNew()

$exepath = join-path $target "\AzureAsyncCopier.exe"
$testPath = "/testcontent"

# analyze
write-output "TESTING ADVANCED MODE"

$jobScriptAdvancedMode = { 
    param ([string]$exe,
    [string]$analyzeMode,
    [string]$path,
    [string]$pathString,
    [string]$advancedMode,
    [string]$batchCount,
    [string]$batchCountVal,
    [string]$batchNum,
    [string]$batchNumVal)
    & $exe $analyzeMode $advancedMode $path $pathString $batchCount $batchCountVal $batchNum $batchNumVal
}

#& $exepath analyze --path $testPath --advanced --batchcount 3 --batchnumber 1
Start-Job -ScriptBlock $jobScriptAdvancedMode -Name "ANALYSIS_ONLY_SMALL_$testnum" -ArgumentList $exepath, "analyze","-p", $testPath, "--advanced", "--batchcount", 3, "--batchnumber", 1

#& $exepath analyze --path $testPath --advanced --batchcount 3 --batchnumber 2
Start-Job -ScriptBlock $jobScriptAdvancedMode -Name "ANALYSIS_ONLY_SMALL_$testnum" -ArgumentList $exepath, "analyze", "-p", $testPath,"--advanced", "--batchcount", 3, "--batchnumber", 2

#& $exepath analyze --path $testPath --advanced --batchcount 3 --batchnumber 3
Start-Job -ScriptBlock $jobScriptAdvancedMode -Name "ANALYSIS_ONLY_SMALL_$testnum" -ArgumentList $exepath, "analyze","-p", $testPath, "--advanced", "--batchcount", 3, "--batchnumber", 3

$jobScriptFolder = { 
    param ([string]$exe,
    [string]$mode)
    & $exe $mode
}

#Write-Output "TESTING create folders"
Start-Job -ScriptBlock $jobScriptFolder -Name "FOLDERJOB1_$testnum" -ArgumentList $exepath, "folder"
Start-Job -ScriptBlock $jobScriptFolder -Name "FOLDERJOB2_$testnum" -ArgumentList $exepath, "folder"
Start-Job -ScriptBlock $jobScriptFolder -Name "FOLDERJOB3_$testnum" -ArgumentList $exepath, "folder"

$jobScriptFile = { 
    param ([string]$exe,
    [string]$mode)
    & $exe $mode
}

#Write-Output "TESTING copy files"
Start-Job -ScriptBlock $jobScriptFile -Name "FILEJOB1_$testnum" -ArgumentList $exepath, "file"
Start-Job -ScriptBlock $jobScriptFile -Name "FILEJOB2_$testnum" -ArgumentList $exepath, "file"

$jobScriptLargeFile = { 
    param ([string]$exe,
    [string]$mode,
    [string]$large)
    & $exe $mode $large
}

#write-output "TESTING copy large files"
Start-Job -ScriptBlock $jobScriptLargeFile -Name "LARGEFILEJOB_$testnum" -ArgumentList $exepath,"file", "-l"

Get-Job | Wait-Job
Get-Job | Receive-Job | Select-String "error" | Select-Object -ExpandProperty Line
Write-Output "If there were no errors, test was good! :D"
Get-Job | Remove-Job

#### END TEST ####
$stopwatch.Stop()
$totalSecs =  $stopwatch.Elapsed.TotalSeconds

Write-Output "Test SMALL ANALYSIS complete after [$totalSecs] seconds..."