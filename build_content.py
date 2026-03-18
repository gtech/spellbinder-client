#!/usr/bin/env python3
"""
Extract game files from spelinst.exe into GameFiles/

GameFiles/ contains the extracted + patched client game data. Used by:
  - Client build scripts (copies into distributable zips)
  - Docker server (mount as volume at /app/Content)

Separate from Content/ which has server config files (Spells.dat, Arenas.dat, .sql).

Usage:
  python build_content.py                    # looks for spelinst.exe in current dir
  python build_content.py path/to/spelinst.exe

Prerequisites:
  Place spelinst.exe (MD5: c4670fd9097ceac07b527349d836a4c3) in the repo root.
  Download from the Internet Archive if needed.
"""

import hashlib
import os
import shutil
import subprocess
import sys

EXPECTED_MD5 = "c4670fd9097ceac07b527349d836a4c3"
CONTENT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "GameFiles")
PATCHES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "patches")
TOOLS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tools")


def md5sum(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(8192), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    # Find installer
    exe_path = sys.argv[1] if len(sys.argv) > 1 else "spelinst.exe"
    if not os.path.exists(exe_path):
        print(f"ERROR: {exe_path} not found.")
        print("Place spelinst.exe in the repo root directory.")
        print("Download from: https://archive.org/details/SpellbinderTheNexusConflict")
        sys.exit(1)

    # Verify MD5
    actual_md5 = md5sum(exe_path)
    if actual_md5 != EXPECTED_MD5:
        print(f"WARNING: MD5 mismatch!")
        print(f"  Expected: {EXPECTED_MD5}")
        print(f"  Actual:   {actual_md5}")
        resp = input("  Continue anyway? [y/N] ")
        if resp.lower() != "y":
            sys.exit(1)
    else:
        print(f"Verified: {exe_path} (MD5 OK)")

    # Extract
    print(f"\n=== Extracting to {CONTENT_DIR} ===")
    if os.path.exists(CONTENT_DIR):
        print(f"Removing existing {CONTENT_DIR}...")
        shutil.rmtree(CONTENT_DIR)

    extract_script = os.path.join(TOOLS_DIR, "extract_game.py")
    subprocess.check_call([sys.executable, extract_script, exe_path, CONTENT_DIR])

    # Patch game.dll for community server compatibility
    game_dll = os.path.join(CONTENT_DIR, "game.dll")
    if os.path.exists(game_dll):
        print(f"\n=== Patching game.dll ===")
        patch_script = os.path.join(PATCHES_DIR, "apply_patches.py")
        patch_def = os.path.join(PATCHES_DIR, "full_v202_discord.json")
        if os.path.exists(patch_def):
            subprocess.check_call([
                sys.executable, patch_script, game_dll, patch_def,
                "--output", game_dll
            ])
        else:
            print(f"  Patch file not found: {patch_def}, skipping")

    # Patch spell.bin with discord community balance changes
    spell_bin = os.path.join(CONTENT_DIR, "spell.bin")
    if os.path.exists(spell_bin):
        spell_patch = os.path.join(PATCHES_DIR, "spellbin_discord.json")
        if os.path.exists(spell_patch):
            print(f"\n=== Patching spell.bin ===")
            subprocess.check_call([
                sys.executable, patch_script, spell_bin, spell_patch,
                "--output", spell_bin
            ])
        else:
            print(f"  spell.bin patch not found, skipping")

    # Create game.exe copy (Wine on Mac needs .exe extension)
    if os.path.exists(game_dll):
        game_exe = os.path.join(CONTENT_DIR, "game.exe")
        shutil.copy2(game_dll, game_exe)
        print(f"Created game.exe (copy of patched game.dll)")

    print(f"\n=== Content ready ===")
    print(f"  {CONTENT_DIR}/")
    print(f"  Used by: docker (volume mount), client builds")
    print(f"")
    print(f"Next steps:")
    print(f"  Build Windows client:  .\\client\\build_win.ps1")
    print(f"  Build Mac client:      ./client/build_mac.sh {CONTENT_DIR}")
    print(f"  Docker server:         docker compose up (mounts Content/)")


if __name__ == "__main__":
    main()
