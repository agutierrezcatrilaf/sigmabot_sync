@echo off
setlocal
REM Tarea que mantiene la API al reiniciar el servidor (opcional; IIS es alternativa)
set TASK_NAME=SigmabotConfigApi
set API_DIR=C:\Sigmabot\SigmabotConfig.Api
set RUN_BAT=%API_DIR%\run-api.bat

if not exist "%RUN_BAT%" (
  echo ERROR: Ajuste API_DIR y copie run-api.bat junto a la API.
  exit /b 1
)

schtasks /Delete /TN "%TASK_NAME%" /F >nul 2>&1
schtasks /Create /TN "%TASK_NAME%" /TR "\"%RUN_BAT%\"" /SC ONSTART /RU SYSTEM /RL HIGHEST /F
echo Tarea %TASK_NAME% creada (al iniciar Windows).
endlocal
