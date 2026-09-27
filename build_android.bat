@echo off
rem ============================================================
rem  Android Release APK Builder (CLI)
rem  ------------------------------------------------------------
rem  STATUS: UNVERIFIED ON THIS MACHINE - DO NOT RELY ON IT.
rem
rem  2026-09-27 test result: the command-line batchmode build
rem  HANGS on this machine (tried 3 times):
rem    - hung at "Application.AssetDatabase Initial Refresh Start"
rem    - hung at "Package Manager log level set to [2]"
rem  Diagnosis: CPU time stopped advancing, log file stopped
rem  growing => a real hang, not just slowness.
rem  Ruled out: network, TEMP folder, disk space (all fine).
rem  Root cause not identified.
rem
rem  ==> PLEASE BUILD FROM THE EDITOR GUI INSTEAD:
rem        Open Tuanjie -> File -> Build Settings -> Build
rem        (For the Spotlight pack use the menu:
rem         Tools / Build / Spotlight Android APK)
rem        Output goes to Builds\Android as well.
rem
rem  This file is kept only as a BACKUP for another machine
rem  where the command-line build might work.
rem ============================================================

setlocal

set PROJECT=E:\xiangsumaoxian
set EDITOR=E:\Program Files\Tuanjie\Hub\Editor\2022.3.62t12\Editor\Tuanjie.exe
set LOG=%PROJECT%\Builds\android-build.log
set APK=%PROJECT%\Builds\Android\PixelAdventure-CrackBlade-release.apk

echo.
echo ============================================
echo  Android Release APK Builder (CLI)
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

if exist "%APK%" del /q "%APK%"

echo Building... this usually takes several minutes.
echo Do NOT close this window.
echo.

"%EDITOR%" -batchmode -nographics -quit -buildTarget Android ^
  -projectPath "%PROJECT%" ^
  -executeMethod CliAndroidBuild.BuildReleaseApk ^
  -logFile "%LOG%"

if errorlevel 1 goto FAIL

if not exist "%APK%" goto FAIL

echo.
echo ============================================
echo  SUCCESS - APK built:
echo  %APK%
echo ============================================
for %%A in ("%APK%") do echo  Size: %%~zA bytes
goto END

:FAIL
echo.
echo ============================================
echo  FAILED - no APK produced.
echo  Check the log: %LOG%
echo  (If it hung, use the editor GUI instead.)
echo ============================================

:END
echo.
pause
endlocal
