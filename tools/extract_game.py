#!/usr/bin/env python3
"""
Extract game data from SpellBinder: The Nexus Conflict full installer.

Download spelinst.exe from the Internet Archive and place in the repo root.

Usage:
  python3 extract_game.py [path/to/spelinst.exe] [output_dir]
"""

import struct
import zlib
import sys
import os
import json


# Offsets for the full game installer (spelinst.exe, MD5: c4670fd9097ceac07b527349d836a4c3)
# Found via tools/find_wise_offsets.py and tools/find_data_base2.py
SCRIPT_OFFSET = 0x4DAB
DATA_BASE = 0x20F1B


def extract_wise_installer(exe_path, output_dir):
    """Extract files from a Wise installer executable."""
    with open(exe_path, "rb") as f:
        exe_data = f.read()

    print(f"Read {len(exe_data)} bytes from {exe_path}")

    if b"WiseMain" not in exe_data:
        print("ERROR: This doesn't appear to be the SpellBinder Wise installer.")
        sys.exit(1)

    # Decompress the installer script
    d = zlib.decompressobj(-zlib.MAX_WBITS)
    script = d.decompress(exe_data[SCRIPT_OFFSET:])
    print(f"Installer script: {len(script)} bytes")

    # Parse file entries from the script.
    # Each game file has a 41-byte descriptor immediately before its path:
    #   [action(1)] [start_off(4)] [end_off(4)] [date(2)] [time(2)]
    #   [decomp_size(4)] [padding(20)] [crc32(4)]
    # Followed by: %MAINDIR%\<path>\0
    entries = []
    idx = 0
    while True:
        idx = script.find(b"%MAINDIR%\\", idx)
        if idx == -1:
            break

        # Find end of path (null terminator)
        end = idx
        while end < len(script) and script[end] != 0:
            end += 1
        path = script[idx:end].decode("ascii", errors="replace")

        # Read the 41-byte descriptor before the path
        # CRC32 is the 4 bytes immediately before the path
        desc_start = idx - 4 - 37  # 4 (CRC) + 37 (rest of descriptor)
        if desc_start < 0:
            idx = end + 1
            continue

        action = script[desc_start]
        off_start = struct.unpack_from("<I", script, desc_start + 1)[0]
        off_end = struct.unpack_from("<I", script, desc_start + 5)[0]
        date = struct.unpack_from("<H", script, desc_start + 9)[0]
        time_val = struct.unpack_from("<H", script, desc_start + 11)[0]
        decomp_size = struct.unpack_from("<I", script, desc_start + 13)[0]
        crc32 = struct.unpack_from("<I", script, idx - 4)[0]

        # Filter for valid file installation entries
        if action == 1 and off_start < off_end and decomp_size < 50_000_000:
            clean_path = path.replace("%MAINDIR%\\", "", 1).replace("\\", os.sep)
            entries.append({
                "path": clean_path,
                "off_start": off_start,
                "off_end": off_end,
                "compressed_size": off_end - off_start,
                "decomp_size": decomp_size,
                "date": date,
                "time": time_val,
                "crc32": crc32,
            })

        idx = end + 1

    print(f"Found {len(entries)} game files in installer script")

    # Extract each file
    os.makedirs(output_dir, exist_ok=True)
    extracted = 0
    failed = 0

    for entry in entries:
        abs_offset = DATA_BASE + entry["off_start"]
        dest_path = os.path.join(output_dir, entry["path"])

        # Create subdirectories as needed
        dest_dir = os.path.dirname(dest_path)
        if dest_dir:
            os.makedirs(dest_dir, exist_ok=True)

        try:
            d = zlib.decompressobj(-zlib.MAX_WBITS)
            result = d.decompress(
                exe_data[abs_offset : abs_offset + entry["compressed_size"] + 256]
            )

            if len(result) != entry["decomp_size"]:
                print(
                    f"  WARN: {entry['path']} size mismatch: "
                    f"got {len(result)}, expected {entry['decomp_size']}"
                )

            with open(dest_path, "wb") as f:
                f.write(result)
            extracted += 1

        except Exception as e:
            print(f"  FAIL: {entry['path']}: {e}")
            failed += 1

    print(f"Extracted {extracted} files ({failed} failed) to {output_dir}/")

    # Save manifest
    manifest = {
        "source": os.path.basename(exe_path),
        "source_size": len(exe_data),
        "files": [
            {
                "path": e["path"],
                "size": e["decomp_size"],
                "compressed_size": e["compressed_size"],
            }
            for e in entries
        ],
    }
    manifest_path = os.path.join(output_dir, "manifest.json")
    with open(manifest_path, "w") as f:
        json.dump(manifest, f, indent=2)
    print(f"Manifest saved to {manifest_path}")

    return entries


def extract_spells(output_dir):
    """Parse SPELL.BIN and output spell data as JSON."""
    spell_bin = os.path.join(output_dir, "SPELL.BIN")
    if not os.path.exists(spell_bin):
        print("SPELL.BIN not found in extracted files")
        return

    print(f"\nParsing spells from {spell_bin}...")

    with open(spell_bin, "rb") as f:
        data = f.read()

    HEADER_SIZE = 0x0C
    RECORD_SIZE = 0x144  # 324 bytes
    NAME_LENGTH = 20

    num_records = (len(data) - HEADER_SIZE) // RECORD_SIZE
    spells = []

    for i in range(num_records):
        offset = HEADER_SIZE + i * RECORD_SIZE
        rec = data[offset : offset + RECORD_SIZE]

        name_bytes = rec[:NAME_LENGTH]
        null_idx = name_bytes.find(b"\x00")
        if null_idx > 0:
            name = name_bytes[:null_idx].decode("ascii", errors="replace").strip()
        else:
            name = ""

        if not name or not name[0].isalpha():
            continue

        spell = {
            "index": i,
            "name": name,
            "damage_base": struct.unpack_from("<i", rec, 0x14)[0],
            "field_18": struct.unpack_from("<i", rec, 0x18)[0],
            "field_1c": struct.unpack_from("<i", rec, 0x1C)[0],
            "field_24": struct.unpack_from("<i", rec, 0x24)[0],
            "level_req": struct.unpack_from("<i", rec, 0x48)[0],
            "field_50": struct.unpack_from("<i", rec, 0x50)[0],
            "fatigue_cost": struct.unpack_from("<i", rec, 0x54)[0],
            "range": struct.unpack_from("<i", rec, 0x58)[0],
            "field_64": struct.unpack_from("<i", rec, 0x64)[0],
            "speed": struct.unpack_from("<i", rec, 0x74)[0],
            "field_84": struct.unpack_from("<i", rec, 0x84)[0],
            "damage_dice_count": struct.unpack_from("<i", rec, 0x90)[0],
            "damage_dice_size": struct.unpack_from("<i", rec, 0x94)[0],
            "damage_bonus": struct.unpack_from("<i", rec, 0x98)[0],
            "cast_timer": struct.unpack_from("<i", rec, 0xA4)[0],
        }
        spells.append(spell)

    spell_path = os.path.join(output_dir, "spells.json")
    with open(spell_path, "w") as f:
        json.dump(spells, f, indent=2)
    print(f"Extracted {len(spells)} spells to {spell_path}")

    summary_path = os.path.join(output_dir, "spells_summary.txt")
    with open(summary_path, "w") as f:
        f.write(
            f"{'#':>3}  {'Name':<24} {'Dmg':>4} {'Dice':>8} {'Fat':>4} "
            f"{'Rng':>4} {'Spd':>5} {'Cast':>5} {'Lvl':>3}\n"
        )
        f.write("-" * 75 + "\n")
        for s in spells:
            dice = (
                f"{s['damage_dice_count']}d{s['damage_dice_size']}"
                f"+{s['damage_bonus']}"
            )
            f.write(
                f"{s['index']:3d}  {s['name']:<24} {s['damage_base']:4d} "
                f"{dice:>8} {s['fatigue_cost']:4d} {s['range']:4d} "
                f"{s['speed']:5d} {s['cast_timer']:5d} {s['level_req']:3d}\n"
            )
    print(f"Spell summary saved to {summary_path}")

    return spells


if __name__ == "__main__":
    exe_path = sys.argv[1] if len(sys.argv) > 1 else "spelinst.exe"
    output_dir = sys.argv[2] if len(sys.argv) > 2 else "Content"

    if not os.path.exists(exe_path):
        print(f"ERROR: {exe_path} not found.")
        print("Download from the Internet Archive and place in the repo root.")
        sys.exit(1)

    extract_wise_installer(exe_path, output_dir)
    extract_spells(output_dir)

    print(f"\nDone! Game files extracted to {output_dir}/")
    print(f"  {output_dir}/manifest.json    - Full file listing")
    print(f"  {output_dir}/game.dll         - Main game executable")
    print(f"  {output_dir}/SPELL.BIN        - Spell data")
    print(f"  {output_dir}/spells.json      - Parsed spell data")
