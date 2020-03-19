param(
[int]$testnum,
[string]$target
)
cls

$newdir = "azurecopiertest"+$testnum
$dir = get-childitem -Directory -Path azurecopiertest*
ren $dir $newdir

$exepath = join-path $target "\AzureAsyncCopier.exe"

write-output "COPY CONFIG"
# copy config to latest release.
copy-item -Path .\AzureAsyncCopier.exe.config -Destination $target -Verbose
# analyze
write-output "TESTING Advanced Mode"
& $exepath advanced C:\Code\copiertest\$newdir 3 1

& $exepath advanced C:\Code\copiertest\$newdir 3 2

& $exepath advanced C:\Code\copiertest\$newdir 3 3

write-output "TESTING create folders"
& $exepath folder

write-output "TESTING copy files"
& $exepath file

write-output "TESTING copy large files"
& {$exepath} largefile