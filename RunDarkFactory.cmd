@echo off
REM Unattended task-board shift. Arguments pass through, e.g. RunDarkFactory.cmd -Hours 4 -MaxTasks 3
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RunDarkFactory.ps1" %*
