@echo off
chcp 65001 >nul
rem ============================================================
rem  一键测试（双击运行）：批处理跑工程全部 EditMode 用例
rem  引擎：团结 2022.3.62t16   项目：y:\PixelAdventureTown
rem  产物：Temp\oneclick_test_results.xml（结果）+ Temp\oneclick_test.log（日志）
rem  退出码：0=全部通过；2=有用例失败；其它=引擎/编译错误（看 log）
rem ============================================================
set ENGINE=Y:\PixelAdventureTown\2022.3.62t16\Editor\Tuanjie.exe
set PROJECT=Y:\PixelAdventureTown
set RESULTS=%PROJECT%\Temp\oneclick_test_results.xml
set LOG=%PROJECT%\Temp\oneclick_test.log

echo [一键测试] 正在启动团结引擎（批处理，跑 EditMode 全部用例）...
echo   结果 XML：%RESULTS%
echo   日志文件：%LOG%
echo   （首次运行要先导入资源，可能等几分钟，请勿关窗）
echo.

"%ENGINE%" -batchmode -nographics -projectPath "%PROJECT%" -runTests -testPlatform EditMode -testResults "%RESULTS%" -logFile "%LOG%"
set EC=%ERRORLEVEL%

echo.
if "%EC%"=="0" (
    echo [一键测试] 全部通过（退出码 0）
) else (
    echo [一键测试] 未全部通过（退出码 %EC%）——详情看结果 XML 与日志
)
if exist "%RESULTS%" echo [一键测试] 结果文件：%RESULTS%
echo.
pause
