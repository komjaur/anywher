# Anywhere

Anywhere is a small 2D exploration and crafting prototype built with Unity. The project focuses on procedurally generated tile maps, collecting resources and crafting new items while exploring the world. It comes with a selection of ready-made systems (inventory, quests, environment management and more) that you can reuse or extend in your own experiments.

## Requirements
- Unity **6000.0.41f1** or newer (see `ProjectSettings/ProjectVersion.txt`)
- Python 3 for running some tooling scripts

## Repository structure
- `Assets/` – game assets and code
  - `Scripts/` – runtime and editor scripts
    - `Core/` – major systems like Inventory, Items, Crafting, Quests, World generation and AI
    - `Database/` – ScriptableObject definitions and managers that store game data
    - `Editor/` – utilities for the Unity editor (for example, auto-populating databases)
  - `Custom/` – shaders used for lighting and sprite effects
  - `Fluid MIDI/` – third-party MIDI playback library
  - `Plugins/` – other third-party libraries (noise generation, Voronoi, etc.)
  - `Prefabs/`, `Scenes/`, `ScriptableObjects/` – typical Unity asset folders
  - `sort_so.py` – helper script to organise ScriptableObject assets
- `Packages/` – Unity package manifest
- `ProjectSettings/` – Unity project configuration
- `CodeCoverage/` – example reports from the Unity Test Framework

## Getting started
1. Install the Unity version listed above
2. Clone the repository and open the project in Unity
3. Press **Play** to run the current scene or create a build via *File › Build Settings*

### ScriptableObject sorter
`Assets/sort_so.py` reorganises ScriptableObjects into a unified folder structure. Run it without arguments to preview the planned moves:

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
Pull requests are welcome. Ensure the project opens and runs correctly in a clean checkout before submitting changes.

## Notable Scripts
- **Inventory** (`Assets/Scripts/Core/Inventory`)
  manages the player's items and UI
- **Crafting** (`Assets/Scripts/Core/Crafting`)
  defines recipes and the crafting manager
- **Item** (`Assets/Scripts/Core/Item`)
  holds item data and the item manager
- **World Generation** (`Assets/Scripts/Core/World/Generation`)
  creates the procedural maps and biome layout
- **Quests** (`Assets/Scripts/Core/Quests`)
  quest definitions, objectives and the quest manager
- **Editor Tools** (`Assets/Scripts/Editor`)
  helper utilities to populate databases and check assets

These and many other scripts can be used as starting points for your own features.

## Tutorial quest chain
The project includes three introductory quests demonstrating core mechanics:

1. **Gather Some Wood** – collect five Logs.
2. **Craft a Torch** – use gathered wood to craft your first light source.
3. **Mine Stone** – try out your new tool on stone blocks.

Completing each step automatically unlocks the next through `QuestDatabase` and
`QuestManager`.

## Persistent saving
The `SaveLoadSystem` serialises the current world, player inventory and quest
progress into a JSON file under Unity's persistent data path. Use
`SaveLoadSystem.Instance.SaveGame(GameManager.Instance)` and
`SaveLoadSystem.Instance.LoadGame(GameManager.Instance)` to store or restore a
session.
