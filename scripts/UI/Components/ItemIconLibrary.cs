using Godot;
using System.Collections.Generic;

public static class ItemIconLibrary
{
	private const string Root = "res://assets/ui/item_icons/";
	public const float InventorySlotWidth = 52.0f;
	public const int InventoryGridGap = 4;
	private static readonly FontVariation StackCountFont = new()
	{
		// Simulated bold works with the project's fallback font, including CJK and
		// numeric glyphs, without adding a separate font asset.
		VariationEmbolden = 1.0f,
	};
	private static readonly Dictionary<string, string> IconFiles = new()
	{
		["equip.helmet.traveler"] = "helmet_traveler.png",
		["equip.helmet.guardian"] = "helmet_guardian.png",
		["equip.helmet.mystic"] = "helmet_mystic.png",
		["equip.weapon.sword"] = "weapon_sword.png",
		["equip.weapon.short_sword"] = "weapon_short_sword.png",
		["equip.weapon.dagger"] = "weapon_dagger.png",
		["equip.weapon.longbow"] = "weapon_longbow.png",
		["equip.weapon.spear"] = "weapon_spear.png",
		["equip.weapon.warhammer"] = "weapon_warhammer.png",
		["equip.weapon.scepter"] = "weapon_scepter.png",
		["equip.weapon.staff"] = "weapon_staff.png",
		["equip.weapon.great_axe"] = "weapon_great_axe.png",
		["equip.weapon.claws"] = "weapon_claws.png",
		["equip.armor.scout"] = "armor_scout.png",
		["equip.armor.plate"] = "armor_plate.png",
		["equip.armor.spirit_robe"] = "armor_spirit.png",
		["equip.boots.traveler"] = "boots_traveler.png",
		["equip.boots.reinforced"] = "boots_reinforced.png",
		["equip.boots.windrunner"] = "boots_windrunner.png",
		["equip.boots.gravity"] = "boots_gravity.png",
		["equip.accessory.swift_ring"] = "accessory_swift_ring.png",
		["equip.accessory.crit_charm"] = "accessory_crit_charm.png",
		["equip.accessory.turtle_amulet"] = "accessory_guard.png",
		["equip.accessory.focus_lens"] = "accessory_focus_lens.png",
		["gem.skill.fireball"] = "gem_01.png",
		["gem.skill.whirlwind"] = "gem_06.png",
		["gem.skill.meteor"] = "gem_01.png",
		["gem.skill.laser"] = "gem_03.png",
		["gem.skill.rocket"] = "skill_rocket.png",
		["gem.skill.ice_shard"] = "skill_ice_shard.png",
		["gem.skill.lightning"] = "skill_lightning.png",
		["gem.skill.chain"] = "gem_02.png",
		["gem.skill.explosion"] = "gem_01.png",
		["gem.skill.piercing"] = "gem_04.png",
		["gem.skill.life_steal"] = "gem_07.png",
		["gem.skill.split"] = "gem_04.png",
		["gem.skill.multishot"] = "gem_06.png",
		["gem.skill.faster_attacks"] = "gem_03.png",
		["gem.skill.critical_strikes"] = "gem_05.png",
		["gem.skill.swift_projectiles"] = "gem_04.png",
		["gem.skill.brutality"] = "gem_01.png",
		["gem.skill.ailment"] = "gem_07.png",
		["loot.slime_mucus"] = "material_magic.png",
		["loot.beast_hide"] = "material_wood.png",
		["loot.sharp_claw"] = "material_bones.png",
		["loot.soft_fur"] = "material_fur.png",
		["loot.small_bone"] = "material_bones.png",
		["loot.insect_wing"] = "gem_06.png",
		["loot.red_horn"] = "material_ore.png",
		["loot.venom_sac"] = "material_venom.png",
		["loot.water_core"] = "gem_02.png",
		["loot.dragon_scale"] = "material_ore.png",
		["loot.cracked_core"] = "material_stone.png",
		["loot.enhance_crystal.t1"] = "gem_02.png",
		["loot.enhance_crystal.t2"] = "gem_02.png",
		["loot.enhance_crystal.t3"] = "gem_02.png",
		["loot.enhance_crystal.t4"] = "gem_02.png",
		["loot.enhance_crystal.t5"] = "gem_02.png",
		["loot.enhance_crystal.t6"] = "gem_02.png",
		["loot.enhance_crystal.t7"] = "gem_02.png",
		["loot.enhance_crystal.t8"] = "gem_02.png",
		["loot.enhance_crystal.t9"] = "gem_02.png",
		["loot.enhance_crystal.t10"] = "gem_02.png",
	};

	private static readonly Dictionary<string, Texture2D> Cache = new();

	public static Texture2D? Get(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId) || itemId.EndsWith(".none", System.StringComparison.Ordinal))
		{
			return null;
		}

		// Refinement appends a star suffix to the runtime stack id (for example
		// equip.weapon.sword@3). The visual identity must remain tied to the
		// catalogue's stable numeric item id, never to that mutable display/stack
		// string. Resolve the number back to its canonical base id before any icon
		// lookup, so every refinement level shares the same image and cache entry.
		string iconKey = ResolveIconKey(itemId);
		if (Cache.TryGetValue(iconKey, out Texture2D? cached))
		{
			return cached;
		}

		if (!IconFiles.TryGetValue(iconKey, out string? fileName))
		{
			// New equipment without its own art reuses a hand-drawn icon from the
			// same slot (weapons matched by type), so it still looks painted rather
			// than a flat procedural blob.
			fileName = FallbackEquipmentIconFile(iconKey);
			if (fileName == null)
			{
				// Consumables etc. have no PNG asset — draw their 2D icon procedurally.
				Texture2D? generated = CreateProceduralIcon(iconKey);
				if (generated != null)
				{
					Cache[iconKey] = generated;
				}
				return generated;
			}
		}

		string resourcePath = Root + fileName;
		// Never pass a missing path to Load(): Godot reports it as a red engine
		// error even though a null texture can be handled safely by the UI.
		if (!ResourceLoader.Exists(resourcePath, "Texture2D"))
		{
			return CreateProceduralIcon(iconKey);
		}

		Texture2D? texture = ResourceLoader.Load<Texture2D>(resourcePath);
		if (texture != null)
		{
			Cache[iconKey] = texture;
		}
		return texture;
	}

	private static string ResolveIconKey(string itemId)
	{
		int uniqueId = BuildCatalog.GetItemUniqueId(itemId);
		if (uniqueId > 0)
		{
			string canonicalId = BuildCatalog.GetItemIdByUniqueId(uniqueId);
			if (!string.IsNullOrWhiteSpace(canonicalId))
			{
				return canonicalId;
			}
		}

		// Compatibility fallback for malformed or retired refined ids that are not
		// present in the current numeric catalogue.
		return BuildCatalog.GetBaseEquipmentId(itemId);
	}

	// Existing hand-drawn PNGs per slot, reused by new equipment that has no art.
	private static readonly string[] HelmetIcons = { "helmet_traveler.png", "helmet_guardian.png", "helmet_mystic.png" };
	private static readonly string[] ArmorIcons = { "armor_scout.png", "armor_plate.png", "armor_spirit.png" };
	private static readonly string[] BootsIcons = { "boots_traveler.png", "boots_reinforced.png", "boots_windrunner.png", "boots_gravity.png" };
	private static readonly string[] AccessoryIcons = { "accessory_swift_ring.png", "accessory_crit_charm.png", "accessory_guard.png", "accessory_focus_lens.png" };
	private static readonly string[] WeaponIcons = { "weapon_sword.png", "weapon_short_sword.png", "weapon_dagger.png", "weapon_longbow.png", "weapon_spear.png", "weapon_warhammer.png", "weapon_scepter.png", "weapon_staff.png", "weapon_great_axe.png", "weapon_claws.png" };

	// Weapon material ids are "<material>_<noun>"; map the noun to a fitting blade.
	private static readonly Dictionary<string, string> WeaponNounIcon = new(System.StringComparer.Ordinal)
	{
		["blade"] = "weapon_sword.png",
		["axe"] = "weapon_great_axe.png",
		["bow"] = "weapon_longbow.png",
		["spear"] = "weapon_spear.png",
		["mace"] = "weapon_warhammer.png",
		["glaive"] = "weapon_spear.png",
		["rod"] = "weapon_staff.png",
		["dirk"] = "weapon_dagger.png",
		["saber"] = "weapon_short_sword.png",
		["halberd"] = "weapon_spear.png",
	};

	private static string? FallbackEquipmentIconFile(string itemId)
	{
		if (BuildCatalog.IsFreeItem(itemId) || BuildCatalog.GetItemKind(itemId) != InventoryItemKind.Equipment)
		{
			return null;
		}

		uint hash = FnvHash(itemId);
		switch (BuildCatalog.GetEquipment(itemId).Slot)
		{
			case EquipmentSlot.Helmet:
				return HelmetIcons[hash % HelmetIcons.Length];
			case EquipmentSlot.Armor:
				return ArmorIcons[hash % ArmorIcons.Length];
			case EquipmentSlot.Boots:
				return BootsIcons[hash % BootsIcons.Length];
			case EquipmentSlot.Accessory:
				return AccessoryIcons[hash % AccessoryIcons.Length];
			case EquipmentSlot.Weapon:
				string[] parts = itemId.Split('.');
				if (parts.Length >= 3)
				{
					string[] seg = parts[2].Split('_');
					if (seg.Length >= 2 && WeaponNounIcon.TryGetValue(seg[1], out string? mapped))
					{
						return mapped;
					}
				}

				return WeaponIcons[hash % WeaponIcons.Length];
			default:
				return null;
		}
	}

	private static uint FnvHash(string value)
	{
		uint hash = 2166136261u;
		foreach (char c in value)
		{
			hash ^= c;
			hash *= 16777619u;
		}

		return hash;
	}

	private static Texture2D? CreateProceduralIcon(string itemId)
	{
		if (itemId == BuildCatalog.TownPortalScrollId)
		{
			return CreateScrollTexture();
		}

		// Equipment without a hand-drawn PNG gets a slot-shaped silhouette tinted by
		// a stable per-item colour, so every piece has a matching icon at any count.
		if (BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment && !BuildCatalog.IsFreeItem(itemId))
		{
			return CreateEquipmentIcon(BuildCatalog.GetEquipment(itemId).Slot, AccentColorFor(itemId));
		}

		return null;
	}

	// Stable colour from the item id (FNV-1a hash → hue). Same id always maps to the
	// same tint, so icons are consistent across sessions and never random.
	private static Color AccentColorFor(string itemId)
	{
		uint hash = 2166136261u;
		foreach (char c in itemId)
		{
			hash ^= c;
			hash *= 16777619u;
		}

		return Color.FromHsv((hash % 360u) / 360.0f, 0.52f, 0.88f);
	}

	private static Texture2D CreateEquipmentIcon(EquipmentSlot slot, Color tint)
	{
		const int size = 64;
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(new Color(0, 0, 0, 0));
		Color dark = tint.Darkened(0.35f);
		Color edge = tint.Lightened(0.30f);

		switch (slot)
		{
			case EquipmentSlot.Helmet:
				FillCircle(image, 32, 32, 16, tint);
				FillRect(image, 14, 34, 50, 42, dark); // brim
				FillRect(image, 27, 30, 37, 40, tint.Darkened(0.55f)); // visor gap
				FillRect(image, 22, 16, 42, 20, edge); // crest highlight
				break;
			case EquipmentSlot.Weapon:
				FillRect(image, 30, 44, 34, 58, dark); // hilt
				FillRect(image, 23, 41, 41, 45, dark); // guard
				for (int y = 8; y < 44; y++) // tapered blade
				{
					int half = Mathf.Max(1, 6 - (y - 8) / 8);
					FillRect(image, 32 - half, y, 32 + half, y + 1, tint);
					SetSafe(image, 32 - half, y, edge);
				}

				break;
			case EquipmentSlot.Armor:
				for (int y = 16; y < 52; y++) // chest trapezoid
				{
					int half = 8 + (y - 16) / 3;
					FillRect(image, 32 - half, y, 32 + half, y + 1, y < 22 ? edge : tint);
				}

				FillCircle(image, 17, 20, 7, dark); // shoulders
				FillCircle(image, 47, 20, 7, dark);
				FillRect(image, 30, 16, 34, 50, dark); // seam
				break;
			case EquipmentSlot.Boots:
				FillRect(image, 24, 10, 38, 48, tint); // shaft
				FillRect(image, 24, 44, 52, 58, tint); // foot
				FillRect(image, 22, 56, 54, 61, dark); // sole
				FillRect(image, 26, 12, 30, 46, edge); // lace highlight
				break;
			default: // Accessory — ring with a gem
				FillCircle(image, 32, 38, 16, tint);
				FillCircle(image, 32, 38, 10, new Color(0, 0, 0, 0));
				FillCircle(image, 32, 18, 6, edge);
				FillCircle(image, 32, 18, 3, tint.Lightened(0.6f));
				break;
		}

		return ImageTexture.CreateFromImage(image);
	}

	// A simple 2D parchment-scroll icon: parchment sheet, two rolled ends, ink
	// lines and a red wax seal. Drawn in code so no image import is needed.
	private static Texture2D CreateScrollTexture()
	{
		const int size = 64;
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(new Color(0, 0, 0, 0));

		var parchment = new Color(0.94f, 0.87f, 0.67f);
		var parchmentEdge = new Color(0.82f, 0.72f, 0.50f);
		var roll = new Color(0.66f, 0.47f, 0.28f);
		var rollDark = new Color(0.48f, 0.32f, 0.17f);
		var ink = new Color(0.36f, 0.28f, 0.18f);
		var seal = new Color(0.82f, 0.18f, 0.16f);

		// Parchment sheet.
		FillRect(image, 17, 13, 47, 51, parchment);
		FillRect(image, 17, 13, 19, 51, parchmentEdge);
		FillRect(image, 45, 13, 47, 51, parchmentEdge);

		// Ink text lines.
		FillRect(image, 22, 22, 43, 24, ink);
		FillRect(image, 22, 28, 45, 30, ink);
		FillRect(image, 22, 34, 41, 36, ink);
		FillRect(image, 22, 40, 44, 42, ink);

		// Rolled top and bottom ends (rounded).
		DrawRoll(image, 11, 7, 53, 17, roll, rollDark);
		DrawRoll(image, 11, 47, 53, 57, roll, rollDark);

		// Red wax seal.
		FillCircle(image, 39, 45, 5, seal);

		return ImageTexture.CreateFromImage(image);
	}

	private static void DrawRoll(Image image, int x0, int y0, int x1, int y1, Color body, Color shade)
	{
		FillRect(image, x0, y0, x1, y1, body);
		// Trim the four corners for a rounded, capsule-like roll.
		SetSafe(image, x0, y0, new Color(0, 0, 0, 0));
		SetSafe(image, x1 - 1, y0, new Color(0, 0, 0, 0));
		SetSafe(image, x0, y1 - 1, new Color(0, 0, 0, 0));
		SetSafe(image, x1 - 1, y1 - 1, new Color(0, 0, 0, 0));
		// Shaded core stripe for a bit of depth.
		int midY = (y0 + y1) / 2;
		FillRect(image, x0 + 1, midY, x1 - 1, midY + 1, shade);
	}

	private static void FillRect(Image image, int x0, int y0, int x1, int y1, Color color)
	{
		for (int y = y0; y < y1; y++)
		{
			for (int x = x0; x < x1; x++)
			{
				SetSafe(image, x, y, color);
			}
		}
	}

	private static void FillCircle(Image image, int cx, int cy, int radius, Color color)
	{
		int radiusSquared = radius * radius;
		for (int y = cy - radius; y <= cy + radius; y++)
		{
			for (int x = cx - radius; x <= cx + radius; x++)
			{
				int dx = x - cx;
				int dy = y - cy;
				if (dx * dx + dy * dy <= radiusSquared)
				{
					SetSafe(image, x, y, color);
				}
			}
		}
	}

	private static void SetSafe(Image image, int x, int y, Color color)
	{
		if (x >= 0 && y >= 0 && x < image.GetWidth() && y < image.GetHeight())
		{
			image.SetPixel(x, y, color);
		}
	}

	public static void Apply(Button button, string itemId, int maxWidth)
	{
		button.Icon = Get(itemId);
		// Every item icon is rendered inside the same square visual boundary.  Source
		// textures may be 46px, 512px, 1254px, or have a different aspect ratio;
		// Button keeps that aspect ratio while this cap prevents large sources from
		// changing the layout or appearing larger than neighbouring equipment.
		button.ExpandIcon = true;
		button.AddThemeConstantOverride("icon_max_width", Mathf.Max(1, maxWidth));
		button.IconAlignment = HorizontalAlignment.Left;
	}

	public static TextureRect CreateRect(string itemId, float size)
	{
		return new TextureRect
		{
			Texture = Get(itemId),
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	// Common RPG stack-count treatment: the quantity is a bold overlay anchored
	// inside the bottom-right corner, so it never consumes layout width or pushes
	// the item icon away from the centre.
	public static Label AddStackCountBadge(Control itemSlot, int count)
	{
		var badge = new Label
		{
			Text = Mathf.Max(count, 0).ToString(),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = false,
			AnchorLeft = 1.0f,
			AnchorTop = 1.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			// The standard inventory icon is 42 px and centred in a 52 px slot.
			// Keep the label's entire rectangle inside that image boundary.
			OffsetLeft = -47.0f,
			OffsetTop = -29.0f,
			OffsetRight = -5.0f,
			OffsetBottom = -7.0f,
		};
		badge.AddThemeFontOverride("font", StackCountFont);
		badge.AddThemeFontSizeOverride("font_size", 17);
		badge.AddThemeColorOverride("font_color", new Color(1.0f, 0.97f, 0.84f));
		badge.AddThemeColorOverride("font_outline_color", new Color(0.015f, 0.018f, 0.022f, 0.98f));
		badge.AddThemeConstantOverride("outline_size", 4);
		itemSlot.AddChild(badge);
		return badge;
	}

	public static void UpdateResponsiveGridColumns(GridContainer grid, Control viewport)
	{
		float availableWidth = Mathf.Max(viewport.Size.X, InventorySlotWidth);
		int columns = Mathf.Max(
			Mathf.FloorToInt((availableWidth + InventoryGridGap) / (InventorySlotWidth + InventoryGridGap)),
			1);
		if (grid.Columns != columns)
		{
			grid.Columns = columns;
		}
	}
}
