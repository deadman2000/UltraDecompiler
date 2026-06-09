@echo off
setlocal

dosbox-x -conf dosbox.conf -nopromptfolder -fastlaunch -c "CD C:\QuickC\programs\decomp~1"
type build.log
del build.log