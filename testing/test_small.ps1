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
write-output "TESTING Advanced Mode"
& $exepath advanced $testPath 3 1

& $exepath advanced $testPath 3 2

& $exepath advanced $testPath 3 3

write-output "TESTING create folders"
& $exepath folder

write-output "TESTING copy files"
& $exepath file

write-output "TESTING copy large files"
& $exepath largefile

$stopwatch.Stop()
$totalSecs =  $stopwatch.Elapsed.TotalSeconds

Write-Output "Test complete after [$totalSecs] seconds..."

