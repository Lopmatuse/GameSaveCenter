@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo ==============================================
echo   GameSaveCenter 一键构建、安装并启动 Playnite
echo ==============================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\dev-install-run.ps1" -Configuration Release
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
    echo [失败] 构建或安装未完成，退出码：%EXIT_CODE%
    echo 请保留本窗口中的错误信息。
    pause
    exit /b %EXIT_CODE%
)

echo [成功] 已构建、验证、替换扩展并启动 Playnite。
echo 安装结果保存在 artifacts\last-dev-install.txt
echo.
pause
