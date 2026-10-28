@echo off
setlocal
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0INSTALAR.ps1"
if errorlevel 1 pause

