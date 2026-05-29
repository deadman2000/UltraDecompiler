@echo off
cd /d "%~dp0"
if not exist "C:\TEMP" mkdir "C:\TEMP"
set PATH=%PATH%;%CD%
set TEMP=C:\TEMP
set TMP=C:\TEMP
set LIB=%CD%
set INCLUDE=%CD%;include

cd programs
msdos qcl.exe /I..\include hello.c
cd ..
exit /b %ERRORLEVEL%
