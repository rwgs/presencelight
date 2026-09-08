@echo off
rem Starts the locally built PresenceLight desktop application.
rem
rem The .NET SDK is installed for the current user rather than machine-wide, so the executable
rem cannot find the shared runtime on its own and exits with 0x80008083 when started directly.
rem Setting DOTNET_ROOT for this process fixes that. See DECISIONS.md for why the toolchain is
rem installed this way.

setlocal

set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "APP=%~dp0..\..\src\DesktopClient\PresenceLight\bin\Debug\net10.0-windows10.0.19041\PresenceLight.exe"

if not exist "%APP%" (
    echo PresenceLight has not been built yet.
    echo Run: dotnet build .\src\DesktopClient\PresenceLight\PresenceLight.csproj -c Debug -p:ChannelName=Standalone
    exit /b 1
)

rem Start from the repository root so the application reads and writes the settings.json there.
pushd "%~dp0..\.."
start "" "%APP%"
popd

endlocal
