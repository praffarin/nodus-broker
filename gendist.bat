@echo off
@chcp 65001 > nul

taskkill /IM nodus-broker.exe /F >nul 2>&1 || ver >nul
timeout /t 1 /nobreak > nul

set REP=dist
if exist %REP% rmdir /S /Q %REP%

set TARGET=win-x64
powershell -ExecutionPolicy Bypass -File "tools\nodus-broker.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK
powershell -ExecutionPolicy Bypass -File "tools\bg-nodus-broker.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK
powershell -ExecutionPolicy Bypass -File "tools\gendist.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK

set TARGET=win-x86
powershell -ExecutionPolicy Bypass -File "tools\nodus-broker.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK
powershell -ExecutionPolicy Bypass -File "tools\bg-nodus-broker.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK
powershell -ExecutionPolicy Bypass -File "tools\gendist.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK

set TARGET=linux-x64
powershell -ExecutionPolicy Bypass -File "tools\nodus-broker.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK
powershell -ExecutionPolicy Bypass -File "tools\bg-nodus-broker.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK
powershell -ExecutionPolicy Bypass -File "tools\gendist.ps1" %TARGET%
if %errorlevel% neq 0 goto NOK

:END
   pause
   exit

:NOK
   if exist %REP% rmdir /S /Q %REP%
   pause
   exit


