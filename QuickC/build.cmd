@echo off
cd /d "%~dp0"
if not exist "C:\TEMP" mkdir "C:\TEMP"
set PATH=%PATH%;%CD%
set TEMP=C:\TEMP
set TMP=C:\TEMP
set LIB=%CD%
set INCLUDE=%CD%\INCLUDE

cd PROGRAMS

msdos qcl.exe /AS /FeADD_S.EXE SLIBCE.LIB /I..\INCLUDE add.c
if errorlevel 1 (
    pause >nul
    cd ..
    exit /b 1
)

cd ..
exit /b 0
