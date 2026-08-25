# 全量修复 Assets 下损坏的 .meta GUID，并同步重写所有引用。
# 损坏形态：guid 不是 32 位 hex（常见为 56 字符 base64，含 /+=）——团结 Codely 等导致。
# 用法：powershell -ExecutionPolicy Bypass -File Tools/FixAllCorruptedGuids.ps1

$ErrorActionPreference = "Stop"
$assetsRoot = "Y:\PixelAdventureTown\Assets"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

function Test-ValidGuid([string]$g) {
    return ($g -match '^[0-9a-fA-F]{32}$')
}

Write-Host "Scanning metas..."
$map = @{}  # oldGuid -> newGuid
$metaFiles = Get-ChildItem $assetsRoot -Recurse -Filter "*.meta" -File
$scanned = 0
foreach ($mf in $metaFiles) {
    $scanned++
    $raw = [System.IO.File]::ReadAllText($mf.FullName)
    $m = [regex]::Match($raw, '(?m)^guid:\s*(.+)$')
    if (-not $m.Success) { continue }
    $old = $m.Groups[1].Value.Trim()
    if (Test-ValidGuid $old) { continue }
    if ($map.ContainsKey($old)) {
        $newGuid = $map[$old]
    } else {
        $newGuid = [guid]::NewGuid().ToString("N")
        $map[$old] = $newGuid
    }
    $raw2 = [regex]::Replace($raw, '(?m)^guid:.*$', "guid: $newGuid", 1)
    [System.IO.File]::WriteAllText($mf.FullName, $raw2, $utf8NoBom)
}

Write-Host "metasScanned=$scanned corruptedUnique=$($map.Count)"

if ($map.Count -eq 0) {
    Write-Host "No corrupted GUIDs. Done."
    exit 0
}

# 长旧 guid 先替换，避免短串误伤（本批均为定长，仍按长度降序）
$ordered = $map.GetEnumerator() | Sort-Object { $_.Key.Length } -Descending

Write-Host "Rewriting references across Assets (text files)..."
$extOk = @(
    '.meta', '.prefab', '.unity', '.asset', '.mat', '.controller', '.anim',
    '.overrideController', '.physicMaterial', '.physicsMaterial2D', '.guiskin',
    '.mask', '.playable', '.mixer', '.spriteatlas', '.spriteatlasv2',
    '.shader', '.shadergraph', '.shadersubgraph', '.compute', '.cginc', '.hlsl',
    '.uss', '.uxml', '.json', '.txt', '.md', '.csv', '.xml', '.yaml', '.yml',
    '.inputactions', '.asmdef', '.asmref', '.cs'
)

$refFiles = Get-ChildItem $assetsRoot -Recurse -File | Where-Object {
    $extOk -contains $_.Extension.ToLowerInvariant()
}

$filesTouched = 0
$replacements = 0
foreach ($f in $refFiles) {
    # 跳过过大文件（>8MB）防误伤二进制误判为文本
    if ($f.Length -gt 8MB) { continue }
    $text = [System.IO.File]::ReadAllText($f.FullName)
    $orig = $text
    foreach ($kv in $ordered) {
        if ($text.Contains($kv.Key)) {
            $text = $text.Replace($kv.Key, $kv.Value)
            $replacements++
        }
    }
    if ($text -ne $orig) {
        [System.IO.File]::WriteAllText($f.FullName, $text, $utf8NoBom)
        $filesTouched++
    }
}

# 映射表落盘，便于复查
$mapPath = "Y:\PixelAdventureTown\Tools\guid-remap-last.json"
$jsonObj = [ordered]@{}
foreach ($kv in ($map.GetEnumerator() | Sort-Object Key)) {
    $jsonObj[$kv.Key] = $kv.Value
}
($jsonObj | ConvertTo-Json -Depth 2) | Set-Content -Path $mapPath -Encoding UTF8

Write-Host "filesTouched=$filesTouched replacementOps=$replacements"
Write-Host "remapSaved=$mapPath"
Write-Host "DONE. Please Unity Refresh / Reimport, then verify Heads/EquipIcons/VFX."
