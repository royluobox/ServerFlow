#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="$ROOT/tools/dotnet8/dotnet.exe"
PROJECT="$ROOT/SetupLauncher/SetupLauncher.csproj"
PFX="$ROOT/tools/signing/ServerFlow-Local-Windows.pfx"
SIGNTOOL="$HOME/.nuget/packages/microsoft.windows.sdk.buildtools/10.0.26100.4948/bin/10.0.26100.0/x64/signtool.exe"
PAYLOAD="$ROOT/SetupLauncher/payload"

MSIX="$(find "$ROOT/dist/release" -maxdepth 1 -name 'ServerFlow_*_x64.msix' -print -quit)"
if [[ -z "$MSIX" ]]; then
  echo "Release MSIX not found under dist/release; run tools/build_msix.sh first." >&2
  exit 1
fi
VERSION="$(basename "$MSIX" | sed -E 's/^ServerFlow_(.*)_x64\.msix$/\1/')"

mkdir -p "$PAYLOAD"
cp "$MSIX" "$PAYLOAD/ServerFlow.msix"
cp "$ROOT/dist/release/ServerFlow-Local.cer" "$PAYLOAD/ServerFlow-Local.cer"

"$DOTNET" build "$PROJECT" -c Release -p:Version="$VERSION" -p:FileVersion="$VERSION" -v:minimal

EXE="$ROOT/SetupLauncher/bin/Release/ServerFlow-Setup.exe"
if [[ ! -f "$EXE" ]]; then
  echo "Setup executable not found at $EXE" >&2
  exit 1
fi

cp "$EXE" "$ROOT/dist/release/ServerFlow-Setup.exe"

export MSYS_NO_PATHCONV=1
"$SIGNTOOL" sign /fd SHA256 /f "$(cygpath -w "$PFX")" /p ServerFlow "$(cygpath -w "$ROOT/dist/release/ServerFlow-Setup.exe")"
"$SIGNTOOL" verify /pa "$(cygpath -w "$ROOT/dist/release/ServerFlow-Setup.exe")"
cp "$ROOT/dist/release/ServerFlow-Setup.exe" "$ROOT/dist/ServerFlow-Setup.exe"

python - "$(cygpath -w "$ROOT/dist/release")" "$(cygpath -w "$ROOT/dist/ServerFlow-Setup.exe")" <<'PY'
import sys, hashlib
from pathlib import Path
release, setup = Path(sys.argv[1]), Path(sys.argv[2])
checksums = []
for file in sorted(release.iterdir()):
    if file.name == 'SHA256SUMS.txt':
        continue
    checksums.append(f"{hashlib.sha256(file.read_bytes()).hexdigest().upper()}  {file.name}\n")
(release / 'SHA256SUMS.txt').write_text(''.join(checksums), encoding='ascii')
print(f"Setup EXE: {setup} ({setup.stat().st_size:,} bytes)")
print(f"SHA256: {hashlib.sha256(setup.read_bytes()).hexdigest().upper()}")
print((release / 'SHA256SUMS.txt').read_text(encoding='ascii'))
PY
