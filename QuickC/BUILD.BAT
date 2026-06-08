@echo off

echo [hello.c] Small /AS + SLIBCE.LIB
..\QCL.EXE /AS /FeHELLO_S.EXE ..\SLIBCE.LIB /I..\INCLUDE hello.c
if errorlevel 1 goto build_error

echo [hello.c] Compact /AC + CLIBC.LIB
..\QCL.EXE /AC /FeHELLO_C.EXE ..\CLIBC.LIB /I..\INCLUDE hello.c
if errorlevel 1 goto build_error

echo [hello.c] Medium /AM + MLIBC.LIB
..\QCL.EXE /AM /FeHELLO_M.EXE ..\MLIBC.LIB /I..\INCLUDE hello.c
if errorlevel 1 goto build_error

echo [hello.c] Large /AL + LLIBC.LIB
..\QCL.EXE /AL /FeHELLO_L.EXE ..\LLIBC.LIB /I..\INCLUDE hello.c
if errorlevel 1 goto build_error

echo.

echo [add.c] Small /AS + SLIBCE.LIB
..\QCL.EXE /AS /FeADD_S.EXE ..\SLIBCE.LIB /I..\INCLUDE add.c
if errorlevel 1 goto build_error

echo [add.c] Compact /AC + CLIBC.LIB
..\QCL.EXE /AC /FeADD_C.EXE ..\CLIBC.LIB /I..\INCLUDE add.c
if errorlevel 1 goto build_error

echo [add.c] Medium /AM + MLIBC.LIB
..\QCL.EXE /AM /FeADD_M.EXE ..\MLIBC.LIB /I..\INCLUDE add.c
if errorlevel 1 goto build_error

echo [add.c] Large /AL + LLIBC.LIB
..\QCL.EXE /AL /FeADD_L.EXE ..\LLIBC.LIB /I..\INCLUDE add.c
if errorlevel 1 goto build_error

goto :eof

:build_error
echo.
echo *** BUILD FAILED inside DOS emulator ***
exit 1

:eof
exit 0