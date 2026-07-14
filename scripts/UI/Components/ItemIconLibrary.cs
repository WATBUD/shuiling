using Godot;
using System.Collections.Generic;

public static class ItemIconLibrary
{
	private const string Root = "res://assets/ui/item_icons/";
	private static readonly Dictionary<string, string> IconFiles = new()
	{
		["equip.helmet.traveler"] = "helmet_traveler.png",
		["equip.helmet.guardian"] = "helmet_guardian.png",
		["equip.helmet.mystic"] = "helmet_mystic.png",
		["equip.weapon.sword"] = "weapon_sword.png",
		["equip.weapon.staff"] = "weapon_magic.png",
		["equip.weapon.great_axe"] = "weapon_heavy.png",
		["equip.weapon.claws"] = "weapon_sword.png",
		["equip.armor.scout"] = "armor_scout.png",
		["equip.armor.plate"] = "armor_plate.png",
		["equip.armor.spirit_robe"] = "armor_spirit.png",
		["equip.accessory.swift_ring"] = "accessory_swift.png",
		["equip.accessory.crit_charm"] = "accessory_magic.png",
		["equip.accessory.turtle_amulet"] = "accessory_guard.png",
		["equip.accessory.focus_lens"] = "accessory_magic.png",
		["gem.attribute.fire"] = "gem_01.png",
		["gem.attribute.water"] = "gem_02.png",
		["gem.attribute.lightning"] = "gem_03.png",
		["gem.attribute.ice"] = "gem_04.png",
		["gem.attribute.poison"] = "gem_05.png",
		["gem.attribute.wind"] = "gem_06.png",
		["gem.attribute.dark"] = "gem_07.png",
		["gem.attribute.light"] = "gem_03.png",
		["gem.skill.fireball"] = "gem_01.png",
		["gem.skill.heal"] = "gem_05.png",
		["gem.skill.shield"] = "gem_02.png",
		["gem.skill.whirlwind"] = "gem_06.png",
		["gem.skill.meteor"] = "gem_01.png",
		["gem.skill.dash"] = "gem_04.png",
		["gem.skill.laser"] = "gem_03.png",
		["gem.skill.summon"] = "gem_07.png",
		["gem.skill.chain"] = "gem_02.png",
		["gem.skill.explosion"] = "gem_01.png",
		["gem.skill.piercing"] = "gem_04.png",
		["gem.skill.life_steal"] = "gem_07.png",
		["loot.slime_mucus"] = "material_magic.png",
		["loot.beast_hide"] = "material_wood.png",
		["loot.sharp_claw"] = "material_bones.png",
		["loot.soft_fur"] = "material_wood.png",
		["loot.small_bone"] = "material_bones.png",
		["loot.insect_wing"] = "gem_06.png",
		["loot.red_horn"] = "material_ore.png",
		["loot.venom_sac"] = "material_venom.png",
		["loot.water_core"] = "gem_02.png",
		["loot.dragon_scale"] = "material_ore.png",
		["loot.cracked_core"] = "material_stone.png",
	};

	private static readonly Dictionary<string, Texture2D> Cache = new();

	public static Texture2D? Get(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId) || itemId.EndsWith(".none", System.StringComparison.Ordinal))
		{
			return null;
		}

		if (Cache.TryGetValue(itemId, out Texture2D? cached))
		{
			return cached;
		}

		if (!IconFiles.TryGetValue(itemId, out string? fileName))
		{
			return null;
		}

		Texture2D? texture = ResourceLoader.Load<Texture2D>(Root + fileName);
		if (texture != null)
		{
			Cache[itemId] = texture;
		}
		return texture;
	}

	public static void Apply(Button button, string itemId, int maxWidth)
	{
		button.Icon = Get(itemId);
		button.ExpandIcon = true;
		button.IconAlignment = HorizontalAlignment.Left;
	}

	public static TextureRect CreateRect(string itemId, float size)
	{
		return new TextureRect
		{
			Texture = Get(itemId),
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}
}
