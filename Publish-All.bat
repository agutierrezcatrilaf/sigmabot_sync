@echo off
setlocal EnableExtensions
REM LEGACY: el monorepo SigmabotSync ya no publica aqui. Delega a los repos separados.

set "SALFA=%~dp0.."
set "PUBLISH_NOPAUSE=1"

echo.
echo ========================================
echo  Publish completo (repos separados)
echo ========================================
echo.

call "%SALFA%SigmabotSync.Api\Publish-Configurador.bat"
if errorlevel 1 goto :fail

call "%SALFA%SigmabotSync.Worker\Publish-Worker.bat"
if errorlevel 1 goto :fail

echo.
echo === Todo listo ===
echo   SigmabotSync.Api\publish\configurador\
echo   SigmabotSync.Worker\publish\console\
echo.
set "PUBLISH_NOPAUSE="
pause
exit /b 0

:fail
echo.
echo ERROR: publish incompleto.
pause
exit /b 1
