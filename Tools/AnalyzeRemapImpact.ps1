$ErrorActionPreference = "Continue"
$j = Get-Content "Y:\PixelAdventureTown\Tools\guid-remap-last.json" -Raw | ConvertFrom-Json
$newSet = @{}
foreach ($p in $j.PSObject.Properties) { $newSet[$p.Value] = $true }

$prefab = Get-ChildItem "Y:\PixelAdventureTown\Assets" -Recurse -Filter "*GuildHall*.prefab" -ErrorAction SilentlyContinue | Select-Object -First 3
if (-not $prefab) {
    $prefab = Get-ChildItem "Y:\PixelAdventureTown\Assets\Resources" -Recurse -Filter "*.prefab" | Select-Object -First 5
}
foreach ($pf in $prefab) {
    Write-Host "FILE $($pf.FullName)"
    Select-String -Path $pf.FullName -Pattern "m_Script: \{fileID: 11500000, guid: ([0-9a-fA-F]{32})" | Select-Object -First 10 | ForEach-Object {
        $g = $_.Matches[0].Groups[1].Value
        Write-Host "  guid=$g remapped=$($newSet.ContainsKey($g))"
    }
}

# Does Library exist with asset database?
Write-Host "LibraryExists=$((Test-Path 'Y:\PixelAdventureTown\Library'))"
