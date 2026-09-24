@echo off
rem ==== Pull remote (boss variant commit) and push local 377d8c3f ====
rem Reason: agent-spawned git cannot write working-tree files here (sandbox),
rem so this runs in YOUR explorer session where writes are allowed.

cd /d "Y:\PixelAdventureTown"

echo [1/4] stash local Boss test switches in WavePlanner.cs ...
git stash push -m "boss-test-switch-20260924" -- "Assets/Scripts/Combat/WavePlanner.cs"

echo [2/4] pull --rebase origin main ...
git pull --rebase origin main
if errorlevel 1 goto FAIL

echo [3/4] push origin main ...
git push origin main
if errorlevel 1 goto FAIL

echo [4/4] done. Local test switches are still in the stash (git stash list).
git --no-pager log --oneline -4
echo.
echo NOTE: run "git stash pop" later if you want the Boss test switches back.
goto END

:FAIL
echo === Something failed, nothing was force-pushed. Current status: ===
git status -sb
echo If rebase conflicted: fix the file, then "git rebase --continue",
echo or abort with "git rebase --abort".

:END
pause
