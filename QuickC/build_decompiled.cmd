@echo off
setlocal

dosbox-x -conf dosbox.conf -nopromptfolder -fastlaunch -silent -c "CD C:\QuickC\programs\decomp~1" -c "make clean" -c "make" > build.log 2>&1
type build.log
del build.log