@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "CERT=%~dp0ServerFlow-Local.cer"
set "AI=%~dp0ServerFlow.appinstaller"
set "MSIX="
for %%f in ("%~dp0ServerFlow_*_x64.msix") do if exist "%%~ff" set "MSIX=%%~ff"

if not defined MSIX (
  echo [ERROR] ServerFlow_*_x64.msix was not found next to this script.
  pause
  exit /b 1
)

rem Called back from the elevated branch: trust the certificate first.
if /i "%~1"=="elevated" goto trust

rem Certificate already trusted machine-wide: no administrator prompt needed.
powershell.exe -NoLogo -NoProfile -NonInteractive -Command "$c=New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('%CERT%'); exit([int](-not (Test-Path ('Cert:\LocalMachine\Root\' + $c.Thumbprint))))" >nul 2>&1
if errorlevel 1 goto elevate
goto install

:trust
echo Trusting the bundled test certificate for this computer...
"%SystemRoot%\System32\certutil.exe" -f -addstore Root "%CERT%" >nul
if errorlevel 1 goto failed
goto install

:install
echo Installing ServerFlow...
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $ai='%AI%'; $msix='%MSIX%'; if (Test-Path $ai) { try { Add-AppxPackage -Path $ai -AppInstallerFile -ForceTargetApplicationShutdown; exit 0 } catch {} }; Add-AppxPackage -Path $msix -ForceUpdateFromAnyVersion -ForceApplicationShutdown"
if errorlevel 1 goto failed
echo.
echo Done. Open "ServerFlow" from the Start menu.
pause
exit /b 0

:elevate
echo First install needs one administrator approval to trust the bundled test certificate.
echo Please choose Yes in the UAC window.
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Start-Process -FilePath $env:ComSpec -ArgumentList '/c','%~nx0 elevated' -WorkingDirectory '%~dp0' -Verb RunAs"
exit /b 0

:failed
echo.
echo Installation failed. Read the error above or see INSTALL.txt.
pause
exit /b 1
