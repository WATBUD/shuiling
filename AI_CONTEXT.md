# AI Session Context

First read for any AI/code session. Kept short so you can route to the right file
without loading the whole project. When you change something structural here, update
this file.

## Project

Godot 4.7 C# creature-collecting action RPG. You capture monsters, build their "cores",
and they fight for you. Build with: `dotnet build "新遊戲專案.csproj"` after any C# change.

- `main_menu.tscn` — startup/main menu (`scripts/UI/Screens/MainMenu.cs`).
- `node_3d.tscn` — gameplay world (`scripts/World/World.cs`).
- World progression design: `docs/world_progression.md` — **map = biome, Tier = difficulty**.
  Every wild map supports Tiers 1–10; never give maps fixed level ranges.
- Rendering: desktop `Forward+`; Windows driver `d3d12`; mobile `gl_compatibility`.

## Domain model — READ THIS BEFORE TOUCHING COMBAT/BUILD

Naming mismatch (the #1 source of confusion): **the code says "gem"; the game/UI says
"core".** They are the same thing. Map:

| Design / UI term | Code symbol |
| --- | --- |
| Core skill / 核心技能 (the active attack, slot 0) | code `SkillGem` / `SkillGemIds[0]` / `SkillGemLevels[0]` (a.k.a. `SupportCore*` internally) |
| Support core / 輔助核心 (extensions, slots 1–6) | code `SkillGem` / `SkillGemIds[1..6]` / `SkillGemLevels[1..6]` |
| Legacy hidden attack element | code `AttributeGem` / `AttributeGemId` (fire/water/… ; none = physical; a.k.a. `MainCore*` internally) |
| Core chain display `[核心] a-b-c` | `SimpleActor.SupportCoreChain` |
| (Core Resonance — REMOVED) | no `RareCombo`/`Resonance` code remains; cores just stack |

NOTE: the game/UI calls slot 0 **核心技能** and slots 1–6 **輔助核心**, but the code
identifiers still use the legacy names — code `MainCore*` (`IsMainCoreUnlocked`) refers
to the hidden element/attribute data, and code `SupportCore*` (`SupportCoreSlotCount`,
`EquipSupportCore`) refers to the complete skill-core array. Trust the table above.

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

- `PlayerController` is one `partial class` split across `PlayerController.*.cs` by
  responsibility. The main `PlayerController.cs` holds only state (fields/consts/enums/
  records), lifecycle (`_Ready`/`_Process`/`_PhysicsProcess`/`_UnhandledInput`), and input
  routing — open a partial for the actual logic:
  - `.Combat.cs` — capture net, `ReceiveDamage`/heal, XP, damage flash
  - `.Camera.cs` — camera modes / orbit / zoom, aim & capture-throw direction
  - `.Visual.cs` — movement fx, animation, player model/equipment, safe-ground recovery
  - `.Party.cs` — capture→party, deploy/store/mount, revive, `GrantStarterBunny`
  - `.Interaction.cs` — interaction prompt, portals, NPC recruit/quest triggers
  - `.Targeting.cs` — click-select / focus target / target markers
  - `.Dialogs.cs` — NPC-quest & map-travel dialogs
  - `.Inventory.cs` — bag counts, gold, equip-consume/return, evolve, gem upgrade, drops
  - `.Shops.cs` — merchant/blacksmith/pet-shop trade + stock; `.Mercenary.cs` — mercenary offers
  - `.Hud.cs` — HUD widgets + status; `.Panels.cs` — panel construction/visibility + pause menu
  - `.Save.cs` — `ExportSaveData`/`ApplySaveData`; `.Formation.cs` — formation board
  - Formation UI: `scripts/UI/Panels/FormationPanel.cs`.
- Actor state, AI, movement, combat, capture, core-driven attack, save round-trip:
  `scripts/Gameplay/Actors/SimpleActor.cs` (large).
- Live projectile + behaviors (split/chain/pierce/explosion): `scripts/Gameplay/Combat/CombatProjectile.cs`.
- Core/build data: elements, support cores, levels/upgrade, catalogs, `CalculateStats`:
  `scripts/Gameplay/Items/BuildSystem.cs`. `InventoryItemKind` now includes
  `Consumable`; `BuildCatalog.TownPortalScrollId` + `Consumables` map + `IsConsumable`.
- Town Portal Scroll (回城卷): emergency retreat consumable, `PlayerController.TownPortal.cs`.
  5 granted on new game; used via T hotkey or the inventory Consumables tab
  (double-click / Use button → `UseTownPortalScroll`). `CanUseTownPortalScroll`
  gates it to the wild when safe (not city/cave, no active boss, not recently
  damaged `MarkRecentCombat`, no monster within ~16m) then `RequestMapTravel("city")`.
  NOTE: the design's multi-city handbook/outpost binding is deferred — the world
  has one city today (the M-key map already covers cross-region travel).
- Build-editing UI (equip/unequip via drag or double-click, upgrade, locked slots, core
  chain): `scripts/UI/Panels/InventoryPanel.cs`.
- Companion info card / party panel: `scripts/UI/Components/CompanionInfoCard.cs`,
  `scripts/UI/Panels/PartyPanel.cs`.
- Warehouse (倉庫): city NPC `name.npc.warehouse_keeper` (spawned in `SpawnCityNpcs`)
  opens `WarehousePanel` (`scripts/UI/Panels/WarehousePanel.cs`) — two-column
  bag|storage, category tabs, double-click/middle-click transfers. Storage is a
  separate `_storageItems` dict on `PlayerController` with silent
  `WarehouseDeposit`/`WarehouseWithdraw` (Inventory.cs), persisted via
  `PlayerSaveData.StorageItems`. Interaction/prompt in Interaction.cs
  (`GetNearestWarehouseKeeper`), panel wiring in Panels.cs
  (`CreateWarehousePanel`/`SetWarehousePanelVisible`, layer 41).
- Monster species / loot tables: `scripts/Gameplay/Monsters/MonsterSpeciesCatalog.cs`,
  `scripts/Gameplay/Items/MonsterLootCatalog.cs`.
- World Tier rules (level bands, stat/reward multipliers, evolution-stage names):
  `scripts/Gameplay/Progression/WorldTierCatalog.cs`. Tiers are PER-PLAYER
  progression (unlock = beat the map boss at your highest tier; saved in each
  player's `SaveGameData.UnlockedMapTiers`/`SelectedMapTiers`). Each populated
  (map, tier) pair is a parallel instance keyed `WildInstanceKey(mapId, tier)`
  (`_spawnedWildInstancesByKey`, `EnsureWildInstancePopulated`,
  `DespawnInactiveWildInstances`, `IsActorInstanceActive` in `World.cs`);
  players share monsters/see each other only on the same map AND tier. Each
  actor stores `WorldTier`. Tiers are also **level-gated**
  (`WorldTierCatalog.GetRequiredPlayerLevel`); `ApplySelectedTier` clamps to a
  tier the player level allows. `World.GetTierMenu(mapId, playerLevel)` returns
  per-tier level range + unlock/level-met/available flags — shared by the portal
  dialog and the M-key map. Tier picker + world map guide: `ShowMapTravelDialog`
  / `ToggleWorldMapGuide` (M key) in `PlayerController.Dialogs.cs`; locked tiers
  show a drawn `MakeLockBadge` padlock. Portal visuals incl. hexagram floor:
  `CreateMapPortal`/`AddPortalHexagram` in `World.cs`.
- World gen, spawning, portals, map travel/save: `scripts/World/World.cs` (large).
- Biome dressing (per-map sky/fog/sun atmosphere via `ApplyMapAtmosphere`, biome
  prop scatter, landmark set-pieces): `scripts/World/World.Biomes.cs`. Scatter
  helpers pick props by `_currentThemeMapId` (set while a map is being built).
  Per-biome ground palette (snow=white, badlands=red, …): `BuildWildGroundPalette`.
- Prop visuals are authored as editable scenes in `assets/scenes/props/*.tscn`
  (tree/rock/landmark/biome props). Each `CreateXxx` calls `TryPlacePropScene`
  first (scene = editor-tweakable look) and only runs its procedural build if the
  `.tscn` is missing (safe fallback). Layout/placement stays in code. NOTE: do
  NOT make grass/flowers into `.tscn` — they are GPU-batched (see below).
- Vegetation perf: grass blades + flowers are NOT individual nodes. During a map
  build (`BeginVegetationBatch`/`EndVegetationBatch`, `scripts/World/World.Vegetation.cs`)
  they accumulate into `MultiMeshInstance3D` (few draw calls total, per-instance
  colour). `CreateGrassPatch`/`CreateFlowerPatch` push into the active batch and
  only build per-node versions if no batch is active.
- Save contracts / manager: `scripts/Core/Save/SaveGameData.cs`, `SaveGameManager.cs`.
- Localization: `scripts/Core/Localization/LocaleText.cs` + `locales/{zh_TW,en}.json`
  (keep both files key-for-key in parity).
- Model/asset loading + fallback materials: `scripts/Core/Assets/ExternalModelLibrary.cs`.
- Multiplayer (host-authoritative, phase 1): autoload `Net` →
  `scripts/Core/Network/NetworkManager.cs` (ENet host/join, custom port, max 5
  players, handshake/seed, player-state relay, monster sync RPCs, damage
  forwarding + kill rewards). World half: `scripts/World/World.Network.cs`
  (host assigns net ids in `RegisterNetworkMonster`, broadcasts state batches;
  clients build puppet monsters). Remote avatars:
  `scripts/Core/Network/RemotePlayerPuppet.cs`. Main-menu host/join dialogs:
  `scripts/UI/Screens/MainMenu.cs`. The host simulates every (map, tier)
  instance that has a player in it (`EnsureWildInstancePopulated` fires from
  `ReceivePlayerState`); tier choice/unlock stays per-player (remote kills
  unlock via `ClientReceiveBossDefeat`). Phase-1 limits: clients see host
  monsters and forward damage (XP/gold via RPC), but no capture of host
  monsters, no loot drops for clients, monsters don't attack clients, caves &
  companions are local-only.
- Listen-server reachability: `scripts/Core/Network/NetworkDiagnostics.cs`
  (port-bind test, Godot `Upnp` discover/external-IP/auto port-map, CGNAT range
  check, Windows `netsh` firewall rule). Host dialog "Network Test" button runs
  it (bind on main thread, UPnP/firewall on a worker → `CallDeferred`);
  `NetworkManager.CreateServer` also fires `TryOpenPort` for best-effort UPnP.
  No master server, so external reachability is inferred from the router's WAN
  IP, not a true reachback.

## Invariants / gotchas

- `CompanionBuildLoadout.SkillGemIds`/`SkillGemLevels` are normalized to
  `SupportCoreSlotCount` by `EnsureSkillSlots()`; old saves with a different length are
  padded/truncated on load. Index cores through `GetSkillGemId(i)`/`GetSkillGemLevel(i)`.
- Equipping never consumes inventory; unequip just sets the slot to `gem.*.none`.
- Every localization key must exist in **both** locale files with the same format args.
- `PlayerController` is already split into `PlayerController.*.cs` partials (see routing
  above) — put new player logic in the matching partial, keep the main file orchestration-
  only. `SimpleActor` and `World` are still large: add focused partial files by
  responsibility rather than growing them.

## Maintenance rules

- Keep scene orchestration in `World`/`PlayerController`; keep data contracts in `Core`.
- New UI panels → `scripts/UI/Panels`; full-screen screens → `scripts/UI/Screens`;
  reusable widgets → `scripts/UI/Components`.
- New gameplay entities → the narrowest `scripts/Gameplay/*` folder.
- Don't create generic "utility" layers just to move code; split only on a real feature
  boundary that reduces what a maintainer must read.
