@echo off
setlocal EnableExtensions
REM Publica API + Angular del configurador (publish\configurador\).
REM Repo: SigmabotSync.Api (GitHub sigmabot_sync). Front: ..\SigmabotConfig
REM Edite SERVER_HOST si despliega en otro servidor.

set "SERVER_HOST=155.254.24.155"
set "API_PORT=5154"
set "WEB_PORT=8080"

for %%I in ("%~dp0..") do set "REPO_ROOT=%%~fI"
set "PS1=%~dp0Build-ConfiguradorRelease.ps1"

echo.
echo === Publish configurador (SigmabotConfig) ===
echo Servidor: %SERVER_HOST%
echo API:      http://%SERVER_HOST%:%API_PORT%
echo Web:      http://%SERVER_HOST%:%WEB_PORT%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" ^
  -ServerHost "%SERVER_HOST%" ^
  -ApiPort %API_PORT% ^
  -WebPort %WEB_PORT% ^
  -RepoRoot "%REPO_ROOT%" %*

if errorlevel 1 (
  echo.
  echo ERROR: el publish fallo.
  if not defined PUBLISH_NOPAUSE pause
  exit /b 1
)

echo.
echo OK. Salida en: %REPO_ROOT%\publish\configurador\
echo ZIP:          %REPO_ROOT%\publish\SigmabotConfig-Servidor.zip
echo.
if not defined PUBLISH_NOPAUSE pause
endlocal
