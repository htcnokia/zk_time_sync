@echo off
chcp 65001 >nul
setlocal
title 中控考勤/门禁时间自动同步 - Windows 服务管理

set "SRV=ZkTimeSyncService"
set "ROOT=%~dp0"
set "EXE=%ROOT%bin\ZkTimeSync.exe"

:: ============ 管理员权限检测 ============
net session >nul 2>&1
if errorlevel 1 (
    echo.
    echo [权限不足] 服务安装/卸载/COM 注册必须以管理员身份运行。
    echo 请右键本脚本 -^> 以管理员身份运行，然后重试。
    echo.
    pause
    exit /b 1
)

:MENU
cls
echo ========================================================
echo        中控考勤/门禁时间自动同步 - Windows 服务管理
echo        运行账户: LocalSystem (无需用户登录, 开机自动运行)
echo ========================================================
echo.
echo   [1] 安装/修复 系统服务 (开机自启 + 网络依赖 + 崩溃自动重启)
echo   [2] 启动服务
echo   [3] 停止服务
echo   [4] 查询服务运行状态
echo   [5] 卸载系统服务
echo   [6] 注册中控 SDK COM 组件 (LocalSystem 服务必做, 仅一次)
echo   [7] 桌面窗口方式启动程序
echo   [8] 立即执行一次静默同步 (命令行)
echo   [0] 退出
echo ========================================================
set /p opt="请输入选项编号: "

if "%opt%"=="1" goto INSTALL
if "%opt%"=="2" goto START
if "%opt%"=="3" goto STOP
if "%opt%"=="4" goto STATUS
if "%opt%"=="5" goto UNINSTALL
if "%opt%"=="6" goto REGDLL
if "%opt%"=="7" goto GUI
if "%opt%"=="8" goto SYNC
if "%opt%"=="0" exit /b 0
goto MENU

:INSTALL
echo.
echo 正在安装/修复系统服务...
"%EXE%" -install
echo.
echo [提示] 若为 Windows Server 且服务未被系统策略阻止, 现在可执行 [2] 启动服务。
echo        断电/重启后服务将以 LocalSystem 自动运行, 无需任何用户登录。
echo.
pause
goto MENU

:START
echo.
net start %SRV%
echo.
"%EXE%" -status
echo.
pause
goto MENU

:STOP
echo.
net stop %SRV%
echo.
pause
goto MENU

:STATUS
echo.
"%EXE%" -status
echo.
pause
goto MENU

:UNINSTALL
echo.
echo 正在停止并卸载系统服务...
net stop %SRV% >nul 2>&1
"%EXE%" -uninstall
echo.
pause
goto MENU

:REGDLL
echo.
echo 正在以机器级 (HKLM) 注册 32 位中控 SDK COM 组件 zkemkeeper.dll...
echo [重要] Windows 服务以 LocalSystem 运行, 必须注册到 HKLM 才能加载,
echo        请用 regsvr32 (非 regsvr32 /i:user) 确保为机器级注册。
if exist "%SystemRoot%\SysWOW64\regsvr32.exe" (
    "%SystemRoot%\SysWOW64\regsvr32.exe" "%ROOT%sdk_x86\zkemkeeper.dll"
) else (
    regsvr32.exe "%ROOT%sdk_x86\zkemkeeper.dll"
)
echo.
echo 注册完成后, 请执行 [1] 安装服务并 [2] 启动。
echo.
pause
goto MENU

:GUI
echo.
start "" "%EXE%"
goto :eof

:SYNC
echo.
"%EXE%" -sync
echo.
pause
goto MENU
