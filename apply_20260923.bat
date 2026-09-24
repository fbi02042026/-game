@echo off
rem ==== One-click apply 2026-09-23 (reward popup layout + daily log) ====
rem Staged files are copied over the real ones by YOUR explorer session,
rem which is not affected by the sandbox write block.

echo [1/3] RewardPopupUI.cs ...
copy /y "Y:\PixelAdventureTown\.workbuddy\tmp_fixed\RewardPopupUI.cs" "Y:\PixelAdventureTown\Assets\Scripts\UI\RewardPopupUI.cs"

echo [2/3] RewardPopupBuilder.cs ...
copy /y "Y:\PixelAdventureTown\.workbuddy\tmp_fixed\RewardPopupBuilder.cs" "Y:\PixelAdventureTown\Assets\Editor\RewardPopupBuilder.cs"

echo [3/3] daily log 2026-09-23.md ...
copy /y "Y:\PixelAdventureTown\.workbuddy\tmp_fixed\2026-09-23.md" "Y:\PixelAdventureTown\.workbuddy\memory\2026-09-23.md"

echo.
echo Done. Back in Tuanjie: wait for recompile, then run Tools/reward-popup/build-prefab once.
pause
