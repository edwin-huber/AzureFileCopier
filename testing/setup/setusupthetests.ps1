$root=10
& mkdir "test"
$sub = $root
while ($sub -gt 0) {
    
    mkdir .\test\$sub
    $sub = $sub -1;
}