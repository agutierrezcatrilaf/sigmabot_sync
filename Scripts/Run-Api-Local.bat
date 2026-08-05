@echo off
setlocal
title SigmabotConfig.Api (local)
cd /d "%~dp0..\SigmabotConfig.Api"
echo API: http://localhost:5154
echo Swagger: http://localhost:5154/swagger
echo.
dotnet run --launch-profile http
pause
