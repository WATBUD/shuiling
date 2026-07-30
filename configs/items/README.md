# Item JSON catalogs

Every real inventory item is defined in exactly one JSON catalog and has two stable identifiers:

- `uniqueId`: positive integer used for compact lookups and future migrations.
- `id`: existing string identifier retained for save-file compatibility and readability.

`uniqueId` uses one global, monotonically increasing sequence shared by every
catalog. Item type is determined by its catalog and `system` value, not by a
numeric range. This avoids imposing a per-category item limit.

Rules:

1. Never reuse or renumber a published `uniqueId`.
2. For a new item, use an unused positive integer. Prefer the next suggested ID
   printed by `tools/validate_item_catalogs.ps1`.
3. Never rename a published string `id` without adding a save migration.
4. Add new items to the matching JSON file; do not add parallel hardcoded item lists.
5. Run `tools/validate_item_catalogs.ps1` before publishing.
6. `gem.attribute.none` and other `.none` values are empty-slot sentinels, not inventory items.
