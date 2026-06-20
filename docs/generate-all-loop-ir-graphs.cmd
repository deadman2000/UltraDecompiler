@echo off

cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "docs\generate-all-loop-ir-from-exe.ps1"
pause