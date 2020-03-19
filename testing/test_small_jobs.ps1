param(
[int]$testnum,
[string]$target
)
cls
$stopwatch =  [system.diagnostics.stopwatch]::StartNew()

$newdir = "testcontent"+$testnum
$dir = get-childitem -Directory -Path azurecopiertest*
ren $dir $newdir

$exepath = join-path $target "\AzureAsyncCopier.exe"

$testPath = Join-Path C:\code\copiertest $newdir
$testPath = Join-Path $testPath \CognitiveBot

write-output "COPYING CONFIG"
# copy config to latest release.
copy-item -Path .\AzureAsyncCopier.exe.config -Destination $target -Verbose
# analyze
write-output "TESTING Advanced Mode"


#Start-Job -ScriptBlock { & $exepath advanced $testPath 3 1 }
$jobScriptAdvancedMode = { 
    param ([string]$exe,
    [string]$advancedMode,
    [string]$path,
    [string]$batchCount,
    [string]$batchNum)
    & $exe $advancedMode $path $batchCount $batchNum
}
Start-Job -ScriptBlock $jobScriptAdvancedMode -Name "JOB1_$testnum" -ArgumentList $exepath, "advanced", $testPath, 3, 1
Start-Job -ScriptBlock $jobScriptAdvancedMode -Name "JOB2_$testnum" -ArgumentList $exepath, "advanced", $testPath, 3, 2
Start-Job -ScriptBlock $jobScriptAdvancedMode -Name "JOB2_$testnum" -ArgumentList $exepath, "advanced", $testPath, 3, 3

$jobScript = { 
    param ([string]$exe,
    [string]$mode)
    & $exe $mode
}

#Write-Output "TESTING create folders"
Start-Job -ScriptBlock $jobScript -Name "FOLDERJOB1_$testnum" -ArgumentList $exepath, "folder"
Start-Job -ScriptBlock $jobScript -Name "FOLDERJOB2_$testnum" -ArgumentList $exepath, "folder"
Start-Job -ScriptBlock $jobScript -Name "FOLDERJOB3_$testnum" -ArgumentList $exepath, "folder"

#Write-Output "TESTING copy files"
Start-Job -ScriptBlock $jobScript -Name "FILEJOB1_$testnum" -ArgumentList $exepath, "file"
Start-Job -ScriptBlock $jobScript -Name "FILEJOB2_$testnum" -ArgumentList $exepath, "file"

#write-output "TESTING copy large files"
Start-Job -ScriptBlock $jobScript -Name "LARGEFILEJOB_$testnum" -ArgumentList $exepath, "largefile"

Get-Job | Wait-Job
Get-Job | Receive-Job | Select-String "error" | Select-Object -ExpandProperty Line
Write-Output "If there were no errors, test was good! :D"
Get-Job | Remove-Job
$stopwatch.Stop()
$totalSecs =  $stopwatch.Elapsed.TotalSeconds

Write-Output "Test complete after [$totalSecs] seconds..."

