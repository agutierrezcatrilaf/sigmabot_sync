@echo off
REM Abre API + Angular en dos ventanas. Cierre cada ventana para detener.

set "REPO=%~dp0.."
set "ANGULAR=%REPO%\..\SigmabotConfig"

if not exist "%ANGULAR%\package.json" (
  echo No se encontro SigmabotConfig en: %ANGULAR%
  pause
  exit /b 1
)

start "SigmabotConfig.Api" cmd /k "%~dp0Run-Api-Local.bat"
timeout /t 3 /nobreak >nul
start "SigmabotConfig Web" cmd /k "%ANGULAR%\Run-Local.bat"

echo.
echo Configurador local:
echo   Web:     http://localhost:4200
echo   API:     http://localhost:5154/swagger
echo.
pause
