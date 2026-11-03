@echo off
rem ============================================================
rem  EditMode Test Runner (CLI) - BACKUP ONLY
rem  ------------------------------------------------------------
rem  STATUS: UNVERIFIED ON THIS MACHINE.
rem  Same known risk as build_android.bat: batchmode may HANG
rem  on this machine (2026-09-27, 3 attempts, root cause unknown).
rem
rem  ==> PREFERRED WAY: run tests inside the Tuanjie editor:
rem        Window -> General -> Test Runner -> EditMode -> Run All
rem
rem  Usage: double-click this file, or run from terminal.
rem  Results XML goes to Builds\editmode-test-results.xml
rem ============================================================

setlocal

set PROJECT=E:\xiangsumaoxian
set EDITOR=E:\Program Files\Tuanjie\Hub\Editor\2022.3.62t16\Editor\Tuanjie.exe
set LOG=%PROJECT%\Builds\editmode-test.log
set RESULT=%PROJECT%\Builds\editmode-test-results.xml

echo.
echo ============================================
echo  EditMode Test Runner (CLI, backup only)
echo  NOTE: may HANG on this machine - see header
echo ============================================
echo.

if not exist "%EDITOR%" (
  echo [ERROR] Tuanjie editor not found:
  echo   %EDITOR%
  goto FAIL
)

tasklist /FI "IMAGENAME eq Tuanjie.exe" 2>nul | find /I "Tuanjie.exe" >nul
if not errorlevel 1 (
  echo [ERROR] Tuanjie is running. Please CLOSE the editor first,
  echo         then run this file again.
  goto FAIL
)

echo Running EditMode tests... this may take a few minutes.
echo Do NOT close this window.
echo.

"%EDITOR%" -batchmode -nographics -projectPath "%PROJECT%" ^
  -runTests -testPlatform EditMode ^
  -testResults "%RESULT%" ^
  -logFile "%LOG%"

if errorlevel 1 goto FAIL

if not exist "%RESULT%" goto FAIL

echo.
echo ============================================
echo  DONE - results written to:
echo  %RESULT%
echo  (open it to see per-test pass/fail)
echo ============================================
goto END

:FAIL
echo.
echo ============================================
echo  FAILED - check the log: %LOG%
echo  (If it hung, use the editor Test Runner instead.)
echo ============================================

:END
echo.
pause
endlocal
