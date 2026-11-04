#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="$ROOT/tools/dotnet8/dotnet.exe"
PROJECT="$ROOT/ServerScheduler/ServerScheduler.csproj"
PFX="$ROOT/tools/signing/ServerFlow-Local-Windows.pfx"
CER="$ROOT/tools/signing/ServerFlow-Local-Windows.cer"
SIGNTOOL="$HOME/.nuget/packages/microsoft.windows.sdk.buildtools/10.0.26100.4948/bin/10.0.26100.0/x64/signtool.exe"
APPX_DIR="$ROOT/dist/appx"
PACKAGE_DIR="$ROOT/dist/ServerFlow-Installer"
RELEASE_DIR="$ROOT/dist/release"
TEMPLATE="$ROOT/tools/installer-template"

if [[ ! -f "$PFX" || ! -f "$CER" ]]; then
  echo "Windows-compatible test certificate not found in tools/signing." >&2
  exit 1
fi

find "$APPX_DIR" -name '*.msix' -delete 2>/dev/null || true

"$DOTNET" publish "$PROJECT" \
  -c Release -r win-x64 -p:Platform=x64 --self-contained true \
  -p:WindowsPackageType=MSIX -p:AppxBundle=Never -p:UapAppxPackageBuildMode=SideloadOnly \
  -p:AppxPackageSigningEnabled=false -p:AppxPackageDir="$APPX_DIR/" \
  -p:GenerateAppxPackageOnBuild=true -v:minimal

MSIX="$(find "$APPX_DIR" -name '*.msix' -print -quit)"
if [[ -z "$MSIX" ]]; then
  echo "MSIX not found under $APPX_DIR" >&2
  exit 1
fi

export MSYS_NO_PATHCONV=1
PFX_WIN="$(cygpath -w "$PFX")"
MSIX_WIN="$(cygpath -w "$MSIX")"
"$SIGNTOOL" sign /fd SHA256 /f "$PFX_WIN" /p ServerFlow "$MSIX_WIN"

mkdir -p "$PACKAGE_DIR" "$RELEASE_DIR"
find "$PACKAGE_DIR" -maxdepth 1 -type f -delete
find "$RELEASE_DIR" -maxdepth 1 -type f -delete
cp "$MSIX" "$PACKAGE_DIR/ServerFlow_1.0.2.0_x64.msix"
cp "$CER" "$PACKAGE_DIR/ServerFlow-Local.cer"
cp "$TEMPLATE/Install-ServerFlow.cmd" "$PACKAGE_DIR/"
cp "$TEMPLATE/INSTALL.txt" "$PACKAGE_DIR/"
cp "$TEMPLATE/ServerFlow.appinstaller" "$PACKAGE_DIR/"

for file in "$PACKAGE_DIR/ServerFlow_1.0.2.0_x64.msix" "$PACKAGE_DIR/ServerFlow-Local.cer" \
            "$PACKAGE_DIR/Install-ServerFlow.cmd" "$PACKAGE_DIR/INSTALL.txt" \
            "$PACKAGE_DIR/ServerFlow.appinstaller"; do
  cp "$file" "$RELEASE_DIR/"
done

RELEASE_DIR_WIN="$(cygpath -w "$RELEASE_DIR")"
PACKAGE_DIR_WIN="$(cygpath -w "$PACKAGE_DIR")"
ZIP_WIN="$(cygpath -w "$ROOT/dist/ServerFlow-Setup.zip")"
python - "$RELEASE_DIR_WIN" "$PACKAGE_DIR_WIN" "$ZIP_WIN" <<'PY'
from pathlib import Path
import hashlib, sys, zipfile
release, folder, output = map(Path, sys.argv[1:])
for directory in (release, folder):
    cmd_path = directory / 'Install-ServerFlow.cmd'
    text = cmd_path.read_text(encoding='ascii').replace('\r\n', '\n').replace('\n', '\r\n')
    cmd_path.write_bytes(text.encode('ascii'))
checksums = []
for file in sorted(release.iterdir()):
    checksums.append(f"{hashlib.sha256(file.read_bytes()).hexdigest().upper()}  {file.name}\n")
(release / 'SHA256SUMS.txt').write_text(''.join(checksums), encoding='ascii')
(folder / 'SHA256SUMS.txt').write_text(''.join(checksums), encoding='ascii')
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for file in sorted(folder.iterdir()):
        archive.write(file, file.name)
print(f'ZIP: {output} ({output.stat().st_size:,} bytes)')
print((release / 'SHA256SUMS.txt').read_text(encoding='ascii'))
PY

echo "MSIX: $PACKAGE_DIR/ServerFlow_1.0.2.0_x64.msix"
echo "AppInstaller: $PACKAGE_DIR/ServerFlow.appinstaller"
echo "Release assets: $RELEASE_DIR"
