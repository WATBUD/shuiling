# Architecture Review & Separation Roadmap

Status: living document. Goal: separate concerns in the oversized/mixed-responsibility
areas of the codebase without destabilizing systems that can only be validated by
running the game.

## Method

Changes are staged by **risk**, because most gameplay behavior cannot be verified by
`dotnet build` alone — only by running the game:

- **Stage 0 — behavior-preserving structural splits.** Move coherent method/field
  clusters of one class into `partial` files. Zero runtime change; fully verified by
  `dotnet build`. Safe to do without a running game.
- **Stage 1 — extract self-contained helpers/services.** Pull stateless logic into
  dedicated classes with narrow interfaces. Low risk; build + light manual test.
- **Stage 2 — behavioral refactors (component/strategy).** Change how objects are
  composed (e.g. actor behavior strategies). High risk; **requires** in-game regression
  testing. Do incrementally, one concern at a time, never as a big bang.

## Current state (file sizes, largest first)

| File | Lines | Shape | Assessment |
|------|-------|-------|------------|
| `Gameplay/Actors/SimpleActor.cs` | ~4450 | single partial (+`.Burrow`) | **God-class.** Monster AI, pet, companion, network puppet, capture, combat, movement, animation, status, nameplate all in one. Top priority. |
| `World/World.cs` | ~3720 | many partials already | Large but already split (`.Biomes/.Caves/.Network/.RuntimeCleanup/...`). Core still heavy. |
| `UI/Panels/InventoryPanel.cs` | ~2130 | single file | Mixed: layout, data binding, drag/drop, tooltips, actions. |
| `Core/Network/NetworkManager.cs` | ~1480 | single file | Transport + session + party sync + puppets. |
| `Gameplay/Items/BuildSystem.cs` | ~1390 | single file | Catalog + stat math + equip rules. |
| `Core/Assets/ExternalModelLibrary.cs` | ~1215 | single file | Model resolve + fallback materials + animation glue + props. |
| `Gameplay/Player/PlayerController.*` | ~5000 total | **12 partials** | **Reference pattern** — this is the target shape. |

`PlayerController` is already decomposed by concern (`.Hud`, `.Combat`, `.Visual`,
`.Inventory`, `.Interaction`, `.Dialogs`, `.Shops`, `.Save`, `.Cards`, `.TownPortal`,
`.WebTrap`, `.Panels`). Bring the God-classes toward this shape first.

## Priority 1 — SimpleActor.cs (Stage 0 split)

Split the single file into concern partials, mirroring `PlayerController`. All within the
same `partial class SimpleActor` — no behavior change, `dotnet build` verifies it. Keep
shared fields in the core file; move whole method clusters.

Proposed partials and their method clusters (line refs approximate, pre-split):

- **`SimpleActor.Animation.cs`** — `UpdateMovementEffects`, `SpawnMovementDust`,
  `UpdateMovementAnimation`, `UpdateHumanoid/MonsterMovementAnimation`,
  `UpdateExternalMovementAnimation`, `SetExternalAnimationState`,
  `SetChildPosition/Rotation`, `GetCachedChildNode`, `StabilizeExternalModelRootMotion`,
  `GetCachedExternalModelNode` (tail cluster ~4183–4448; contiguous — easiest to move).
- **`SimpleActor.Capture.cs`** — `UpdateCaptureState`, `SyncCaptureProtection`,
  `RefreshCaptureShield`, `EnterMourning`/`ExitMourning`, `ShowMournBubble`
  (~375–560; contiguous).
- **`SimpleActor.Network.cs`** — `UpdateNetworkPuppet` and puppet sync helpers (~1447+).
- **`SimpleActor.Combat.cs`** — `TryRunMonsterCombat`, `ReceiveDamage`,
  `UpdateWhirlwindSpin`/`BeginWhirlwindSpin`/`ApplyWhirlwindSpinRotation`, retaliation.
- **`SimpleActor.Squad.cs`** — `UpdateSquadActivity`, `UpdatePetDialogue`, follow logic.
- **`SimpleActor.Core.cs`** (or keep `SimpleActor.cs`) — fields, `_Ready`,
  `_PhysicsProcess` dispatcher, the behavior gates (`IsInvulnerable`,
  `IsActiveWorldTarget`, …), `TickActorTimers`.
- Done already: **`SimpleActor.Burrow.cs`** (mole dig behavior) — the pattern to copy.

### Stage 2 follow-up (needs runtime tests)
The `_PhysicsProcess` dispatcher already branches by role (puppet / captured / wild).
That is a latent **behavior strategy**. Later, extract `IActorBehavior`
(`MonsterBehavior`, `PetBehavior`, `CompanionBehavior`, `PuppetBehavior`) and have the
actor delegate its per-frame update, so role logic stops sharing one method body and
species quirks (burrow, whirlwind) become pluggable abilities instead of `DisplayName`
string checks. Do this only with in-game regression coverage.

## Priority 2 — the other single-file classes (Stage 0/1)

- **InventoryPanel** → `.Layout` / `.Binding` / `.DragDrop` / `.Tooltip` partials.
- **NetworkManager** → separate transport, session lifecycle, and party/puppet sync.
- **BuildSystem** → split the static catalog data from the stat-calculation service.
- **ExternalModelLibrary** → split model resolution, fallback-material logic, animation
  glue, and prop placement.

## Cross-cutting issue found this session — renderer compatibility

The project targets `gl_compatibility` for mobile (`renderer/rendering_method.mobile`),
and the Mac editor falls back to it. **`GpuParticles3D` does not exist under
`gl_compatibility`.** Programmatic `new GpuParticles3D` usages will fail on that target:

- `World.cs` (fountain / rising / orbit particles)
- `Gameplay/Player/PlayerController.TownPortal.cs`, `PlayerController.WebTrap.cs`
- `Effects/BossMagicCircleVfx.cs`, `Effects/KenneyParticleVfx.cs` (`CreateBurst`),
  `Effects/KenneyParticleVfxPreview.cs`

Recommendation: decide the shipping renderer. If `gl_compatibility` stays a target,
migrate these to `CpuParticles3D` (as the drop scenes and mole dust already do), or gate
GPU particles behind a renderer check with a CPU fallback. Track as its own task.

## Progress log

**World drops** — visuals moved to authored scenes (`assets/scenes/props/drops/*.tscn`),
`WorldDrop` reduced to logic, spawning centralized in `WorldDropFactory`, reuse via
`WorldDropPool`.

**SimpleActor — fully decomposed (Stage-0 complete).** Core `SimpleActor.cs` went from
**4449 → 2533 lines**; every extraction is a behavior-preserving move within the same
partial class, verified by `dotnet build` (0 warnings/errors) and committed individually:

| Partial | Concern |
|---------|---------|
| `SimpleActor.Burrow.cs` | mole dig behavior |
| `SimpleActor.Capture.cs` | capture-protection + mourning |
| `SimpleActor.Animation.cs` | movement fx, procedural body/ext-model animation |
| `SimpleActor.Network.cs` | network-puppet lifecycle/sync |
| `SimpleActor.Squad.cs` | companion follow / squad activity / pet dialogue |
| `SimpleActor.Loot.cs` | monster/boss loot rolls |
| `SimpleActor.Status.cs` | element status effects |
| `SimpleActor.BossMovement.cs` | boss obstacle avoidance |
| `SimpleActor.Combat.cs` | damage, attacks, projectiles, retaliation, whirlwind |

Core retains fields, `_Ready`, `_PhysicsProcess` dispatcher + behavior gates, and shared
seams (`TryRunMonsterCombat`, `SpawnCombatEffect`, `GetCachedPlayerNode`, `Defeat`).

**Other God-files — lead cut done (Stage-0).** One safe concern extracted from each,
build-verified and committed:

| File | Extracted partial |
|------|-------------------|
| `NetworkManager.cs` | `NetworkManager.Party.cs` (party sync) |
| `ExternalModelLibrary.cs` | `ExternalModelLibrary.FallbackMaterials.cs` |
| `InventoryPanel.cs` | `InventoryPanel.Tooltips.cs` + `InventoryDragButtons.cs` |
| `BuildSystem.cs` (`BuildCatalog`) | `BuildCatalog.Calculation.cs` |

**SimpleActor — fully decomposed.** Six more slices extracted (Save, Death,
Progression, Nameplate, Build, State), taking core `SimpleActor.cs` to **1335 lines**
(from 4449 — a 70% reduction) across 15 concern partials. Core now holds only fields,
`_Ready`/`_ExitTree`/`_Process`, the `_PhysicsProcess` dispatcher + behavior gates,
display getters, and shared seams. All build-verified, one commit per concern.

### Remaining (lower-priority tail, per this roadmap)
- NetworkManager: gift-mail, monster/puppet sync, transport, session partials.
- InventoryPanel: sorting, UI factories, layout, data-binding partials.
- BuildCatalog: split definition types + catalog data from `BuildCatalog.Data.cs`.
- ExternalModelLibrary: animation-glue + prop-placement partials.
- Stage 2 (needs in-game tests): extract `IActorBehavior` strategy from the
  `_PhysicsProcess` role branches; migrate remaining `GpuParticles3D` for gl_compatibility.
