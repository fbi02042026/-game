$ErrorActionPreference = "Continue"
# Find what guid prefabs use for BattleManager by searching known scene/prefab that should have it
$candidates = @(
  "Y:\PixelAdventureTown\Assets\Scenes",
  "Y:\PixelAdventureTown\Assets\Resources\Prefabs",
  "Y:\PixelAdventureTown\Assets\Prefabs"
)
# Look for MonoBehaviour blocks near 'BattleManager' name is rare; instead sample a battle scene for m_Script lines
$scene = Get-ChildItem "Y:\PixelAdventureTown\Assets" -Recurse -Filter "*.unity" | Select-Object -First 5
foreach ($s in $scene) {
    Write-Host "SCENE $($s.FullName)"
    Select-String -Path $s.FullName -Pattern "m_Script: \{fileID: 11500000, guid:" | Select-Object -First 3 | ForEach-Object { $_.Line.Trim() }
}

# History of BattleManager.cs.meta
Write-Host "=== git log BattleManager.cs.meta ==="
git -C "Y:\PixelAdventureTown" log --oneline -5 -- "Assets/Scripts/Managers/BattleManager.cs.meta"
Write-Host "=== oldest known in git ==="
git -C "Y:\PixelAdventureTown" log --diff-filter=A --pretty=format:%H -- "Assets/Scripts/Managers/BattleManager.cs.meta" | Select-Object -First 1 | ForEach-Object {
    git -C "Y:\PixelAdventureTown" show "${_}:Assets/Scripts/Managers/BattleManager.cs.meta" | Select-Object -First 5
}

# Try a few older commits for hex guid
Write-Host "=== recent commits meta guid ==="
git -C "Y:\PixelAdventureTown" log --oneline -15 | ForEach-Object {
    $hash = ($_ -split ' ')[0]
    $content = git -C "Y:\PixelAdventureTown" show "${hash}:Assets/Scripts/Managers/BattleManager.cs.meta" 2>$null
    if ($content) {
        $g = ($content | Select-String "^guid:" | Select-Object -First 1).ToString()
        Write-Host "$hash $g"
    }
}
