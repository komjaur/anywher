# Anywhere Unity Project

This repository hosts the source for **Anywhere**, a Unity-based game prototype used for experimenting with 2D mechanics and systems. It contains a variety of scripts and assets that can serve as a starting point for new gameplay features or learning how Unity projects are structured.

## Requirements
- Unity **6000.0.41f1** or newer (see `ProjectSettings/ProjectVersion.txt`).
- Python 3 for running tooling scripts.

## Repository Layout
- `Assets/` – game content, C# scripts, prefabs and editor tools. Notable subfolders include:
  - `Scripts/` – runtime and editor code such as inventory, crafting and environment systems.
  - `ScriptableObjects/` – data assets that can be organised via the sorting script below.
  - `Prefabs/`, `Scenes/`, `Resources/` – typical Unity asset folders.
- `Packages/` – package manifest referencing Unity packages (URP, Input System, etc.).
- `ProjectSettings/` – Unity project configuration files.
- `CodeCoverage/` – example coverage reports produced by the Unity Test Framework.

## Getting Started
1. Install a compatible Unity Editor version.
2. Clone this repository and open it with Unity.
3. Press **Play** to run the current scene or open *File › Build Settings* to create a build.

### ScriptableObject Sorter
`Assets/sort_so.py` reorganises ScriptableObject assets into a unified folder structure. Run it without arguments to preview the planned moves:

```bash
python Assets/sort_so.py
```

Add `--apply` to actually move files. The classifier recognises asset names like:

- `Area_<DIM>_<Name>.asset`
- `Biome_<DIM>_<Area>_<Biome>.asset`
- `Item_<Name>.asset`
- `Tile_<Name>.asset`

Extend `sort_so.py` with additional `classify_*` functions to support new asset types.

## Contributing
Pull requests are welcome. Ensure that the project opens and runs correctly in a clean checkout before submitting changes.
