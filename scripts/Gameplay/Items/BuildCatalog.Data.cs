using Godot;
using System.Collections.Generic;

public static partial class BuildCatalog
{
	public static CompanionIdentity GetIdentity(SimpleActor actor)
	{
		string identityId = IdentityByActorName.TryGetValue(actor.DisplayName, out string? mappedId)
			? mappedId
			: actor.ActorKind == "monster" ? "identity.redhorn" : "identity.traveler";
		return Identities.TryGetValue(identityId, out CompanionIdentity? identity) ? identity : Identities["identity.traveler"];
	}

	// 回傳寵物的種族群組 id（如 race.dragon）。
	public static string GetRaceId(SimpleActor actor)
	{
		string identityId = GetIdentity(actor).Id;
		return RaceByIdentity.TryGetValue(identityId, out string? raceId) ? raceId : DefaultRaceId;
	}

	// 種族群組 id 本身就是本地化 key（race.human / race.beast ...），這裡集中一處方便日後改動。
	public static string GetRaceNameKey(string raceId)
	{
		return string.IsNullOrEmpty(raceId) ? DefaultRaceId : raceId;
	}

	// 回傳寵物的屬性 id（如 fire / water / physical）。
	public static string GetElementId(SimpleActor actor)
	{
		return actor.CurrentBuildStats.DamageElementId;
	}

	public static CompanionBuildLoadout CreateStarterLoadout(SimpleActor actor)
	{
		var loadout = new CompanionBuildLoadout();
		string identityId = GetIdentity(actor).Id;

		if (actor.CombatRole == "Support")
		{
			loadout.WeaponId = "equip.weapon.staff";
			loadout.ArmorId = "equip.armor.spirit_robe";
			loadout.SkillGemIds = new[] { "gem.skill.ice_shard", "gem.skill.ailment", "gem.skill.life_steal" };
		}
		else if (actor.CombatRole == "Tank")
		{
			loadout.HelmetId = "equip.helmet.guardian";
			loadout.WeaponId = "equip.weapon.great_axe";
			loadout.ArmorId = "equip.armor.plate";
			loadout.AccessoryId = "equip.accessory.turtle_amulet";
			loadout.SkillGemIds = new[] { "gem.skill.whirlwind", "gem.skill.brutality", "gem.skill.life_steal" };
		}
		else if (actor.CombatRole == "Ranged")
		{
			loadout.HelmetId = "equip.helmet.mystic";
			loadout.WeaponId = "equip.weapon.staff";
			loadout.ArmorId = "equip.armor.spirit_robe";
			loadout.AccessoryId = "equip.accessory.focus_lens";
			loadout.SkillGemIds = new[] { "gem.skill.laser", "gem.skill.chain", "gem.skill.none" };
		}
		else if (identityId == "identity.wolf")
		{
			loadout.WeaponId = "equip.weapon.claws";
			loadout.AccessoryId = "equip.accessory.crit_charm";
			loadout.SkillGemIds = new[] { "gem.skill.whirlwind", "gem.skill.faster_attacks", "gem.skill.critical_strikes" };
		}
		else if (identityId == "identity.dragon")
		{
			loadout.HelmetId = "equip.helmet.guardian";
			loadout.WeaponId = "equip.weapon.great_axe";
			loadout.ArmorId = "equip.armor.plate";
			loadout.SkillGemIds = new[] { "gem.skill.meteor", "gem.skill.explosion", "gem.skill.none" };
		}
		else if (identityId == "identity.water_spirit")
		{
			loadout.WeaponId = "equip.weapon.staff";
			loadout.ArmorId = "equip.armor.spirit_robe";
			loadout.SkillGemIds = new[] { "gem.skill.ice_shard", "gem.skill.piercing", "gem.skill.ailment" };
		}
		else if (identityId == "identity.venom_imp")
		{
			loadout.WeaponId = "equip.weapon.claws";
			loadout.SkillGemIds = new[] { "gem.skill.whirlwind", "gem.skill.life_steal", "gem.skill.ailment" };
		}

		return loadout;
	}

	public static EquipmentDefinition GetEquipment(string id)
	{
		string baseId = GetBaseEquipmentId(id);
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Id == baseId)
			{
				return equipment;
			}
		}

		return Equipment.Find(equipment => equipment.Id == "equip.helmet.traveler")!;
	}

	public static AttributeGemDefinition GetAttributeGem(string id)
	{
		foreach (AttributeGemDefinition gem in AttributeGems)
		{
			if (gem.Id == id)
			{
				return gem;
			}
		}

		return AttributeGems[0];
	}

	public static SkillGemDefinition GetSkillGem(string id)
	{
		foreach (SkillGemDefinition gem in SkillGems)
		{
			if (gem.Id == id)
			{
				return gem;
			}
		}

		return SkillGems[0];
	}

	public static AttackModeDefinition GetAttackMode(string id)
	{
		// Before the three-mode split, attack_nearest still honored player-designated
		// targets first. Preserve that behavior when loading an older save.
		if (id == LegacyAiAttackNearest)
		{
			return AttackModes[0];
		}

		foreach (AttackModeDefinition mode in AttackModes)
		{
			if (mode.Id == id)
			{
				return mode;
			}
		}

		return AttackModes[0];
	}

	public static List<EquipmentDefinition> GetEquipmentDefinitions(EquipmentSlot slot)
	{
		var definitions = new List<EquipmentDefinition>();
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Slot == slot)
			{
				definitions.Add(equipment);
			}
		}

		return definitions;
	}

	public static List<AttributeGemDefinition> GetAttributeGemDefinitions()
	{
		return new List<AttributeGemDefinition>(AttributeGems);
	}

	public static List<SkillGemDefinition> GetSkillGemDefinitions()
	{
		return new List<SkillGemDefinition>(SkillGems);
	}

	public static List<AttackModeDefinition> GetAttackModeDefinitions()
	{
		return new List<AttackModeDefinition>(AttackModes);
	}

	public static List<string> GetAllEquipmentItemIds()
	{
		// Complete equipment catalogue for developer test worlds. Normal new games
		// intentionally start without free equipment in the bag.
		var ids = new List<string>();
		foreach (EquipmentDefinition equipment in Equipment)
		{
			ids.Add(equipment.Id);
		}

		return ids;
	}

	// All non-free active/support cores, used by test mode.
	public static List<string> GetAllGemItemIds()
	{
		var ids = new List<string>();
		foreach (AttributeGemDefinition gem in AttributeGems)
		{
			if (!IsFreeItem(gem.Id))
			{
				ids.Add(gem.Id);
			}
		}

		foreach (SkillGemDefinition gem in SkillGems)
		{
			if (!IsFreeItem(gem.Id))
			{
				ids.Add(gem.Id);
			}
		}

		return ids;
	}

	// Canonical inventory catalogue for developer/test worlds. New definitions
	// added to any source catalogue automatically appear here without maintaining
	// a separate starter-item list.
	public static List<string> GetAllInventoryItemIds()
	{
		var ids = new HashSet<string>(System.StringComparer.Ordinal);
		foreach (string id in GetAllEquipmentItemIds())
		{
			if (!IsFreeItem(id))
			{
				ids.Add(id);
			}
		}
		foreach (string id in GetAllGemItemIds())
		{
			ids.Add(id);
		}
		foreach (MonsterLootDefinition material in MonsterLootCatalog.Materials)
		{
			ids.Add(material.Id);
		}
		foreach (string id in Consumables.Keys)
		{
			ids.Add(id);
		}

		var result = new List<string>(ids);
		result.Sort(System.StringComparer.Ordinal);
		return result;
	}

	public static bool IsFreeItem(string id)
	{
		return id is "gem.attribute.none" or "gem.skill.none"
			|| id.EndsWith(".none", System.StringComparison.Ordinal);
	}

	public static bool IsRetiredSkillCore(string id)
	{
		return id is "gem.skill.heal" or "gem.skill.shield" or "gem.skill.dash";
	}

	public static bool IsRetiredAttributeGem(string id)
	{
		return id.StartsWith("gem.attribute.", System.StringComparison.Ordinal)
			&& id != "gem.attribute.none";
	}

	public static int GetItemUniqueId(string id)
	{
		string baseId = GetBaseEquipmentId(id);
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Id == baseId)
			{
				return equipment.UniqueId;
			}
		}
		foreach (SkillGemDefinition gem in SkillGems)
		{
			if (gem.Id == id)
			{
				return gem.UniqueId;
			}
		}
		foreach (ConsumableDefinition item in ItemCatalogLoader.LoadConsumablesCached())
		{
			if (item.Id == id)
			{
				return item.UniqueId;
			}
		}
		foreach (MonsterLootDefinition material in MonsterLootCatalog.Materials)
		{
			if (material.Id == id)
			{
				return material.UniqueId;
			}
		}
		return 0;
	}

	public static string GetItemIdByUniqueId(int uniqueId)
	{
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.UniqueId == uniqueId)
			{
				return equipment.Id;
			}
		}
		foreach (SkillGemDefinition gem in SkillGems)
		{
			if (gem.UniqueId == uniqueId)
			{
				return gem.Id;
			}
		}
		foreach (ConsumableDefinition item in ItemCatalogLoader.LoadConsumablesCached())
		{
			if (item.UniqueId == uniqueId)
			{
				return item.Id;
			}
		}
		foreach (MonsterLootDefinition material in MonsterLootCatalog.Materials)
		{
			if (material.UniqueId == uniqueId)
			{
				return material.Id;
			}
		}
		return string.Empty;
	}

	public static bool IsConsumable(string id)
	{
		return Consumables.ContainsKey(id);
	}

	public static string GetItemNameKey(string id)
	{
		string equipmentId = GetBaseEquipmentId(id);
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Id == equipmentId)
			{
				return equipment.NameKey;
			}
		}

		foreach (AttributeGemDefinition gem in AttributeGems)
		{
			if (gem.Id == id)
			{
				return gem.NameKey;
			}
		}

		foreach (SkillGemDefinition gem in SkillGems)
		{
			if (gem.Id == id)
			{
				return gem.NameKey;
			}
		}

		if (Consumables.TryGetValue(id, out string? consumableNameKey))
		{
			return consumableNameKey;
		}

		return id;
	}

	public static InventoryItemKind GetItemKind(string id)
	{
		string equipmentId = GetBaseEquipmentId(id);
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Id == equipmentId)
			{
				return InventoryItemKind.Equipment;
			}
		}

		foreach (AttributeGemDefinition gem in AttributeGems)
		{
			if (gem.Id == id)
			{
				return InventoryItemKind.AttributeGem;
			}
		}

		foreach (SkillGemDefinition gem in SkillGems)
		{
			if (gem.Id == id)
			{
				return InventoryItemKind.SkillGem;
			}
		}

		if (Consumables.ContainsKey(id))
		{
			return InventoryItemKind.Consumable;
		}

		return InventoryItemKind.SkillGem;
	}

	public static string GetNextEquipmentId(EquipmentSlot slot, string currentId)
	{
		var matching = new List<EquipmentDefinition>();
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Slot == slot)
			{
				matching.Add(equipment);
			}
		}

		string currentBaseId = GetBaseEquipmentId(currentId);
		for (int index = 0; index < matching.Count; index++)
		{
			if (matching[index].Id == currentBaseId)
			{
				return matching[(index + 1) % matching.Count].Id;
			}
		}

		return matching.Count > 0 ? matching[0].Id : currentId;
	}

	public static string GetNextAttributeGemId(string currentId)
	{
		for (int index = 0; index < AttributeGems.Count; index++)
		{
			if (AttributeGems[index].Id == currentId)
			{
				return AttributeGems[(index + 1) % AttributeGems.Count].Id;
			}
		}

		return AttributeGems[0].Id;
	}

	public static bool IsMainCoreUnlocked(int level)
	{
		return level >= MainCoreUnlockLevel;
	}

	public static int GetUnlockedSupportCoreCount(int level)
	{
		int count = 0;
		foreach (int threshold in SupportCoreUnlockLevels)
		{
			if (level >= threshold)
			{
				count++;
			}
		}

		return count;
	}

	public static int GetSupportCoreUnlockLevel(int index)
	{
		return index >= 0 && index < SupportCoreUnlockLevels.Length ? SupportCoreUnlockLevels[index] : int.MaxValue;
	}

	public static int GetTotalCoreSlots(int level)
	{
		return (IsMainCoreUnlocked(level) ? 1 : 0) + GetUnlockedSupportCoreCount(level);
	}

	public static bool IsUpgradeableSkillGem(string gemId)
	{
		return GetSkillGem(gemId).BehaviorId != ProjectileBehavior.None;
	}

	public static bool IsRangedActiveSkillGem(string gemId) => GetSkillGem(gemId).IsRangedActiveSkill;

	public static bool IsProjectileActiveSkillGem(string gemId) => GetSkillGem(gemId).UsesProjectile;

	public static bool IsProjectileSupportGem(string gemId) => GetSkillGem(gemId).RequiresProjectile;

	public static bool IsMainAttackCore(string gemId)
	{
		return IsRangedActiveSkillGem(gemId) || gemId == "gem.skill.whirlwind";
	}

	public static bool IsSupportCore(string gemId)
	{
		return GetSkillGem(gemId).IsSupportEffect;
	}

	public static bool HasMainAttackCore(CompanionBuildLoadout loadout)
	{
		return IsMainAttackCore(loadout.GetSkillGemId(0));
	}

	public static string GetSkillGemCategoryKey(string gemId)
	{
		SkillGemDefinition gem = GetSkillGem(gemId);
		if (IsSupportCore(gemId))
		{
			return "tooltip.gem_category.support";
		}

		if (gem.IsRangedActiveSkill || gem.EnablesHeal || gem.EnablesShield || gemId == "gem.skill.whirlwind")
		{
			return "tooltip.gem_category.active";
		}

		return "tooltip.gem_category.effect";
	}

	public static bool HasRangedActiveSkill(CompanionBuildLoadout loadout)
	{
		return IsRangedActiveSkillGem(loadout.GetSkillGemId(0));
	}

	public static bool HasProjectileActiveSkill(CompanionBuildLoadout loadout)
	{
		return IsProjectileActiveSkillGem(loadout.GetSkillGemId(0));
	}

	public static string GetNextSkillGemId(string currentId)
	{
		for (int index = 0; index < SkillGems.Count; index++)
		{
			if (SkillGems[index].Id == currentId)
			{
				return SkillGems[(index + 1) % SkillGems.Count].Id;
			}
		}

		return SkillGems[0].Id;
	}

	public static string GetNextAttackModeId(string currentId)
	{
		for (int index = 0; index < AttackModes.Count; index++)
		{
			if (AttackModes[index].Id == currentId)
			{
				return AttackModes[(index + 1) % AttackModes.Count].Id;
			}
		}

		return AttackModes[0].Id;
	}

	public static string GetDefaultAttackModeId(SimpleActor actor)
	{
		return AiManualOnly;
	}

	public static string LocalizedList(string[] keys)
	{
		if (keys.Length == 0)
		{
			return "-";
		}

		var values = new List<string>();
		foreach (string key in keys)
		{
			values.Add(LocaleText.T(key));
		}

		return string.Join(" / ", values);
	}

	public static string LocalizedSkillGems(CompanionBuildLoadout loadout)
	{
		var values = new List<string>();
		foreach (string skillId in loadout.SkillGemIds)
		{
			values.Add(LocaleText.T(GetSkillGem(skillId).NameKey));
		}

		return string.Join(" / ", values);
	}

	public static string LocalizedEquipmentSet(CompanionBuildLoadout loadout)
	{
		return string.Join(" / ", new[]
		{
			LocaleText.T(GetEquipment(loadout.HelmetId).NameKey),
			LocaleText.T(GetEquipment(loadout.WeaponId).NameKey),
			LocaleText.T(GetEquipment(loadout.ArmorId).NameKey),
			LocaleText.T(GetEquipment(loadout.BootsId).NameKey),
			LocaleText.T(GetEquipment(loadout.AccessoryId).NameKey),
		});
	}
}
