@echo off
setlocal
cd /d "%~dp0"
where git >nul 2>nul || (
  echo [ERROR] Git was not found in PATH.
  exit /b 1
)
git rev-parse --is-inside-work-tree >nul 2>nul || (
  echo [ERROR] This folder is not a Git working tree.
  exit /b 1
)
echo Configuring repository-local Windows Git behavior...
git config --local core.filemode false
git config --local core.autocrlf false
git update-index --refresh >nul 2>nul
echo.
echo Current status:
git status --short
echo.
echo Done. Source line endings are controlled by .gitattributes.
exit /b 0
