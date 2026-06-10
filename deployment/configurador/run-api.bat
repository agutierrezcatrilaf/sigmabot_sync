@echo off
setlocal
REM Carpeta donde desplegó publish\configurador\api (ajustar si aplica)
set API_DIR=%~dp0
if exist "%API_DIR%..\api\" set API_DIR=%API_DIR%..\api\
cd /d "%API_DIR%"

set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://0.0.0.0:5154

echo Iniciando SigmabotConfig.Api en %ASPNETCORE_URLS%
echo Carpeta: %API_DIR%
dotnet SigmabotConfig.Api.dll

endlocal
