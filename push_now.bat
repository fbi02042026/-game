@echo off
rem ==== Push local commits to GitHub (origin/main) ====
rem Double-click this file. First time it may open a browser to sign in to GitHub.
rem After that Windows remembers the login and this will just work.

cd /d "E:\xiangsumaoxian"

echo.
echo ============================================
echo  Uploading local commits to GitHub...
echo ============================================
echo.

git status -sb
echo.

git push origin main
if errorlevel 1 goto FAIL

echo.
echo ============================================
echo  SUCCESS - remote is now up to date.
echo ============================================
echo.
git --no-pager log --oneline -3
goto END

:FAIL
echo.
echo ============================================
echo  FAILED - nothing was uploaded.
echo  If it asks you to sign in, please complete
echo  the GitHub login in the browser window.
echo ============================================
echo.

:END
echo.
pause
