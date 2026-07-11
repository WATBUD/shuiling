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
