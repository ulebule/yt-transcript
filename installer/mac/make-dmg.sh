#!/bin/bash
# Wraps a `dotnet publish` output folder into "YT Transcript.app" and a drag-to-Applications .dmg.
# Usage: installer/mac/make-dmg.sh <version> <rid: osx-arm64|osx-x64> <publish dir> <output dir>
set -euo pipefail

VERSION=$1
RID=$2
PUBLISH=$3
OUT=$4
ARCH=$([ "$RID" = "osx-arm64" ] && echo "apple-silicon" || echo "intel")

WORK=$(mktemp -d)
APP="$WORK/root/YT Transcript.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp -R "$PUBLISH"/. "$APP/Contents/MacOS/"
sed "s/__VERSION__/$VERSION/g" "$(dirname "$0")/Info.plist" > "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/YtTranscript"

# ad-hoc signature (required to run on Apple Silicon; not notarized)
codesign --force --deep --sign - "$APP"

ln -s /Applications "$WORK/root/Applications"
mkdir -p "$OUT"
hdiutil create -volname "YT Transcript" -srcfolder "$WORK/root" -ov -format UDZO \
  "$OUT/YtTranscript-$VERSION-mac-$ARCH.dmg"

rm -rf "$WORK"
