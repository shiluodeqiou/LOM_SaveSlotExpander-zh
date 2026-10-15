@echo off
rem ---------------------------------------------------------------
rem  LOM SaveSlotExpander (Chinese) - one click installer
rem  All user-facing text lives in install.ps1, which is UTF-8
rem  with BOM.  Keeping this wrapper pure ASCII avoids any code
rem  page problems in the console.
rem
rem  Double click this file to install.
rem  You can also drag the game folder (the one with Mortal.exe)
rem  onto this file to point the installer at it.
rem ---------------------------------------------------------------
setlocal

if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -GameRoot "%~1"
)

echo.
pause
endlocal
