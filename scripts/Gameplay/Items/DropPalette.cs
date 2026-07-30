using Godot;
using System.Collections.Generic;

// Central colour + material source for world drops. Item tints are derived from
// the item id (the same rules WorldDrop used to inline), and body materials are
// cached and shared read-only: a defeated boss can emit 8+ drops in one frame,
// so building a fresh material per drop was a measurable slice of the
// death-frame hitch. Cached materials are never mutated after creation.
public static class DropPalette
{
	public static readonly Color Gold = new(1.0f, 0.78f, 0.18f, 0.96f);

	private static readonly Dictionary<uint, StandardMaterial3D> BodyMaterialCache = new();

	public static Color ForItem(string itemId)
	{
		if (string.IsNullOrEmpty(itemId))
		{
			return new Color(0.82f, 0.92f, 1.0f, 0.95f);
		}

		if (itemId.StartsWith("equip."))
		{
			return new Color(0.50f, 0.78f, 1.0f, 0.95f);
		}

		if (itemId.StartsWith("gem.attribute."))
		{
			return new Color(0.96f, 0.46f, 1.0f, 0.95f);
		}

		if (itemId.StartsWith("gem.skill."))
		{
			return new Color(0.40f, 1.0f, 0.66f, 0.95f);
		}

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return MonsterLootCatalog.GetDropColor(itemId);
		}

		return new Color(0.82f, 0.92f, 1.0f, 0.95f);
	}

	// A shared emissive body material for the given tint. Keyed by colour so item
	// drops of the same category reuse one instance across the whole session.
	public static StandardMaterial3D GetBodyMaterial(Color color)
	{
		uint key = color.ToRgba32();
		if (BodyMaterialCache.TryGetValue(key, out StandardMaterial3D? cached))
		{
			return cached;
		}

		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color * 0.45f,
			Roughness = 0.35f,
			Metallic = 0.12f,
		};
		BodyMaterialCache[key] = material;
		return material;
	}
}
