# Script Architecture

This project keeps C# scripts grouped by responsibility:

- `Core/Assets`: shared model and asset loading helpers.
- `Core/Localization`: language tables and formatting helpers.
- `Core/Save`: save data contracts, save/load manager, and launch options.
- `Gameplay/Actors`: NPC, monster, and companion actor logic.
- `Gameplay/Combat`: combat projectiles and capture net behavior.
- `Gameplay/Items`: equipment/build catalogs and world drops.
- `Gameplay/Player`: player controller, camera, HUD hooks, interaction, party state, and save integration.
- `World`: map construction, city/wild map switching, spawning, and world save integration.
- `UI/Panels`: reusable in-game panels and HUD-like panel controls.
- `UI/Screens`: full-screen game screens such as the main menu.
- `Effects`: short-lived visual effects.

When adding a new feature, place the script in the narrowest matching folder. If a script starts coordinating multiple systems, keep data contracts in `Core` and leave scene-specific orchestration in the owner area such as `World` or `Gameplay/Player`.

## AI-Friendly Entry Points

Read `../AI_CONTEXT.md` first for a minimal routing map.

Common routes:

- Camera, movement, keybinds, HUD, shops, save hooks: `Gameplay/Player/PlayerController.cs`.
- Formation board state and companion slot offsets: `Gameplay/Player/PlayerController.Formation.cs`.
- Formation drag/drop UI: `UI/Panels/FormationPanel.cs`.
- Companion combat/follow/AI/build behavior: `Gameplay/Actors/SimpleActor.cs`.
- World generation, portals, spawning, map switching: `World/World.cs`.
- Build/equipment/gem definitions: `Gameplay/Items/BuildSystem.cs`.
- Model loading and fallback materials: `Core/Assets/ExternalModelLibrary.cs`.

Avoid opening the largest orchestration files unless the route points there. Prefer reading the focused panel/catalog/helper first, then open the owner file only around the call site.

## Split Policy

The largest classes are partial candidates. Keep each partial centered on one reason to change:

- `PlayerController.*.cs`: player-owned state and player-facing orchestration.
- `World.*.cs`: world construction, spawning, map travel, and save application.
- `SimpleActor.*.cs`: actor state, combat, movement, visual animation, and build integration.

Do not create generic utility layers just to move code. Split only when the new file has a clear feature boundary and reduces the amount of code a maintainer must read.
