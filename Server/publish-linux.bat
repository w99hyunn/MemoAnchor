@echo off
cd /d "%~dp0"
set "PUBLISH_OUTPUT=%~dp0Build\Linux"
echo [publish-linux] Building Linux self-contained (runtime included)...
dotnet publish -p:PublishProfile=Linux-SelfContained --output "%PUBLISH_OUTPUT%"
if %ERRORLEVEL% neq 0 (
    echo [publish-linux] Build failed.
    exit /b 1
)
set "RECONSTRUCTION_OUTPUT=%PUBLISH_OUTPUT%\Reconstruction"
if not exist "%RECONSTRUCTION_OUTPUT%" mkdir "%RECONSTRUCTION_OUTPUT%"
copy /Y "%~dp0Reconstruction\server.py" "%RECONSTRUCTION_OUTPUT%\server.py" >nul
copy /Y "%~dp0Reconstruction\visual_localizer.py" "%RECONSTRUCTION_OUTPUT%\visual_localizer.py" >nul
copy /Y "%~dp0Reconstruction\reconstruct_open3d.py" "%RECONSTRUCTION_OUTPUT%\reconstruct_open3d.py" >nul
copy /Y "%~dp0Reconstruction\requirements.txt" "%RECONSTRUCTION_OUTPUT%\requirements.txt" >nul
copy /Y "%~dp0Reconstruction\reconstruct_open3d_tsdf.py" "%RECONSTRUCTION_OUTPUT%\reconstruct_open3d_tsdf.py" >nul
copy /Y "%~dp0Reconstruction\reconstruction_common.py" "%RECONSTRUCTION_OUTPUT%\reconstruction_common.py" >nul
copy /Y "%~dp0Reconstruction\inspect_rgbd_frame.py" "%RECONSTRUCTION_OUTPUT%\inspect_rgbd_frame.py" >nul
echo.
echo [publish-linux] ASP.NET Core and Reconstruction package ready.
echo [publish-linux] Output: Build\Linux\

pause
exit /b 0
