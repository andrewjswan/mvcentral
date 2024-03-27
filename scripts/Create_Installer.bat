@echo off
cls
Title Creating MediaPortal mvCentral Installer

if "%programfiles(x86)%XXX"=="XXX" goto 32BIT
    :: 64-bit
    set PROGS=%programfiles(x86)%
    goto CONT
:32BIT
    set PROGS=%ProgramFiles%
:CONT

IF NOT EXIST "%PROGS%\Team MediaPortal\MediaPortal\" SET PROGS=C:

:: Get version from DLL
FOR /F "tokens=*" %%i IN ('Tools\sigcheck.exe /accepteula /nobanner /n "..\mvCentral\bin\Release\mvCentral.dll"') DO (SET version=%%i)

:: Temp xmp2 file
copy ..\MPEI\mvCentral.xmp2 ..\MPEI\mvCentralTemp.xmp2

:: Build MPE1
"%PROGS%\Team MediaPortal\MediaPortal\MPEMaker.exe" ..\MPEI\mvCentralTemp.xmp2 /B /V=%version% /UpdateXML

:: Cleanup
del ..\MPEI\mvCentralTemp.xmp2
