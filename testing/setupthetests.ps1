$root=8
& mkdir ".\testcontent"
$currentFolder = $PSScriptRoot + "\testcontent"
Write-Output "Working in "$currentFolder

function CreateSubFolders([string]$currentPath, [int]$numberOfChildren ){
    
    Write-Output $currentPathcd
    $sub = $numberOfChildren
    Write-Output "creating "$sub
    while($sub -gt 0){
        & mkdir $currentPath\$sub
        & copy-item $PSScriptRoot\testfile.txt $currentPath\$sub\testfile.txt
        & copy-item $PSScriptRoot\office.jpg $currentPath\$sub\office.jpg
        $newsub = $sub-1
        CreateSubFolders $currentPath\$sub $newsub
        $sub = $sub-1
    }
}

Write-Output "CREATING SUBFOLDERS..."
CreateSubFolders $currentFolder $root

Write-Output "Creating LARGE FILE"
Compress-Archive -Path ./testcontent/5 -DestinationPath $PSScriptRoot\largefile.zip
Compress-Archive -Update -Path ./testcontent/4 -DestinationPath $PSScriptRoot\largefile.zip
Compress-Archive -Update -Path ./testcontent/3 -DestinationPath $PSScriptRoot\largefile.zip
Compress-Archive -Update -Path ./testcontent/2 -DestinationPath $PSScriptRoot\largefile.zip
Compress-Archive -Update -Path ./testcontent/1 -DestinationPath $PSScriptRoot\largefile.zip

Write-Output "COPYING LARGE FILE..."
& xcopy $PSScriptRoot\largefile.zip $currentFolder\5\