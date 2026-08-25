$ErrorActionPreference = "Continue"
$bad = 0
$ok = 0
Get-ChildItem "Y:\PixelAdventureTown\Assets" -Recurse -Filter "*.meta" | ForEach-Object {
    $raw = Get-Content $_.FullName -TotalCount 5 -ErrorAction SilentlyContinue
    $line = $raw | Where-Object { $_ -match "^guid:" } | Select-Object -First 1
    if (-not $line) { return }
    $g = ($line -replace "^guid:\s*", "").Trim()
    if ($g -match "^[0-9a-fA-F]{32}$") { $script:ok++ }
    else {
        $script:bad++
        if ($script:bad -le 8) { Write-Host "BAD $($_.FullName) => $g" }
    }
}
Write-Host "ok=$ok bad=$bad"

# Find leftover base64-looking guids in asset yaml
$leftover = 0
Get-ChildItem "Y:\PixelAdventureTown\Assets" -Recurse -Include *.prefab,*.unity,*.asset,*.mat,*.controller -File -ErrorAction SilentlyContinue | ForEach-Object {
    $c = [System.IO.File]::ReadAllText($_.FullName)
    if ($c -match "guid: [A-Za-z0-9+/]{40,}=") {
        $script:leftover++
        if ($script:leftover -le 10) { Write-Host "REF $($_.FullName)" }
    }
}
Write-Host "leftoverRefFiles=$leftover"

if (Test-Path "Y:\PixelAdventureTown\Tools\guid-remap-last.json") {
    $j = Get-Content "Y:\PixelAdventureTown\Tools\guid-remap-last.json" -Raw | ConvertFrom-Json
    Write-Host "remapKeys=$($j.PSObject.Properties.Count)"
}
