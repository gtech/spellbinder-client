#!/usr/bin/env python3
"""
SpellBinder Binary Patcher

Applies documented patches to a clean game binary. Verifies MD5 checksums
before and after to ensure correctness. Does not distribute copyrighted code.

Usage:
  python3 apply_patches.py <clean_binary> [patch_file] [--output <path>]

Examples:
  python3 apply_patches.py game.dll.clean
  python3 apply_patches.py game.dll.clean demo_v070.json --output game.dll
  python3 apply_patches.py game.exe full_v101.json
"""

import argparse
import hashlib
import json
import os
import sys


def md5sum(data: bytes) -> str:
    return hashlib.md5(data).hexdigest()


def apply_patches(binary: bytearray, patches: list) -> int:
    """Apply patches to binary data. Returns number of patches applied."""
    applied = 0
    for patch in patches:
        name = patch["name"]
        offset = int(patch["file_offset"], 16)
        original = bytes.fromhex(patch["original"])
        patched = bytes.fromhex(patch["patched"])

        # Verify original bytes match
        actual = bytes(binary[offset:offset + len(original)])
        if actual == patched:
            print(f"  [{name}] already applied, skipping")
            applied += 1
            continue
        elif actual != original:
            print(f"  [{name}] MISMATCH at 0x{offset:X}!")
            print(f"    expected: {original.hex()}")
            print(f"    actual:   {actual.hex()}")
            print(f"    Skipping this patch.")
            continue

        # Apply
        binary[offset:offset + len(patched)] = patched
        print(f"  [{name}] applied at 0x{offset:X} ({len(patched)} bytes)")
        applied += 1

    return applied


def main():
    parser = argparse.ArgumentParser(description="SpellBinder Binary Patcher")
    parser.add_argument("binary", help="Path to clean game binary")
    parser.add_argument("patch_file", nargs="?", default=None,
                        help="Patch definition JSON (auto-detected from MD5 if omitted)")
    parser.add_argument("--output", "-o", default=None,
                        help="Output path (default: <binary>.patched)")
    parser.add_argument("--list", "-l", action="store_true",
                        help="List patches without applying")
    args = parser.parse_args()

    # Read binary
    with open(args.binary, "rb") as f:
        data = bytearray(f.read())

    clean_hash = md5sum(bytes(data))
    print(f"Input: {args.binary} ({len(data):,} bytes, MD5: {clean_hash})")

    # Find patch file
    patches_dir = os.path.dirname(os.path.abspath(__file__))

    if args.patch_file:
        patch_path = args.patch_file
        if not os.path.exists(patch_path):
            patch_path = os.path.join(patches_dir, args.patch_file)
    else:
        # Auto-detect from MD5
        patch_path = None
        for fname in os.listdir(patches_dir):
            if not fname.endswith(".json"):
                continue
            fpath = os.path.join(patches_dir, fname)
            with open(fpath) as f:
                pdata = json.load(f)
            if pdata.get("clean_md5") == clean_hash:
                patch_path = fpath
                print(f"Auto-detected patch file: {fname}")
                break

        if not patch_path:
            print(f"ERROR: No patch file found for MD5 {clean_hash}")
            print(f"Available patch files in {patches_dir}:")
            for fname in sorted(os.listdir(patches_dir)):
                if fname.endswith(".json"):
                    with open(os.path.join(patches_dir, fname)) as f:
                        pdata = json.load(f)
                    print(f"  {fname}: {pdata.get('target', '?')} (MD5: {pdata.get('clean_md5', '?')[:12]}...)")
            sys.exit(1)

    # Load patches
    with open(patch_path) as f:
        pdata = json.load(f)

    print(f"Patch set: {pdata.get('target', '?')}")
    print(f"Expected clean MD5: {pdata.get('clean_md5', '?')}")
    print()

    # Verify clean MD5
    expected_md5 = pdata.get("clean_md5")
    if expected_md5 and clean_hash != expected_md5:
        print(f"WARNING: MD5 mismatch!")
        print(f"  Expected: {expected_md5}")
        print(f"  Actual:   {clean_hash}")
        print(f"  This binary may not be the correct version.")
        resp = input("  Continue anyway? [y/N] ")
        if resp.lower() != "y":
            sys.exit(1)
        print()

    patches = pdata.get("patches", [])

    if args.list:
        print(f"Patches ({len(patches)}):")
        for p in patches:
            print(f"  [{p['name']}] {p['description']}")
            print(f"    offset: {p['file_offset']} -> {p['virtual_address']}")
            print(f"    {len(p['original'])//2} bytes: {p['original'][:20]}... -> {p['patched'][:20]}...")
            print()
        return

    # Apply
    print(f"Applying {len(patches)} patches:")
    applied = apply_patches(data, patches)
    print()

    # Verify result
    result_hash = md5sum(bytes(data))
    expected_patched = pdata.get("patched_md5")
    if expected_patched:
        if result_hash == expected_patched:
            print(f"Result MD5: {result_hash} [OK] (matches expected)")
        else:
            print(f"Result MD5: {result_hash} (expected {expected_patched})")
    else:
        print(f"Result MD5: {result_hash}")

    print(f"Applied {applied}/{len(patches)} patches")

    # Write output
    output = args.output or (args.binary + ".patched")
    with open(output, "wb") as f:
        f.write(data)
    print(f"Written to: {output}")


if __name__ == "__main__":
    main()
