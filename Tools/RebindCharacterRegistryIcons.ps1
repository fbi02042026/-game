$ErrorActionPreference = "Stop"
$art = "Y:\PixelAdventureTown\Assets\Art\UI\Icons\Heads"
$map = @{}
Get-ChildItem $art -Filter "icon_*.png.meta" | ForEach-Object {
    $file = $_.Name.Replace(".meta", "").Replace(".png", "")
    $id = $file
    if ($id.StartsWith("icon_")) { $id = $id.Substring(5) }
    $guidLine = Get-Content $_.FullName -TotalCount 2 | Where-Object { $_ -match "^guid:" }
    $guid = ($guidLine -replace "^guid:\s*", "").Trim()
    if ($guid -match "^[0-9a-fA-F]{32}$") {
        $map[$id] = $guid
        Write-Host "$id=$guid"
    }
}

$reg = "Y:\PixelAdventureTown\Assets\Resources\Config\CharacterRegistry.asset"
$lines = Get-Content $reg
$out = New-Object System.Collections.Generic.List[string]
$curId = $null
foreach ($line in $lines) {
    if ($line -match "^\s*- characterId:\s*(\S+)") { $curId = $Matches[1] }
    if ($line -match "^\s*iconSprite:" -and $curId -ne $null -and $map.ContainsKey($curId)) {
        [void]$out.Add("    iconSprite: {fileID: 21300000, guid: $($map[$curId]), type: 3}")
    }
    else {
        [void]$out.Add($line)
    }
}
[System.IO.File]::WriteAllLines($reg, $out.ToArray(), [System.Text.UTF8Encoding]::new($false))
Write-Host "registryUpdated keys=$($map.Count)"
