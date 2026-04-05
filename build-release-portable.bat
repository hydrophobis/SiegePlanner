@echo off
setlocal enabledelayedexpansion

echo ========================================
echo R6 Planner - Portable Release Build
echo ========================================
echo.

REM Check if dotnet is installed
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK not found. Please install .NET 8.0 SDK.
    echo Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo Checking .NET SDK version...
dotnet --version
echo.

REM Set build configuration
set CONFIG=Release
set FRAMEWORK=net8.0-windows
set OUTPUT_DIR=release-portable
set PUBLISH_DIR=%OUTPUT_DIR%\R6Planner

echo Configuration: %CONFIG%
echo Framework: %FRAMEWORK%
echo Output Directory: %OUTPUT_DIR%
echo.

REM Clean previous builds
echo Cleaning previous builds...
if exist "%OUTPUT_DIR%" (
    rmdir /s /q "%OUTPUT_DIR%"
)
echo Clean complete.
echo.

REM Restore dependencies
echo Restoring NuGet packages...
dotnet restore R6Planner.csproj
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to restore packages.
    pause
    exit /b 1
)
echo.

REM Publish portable release
echo Publishing portable release...
dotnet publish R6Planner.csproj -c %CONFIG% -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%PUBLISH_DIR%"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish failed.
    pause
    exit /b 1
)
echo.

REM Copy resources
echo Copying game resources...
if exist "Maps" (
    xcopy /E /I /Y "Maps" "%PUBLISH_DIR%\Maps\"
)
if exist "Configs" (
    xcopy /E /I /Y "Configs" "%PUBLISH_DIR%\Configs\"
)
echo.

REM Create README for distribution
echo Creating distribution README...
(
echo R6 Planner - Portable Release
echo ==============================
echo.
echo To run the application:
echo 1. Extract all files to a folder
echo 2. Run R6Planner.exe
echo.
echo Requirements:
echo - Windows 10 or later
echo - No additional software needed ^(self-contained^)
echo.
echo All game maps and configurations are included.
echo.
echo Build Date: %DATE% %TIME%
) > "%PUBLISH_DIR%\README.txt"
echo.

REM Create a zip archive if 7-Zip or PowerShell is available
echo Creating distribution archive...
set ARCHIVE_NAME=%OUTPUT_DIR%\R6Planner-Portable.zip

where 7z >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Using 7-Zip to create archive...
    7z a -tzip "%ARCHIVE_NAME%" ".\%PUBLISH_DIR%\*"
) else (
    echo Using PowerShell to create archive...
    powershell -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%ARCHIVE_NAME%' -Force"
)

if exist "%ARCHIVE_NAME%" (
    echo Archive created: %ARCHIVE_NAME%
) else (
    echo Warning: Could not create archive. Files are available in %PUBLISH_DIR%
)
echo.

REM Display build summary
echo ========================================
echo BUILD COMPLETE!
echo ========================================
echo.
echo Portable release files: %PUBLISH_DIR%
echo Main executable: %PUBLISH_DIR%\R6Planner.exe
if exist "%ARCHIVE_NAME%" (
    echo Distribution archive: %ARCHIVE_NAME%
)
echo.
echo The portable version includes all dependencies and can run
echo on any Windows 10+ system without installing .NET.
echo.

REM Ask if user wants to open the release folder
set /p OPEN_FOLDER="Open release folder? (Y/N): "
if /i "%OPEN_FOLDER%"=="Y" (
    start "" "%OUTPUT_DIR%"
)

echo.
echo Press any key to exit...
pause >nul
