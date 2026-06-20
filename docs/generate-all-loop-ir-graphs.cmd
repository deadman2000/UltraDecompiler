@echo off

cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "Tools\generate-all-loop-ir-from-exe.ps1"
