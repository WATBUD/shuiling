# AI Session Context

First read for any AI/code session. Kept short so you can route to the right file
without loading the whole project. When you change something structural here, update
this file.

## Project

Godot 4.7 C# creature-collecting action RPG. You capture monsters, build their "cores",
and they fight for you. Build with: `dotnet build "新遊戲專案.csproj"` after any C# change.

- `main_menu.tscn` — startup/main menu (`scripts/UI/Screens/MainMenu.cs`).
- `node_3d.tscn` — gameplay world (`scripts/World/World.cs`).
- Rendering: desktop `Forward+`; Windows driver `d3d12`; mobile `gl_compatibility`.

## Domain model — READ THIS BEFORE TOUCHING COMBAT/BUILD

Naming mismatch (the #1 source of confusion): **the code says "gem"; the game/UI says
"core".** They are the same thing. Map:

| Design / UI term | Code symbol |
| --- | --- |
| Main core (attack element) | `AttributeGem` / `AttributeGemId` (element: fire/water/… ; none = physical) |
| Support core (chain of ≤6) | `SkillGem` / `SkillGemIds[]` / `SkillGemLevels[]` |
| Core chain display `[核心] a-b-c` | `SimpleActor.SupportCoreChain` |
| (Core Resonance — REMOVED) | no `RareCombo`/`Resonance` code remains; cores just stack |

Combat facts:
- **The player has no outgoing attack** — combat is entirely companion-driven. The player
  only takes damage (`PlayerController.ReceiveDamage`) and throws the capture net.
- Captured companions (and NPCs/monsters) are all `SimpleActor`. Companion attacks spawn a
  live `CombatProjectile` that travels, collides, and applies damage on hit. Monsters use
  the legacy instant-hit path (`SimpleActor.LegacyAttackActor`).
- Core slots are **level-gated**: main core unlocks at Lv3; the 6 support slots unlock at
  Lv 6/10/15/21/28/36 (`BuildCatalog.SupportCoreSlotCount`, `SupportCoreUnlockLevels`).
- Support-core behaviors stack freely and are applied on projectile hit: split, multishot,
  chain, pierce, explosion (`ProjectileBehavior` + `ProjectileBehaviorProfile`). The
  projectile-support cores only work when a ranged active skill (fireball/meteor/laser) is
  equipped (`IsRangedActiveSkill` / `IsProjectileSupportGem`).
- 9 elements with an `ElementChart` multiplier and status effects (slow/stun/poison/burn)
  applied in `SimpleActor.ReceiveDamage` / `ApplyElementStatus`.
- Attack modes (`BuildCatalog.AttackModes`): `command_priority` (auto, but favors the
  player's designated `FocusedTarget`), `independent` (auto nearest, ignores orders —
  `AiAttackNearest` behavior), `manual` (only strikes the designated `FocusedTarget`).

## Route to the right file

- Player input, camera, HUD, party, inventory/shop hooks, save, capture net, starter pet
  (`GrantStarterBunny`): `scripts/Gameplay/Player/PlayerController.cs` (large — jump to the
  method, don't read whole).
- Formation board state / slot offsets: `scripts/Gameplay/Player/PlayerController.Formation.cs`,
  `scripts/UI/Panels/FormationPanel.cs`.
- Actor state, AI, movement, combat, capture, core-driven attack, save round-trip:
  `scripts/Gameplay/Actors/SimpleActor.cs` (large).
- Live projectile + behaviors (split/chain/pierce/explosion): `scripts/Gameplay/Combat/CombatProjectile.cs`.
- Core/build data: elements, support cores, levels/upgrade, catalogs, `CalculateStats`:
  `scripts/Gameplay/Items/BuildSystem.cs`.
- Build-editing UI (equip/unequip via drag or double-click, upgrade, locked slots, core
  chain): `scripts/UI/Panels/InventoryPanel.cs`.
- Companion info card / party panel: `scripts/UI/Components/CompanionInfoCard.cs`,
  `scripts/UI/Panels/PartyPanel.cs`.
- Monster species / loot tables: `scripts/Gameplay/Monsters/MonsterSpeciesCatalog.cs`,
  `scripts/Gameplay/Items/MonsterLootCatalog.cs`.
- World gen, spawning, portals, map travel/save: `scripts/World/World.cs` (large).
- Save contracts / manager: `scripts/Core/Save/SaveGameData.cs`, `SaveGameManager.cs`.
- Localization: `scripts/Core/Localization/LocaleText.cs` + `locales/{zh_TW,en}.json`
  (keep both files key-for-key in parity).
- Model/asset loading + fallback materials: `scripts/Core/Assets/ExternalModelLibrary.cs`.

## Invariants / gotchas

- `CompanionBuildLoadout.SkillGemIds`/`SkillGemLevels` are normalized to
  `SupportCoreSlotCount` by `EnsureSkillSlots()`; old saves with a different length are
  padded/truncated on load. Index cores through `GetSkillGemId(i)`/`GetSkillGemLevel(i)`.
- Equipping never consumes inventory; unequip just sets the slot to `gem.*.none`.
- Every localization key must exist in **both** locale files with the same format args.
- The three large files (`PlayerController`, `SimpleActor`, `World`) are split candidates —
  add focused `*.partial.cs` files rather than growing them.

## Maintenance rules

- Keep scene orchestration in `World`/`PlayerController`; keep data contracts in `Core`.
- New UI panels → `scripts/UI/Panels`; full-screen screens → `scripts/UI/Screens`;
  reusable widgets → `scripts/UI/Components`.
- New gameplay entities → the narrowest `scripts/Gameplay/*` folder.
- Don't create generic "utility" layers just to move code; split only on a real feature
  boundary that reduces what a maintainer must read.
