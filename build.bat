@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo   NATO Quartermaster - SPT 4.1.x Builder
echo ============================================
echo.

dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET 10 SDK was not found.
    echo Install the .NET 10 SDK, then run this file again.
    echo https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    pause
    exit /b 1
)

echo Restoring SPT packages...
dotnet restore
if errorlevel 1 goto :fail

echo.
echo Building release...
dotnet build -c Release
if errorlevel 1 goto :fail

echo.
echo SUCCESS.
echo Your installable ZIP is in:
echo   ReleaseZip\Michael-NATOQuartermaster-1.1.3.zip
echo.
pause
exit /b 0

:fail
echo.
echo BUILD FAILED. Copy the error text and send it to ChatGPT.
echo.
pause
exit /b 1
