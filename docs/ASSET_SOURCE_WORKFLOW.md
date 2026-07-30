# Runtime Asset Workflow

## Hard rule

`assets/_downloads/` is a local **source/reference pool only**. It is ignored by Git and
is not included in GitHub Actions, releases, updater packages, or another player's game.

Never reference that directory from C#, Godot scenes, resources, shaders, configuration,
or localization files. A locally working `res://assets/_downloads/...` path is a release
bug.

## Adding a downloaded asset

1. Browse and preview the source pack under `assets/_downloads/`.
2. Select only the files the game actually needs.
3. Copy them into a tracked runtime directory:
   - Models: `assets/models/<category>/<pack>/`
   - VFX textures: `assets/effects/textures/<pack>/`
   - UI textures: `assets/ui/<category>/`
   - Audio: `assets/audio/<category>/`
4. Copy the pack's license into the same runtime pack directory.
5. Reference only the new `res://assets/...` runtime path.
6. Do not copy staging `.import` files. Let Godot import the curated runtime copy.
7. Run:

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools/validate_runtime_assets.ps1
   dotnet build shuiling.csproj -c Release
   ```

8. Confirm the copied source assets are visible in `git status` and commit them with the
   code that uses them.

## Before considering asset work complete

- The game works after temporarily renaming or removing the local `_downloads` folder.
- No runtime file contains `_downloads`.
- Every model texture and material dependency is inside a tracked runtime directory.
- The relevant license is stored beside the curated pack.
- GitHub Actions passes the runtime asset validation step.

The release workflow intentionally fails when a runtime file references the local source
pool. This protects updater users from missing models, textures, particles, and fallback
visuals.
