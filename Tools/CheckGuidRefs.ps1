$ErrorActionPreference = "Continue"
Write-Host "=== CharacterRegistry dunbing102 ==="
Select-String -Path "Y:\PixelAdventureTown\Assets\Resources\Config\CharacterRegistry.asset" -Pattern "dunbing102" -Context 0,3 | ForEach-Object { $_.Line; $_.Context.PostContext }
Write-Host "=== icon_dunbing102.meta ==="
Get-Content "Y:\PixelAdventureTown\Assets\Art\UI\Icons\Heads\icon_dunbing102.png.meta" -TotalCount 2
Write-Host "=== equip_sword_1 icon ==="
Select-String -Path "Y:\PixelAdventureTown\Assets\Resources\Config\Equips\equip_sword_1.asset" -Pattern "icon"
Write-Host "=== Sword_1.meta ==="
Get-Content "Y:\PixelAdventureTown\Assets\Resources\UI\EquipIcons\Sword_1.png.meta" -TotalCount 2
Write-Host "=== BattleManager.cs.meta ==="
Get-Content "Y:\PixelAdventureTown\Assets\Scripts\Managers\BattleManager.cs.meta" -TotalCount 2
Write-Host "=== git HEAD Scripts.meta ==="
git -C "Y:\PixelAdventureTown" show "HEAD:Assets/Scripts.meta" | Select-Object -First 5
Write-Host "=== current Scripts.meta ==="
Get-Content "Y:\PixelAdventureTown\Assets\Scripts.meta" -TotalCount 5
Write-Host "=== sample remap entries ==="
$j = Get-Content "Y:\PixelAdventureTown\Tools\guid-remap-last.json" -Raw | ConvertFrom-Json
$props = @($j.PSObject.Properties)
Write-Host "count=$($props.Count)"
$props | Select-Object -First 3 | ForEach-Object { Write-Host "$($_.Name) -> $($_.Value)" }
