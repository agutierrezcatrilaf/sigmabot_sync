@echo off
setlocal EnableExtensions
REM Publica API + Angular desde el repo SigmabotSync.Api (sigmabot_sync).
REM Requiere SigmabotConfig (Angular) en carpeta hermana: ..\SigmabotConfig

set "REPO=%~dp0"
set "PUBLISH_NOPAUSE=1"

echo.
echo ========================================
echo  Publish SigmabotSync.Api (configurador)
echo ========================================
echo.

call "%REPO%Scripts\Publish-Configurador.bat" %*
if errorlevel 1 goto :fail

echo.
echo === Listo ===
echo   publish\configurador\
echo   publish\SigmabotConfig-Servidor.zip
echo.
set "PUBLISH_NOPAUSE="
pause
exit /b 0

:fail
echo.
echo ERROR: publish incompleto.
pause
exit /b 1
