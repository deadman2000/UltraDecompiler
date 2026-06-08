@echo off
setlocal enabledelayedexpansion

rem CI-версия сборки эталонных примеров QuickC.
rem Используется в GitHub Actions (windows-latest).
rem Не требует прав на C:\ и не содержит pause.

cd /d "%~dp0"

rem Создаём временную директорию внутри QuickC (портативно и безопасно в CI)
set "QCTEMP=%CD%\ci_temp"
if not exist "%QCTEMP%" mkdir "%QCTEMP%"

set "PATH=%PATH%;%CD%"
set "TEMP=%QCTEMP%"
set "TMP=%QCTEMP%"
set "LIB=%CD%"
set "INCLUDE=%CD%\INCLUDE"

echo === Building QuickC reference examples (all memory models) ===
echo Working directory: %CD%
echo TEMP=%TEMP%
echo LIB=%LIB%
echo INCLUDE=%INCLUDE%
echo.

cd PROGRAMS
if errorlevel 1 (
    echo ERROR: cannot cd to PROGRAMS
    exit /b 1
)

for %%f in (*.c) do (
    echo [%%~nxf] Small /AS + SLIBCE.LIB
    msdos qcl.exe /AS /Fe%%~nf_S.EXE SLIBCE.LIB /I..\INCLUDE "%%f"
    if errorlevel 1 (
        echo ERROR: failed to build %%~nxf (Small model)
        cd ..
        exit /b 1
    )

    echo [%%~nxf] Compact /AC + CLIBC.LIB
    msdos qcl.exe /AC /Fe%%~nf_C.EXE CLIBC.LIB /I..\INCLUDE "%%f"
    if errorlevel 1 (
        echo ERROR: failed to build %%~nxf (Compact model)
        cd ..
        exit /b 1
    )

    echo [%%~nxf] Medium /AM + MLIBC.LIB
    msdos qcl.exe /AM /Fe%%~nf_M.EXE MLIBC.LIB /I..\INCLUDE "%%f"
    if errorlevel 1 (
        echo ERROR: failed to build %%~nxf (Medium model)
        cd ..
        exit /b 1
    )

    echo [%%~nxf] Large /AL + LLIBC.LIB
    msdos qcl.exe /AL /Fe%%~nf_L.EXE LLIBC.LIB /I..\INCLUDE "%%f"
    if errorlevel 1 (
        echo ERROR: failed to build %%~nxf (Large model)
        cd ..
        exit /b 1
    )

    echo.
)

cd ..

echo.
echo === SUCCESS: All QuickC examples built successfully ===
exit /b 0
