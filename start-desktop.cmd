@echo off
REM ============================================================
REM Gewu desktop launcher — double-click to start backend + Electron shell.
REM
REM Opens two windows:
REM   [Gewu Backend] .NET API on http://localhost:5145
REM   [Gewu Desktop] the Electron window
REM
REM Two things this does that are easy to get wrong by hand:
REM
REM   1. Rebuilds the Angular app first. The shell serves whatever is in
REM      frontend-web\dist, so a stale build means you are using old UI
REM      while everything looks perfectly normal.
REM   2. Checks Electron's binary actually downloaded. npm install can
REM      finish "successfully" with the 115 MB runtime missing.
REM
REM (CORS used to be a third: the renderer's origin is app://gewu, which the
REM backend must allow. appsettings.Development.json now lists it, so a dev
REM run needs nothing — but a non-Development server still does.)
REM
REM Close either window to stop that side. Ctrl+C also works.
REM ============================================================

setlocal
set "ROOT=%~dp0"
cd /d "%ROOT%"

echo.
echo [0/5] Freeing port 5145 if anything is listening...
powershell -NoProfile -Command ^
  "Get-NetTCPConnection -LocalPort 5145 -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { try { $p = Get-Process -Id $_ -ErrorAction Stop; Write-Host ('  killing PID ' + $_ + ' (' + $p.ProcessName + ')'); Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue } catch {} }"

echo.
echo [1/5] Checking desktop dependencies...
if not exist "%ROOT%frontend-desktop\node_modules" (
  echo   installing ^(first run — this pulls Electron, ~115 MB^)...
  pushd "%ROOT%frontend-desktop"
  call npm install
  popd
)

if not exist "%ROOT%frontend-desktop\node_modules\electron\dist\electron.exe" (
  echo.
  echo   ERROR: Electron's binary is missing.
  echo.
  echo   npm install can report success with the 115 MB runtime never
  echo   downloaded. Fetch it by hand:
  echo.
  echo     cd frontend-desktop
  echo     node node_modules\electron\install.js
  echo.
  pause
  exit /b 1
)

echo.
echo [2/5] Building the Angular app ^(the shell serves this build^)...
pushd "%ROOT%frontend-web"
call npm run build
if errorlevel 1 (
  echo.
  echo   ERROR: the web build failed. Fix that first — the shell would
  echo   otherwise start and serve the PREVIOUS build, which looks fine.
  echo.
  popd
  pause
  exit /b 1
)
popd

echo.
echo [3/5] Starting backend on http://localhost:5145...
REM The `http` launch profile runs as Development, and
REM appsettings.Development.json already lists app://gewu among the allowed
REM CORS origins — so nothing extra is needed here. A non-Development server
REM must add that origin itself, or the app renders fine and cannot make a
REM single request.
start "Gewu Backend" cmd /k "cd /d "%ROOT%backend" && dotnet run --project src\Gewu.Api --launch-profile http"

echo [4/5] Waiting for the backend to answer...
REM /api/games needs auth, so a 401 IS the server being up. Treat any HTTP
REM response as ready — waiting for 200 here would wait forever.
powershell -NoProfile -Command ^
  "for ($i=0; $i -lt 60; $i++) { try { Invoke-WebRequest -Uri http://localhost:5145/api/games -UseBasicParsing -TimeoutSec 1 | Out-Null; Write-Host '  backend is up'; break } catch { if ($_.Exception.Response) { Write-Host '  backend is up'; break } }; Start-Sleep -Seconds 1 }"

echo.
echo [5/5] Opening the desktop window...
start "Gewu Desktop" cmd /k "cd /d "%ROOT%frontend-desktop" && npm start"

echo.
echo Launched. Register an account in the window to start playing.
echo.
echo Versus games need two players: double-click this file's sibling
echo start-dev.cmd to get a browser client on http://localhost:4200
echo talking to the same backend.
echo.
echo You can close this window; the two it opened keep running.
endlocal
