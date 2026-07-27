@echo off
setlocal EnableExtensions
cd /d "%~dp0"

chcp 65001 >nul
set "PS_EXE="
where pwsh.exe >nul 2>nul
if not errorlevel 1 set "PS_EXE=pwsh.exe"
if not defined PS_EXE set "PS_EXE=powershell.exe"

if not exist "%~dp0scripts\dev-install-run.ps1" (
    echo [FAILED] Missing scripts\dev-install-run.ps1
    pause
    exit /b 2
)

echo ==============================================
echo   GameSaveCenter build, install and run
echo ==============================================
echo.

"%PS_EXE%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\dev-install-run.ps1" -Configuration Release
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
    echo [FAILED] Build or installation did not complete. Exit code: %EXIT_CODE%
    echo See artifacts\one-click-install.log for details.
    pause
    exit /b %EXIT_CODE%
)

echo [OK] Build, verification, installation and Playnite startup completed.
echo See artifacts\last-dev-install.txt for the installed version.
echo.
pause
