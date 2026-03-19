# SpellBinder Client

Portable game client for SpellBinder: The Nexus Conflict (1999). Connects to community servers — no manual config editing needed.

You can find the server here: https://github.com/Mindl-dev/Spellbinder

## For Players

Download the latest release zip for your platform, extract it, and double-click the launcher.

**Windows:** `Play.exe` — server picker, crash watchdog, dgVoodoo for fullscreen borderless
**macOS:** `SpellBinder.app` — server picker, CrossOver/Wine integration. See [Mac setup](https://github.com/gtech/spellbinder-client/blob/docs/mac-setup.md)

## For Developers

### Building from source

Prerequisites: Python 3, `spelinst.exe` in the repo root.

https://archive.org/details/spelinst

```bash
# 1. Extract + patch game content
python build_content.py spelinst.exe

# 2. Build client
.\client\build_win.ps1 -Release          # Windows → SpellBinder-win.zip
./client/build_mac.sh --release           # macOS   → SpellBinder-mac.zip
```

### Custom server address

```powershell
.\client\build_win.ps1 -Server "192.168.1.100"
```
```bash
./client/build_mac.sh --server 192.168.1.100
```

Players can also type any address directly in the server picker dropdown.

### Adding servers to the default list

**Windows:** Edit the `Servers` array in `Play.cs`:
```csharp
private static readonly string[][] Servers = new string[][]
{
    new[] { "Community Server", "45.33.60.131" },
    new[] { "Localhost", "127.0.0.1" },
    new[] { "My Server", "192.168.1.100" },
};
```

**macOS:** Edit `servers.txt` (bundled inside the .app at `Contents/Resources/servers.txt`):
```
# name|address
Community Server|45.33.60.131
Localhost|127.0.0.1
My Server|192.168.1.100
```

### File structure

```
Play.cs              # Windows launcher source (C# WinForms)
build_win.ps1        # Windows build script
build_mac.sh         # macOS build script
launch_mac.sh        # macOS runtime launcher
dgVoodoo.conf        # dgVoodoo config (fake fullscreen, 4:3, no watermark)
defaults/
  keyboard.dat       # Default keybind configuration
```

### How it works

**Windows:**
`Play.exe` sits at the root of the distribution. Game files live in `game/`. The launcher writes the server address to `game/main.dat`, then launches `game.dll` (which is actually an EXE despite the extension) directly — bypassing `spell.exe` which requires admin and shows an update dialog.

dgVoodoo 2.86 wraps DirectDraw → D3D11 for compatibility with modern Windows. Config: fake fullscreen, 4:3 aspect ratio, bilinear scaling.

**macOS:**
`SpellBinder.app` is a Cocoa .app bundle. The launcher detects CrossOver (recommended, gives correct wall rendering) or falls back to stock Wine via Homebrew (playable but some walls invisible due to a Wine wined3d framebuffer bug on macOS).

`game.dll` is copied to `game.exe` because Wine needs the `.exe` extension to execute it.
