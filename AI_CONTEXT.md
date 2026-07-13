# AI Session Context

Use this file as the first read for future AI/code sessions. It is intentionally short so the session can route to the right files without loading the whole project.

## Project

Godot 4.7 C# creature-collecting action RPG.

Main scenes:
- `main_menu.tscn`: startup/main menu.
- `node_3d.tscn`: gameplay world scene, script `scripts/World/World.cs`.

Current rendering setup:
- Desktop: `Forward+`.
- Windows driver override: `d3d12`.
- Mobile: `gl_compatibility`.

## Read Only What You Need

Player input, camera, HUD, party, inventory, shops, save hooks:
- `scripts/Gameplay/Player/PlayerController.cs`

Player formation board state and companion slot offsets:
- `scripts/Gameplay/Player/PlayerController.Formation.cs`
- `scripts/UI/Panels/FormationPanel.cs`

NPC/monster/companion behavior, combat, capture, build stats:
- `scripts/Gameplay/Actors/SimpleActor.cs`

Map generation, spawning, portals, map save/load:
- `scripts/World/World.cs`

Build system data:
- `scripts/Gameplay/Items/BuildSystem.cs`

Localization:
- `scripts/Core/Localization/LocaleText.cs`
- `scripts/Core/Localization/locales/zh_TW.json`
- `scripts/Core/Localization/locales/en.json`

Save contracts:
- `scripts/Core/Save/SaveGameData.cs`
- `scripts/Core/Save/SaveGameManager.cs`

Asset/model loading:
- `scripts/Core/Assets/ExternalModelLibrary.cs`

## Maintenance Rules

- Prefer adding small partial files or focused helper classes over growing `PlayerController.cs`, `World.cs`, or `SimpleActor.cs`.
- Keep scene orchestration in `World` or `PlayerController`; keep data contracts in `Core`.
- New UI panels belong in `scripts/UI/Panels`.
- New full-screen screens belong in `scripts/UI/Screens`.
- New gameplay entities belong in the narrowest `scripts/Gameplay/*` folder.
- When changing C# code, run `dotnet build "新遊戲專案.csproj"`.
