$ErrorActionPreference = "Continue"
$bmMeta = (Get-Content "Y:\PixelAdventureTown\Assets\Scripts\Managers\BattleManager.cs.meta" -TotalCount 2 | Where-Object { $_ -match "^guid:" })
$bmGuid = ($bmMeta -replace "^guid:\s*","").Trim()
Write-Host "BattleManagerGuid=$bmGuid"

$j = Get-Content "Y:\PixelAdventureTown\Tools\guid-remap-last.json" -Raw | ConvertFrom-Json
$old = $null
foreach ($p in $j.PSObject.Properties) {
    if ($p.Value -eq $bmGuid) { $old = $p.Name; break }
}
Write-Host "oldEncrypted=$old"

# How many prefabs reference the NEW guid vs look for any m_Script with missing?
$newHits = 0
$sample = @()
Get-ChildItem "Y:\PixelAdventureTown\Assets" -Recurse -Include *.prefab,*.unity,*.asset -File | ForEach-Object {
    $t = [System.IO.File]::ReadAllText($_.FullName)
    if ($t.Contains($bmGuid)) {
        $script:newHits++
        if ($sample.Count -lt 5) { $script:sample += $_.FullName }
    }
}
Write-Host "prefabsReferencingNewGuid=$newHits"
$sample | ForEach-Object { Write-Host "  $_" }

# If old encrypted existed in files before fix - now should be 0
if ($old) {
    $oldHits = 0
    Get-ChildItem "Y:\PixelAdventureTown\Assets" -Recurse -Include *.prefab,*.unity,*.asset -File | ForEach-Object {
        if ([System.IO.File]::ReadAllText($_.FullName).Contains($old)) { $script:oldHits++ }
    }
    Write-Host "prefabsStillHaveOldEncrypted=$oldHits"
}

# Count script metas that were remapped (Scripts folder)
$scriptRemap = 0
Get-ChildItem "Y:\PixelAdventureTown\Assets\Scripts" -Recurse -Filter "*.cs.meta" | ForEach-Object {
    $g = ((Get-Content $_.FullName -TotalCount 2 | Where-Object { $_ -match "^guid:" }) -replace "^guid:\s*","").Trim()
    foreach ($p in $j.PSObject.Properties) {
        if ($p.Value -eq $g) { $script:scriptRemap++; break }
    }
}
Write-Host "scriptMetasInRemap=$scriptRemap"
