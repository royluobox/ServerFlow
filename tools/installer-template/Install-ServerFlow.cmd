@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoLogo -NoProfile -NonInteractive -Command "$i=[Security.Principal.WindowsIdentity]::GetCurrent(); $p=New-Object Security.Principal.WindowsPrincipal($i); if(-not $p.IsInRole(544)){exit 1}"
if errorlevel 1 goto elevate

echo Installing test certificate to LocalMachine Root...
"%SystemRoot%\System32\certutil.exe" -f -addstore Root "ServerFlow-Local.cer"
if errorlevel 1 goto failed

echo Installing ServerFlow with App Installer update source...
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; try { Add-AppxPackage -Path (Join-Path $PWD 'ServerFlow.appinstaller') -AppInstallerFile -ForceTargetApplicationShutdown } catch { Add-AppxPackage -Path (Join-Path $PWD 'ServerFlow_x64.msix') -ForceUpdateFromAnyVersion -ForceApplicationShutdown }"
if errorlevel 1 goto failed

echo.
echo ServerFlow installed. Updates are checked on launch and in the background.
pause
exit /b 0

:elevate
echo Requesting administrator approval. Please choose Yes in the UAC window.
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'cmd.exe' -ArgumentList '/c','Install-ServerFlow.cmd' -WorkingDirectory $PWD -Verb RunAs"
exit /b 0

:failed
echo.
echo Installation failed. See the error above or read INSTALL.txt.
pause
exit /b 1
