param(
[int]$testnum,
[string]$target
)
cls
$stopwatch =  [system.diagnostics.stopwatch]::StartNew()

$newdir = "azurecopiertest"+$testnum
$dir = get-childitem -Directory -Path azurecopiertest*
ren $dir $newdir

$exepath = join-path $target "\AzureAsyncCopier.exe"

$testPath = Join-Path C:\code\copiertest $newdir
$testPath = Join-Path $testPath \CognitiveBot

write-output "COPYING CONFIG"
# copy config to latest release.
copy-item -Path .\AzureAsyncCopier.exe.config -Destination $target -Verbose
# analyze
write-output "TESTING ANALYSIS MODE ONLY"


$jobScript = { 
    param (
    [string]$exe,
    [string]$mode,
    [string]$testPath
    )
    & $exe $mode $testPath
}

#Write-Output "TESTING create folders"
# Start-Job -ScriptBlock $jobScript -Name "ANALYSIS_ONLY_SMALL_$testnum" -ArgumentList $exepath, "analyze", $testPath

& $exepath analyze --path $testPath

write-output "TESTING ANALYSIS QUIET MODE"
& $exepath analyze --path $testPath --quietmode

Get-Job | Wait-Job
Get-Job | Receive-Job | Select-String "error" | Select-Object -ExpandProperty Line
Write-Output "If there were no errors, test was good! :D"
Get-Job | Remove-Job
$stopwatch.Stop()
$totalSecs =  $stopwatch.Elapsed.TotalSeconds

Write-Output "Test complete after [$totalSecs] seconds..."

