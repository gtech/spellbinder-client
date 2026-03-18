# SpellBinder: Mac Setup Guide

SpellBinder (1999) runs on macOS via Wine. There are two paths — CrossOver gives perfect rendering, stock Wine has a known visual glitch.

## Option A: CrossOver (Recommended)

CrossOver provides the best rendering — all walls and geometry display correctly.

### Prerequisites
- **CrossOver** — [codeweavers.com/crossover](https://www.codeweavers.com/crossover) ($74 or 14-day free trial)

### Setup (one-time, ~2 minutes)

1. Install CrossOver and open it
2. Create a bottle: **Bottle** menu → **New Bottle**
   - Name: `spellbinder`
   - Template: **Windows XP**
   - Click **Create** (wait for it to finish)
3. Download the SpellBinder Mac release zip and extract it
4. Double-click `SpellBinder.app`
   - Pick a server and click OK
   - CrossOver opens — in the dialog, click **Run Command**
   - Browse to `C:\game\game.exe`
   - Click **Run**

**Important:** The bottle must be created through CrossOver's GUI, not the command line. The GUI sets up the display context correctly.

### After first launch

Once you've run the game once, CrossOver remembers it. For future launches:
1. Double-click `SpellBinder.app` (sets server address)
2. In CrossOver, the game appears in the bottle's program list — just click it

## Option B: Stock Wine (Free, visual glitch)

Free but has a known rendering issue — some walls are invisible. Playable for casual games but gives an unfair advantage in competitive play.

### Prerequisites
- **Homebrew** — [brew.sh](https://brew.sh)
- **XQuartz** — installed via Homebrew
- **Wine** — installed via Homebrew

### Install

```bash
# Install Homebrew (if not already installed)
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Install Wine and XQuartz
brew install --cask xquartz
brew tap gcenx/wine
brew install --cask wine-crossover
```

**Log out and back in** after installing XQuartz.

### Build the .app

```bash
git clone https://github.com/gtech/Spellslinger.git
cd Spellslinger
./client/build_mac.sh /path/to/game/files
```

This creates `SpellBinder.app` — double-click to play.

### Known Issues (Stock Wine)

| Issue | Status | Notes |
|-------|--------|-------|
| Some walls invisible | Known | Wine's wined3d `GL_INVALID_FRAMEBUFFER_OPERATION` on macOS. CrossOver fixes this. |
| XQuartz terminal appears | Cosmetic | An X11 terminal window opens alongside the game. Minimize it. |
| Sluggish performance | Known | Intel integrated graphics + Wine overhead. Playable but not smooth. |

## Technical Details

The wall rendering issue is caused by Wine's OpenGL framebuffer setup failing on macOS:

```
GL_INVALID_FRAMEBUFFER_OPERATION (0x506) from glClear
```

Apple's OpenGL implementation (capped at 4.1, deprecated since macOS 10.14) doesn't fully support the framebuffer configuration that Wine's `wined3d` creates for old Direct3D Immediate Mode games. CodeWeavers' CrossOver includes patches to `wined3d` that work around this.

The CrossOver source is open (LGPL) at [github.com/Gcenx/winecx](https://github.com/Gcenx/winecx). The relevant patches are in `dlls/wined3d/`. The free Homebrew `wine-crossover` package is based on an older version (23.7.1) that doesn't include the fix — CrossOver 26.0 does.

## File Structure

```
SpellBinder.app/
  Contents/
    MacOS/
      SpellBinder       # Native Cocoa launcher binary
      launch.sh         # Shell script that picks server and launches Wine
    Resources/
      game/             # Game files (game.exe, main.dat, data files)
      servers.txt       # Editable server list (name|address per line)
      wineprefix/       # Wine prefix (created on first launch)
    Info.plist
```

### Adding Servers

Edit `SpellBinder.app/Contents/Resources/servers.txt`:

```
# name|address
Community Server|45.33.60.131
Localhost|127.0.0.1
My Server|192.168.1.100
```
