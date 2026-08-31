@echo off
REM ============================================================
REM Gewu desktop launcher -- double-click to start backend + Electron shell.
REM
REM Opens two windows:
REM   [Gewu Backend] .NET API on http://localhost:5145
REM   [Gewu Desktop] the Electron window
REM
REM ASCII only, on purpose. An em dash here comes out as mojibake in the
REM console's default codepage, which is what the first version shipped.
REM
REM Two things this does that are easy to get wrong by hand:
REM
REM   1. Rebuilds the Angular app first. The shell serves whatever is in
REM      frontend-web\dist, so a stale build means you are using old UI
REM      while everything looks perfectly normal.
REM   2. Fetches Electron's 115 MB runtime itself when npm's own installer
REM      fails. That failure is common (a transient ECONNRESET is enough)
REM      and npm ROLLS BACK on it, so node_modules\electron can be gone
REM      entirely rather than just missing its binary. Both cases are
REM      detected separately below, because the fix differs.
REM
REM (CORS used to be a third: the renderer's origin is app://gewu, which the
REM backend must allow. appsettings.Development.json now lists it, so a dev
REM run needs nothing -- but a non-Development server still does.)
REM
REM Close either window to stop that side. Ctrl+C also works.
REM ============================================================

setlocal
set "ROOT=%~dp0"
set "DESKTOP=%ROOT%frontend-desktop"
cd /d "%ROOT%"

echo.
echo [0/5] Freeing port 5145 if anything is listening...
powershell -NoProfile -Command ^
  "Get-NetTCPConnection -LocalPort 5145 -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { try { $p = Get-Process -Id $_ -ErrorAction Stop; Write-Host ('  killing PID ' + $_ + ' (' + $p.ProcessName + ')'); Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue } catch {} }"

echo.
echo [1/5] Checking desktop dependencies...
if not exist "%DESKTOP%\node_modules\electron\package.json" (
  echo   running npm install ^(first run pulls Electron, about 115 MB^)...
  pushd "%DESKTOP%"
  call npm install
  popd
)

REM npm rolls back the whole install when Electron's postinstall fails, so the
REM package itself can be missing. That is a different problem from "the binary
REM did not download", and telling you to run electron's install.js here would
REM just report a missing module.
if not exist "%DESKTOP%\node_modules\electron\package.json" (
  echo.
  echo   ERROR: npm install did not leave node_modules\electron behind.
  echo.
  echo   npm rolls the install back when Electron's postinstall fails, so this
  echo   is npm itself failing, not just the runtime download. Run it again and
  echo   read the first error, not the last:
  echo.
  echo     cd frontend-desktop
  echo     npm install
  echo.
  echo   If it reports EPERM while removing directories, something has a lock
  echo   on node_modules -- close any running Gewu window and retry.
  echo.
  pause
  exit /b 1
)

REM The package is there but the runtime is not: npm's downloader failed (a
REM transient ECONNRESET does it). Fetch it directly -- measured at about 12
REM seconds against the same URL npm's installer was stuck on.
if not exist "%DESKTOP%\node_modules\electron\dist\electron.exe" (
  echo   Electron's runtime is missing; fetching it directly...
  powershell -NoProfile -Command ^
    "$ErrorActionPreference='Stop';" ^
    "$d='%DESKTOP%';" ^
    "$v=(Get-Content (Join-Path $d 'node_modules\electron\package.json') -Raw | ConvertFrom-Json).version;" ^
    "$zip=Join-Path $env:TEMP ('electron-'+$v+'-win32-x64.zip');" ^
    "$url='https://github.com/electron/electron/releases/download/v'+$v+'/electron-v'+$v+'-win32-x64.zip';" ^
    "for ($i=1; $i -le 3; $i++) { try { if (-not (Test-Path $zip) -or (Get-Item $zip).Length -lt 50MB) { Write-Host ('  downloading Electron ' + $v + ' (attempt ' + $i + ')...'); Invoke-WebRequest $url -OutFile $zip -UseBasicParsing }; break } catch { Write-Host ('  attempt ' + $i + ' failed: ' + $_.Exception.Message); Remove-Item $zip -Force -ErrorAction SilentlyContinue; if ($i -eq 3) { throw } } };" ^
    "$dest=Join-Path $d 'node_modules\electron\dist';" ^
    "New-Item -ItemType Directory -Force $dest | Out-Null;" ^
    "Write-Host '  extracting...';" ^
    "Expand-Archive $zip -DestinationPath $dest -Force;" ^
    "Set-Content -Path (Join-Path $d 'node_modules\electron\path.txt') -Value 'electron.exe' -NoNewline;" ^
    "Write-Host '  runtime installed'"
)

if not exist "%DESKTOP%\node_modules\electron\dist\electron.exe" (
  echo.
  echo   ERROR: could not get Electron's runtime.
  echo.
  echo   Download this by hand, unzip it into
  echo   frontend-desktop\node_modules\electron\dist, then put the single line
  echo   electron.exe into frontend-desktop\node_modules\electron\path.txt:
  echo.
  echo     https://github.com/electron/electron/releases
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
  echo   ERROR: the web build failed. Fix that first -- the shell would
  echo   otherwise start and serve the PREVIOUS build, which looks fine.
  echo.
  popd
  pause
  exit /b 1
)
popd

echo.
echo [3/5] Starting backend on http://localhost:5145...
start "Gewu Backend" cmd /k "cd /d "%ROOT%backend" && dotnet run --project src\Gewu.Api --launch-profile http"

echo [4/5] Waiting for the backend to answer...
REM /api/games needs auth, so a 401 IS the server being up. Treat any HTTP
REM response as ready -- waiting for 200 here would wait forever.
powershell -NoProfile -Command ^
  "for ($i=0; $i -lt 60; $i++) { try { Invoke-WebRequest -Uri http://localhost:5145/api/games -UseBasicParsing -TimeoutSec 1 | Out-Null; Write-Host '  backend is up'; break } catch { if ($_.Exception.Response) { Write-Host '  backend is up'; break } }; Start-Sleep -Seconds 1 }"

echo.
echo [5/5] Opening the desktop window...
start "Gewu Desktop" cmd /k "cd /d "%DESKTOP%" && npm start"

echo.
echo Launched. Register an account in the window to start playing.
echo.
echo Versus games need two players: double-click start-dev.cmd to get a
echo browser client on http://localhost:4200 talking to the same backend.
echo.
echo You can close this window; the two it opened keep running.
endlocal
