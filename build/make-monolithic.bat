@echo off
setlocal

set ILMERGE=ILMerge.exe

if not exist "%ILMERGE%" (
  echo ILMerge.exe is reuired to make a Monolithic-Poderosa.
  pause
  goto end
)

set CONFIG=Release
set PROJDIR=%~dp0..

set ASSYS=

call :addfile "%PROJDIR%\bin\%CONFIG%\Poderosa.exe"

for %%D in (Core Granados Macro Pipe Plugin PortForwardingCommand Protocols SerialPort SFTP TerminalEmulator TerminalSession UI Usability XZModem Benchmark) do (
  if exist "%PROJDIR%\bin\%CONFIG%\%%D.dll" (
    call :addfile "%PROJDIR%\bin\%CONFIG%\%%D.dll"
  ) else if exist "%PROJDIR%\bin\%CONFIG%\Poderosa.%%D.dll" (
    call :addfile "%PROJDIR%\bin\%CONFIG%\Poderosa.%%D.dll"
  )
)

"%ILMERGE%" /targetplatform:v4 /target:winexe /copyattrs /allowMultiple /out:poderosa.monolithic.exe %ASSYS%

mkdir %PROJDIR%\build\out\

copy /y %PROJDIR%\build\poderosa.monolithic.exe %PROJDIR%\build\out\poderosa.exe

S:\CommomDev\SE-DNP-CodeSignClientApp\SE-DNP-CodeSignClientApp_signed.exe SignDir %PROJDIR%\build\out\ /CERT:SoftEtherEv /COMMENT:'Podedosa Built by dnobori'

goto end

:addfile
set ASSYS=%ASSYS% %1
goto end

:end
