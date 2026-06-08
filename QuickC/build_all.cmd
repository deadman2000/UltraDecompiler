@echo off
cd /d "%~dp0"
if not exist "C:\TEMP" mkdir "C:\TEMP"
set PATH=%PATH%;%CD%
set TEMP=C:\TEMP
set TMP=C:\TEMP
set LIB=%CD%
set INCLUDE=%CD%\INCLUDE

cd PROGRAMS

for %%f in (*.c) do (
    echo [%%~nxf] Small /AS + SLIBCE.LIB
    msdos qcl.exe /AS /Fe%%~nf_S.EXE SLIBCE.LIB /I..\INCLUDE "%%f"
    if errorlevel 1 (
        cd ..
        exit /b 1
    )

    echo [%%~nxf] Compact /AC + CLIBC.LIB
    msdos qcl.exe /AC /Fe%%~nf_C.EXE CLIBC.LIB /I..\INCLUDE "%%f"
    if errorlevel 1 (
        cd ..
        exit /b 1
    )

    echo [%%~nxf] Medium /AM + MLIBC.LIB
    msdos qcl.exe /AM /Fe%%~nf_M.EXE MLIBC.LIB /I..\INCLUDE "%%f"
    if errorlevel 1 (
        cd ..
        exit /b 1
    )

    echo [%%~nxf] Large /AL + LLIBC.LIB
    msdos qcl.exe /AL /Fe%%~nf_L.EXE LLIBC.LIB /I..\INCLUDE "%%f"
    if errorlevel 1 (
        cd ..
        exit /b 1
    )

    echo.
)

cd ..
exit /b 0
