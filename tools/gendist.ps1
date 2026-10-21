# Configuration de l'encodage
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['*:Encoding'] = 'UTF8'

if ($args.Count -gt 0) {
    $target = $args[0]
} else {
    $target = "win-x64"
}
$source1 = "./src/bg-nodus-broker/dist/$target"
$source2 = "./src/nodus-broker/dist/$target"
$destination = "./dist/$target"

New-Item -ItemType Directory -Path $destination -Force | Out-Null
Write-Host "Fusion des distributions " -NoNewline
Copy-Item "$source2\*" $destination -Recurse -Force
Copy-Item "$source1\*" $destination -Recurse -Force
