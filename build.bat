@echo off
setlocal
chcp 65001 >nul
title 编译中控考勤/门禁时间自动同步程序 (C# 5 / .NET Framework 4.5)
echo ========================================================
echo        中控时间自动同步程序构建脚本 (C# 5.0)
echo ========================================================

:: 自动探测 CSC 编译器路径（优先检测 x64 的 Framework64 目录，兼容 32 位 Framework）
set "CSC="
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) else if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if "%CSC%"=="" (
    echo [错误] 未在系统目录中找到 .NET Framework 4.0/4.5 编译器 csc.exe
    echo 请确认系统已安装 .NET Framework 4.0 或更高版本。
    pause
    exit /b 1
)

echo 使用编译器: %CSC%
echo 正在准备输出目录...
if not exist "bin" mkdir bin

echo 正在编译程序...
:: 编译参数说明:
:: /target:winexe: 生成带界面的可执行文件（支持双击打开GUI，也支持命令行参数作为服务运行）
:: /platform:x86: 中控 zkemkeeper.dll 是 32 位 COM 组件，指定 x86 确保绝对兼容运行
:: /langversion:5: 严格指定 C# 5 语法版本
:: /r: 引用必要的系统程序集

"%CSC%" /target:winexe /platform:x86 /langversion:5 /optimize+ /out:bin\ZkTimeSync.exe /r:System.dll /r:System.Core.dll /r:System.Data.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.ServiceProcess.dll /r:System.Configuration.Install.dll /r:System.Web.Extensions.dll /r:System.Xml.dll src\*.cs

if errorlevel 1 (
    echo ========================================================
    echo [错误] 编译失败，请检查上方代码报错信息！
    echo ========================================================
    pause
    exit /b 1
)

echo 复制默认配置文件到输出目录（已存在则保留运行时配置，避免覆盖设备同步状态）...
if exist "config.json" if not exist "bin\config.json" copy /y "config.json" "bin\config.json" >nul 2>&1

echo ========================================================
echo [成功] 编译完成！生成文件位于 bin\ZkTimeSync.exe
echo ========================================================
pause
endlocal
