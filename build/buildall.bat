@echo off
setlocal

if "%1" == "" (
  echo Usage: buildall.bat config
  goto :end
)

call "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\Tools\VsDevCmd.bat"

set CONFIGNAME=%1

set PORTFORWARDING=%~dp0..\PortForwarding.sln
set PODEROSA=%~dp0..\poderosa.sln
set CONTRIBPLUGINS=%~dp0..\ContributedPlugins.sln

set PORTFORWARDING_LOG=%~dp0PortForwarding-build-%CONFIGNAME%.log
set PODEROSA_LOG=%~dp0Poderosa-build-%CONFIGNAME%.log
set CONTRIBPLUGINS_LOG=%~dp0ContributedPlugins-build-%CONFIGNAME%.log

if exist "%PODEROSA_LOG%"       del "%PODEROSA_LOG%"
if exist "%PORTFORWARDING_LOG%" del "%PORTFORWARDING_LOG%"
if exist "%CONTRIBPLUGINS_LOG%" del "%CONTRIBPLUGINS_LOG%"

devenv.exe /Clean "%CONFIGNAME%" "%PODEROSA%"
if ERRORLEVEL 1 goto builderr

devenv.exe /Clean "%CONFIGNAME%" "%PORTFORWARDING%"
if ERRORLEVEL 1 goto builderr

devenv.exe /Clean "%CONFIGNAME%" "%CONTRIBPLUGINS%"
if ERRORLEVEL 1 goto builderr

devenv.exe /Rebuild "%CONFIGNAME%" "%PODEROSA%"       /Out "%PODEROSA_LOG%"
if ERRORLEVEL 1 goto builderr

devenv.exe /Build   "%CONFIGNAME%" "%PORTFORWARDING%" /Out "%PORTFORWARDING_LOG%"
if ERRORLEVEL 1 goto builderr

devenv.exe /Build   "%CONFIGNAME%" "%CONTRIBPLUGINS%" /Out "%CONTRIBPLUGINS_LOG%"
if ERRORLEVEL 1 goto builderr

echo Build Succeeded
goto end

:builderr
echo Build failed !!!

:end
