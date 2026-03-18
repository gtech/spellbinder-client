#!/bin/bash
# build_mac.sh — Build a distributable SpellBinder.app for macOS
# Usage: ./client/build_mac.sh [--server IP] [--release]
# Prerequisites: run build_content.py first
# Requires: CrossOver (recommended) or Wine via Homebrew
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
GAME_DIR="${REPO_ROOT}/GameFiles"
APP_NAME="SpellBinder"
APP_BUNDLE="${APP_NAME}.app"
SERVER=""

# Parse args
while [ $# -gt 0 ]; do
    case "$1" in
        --server) SERVER="$2"; shift 2 ;;
        --release) RELEASE=1; shift ;;
        *) GAME_DIR="$1"; shift ;;
    esac
done

# --- Validate game files ---
if [ ! -f "$GAME_DIR/game.dll" ] && [ ! -f "$GAME_DIR/game.exe" ]; then
    echo "ERROR: game.dll/game.exe not found in $GAME_DIR"
    echo "Run 'python build_content.py' first."
    exit 1
fi

# --- Build .app bundle ---
echo "Building ${APP_BUNDLE}..."
rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources/game"

# Copy game files (skip Windows-only wrappers)
echo "Copying game files..."
for f in "$GAME_DIR"/*; do
    base="$(basename "$f")"
    case "$base" in
        DDraw.dll|D3DImm.dll|D3D8.dll|D3D9.dll|dgVoodoo*|UNWISE.EXE|spell.exe|Play.exe|play.exe)
            echo "  Skipping $base (not needed on Mac)"
            ;;
        *)
            cp -r "$f" "$APP_BUNDLE/Contents/Resources/game/"
            ;;
    esac
done

# Ensure game.exe exists (the game binary is game.dll but Wine needs .exe)
if [ ! -f "$APP_BUNDLE/Contents/Resources/game/game.exe" ]; then
    cp "$APP_BUNDLE/Contents/Resources/game/game.dll" "$APP_BUNDLE/Contents/Resources/game/game.exe"
fi

# Server config
cat > "$APP_BUNDLE/Contents/Resources/servers.txt" << 'SERVERS'
# SpellBinder server list — one per line: name|address
# Edit this file to add/remove servers
Community Server|45.33.60.131
Localhost|127.0.0.1
SERVERS

# Default keybinds
if [ -d "$SCRIPT_DIR/defaults" ]; then
    cp "$SCRIPT_DIR/defaults/"* "$APP_BUNDLE/Contents/Resources/game/" 2>/dev/null
fi

# Launch script
cp "$SCRIPT_DIR/launch_mac.sh" "$APP_BUNDLE/Contents/MacOS/launch.sh"
chmod +x "$APP_BUNDLE/Contents/MacOS/launch.sh"

# Native Cocoa launcher (registers with macOS window server)
if command -v clang &>/dev/null; then
    echo "Compiling native Cocoa launcher..."
    cat > /tmp/SpellBinderLauncher.m << 'OBJC'
#import <Cocoa/Cocoa.h>

@interface AppDelegate : NSObject <NSApplicationDelegate>
@end

@implementation AppDelegate
- (void)applicationDidFinishLaunching:(NSNotification *)notification {
    NSString *appDir = [[NSBundle mainBundle] bundlePath];
    NSString *launchScript = [NSString stringWithFormat:@"%@/Contents/MacOS/launch.sh", appDir];
    NSTask *task = [[NSTask alloc] init];
    [task setLaunchPath:@"/bin/bash"];
    [task setArguments:@[launchScript]];
    [task launch];
    [task waitUntilExit];
    [NSApp terminate:nil];
}
@end

int main(int argc, const char *argv[]) {
    @autoreleasepool {
        NSApplication *app = [NSApplication sharedApplication];
        [app setActivationPolicy:NSApplicationActivationPolicyRegular];
        AppDelegate *delegate = [[AppDelegate alloc] init];
        [app setDelegate:delegate];
        [app run];
    }
    return 0;
}
OBJC
    clang -framework Cocoa -o "$APP_BUNDLE/Contents/MacOS/$APP_NAME" /tmp/SpellBinderLauncher.m
    rm /tmp/SpellBinderLauncher.m
else
    # Fallback: shell script launcher
    cat > "$APP_BUNDLE/Contents/MacOS/$APP_NAME" << EXEC
#!/bin/bash
DIR="\$(cd "\$(dirname "\$0")" && pwd)"
exec "\$DIR/launch.sh"
EXEC
    chmod +x "$APP_BUNDLE/Contents/MacOS/$APP_NAME"
fi

# Info.plist
cat > "$APP_BUNDLE/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>SpellBinder</string>
    <key>CFBundleDisplayName</key>
    <string>SpellBinder: The Nexus Conflict</string>
    <key>CFBundleExecutable</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>net.spellbinder.client</string>
    <key>CFBundleVersion</key>
    <string>0.1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

# Set server address if provided
if [ -n "$SERVER" ]; then
    echo "Setting server to $SERVER"
    MAIN_DAT="$APP_BUNDLE/Contents/Resources/game/main.dat"
    [ -f "$MAIN_DAT" ] && sed -i '' "s/^address=.*/address=${SERVER}/" "$MAIN_DAT"
fi

echo ""
echo "=== Built ${APP_BUNDLE} ==="
echo "Game files: ${APP_BUNDLE}/Contents/Resources/game/"
echo "Server list: ${APP_BUNDLE}/Contents/Resources/servers.txt"
echo ""
echo "Prerequisites for users:"
echo "  CrossOver (recommended): codeweavers.com/crossover"
echo "  Or stock Wine: brew tap gcenx/wine && brew install --cask wine-crossover"

if [ -n "$RELEASE" ]; then
    ZIP_NAME="SpellBinder-mac.zip"
    rm -f "$ZIP_NAME"
    echo ""
    echo "Creating $ZIP_NAME..."
    zip -r -q "$ZIP_NAME" "$APP_BUNDLE"
    SIZE=$(du -sh "$ZIP_NAME" | cut -f1)
    echo "Created $ZIP_NAME ($SIZE)"
else
    echo ""
    echo "To distribute: zip -r SpellBinder-mac.zip ${APP_BUNDLE}"
fi
