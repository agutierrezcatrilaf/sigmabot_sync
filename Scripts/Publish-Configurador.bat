@echo off
setlocal EnableExtensions
REM LEGACY: delega al repo SigmabotSync.Api

set "SALFA=%~dp0..\.."
call "%SALFA%SigmabotSync.Api\Scripts\Publish-Configurador.bat" %*
