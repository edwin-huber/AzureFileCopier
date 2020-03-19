param(
[string]$target
)
Clear-Host
$stopwatch =  [system.diagnostics.stopwatch]::StartNew()

$exepath = join-path $target "\AzureAsyncCopier.exe"

$testPath = get-childitem -Directory -Path testconten*

write-output "COPYING CONFIG"
# copy config to latest release.
copy-item -Path .\AzureAsyncCopier.exe.config -Destination $target -Verbose
# analyze
write-output "TESTING ANALYSIS MODE"

& $exepath analyze --path $testPath

$stopwatch.Stop()
$totalSecs =  $stopwatch.Elapsed.TotalSeconds

Write-Output "Test ANALYSIS complete after [$totalSecs] seconds..."

write-output "TESTING FOLDERCOPY MODE - SUBFOLDER ALSO"

$stopwatch =  [system.diagnostics.stopwatch]::StartNew()
& $exepath folder --destinationsubfolder mysub --pathtoremove "newtests\testing\testcontent"
$stopwatch.Stop()
$totalSecs =  $stopwatch.Elapsed.TotalSeconds

Write-Output "Test complete after [$totalSecs] seconds..."

write-output "TESTING FOLDERCOPY MODE - SUBFOLDER ALSO"

$stopwatch =  [system.diagnostics.stopwatch]::StartNew()
& $exepath file --destinationsubfolder mysub --pathtoremove "REPO\AzureAsyncFileCopier\testing\testcontent"
$stopwatch.Stop()
$totalSecs =  $stopwatch.Elapsed.TotalSeconds

Write-Output "Test complete after [$totalSecs] seconds..."