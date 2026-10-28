@echo off
rem Remove tudo que o FC27-Steam-Setup criou (manifest, pasta e processo)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0FC27-Steam-Setup.ps1" -Remover
pause

