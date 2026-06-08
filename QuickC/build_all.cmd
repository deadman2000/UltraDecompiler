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
    for %%m in (S C M L) do (
        msdos qcl.exe /nologo /A%%m /Fe%%~nf_%%m.EXE /I..\INCLUDE "%%f"
        if errorlevel 1 (
            cd ..
            pause
            exit /b 1
        )
    )

    echo.
)

cd ..
pause
exit /b 0
