$ErrorActionPreference = "Stop"

function Fix-MetaGuid([string]$path) {
    if (-not (Test-Path $path)) { return $false }
    $raw = [System.IO.File]::ReadAllText($path)
    if ($raw -notmatch "(?m)^guid:") { return $false }
    $guidLine = ([regex]::Match($raw, "(?m)^guid:\s*(.+)$")).Groups[1].Value.Trim()
    if ($guidLine -match "^[0-9a-fA-F]{32}$") { return $false }
    $newGuid = [guid]::NewGuid().ToString("N")
    $raw2 = [regex]::Replace($raw, "(?m)^guid:.*$", "guid: $newGuid", 1)
    [System.IO.File]::WriteAllText($path, $raw2, [System.Text.UTF8Encoding]::new($false))
    return $true
}

$art = "Y:\PixelAdventureTown\Assets\Art\UI\Icons\Heads"
$res = "Y:\PixelAdventureTown\Assets\Resources\UI\Heads"
New-Item -ItemType Directory -Force -Path $res | Out-Null

$fixed = 0
Get-ChildItem $art -Filter "*.meta" -ErrorAction SilentlyContinue | ForEach-Object {
    if (Fix-MetaGuid $_.FullName) { $script:fixed++ }
}

# Sync png from Art -> Resources, then regenerate Resources metas with valid GUIDs
Get-ChildItem $art -Filter "icon_*.png" | ForEach-Object {
    $dstPng = Join-Path $res $_.Name
    Copy-Item $_.FullName $dstPng -Force
    $dstMeta = $dstPng + ".meta"
    $srcMeta = $_.FullName + ".meta"
    if (Test-Path $srcMeta) {
        Copy-Item $srcMeta $dstMeta -Force
        # Resources copy must have its own GUID (cannot share with Art)
        $raw = [System.IO.File]::ReadAllText($dstMeta)
        $newGuid = [guid]::NewGuid().ToString("N")
        $raw2 = [regex]::Replace($raw, "(?m)^guid:.*$", "guid: $newGuid", 1)
        [System.IO.File]::WriteAllText($dstMeta, $raw2, [System.Text.UTF8Encoding]::new($false))
    }
}

Write-Host "fixedArtMeta=$fixed"
Write-Host "resPngCount=$((Get-ChildItem $res -Filter 'icon_*.png').Count)"
Write-Host "sampleArt=$((Get-Content (Join-Path $art 'icon_dunbing102.png.meta') -TotalCount 2)[1])"
Write-Host "sampleRes=$((Get-Content (Join-Path $res 'icon_dunbing102.png.meta') -TotalCount 2)[1])"
