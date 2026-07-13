# Player Controller Files

`PlayerController` is partial by feature boundary.

- `PlayerController.cs`: main lifecycle, input, movement, camera, HUD, inventory/shop/save hooks, interaction orchestration.
- `PlayerController.Formation.cs`: formation board data, companion slot assignment, formation local offsets, and formation UI refresh.

When adding player features, create another partial only if it keeps a complete feature area together and prevents `PlayerController.cs` from growing further. Good candidates are `PlayerController.Camera.cs`, `PlayerController.Hud.cs`, or `PlayerController.Shops.cs` when those areas are next touched.
