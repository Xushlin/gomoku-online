@echo off
REM ============================================================
REM Gomoku dev launcher — double-click to start backend + frontend.
REM
REM Opens two windows:
REM   [Gomoku Backend]  .NET API on http://localhost:5145
REM   [Gomoku Frontend] Angular dev server on http://localhost:4200
REM Then opens the browser at http://localhost:4200 once it's up.
REM
REM Close either window to stop that side. Ctrl+C also works.
REM ============================================================

setlocal
set "ROOT=%~dp0"
cd /d "%ROOT%"

echo.
echo [1/3] Starting backend (dotnet run, http://localhost:5145)...
start "Gomoku Backend" cmd /k "cd /d "%ROOT%backend" && dotnet run --project src\Gomoku.Api --launch-profile http"

echo [2/3] Starting frontend (npm start, http://localhost:4200)...
start "Gomoku Frontend" cmd /k "cd /d "%ROOT%frontend-web" && npm start"

echo [3/3] Waiting for frontend to come up, then opening browser...
REM Poll until port 4200 answers (up to ~60s), then launch the browser.
powershell -NoProfile -Command ^
  "for ($i=0; $i -lt 60; $i++) { try { $r = Invoke-WebRequest -Uri http://localhost:4200 -UseBasicParsing -TimeoutSec 1; if ($r.StatusCode -eq 200) { Start-Process 'http://localhost:4200'; break } } catch {}; Start-Sleep -Seconds 1 }"

echo.
echo Launched. You can close this window; the backend + frontend keep running
echo in their own windows. Close those to stop.
endlocal
