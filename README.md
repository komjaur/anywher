# Anywhere Unity Project

This repository contains the source for a Unity project.

## Structure
- `Assets/` – game assets, scripts and editor tools
- `Packages/` – package manifest and lock file
- `ProjectSettings/` – Unity project settings
- `CodeCoverage/` – generated coverage reports (ignored after this commit)

## Development
1. Open the project in a compatible version of the Unity Editor.
2. Run or build as required.

### Sorting ScriptableObjects
`Assets/sort_so.py` can reorganize ScriptableObject assets into a structured
layout. Run it with Python 3:

```bash
python Assets/sort_so.py --apply
```

Run without `--apply` to preview the planned moves.

The sorter currently recognizes these naming patterns:

- `Area_<DIM>_<Name>.asset`
- `Biome_<DIM>_<Area>_<Biome>.asset`
- `Item_<Name>.asset`
- `Tile_<Name>.asset`

