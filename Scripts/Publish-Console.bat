@echo off
setlocal EnableExtensions
REM LEGACY: delega al repo SigmabotSync.Worker

set "SALFA=%~dp0..\.."
call "%SALFA%SigmabotSync.Worker\Scripts\Publish-Console.bat" %*
