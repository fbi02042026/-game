$meta = "Y:\PixelAdventureTown\Assets\Resources\VFX\other\world\传送.prefab.meta"
$g = [guid]::NewGuid().ToString("N")
$raw = [IO.File]::ReadAllText($meta)
$raw2 = [regex]::Replace($raw, "(?m)^guid:.*$", "guid: $g", 1)
[IO.File]::WriteAllText($meta, $raw2)
Write-Host "newGuid=$g"
Write-Host "worldVfx:"
Get-ChildItem "Y:\PixelAdventureTown\Assets\Resources\VFX\other\world" | ForEach-Object { $_.Name }
Write-Host "artOther:"
Get-ChildItem "Y:\PixelAdventureTown\Assets\Art\UI\other" -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }
