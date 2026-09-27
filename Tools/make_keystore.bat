@echo off
rem ============================================================
rem  One-click release keystore for PixelAdventure
rem  DOUBLE-CLICK this file to run.
rem
rem  NOTE: this file is intentionally ENGLISH-ONLY.
rem  Chinese text in a .bat becomes garbled because cmd.exe
rem  decodes .bat files as GBK while our tools write UTF-8.
rem  Chinese explanation lives in: Keys\README.txt
rem
rem  Output:
rem    E:\xiangsumaoxian\Keys\pixeladventure-release.jks
rem    E:\xiangsumaoxian\Keys\keystore-info.txt  (fingerprints)
rem ============================================================

set KEYTOOL=E:\Program Files\Tuanjie\Hub\Editor\2022.3.62t12\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe
set KEYDIR=E:\xiangsumaoxian\Keys
set KS=%KEYDIR%\pixeladventure-release.jks
set INFO=%KEYDIR%\keystore-info.txt
set ALIAS=pixeladventure
set DNAME=CN=PixelAdventure, OU=Games, O=PixelAdventure, L=Beijing, ST=Beijing, C=CN

echo.
echo ============================================================
echo  Release keystore generator
echo ============================================================
echo.
echo  Output   : %KS%
echo  Alias    : %ALIAS%
echo  Validity : 10000 days (about 27 years)
echo  Algorithm: RSA 4096
echo.

if not exist "%KEYTOOL%" (
    echo [ERROR] keytool not found:
    echo   %KEYTOOL%
    echo   Please check the Tuanjie editor install path.
    goto END
)

if not exist "%KEYDIR%" mkdir "%KEYDIR%"

if exist "%KS%" (
    echo [WARN] A keystore already exists:
    echo        %KS%
    echo.
    echo        keytool will NOT overwrite it. Delete or rename the
    echo        old file yourself first if you really want to redo it.
    echo.
    pause
    goto END
)

echo ------------------------------------------------------------
echo  You will be asked for a password TWICE.
echo.
echo   * Nothing is shown while you type. That is NORMAL.
echo     It is NOT a broken keyboard and NOT an IME issue.
echo   * Press Enter when done.
echo   * Write the password down / store it in a password manager.
echo     It cannot be recovered - losing it means you must create a
echo     new signature, which breaks upgrades for existing players.
echo.
echo  Suggestion: 16+ chars, mixed upper/lower/digit/symbol.
echo              Do NOT reuse a birthday, phone number or common
echo              password from another account.
echo ------------------------------------------------------------
echo.

"%KEYTOOL%" -genkeypair -v ^
  -keystore "%KS%" ^
  -alias %ALIAS% ^
  -keyalg RSA -keysize 4096 -validity 10000 ^
  -dname "%DNAME%"

if errorlevel 1 (
    echo.
    echo ============================================================
    echo  [FAILED] keystore was NOT created.
    echo  Tell the assistant the error message above.
    echo ============================================================
    goto END
)

echo.
echo Keystore created. Now exporting fingerprints...
echo.
echo Please type the SAME password one more time when asked:
echo.

"%KEYTOOL%" -list -v -keystore "%KS%" -alias %ALIAS% > "%INFO%" 2>&1

echo.
echo ============================================================
echo  SUCCESS
echo.
echo  Keystore : %KS%
echo  Info     : %INFO%
echo.
echo  Next steps in Tuanjie:
echo    1. Edit - Project Settings - Player - Android tab
echo    2. Publishing Settings - tick "Custom Keystore"
echo    3. Keystore path : %KS%
echo    4. Fill password / alias / key password
echo    5. Click Validate and make sure it reports no error
echo.
echo  IMPORTANT: back up the whole Keys folder to USB or cloud.
echo             Never commit it to git. Never send it to anyone.
echo ============================================================

:END
echo.
pause
endlocal
