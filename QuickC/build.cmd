@echo off
cd /d "%~dp0"
if not exist "C:\TEMP" mkdir "C:\TEMP"
set PATH=%PATH%;%CD%
set TEMP=C:\TEMP
set TMP=C:\TEMP
set LIB=%CD%
set INCLUDE=%CD%\INCLUDE

cd PROGRAMS

echo [Small /AS + SLIBCE.LIB]
msdos qcl.exe /AS /FeHELLO_S.EXE SLIBCE.LIB /I..\INCLUDE hello.c
if errorlevel 1 exit /b 1

echo [Compact /AC + CLIBC.LIB]
msdos qcl.exe /AC /FeHELLO_C.EXE CLIBC.LIB /I..\INCLUDE hello.c
if errorlevel 1 exit /b 1

echo [Medium /AM + MLIBC.LIB]
msdos qcl.exe /AM /FeHELLO_M.EXE MLIBC.LIB /I..\INCLUDE hello.c
if errorlevel 1 exit /b 1

echo [Large /AL + LLIBC.LIB]
msdos qcl.exe /AL /FeHELLO_L.EXE LLIBC.LIB /I..\INCLUDE hello.c
if errorlevel 1 exit /b 1

cd ..
exit /b 0
