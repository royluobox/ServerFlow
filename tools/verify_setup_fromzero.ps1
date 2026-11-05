$ErrorActionPreference = 'Stop'
$root = 'C:/Users/机械/WorkBuddy/2026-11-04-14-44-55'
$log = Join-Path $root 'dist\setup-verify.log'
$tp = 'C16B8C5B509B82C3B679EAEE8309EA77437CCBD2'
function Log($text) { $text | Out-File -FilePath $log -Append -Encoding utf8 }

"=== verify start $(Get-Date -Format s) ===" | Out-File -FilePath $log -Encoding utf8

Get-Process -Name 'ServerScheduler' -ErrorAction SilentlyContinue | Stop-Process -Force
$pkg = Get-AppxPackage -Name 'ServerFlow.Scheduler'
if ($pkg) {
    Remove-AppxPackage -Package $pkg.PackageFullName
    Log "removed package $($pkg.PackageFullName)"
} else {
    Log 'package was not installed'
}

certutil.exe -delstore Root $tp 2>&1 | Out-Null
$certBefore = Test-Path ('Cert:/LocalMachine/Root/' + $tp)
Log "cert trusted before install: $certBefore"
Log "package installed before install: $([bool](Get-AppxPackage -Name 'ServerFlow.Scheduler'))"

$exe = Join-Path $root 'dist\ServerFlow-Setup.exe'
$p = Start-Process -FilePath $exe -ArgumentList '/quiet' -PassThru -Wait
Log "setup exit code: $($p.ExitCode)"

$pkg2 = Get-AppxPackage -Name 'ServerFlow.Scheduler'
if ($pkg2) {
    Log "installed: $($pkg2.PackageFullName) status=$($pkg2.Status)"
} else {
    Log 'NOT INSTALLED'
}
Log "cert trusted after install: $(Test-Path ('Cert:/LocalMachine/Root/' + $tp))"
Log "=== verify done $(Get-Date -Format s) ==="
