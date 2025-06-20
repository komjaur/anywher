#!/usr/bin/env python3
"""
sort_so.py – Re-arrange ScriptableObject assets into the new folder layout
==============================================================

Dry-run (just print the plan)  :  python sort_so.py
Actually move the files        :  python sort_so.py --apply

Rules implemented
-----------------
• Area_??_X.asset          → Assets/Content/Areas/<Dimension>/<X>/Area_??_X.asset
• Biome_??_X_Y.asset       → …/<Dimension>/<X>/Biomes/<Y>.asset

Extend CLASSIFIERS with more `classify_*` functions for Tiles, Blueprints, etc.
"""

import argparse
import os
import re
import shutil
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
#  Project-root discovery
# ---------------------------------------------------------------------------

DEF_ASSETS = "Assets"

def find_project_root(start: Path) -> Path:
    """Walk up from *start* until we find a directory containing 'Assets'."""
    current = start.resolve()
    for _ in range(10):                              # fail-safe: don’t walk forever
        if (current / DEF_ASSETS).is_dir():
            return current
        if current.parent == current:
            break                                   # reached filesystem root
        current = current.parent
    sys.exit("Could not locate an 'Assets' folder – aborting.")

PROJECT_ROOT = find_project_root(Path(__file__).parent)
SRC_ROOT = PROJECT_ROOT / DEF_ASSETS / "ScriptableObjects"

# ---------------------------------------------------------------------------
#  Helpers
# ---------------------------------------------------------------------------

DIMENSION = {"OW": "Overworld", "SW": "Skyworld", "UW": "Underworld"}

def pascal_case(text: str) -> str:
    """Convert 'frozen tundra' → 'FrozenTundra' (remove spaces / exotic chars)."""
    parts = re.split(r"[^A-Za-z0-9]+", text)
    return "".join(p.capitalize() for p in parts if p)

def move_asset(src: Path, dest: Path, apply: bool):
    """Move asset + meta (or just print the plan)."""
    meta_src  = src.with_suffix(src.suffix + ".meta")
    meta_dest = dest.with_suffix(dest.suffix + ".meta")

    rel_src  = src.relative_to(PROJECT_ROOT)
    rel_dest = dest.relative_to(PROJECT_ROOT)

    if apply:
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(src, dest)
        if meta_src.exists():
            shutil.move(meta_src, meta_dest)
        print(f"MOVE  {rel_src}  →  {rel_dest}")
    else:
        print(f"PLAN  {rel_src}  →  {rel_dest}")

# ---------------------------------------------------------------------------
#  Classification rules
# ---------------------------------------------------------------------------

def classify_area(asset_name: str) -> Path | None:
    """
    Area_OW_Desert.asset  =>  Assets/Content/Areas/Overworld/Desert/Area_OW_Desert.asset
    """
    m = re.fullmatch(r"Area_(\w{2})_(.+)\.asset", asset_name, re.I)
    if not m:
        return None
    dim_code, area = m.groups()
    return (PROJECT_ROOT / DEF_ASSETS / "Content" / "Areas" /
            DIMENSION.get(dim_code, dim_code) / pascal_case(area) / asset_name)

def classify_biome(asset_name: str) -> Path | None:
    """Biome_OW_Desert_Dune Sea.asset → Assets/Content/Areas/Overworld/Desert/Biomes/DuneSea.asset"""
    m = re.fullmatch(r"Biome_(\w{2})_(\w+?)_(.+)\.asset", asset_name, re.I)
    if not m:
        return None
    dim_code, area, biome = m.groups()
    return (PROJECT_ROOT / DEF_ASSETS / "Content" / "Areas" /
            DIMENSION.get(dim_code, dim_code) / pascal_case(area) / "Biomes" /
            f"{pascal_case(biome)}.asset")


def classify_item(asset_name: str) -> Path | None:
    """Item_Log.asset → Assets/Content/Items/Log.asset"""
    m = re.fullmatch(r"Item_(.+)\.asset", asset_name, re.I)
    if not m:
        return None
    return PROJECT_ROOT / DEF_ASSETS / "Content" / "Items" / f"{pascal_case(m.group(1))}.asset"


def classify_tile(asset_name: str) -> Path | None:
    """Tile_Dirt.asset → Assets/Content/Tiles/Dirt.asset"""
    m = re.fullmatch(r"Tile_(.+)\.asset", asset_name, re.I)
    if not m:
        return None
    return PROJECT_ROOT / DEF_ASSETS / "Content" / "Tiles" / f"{pascal_case(m.group(1))}.asset"

# Put additional classify_* functions above and append them here.
CLASSIFIERS = (
    classify_area,
    classify_biome,
    classify_item,
    classify_tile,
)

# ---------------------------------------------------------------------------
#  Main
# ---------------------------------------------------------------------------

def main(apply: bool):
    if not SRC_ROOT.is_dir():
        sys.exit("Could not find 'Assets/ScriptableObjects' – aborting.")

    for path in SRC_ROOT.rglob("*.asset"):
        # Skip editor or backup files, if any
        if path.name.endswith(".meta"):
            continue

        target: Path | None = None
        for classify in CLASSIFIERS:
            target = classify(path.name)
            if target:
                break

        if target:
            move_asset(path, target, apply)
        else:
            print(f"SKIP  {path.relative_to(PROJECT_ROOT)}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Sort ScriptableObjects into the new folder structure.")
    parser.add_argument("--apply", action="store_true", help="Actually move files (omit for dry-run).")
    args = parser.parse_args()
    main(apply=args.apply)
