@echo off
cd /d "%~dp0"
echo [publish-linux] Building Linux self-contained (runtime included)...
dotnet publish -p:PublishProfile=Linux-SelfContained
if %ERRORLEVEL% neq 0 (
    echo [publish-linux] Build failed.
    exit /b 1
)
set "PUBLISH_OUTPUT=%~dp0bin\Release\net8.0\linux-x64\self-contained\publish"
set "RECONSTRUCTION_OUTPUT=%PUBLISH_OUTPUT%\Reconstruction"
if not exist "%RECONSTRUCTION_OUTPUT%" mkdir "%RECONSTRUCTION_OUTPUT%"
copy /Y "%~dp0..\tools\reconstruction_server\server.py" "%RECONSTRUCTION_OUTPUT%\server.py" >nul
copy /Y "%~dp0..\tools\reconstruction_server\reconstruct_open3d.py" "%RECONSTRUCTION_OUTPUT%\reconstruct_open3d.py" >nul
copy /Y "%~dp0..\tools\reconstruction_server\requirements.txt" "%RECONSTRUCTION_OUTPUT%\requirements.txt" >nul
copy /Y "%~dp0..\tools\reconstruction\reconstruct_open3d_tsdf.py" "%RECONSTRUCTION_OUTPUT%\reconstruct_open3d_tsdf.py" >nul
copy /Y "%~dp0..\tools\reconstruction\reconstruction_common.py" "%RECONSTRUCTION_OUTPUT%\reconstruction_common.py" >nul
copy /Y "%~dp0..\tools\reconstruction\inspect_rgbd_frame.py" "%RECONSTRUCTION_OUTPUT%\inspect_rgbd_frame.py" >nul
echo.
echo [publish-linux] ASP.NET Core and Reconstruction package ready.
echo [publish-linux] Output: bin\Release\net8.0\linux-x64\self-contained\publish\

pause
exit /b 0
