#!/bin/bash
set -e

echo "=== Building Classic macOS Launchpad ==="

# Define target directories
APP_DIR="Launchpad Classic.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

# Clean previous build if it exists
if [ -d "$APP_DIR" ]; then
    echo "Cleaning old build..."
    rm -rf "$APP_DIR"
fi

echo "Creating application bundle directory structure..."
mkdir -p "$MACOS_DIR"
mkdir -p "$RESOURCES_DIR"

echo "Compiling Swift files..."
SDK_PATH=$(xcrun --show-sdk-path --sdk macosx)

swiftc -O \
    -sdk "$SDK_PATH" \
    AppScanner.swift \
    VisualEffectView.swift \
    LaunchpadView.swift \
    main.swift \
    -o "$MACOS_DIR/Launchpad"

echo "Copying Info.plist..."
cp Info.plist "$CONTENTS_DIR/Info.plist"

# Optional version stamping for release builds (APP_VERSION=1.2 ./build.sh)
if [ -n "${APP_VERSION:-}" ]; then
    echo "Stamping version $APP_VERSION..."
    /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $APP_VERSION" "$CONTENTS_DIR/Info.plist"
    /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $APP_VERSION" "$CONTENTS_DIR/Info.plist"
fi

echo "Copying AppIcon.icns..."
cp AppIcon.icns "$RESOURCES_DIR/AppIcon.icns"

echo "Setting permissions..."
chmod +x "$MACOS_DIR/Launchpad"

echo "Copying to /tmp to bypass iCloud Drive file provider restrictions for codesigning..."
TMP_APP_DIR="/tmp/Launchpad_Classic_build.app"
rm -rf "$TMP_APP_DIR"
cp -R "$APP_DIR" "$TMP_APP_DIR"

echo "Cleaning extended attributes in /tmp..."
xattr -cr "$TMP_APP_DIR"

# Use a real Developer ID when provided (CODESIGN_IDENTITY="Developer ID Application: ..."),
# otherwise fall back to ad-hoc signing for local builds.
IDENTITY="${CODESIGN_IDENTITY:--}"
if [ "$IDENTITY" = "-" ]; then
    echo "Ad-hoc codesigning in /tmp..."
else
    echo "Codesigning with identity: $IDENTITY"
fi
codesign --force --deep --sign "$IDENTITY" "$TMP_APP_DIR"

echo "Copying signed app back..."
rm -rf "$APP_DIR"
cp -R "$TMP_APP_DIR" "$APP_DIR"
rm -rf "$TMP_APP_DIR"

echo "Verifying signature..."
codesign --verify --verbose "$APP_DIR"

echo "=== Build Successful! ==="
echo "Launchpad Classic.app has been created at: $(pwd)/$APP_DIR"
