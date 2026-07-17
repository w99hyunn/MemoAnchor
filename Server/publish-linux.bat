@echo off
cd /d "%~dp0"
echo [publish-linux] Building Linux self-contained (runtime included)...
dotnet publish -p:PublishProfile=Linux-SelfContained
if %ERRORLEVEL% neq 0 (
    echo [publish-linux] Build failed.
    exit /b 1
)
echo.
echo [publish-linux] Done. Output: bin\Release\net8.0\linux-x64\self-contained\publish\

pause
exit /b 0
