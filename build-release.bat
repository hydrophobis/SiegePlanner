@echo off
setlocal enabledelayedexpansion

echo ========================================
echo R6 Planner - Release Build Script
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

REM Display .NET version
echo Checking .NET SDK version...
dotnet --version
echo.

REM Set build configuration
set CONFIG=Release
set FRAMEWORK=net8.0-windows
set OUTPUT_DIR=release
set PUBLISH_DIR=%OUTPUT_DIR%\publish

echo Configuration: %CONFIG%
echo Framework: %FRAMEWORK%
echo Output Directory: %OUTPUT_DIR%
echo.

REM Clean previous builds
echo Cleaning previous builds...
if exist "%OUTPUT_DIR%" (
    rmdir /s /q "%OUTPUT_DIR%"
)
if exist "bin\%CONFIG%" (
    rmdir /s /q "bin\%CONFIG%"
)
if exist "obj\%CONFIG%" (
    rmdir /s /q "obj\%CONFIG%"
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

REM Build the project
echo Building project in %CONFIG% mode...
dotnet build R6Planner.csproj -c %CONFIG% --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed.
    pause
    exit /b 1
)
echo Build successful.
echo.

REM Publish self-contained executable
echo Publishing self-contained release...
dotnet publish R6Planner.csproj -c %CONFIG% -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -o "%PUBLISH_DIR%"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Publish failed.
    pause
    exit /b 1
)
echo.

REM Copy additional resources
echo Copying additional resources...
if exist "Maps" (
    xcopy /E /I /Y "Maps" "%PUBLISH_DIR%\Maps\"
)
if exist "Configs" (
    xcopy /E /I /Y "Configs" "%PUBLISH_DIR%\Configs\"
)
if exist "Operators" (
    xcopy /E /I /Y "Operators" "%PUBLISH_DIR%\Operators\"
)
echo.

REM Create version info file
echo Creating version info...
echo R6 Planner - Release Build > "%PUBLISH_DIR%\VERSION.txt"
echo Build Date: %DATE% %TIME% >> "%PUBLISH_DIR%\VERSION.txt"
echo Configuration: %CONFIG% >> "%PUBLISH_DIR%\VERSION.txt"
echo Framework: %FRAMEWORK% >> "%PUBLISH_DIR%\VERSION.txt"
echo.

REM Display build summary
echo ========================================
echo BUILD COMPLETE!
echo ========================================
echo.
echo Release files are located in: %PUBLISH_DIR%
echo Main executable: %PUBLISH_DIR%\R6Planner.exe
echo.
echo You can now distribute the contents of the '%PUBLISH_DIR%' folder.
echo.

REM Ask if user wants to open the release folder
set /p OPEN_FOLDER="Open release folder? (Y/N): "
if /i "%OPEN_FOLDER%"=="Y" (
    start "" "%PUBLISH_DIR%"
)

echo.
echo Press any key to exit...
pause >nul
