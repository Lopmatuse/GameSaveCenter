@echo off
setlocal EnableExtensions
cd /d "%~dp0"

where git >nul 2>&1
if errorlevel 1 (
  echo [ERROR] Git was not found in PATH.
  pause
  exit /b 1
)

for /f "delims=" %%A in ('git status --porcelain') do set "GSC_DIRTY=1"
if defined GSC_DIRTY (
  echo [ERROR] The working tree is not clean. Commit or discard changes first.
  git status --short --branch
  pause
  exit /b 1
)

echo [INFO] Fetching the latest origin/main...
git fetch origin main
if errorlevel 1 (
  echo.
  echo [ERROR] Fetch failed. Check the SSH key and network connection.
  pause
  exit /b 1
)

git merge-base --is-ancestor origin/main main
if errorlevel 1 (
  echo.
  echo [ERROR] origin/main changed after this package was prepared.
  echo [ERROR] Push was stopped to avoid overwriting remote work.
  pause
  exit /b 1
)

echo.
echo [INFO] Repository status:
git status --short --branch
echo.
echo [INFO] Remote:
git remote -v
echo.
echo [INFO] Pushing main to origin...
git push origin main
if errorlevel 1 (
  echo.
  echo [ERROR] Push failed. Review the message above.
  pause
  exit /b 1
)

echo.
echo [OK] Push completed successfully.
pause
exit /b 0
