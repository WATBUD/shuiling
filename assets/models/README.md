# External Model Drop Zone

Downloaded `.glb` and `.gltf` models live here and replace the current procedural visuals. The game keeps its existing collision and combat logic, then loads the first matching model it can find.

## Downloaded into this project

- `monsters/*.gltf`: Quaternius Ultimate Monsters Bundle, copied from the official Google Drive glTF folder.
- `characters/*.glb`: KayKit Adventurers third-party character models for NPCs.
- `characters/*.gltf`: Quaternius humanoid fallback models from the CC0 monster bundle.
- `environment/*.glb`: Kenney Fantasy Town Kit props and scenery.
- `licenses/KENNEY_*.txt`: Kenney license files bundled with the downloaded packs.

## Recommended sources

- [Quaternius / Poly Pizza Ultimate Monsters Bundle](https://poly.pizza/bundle/Ultimate-Monsters-Bundle-5oyGWAmOB6): 50 CC0 monsters with attack, death, run, and walk animations. Download GLTF/FBX, then rename useful files into `monsters/`.
- [Quaternius Universal Base Characters](https://quaternius.com/packs/universalbasecharacters.html): CC0 rigged base characters in glTF/FBX/Blend/OBJ. Good for villagers, guards, and party members.
- [Quaternius RPG Character Pack](https://quaternius.com/packs/rpgcharacters.html): CC0 fantasy characters in glTF/FBX. Good for archer, warrior, mage, and healer NPCs.
- [Kenney Assets](https://kenney.nl/assets): large free game asset library. Check each selected pack's page before shipping; many Kenney packs are CC0, but keep the specific pack license with the asset.
- [KayKit Adventurers](https://kaylousberg.itch.io/kaykit-adventurers): CC0 stylized adventurer characters with Godot-friendly GLB assets and many animations.

## Expected paths

Characters:

- `characters/warrior.glb`
- `characters/guard.glb`
- `characters/knight.glb`
- `characters/adventurer.glb`
- `characters/archer.glb`
- `characters/hunter.glb`
- `characters/ranger.glb`
- `characters/bowman.glb`
- `characters/mage.glb`
- `characters/healer.glb`
- `characters/wizard.glb`
- `characters/apprentice.glb`

Monsters:

- `monsters/orc.glb`
- `monsters/wolf.glb`
- `monsters/golem.glb`
- `monsters/beast.glb`
- `monsters/slime.glb`
- `monsters/imp.glb`
- `monsters/spitter.glb`
- `monsters/dragon.glb`
- `monsters/ghost.glb`

Environment:

- `environment/tree.glb`
- `environment/tree_01.glb`
- `environment/oak_tree.glb`
- `environment/pine_tree.glb`
- `environment/rock.glb`
- `environment/rock_01.glb`
- `environment/boulder.glb`
- `environment/stone.glb`

You can add more filenames by extending `scripts/Core/Assets/ExternalModelLibrary.cs`.
