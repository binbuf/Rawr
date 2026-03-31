#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
VERSION="${1:-1.0.0}"
RID="${2:-osx-arm64}"
ARCH_SUFFIX="${RID#osx-}"
PUBLISH_DIR="$REPO_ROOT/publish/$RID"
APP_NAME="Rawr"
APP_BUNDLE="$SCRIPT_DIR/$APP_NAME.app"
OUTPUT_DIR="$SCRIPT_DIR/output"

# --- Convert .ico to .icns ---
ICONSET_DIR="$SCRIPT_DIR/Rawr.iconset"
rm -rf "$ICONSET_DIR"
mkdir -p "$ICONSET_DIR"

# Extract PNG from ICO
sips -s format png "$REPO_ROOT/src/Rawr.UI/Assets/dinosaur.ico" \
     --out "$ICONSET_DIR/icon_512x512.png" \
     --resampleWidth 512

# Generate required icon sizes
for size in 16 32 64 128 256; do
    sips -z $size $size "$ICONSET_DIR/icon_512x512.png" \
         --out "$ICONSET_DIR/icon_${size}x${size}.png"
done

# Generate @2x variants
cp "$ICONSET_DIR/icon_512x512.png" "$ICONSET_DIR/icon_256x256@2x.png"
sips -z 1024 1024 "$ICONSET_DIR/icon_512x512.png" \
     --out "$ICONSET_DIR/icon_512x512@2x.png" 2>/dev/null || \
     cp "$ICONSET_DIR/icon_512x512.png" "$ICONSET_DIR/icon_512x512@2x.png"
for size in 16 32 128; do
    double=$((size * 2))
    cp "$ICONSET_DIR/icon_${double}x${double}.png" \
       "$ICONSET_DIR/icon_${size}x${size}@2x.png"
done

iconutil -c icns "$ICONSET_DIR" -o "$SCRIPT_DIR/Rawr.icns"
rm -rf "$ICONSET_DIR"

# --- Create .app bundle ---
rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy published binaries
cp -R "$PUBLISH_DIR/"* "$APP_BUNDLE/Contents/MacOS/"
chmod +x "$APP_BUNDLE/Contents/MacOS/Rawr.UI"

# Copy Info.plist and inject version
cp "$SCRIPT_DIR/Info.plist" "$APP_BUNDLE/Contents/"
plutil -replace CFBundleVersion -string "$VERSION" "$APP_BUNDLE/Contents/Info.plist"
plutil -replace CFBundleShortVersionString -string "$VERSION" "$APP_BUNDLE/Contents/Info.plist"

# Copy icon
cp "$SCRIPT_DIR/Rawr.icns" "$APP_BUNDLE/Contents/Resources/"

# --- Create DMG ---
mkdir -p "$OUTPUT_DIR"

# Remove existing DMG if present (create-dmg fails otherwise)
rm -f "$OUTPUT_DIR/Rawr-${ARCH_SUFFIX}.dmg"

create-dmg \
    --volname "$APP_NAME" \
    --volicon "$SCRIPT_DIR/Rawr.icns" \
    --window-pos 200 120 \
    --window-size 600 400 \
    --icon-size 100 \
    --icon "$APP_NAME.app" 150 185 \
    --app-drop-link 450 185 \
    --hide-extension "$APP_NAME.app" \
    "$OUTPUT_DIR/Rawr-${ARCH_SUFFIX}.dmg" \
    "$APP_BUNDLE"

echo "DMG created: $OUTPUT_DIR/Rawr-${ARCH_SUFFIX}.dmg"
