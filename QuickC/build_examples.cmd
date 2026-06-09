@echo off
setlocal

dosbox-x -conf dosbox.conf -nopromptfolder -fastlaunch -silent -c "make clean" -c "make" > build.log 2>&1
type build.log
del build.log