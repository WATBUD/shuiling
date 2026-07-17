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

Read `../AI_CONTEXT.md` first — it owns the per-feature routing map and the domain model
(including the "gem" (code) = "core" (game) naming). This file only describes the folder
layout, so the two don't drift.

Avoid opening the largest orchestration files whole. Route to the method via `AI_CONTEXT.md`,
then read only around the call site.

## Split Policy

The largest classes are partial candidates. Keep each partial centered on one reason to change:

- `PlayerController.*.cs`: player-owned state and player-facing orchestration.
- `World.*.cs`: world construction, spawning, map travel, and save application.
- `SimpleActor.*.cs`: actor state, combat, movement, visual animation, and build integration.

Do not create generic utility layers just to move code. Split only when the new file has a clear feature boundary and reduces the amount of code a maintainer must read.
