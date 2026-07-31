using Godot;
using System.Collections.Generic;

// Build definition types moved to BuildDefinitions.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

public static partial class BuildCatalog
{
	// Three distinct combat behaviors: fully independent auto targeting, player-command
	// priority with automatic fallback, and manual designated-target-only combat.
	public const string AiManualOnly = "manual";
	public const string AiAttackNearest = "independent";
	public const string AiCommandPriority = "command_priority";
	private const string LegacyAiAttackNearest = "attack_nearest";

	private static readonly Dictionary<string, CompanionIdentity> Identities = new()
	{
		["identity.water_spirit"] = new CompanionIdentity
		{
			Id = "identity.water_spirit",
			PassiveKeys = new[] { "identity.passive.water_damage", "identity.passive.water_aoe", "identity.passive.vitality" },
			UniqueSkillKeys = new[] { "identity.skill.water_cannon" },
			MaxHealthBonus = 18,
			AttackMultiplier = 1.04f,
			DefenseBonus = 3,
			AttackRangeBonus = 0.8f,
			ElementAffinityId = "water",
			ElementAffinityDamageMultiplier = 1.30f,
		},
		["identity.wolf"] = new CompanionIdentity
		{
			Id = "identity.wolf",
			PassiveKeys = new[] { "identity.passive.move_speed", "identity.passive.crit_rate" },
			UniqueSkillKeys = new[] { "identity.skill.bite", "identity.skill.howl" },
			AttackBonus = 4,
			MoveSpeedMultiplier = 1.20f,
			CritChanceBonus = 0.10f,
			AttackCooldownMultiplier = 0.94f,
		},
		["identity.dragon"] = new CompanionIdentity
		{
			Id = "identity.dragon",
			PassiveKeys = new[] { "identity.passive.fire_damage", "identity.passive.vitality", "identity.passive.attack_range" },
			UniqueSkillKeys = new[] { "identity.skill.dragon_breath" },
			MaxHealthMultiplier = 1.18f,
			AttackMultiplier = 1.12f,
			DefenseBonus = 8,
			AttackRangeBonus = 1.2f,
			DetectionRadiusBonus = 4.0f,
			ElementAffinityId = "fire",
			ElementAffinityDamageMultiplier = 1.25f,
		},
		["identity.redhorn"] = new CompanionIdentity
		{
			Id = "identity.redhorn",
			PassiveKeys = new[] { "identity.passive.power_strike", "identity.passive.thick_hide" },
			UniqueSkillKeys = new[] { "identity.skill.horn_crash" },
			MaxHealthMultiplier = 1.12f,
			DefenseMultiplier = 1.12f,
			AttackBonus = 3,
			AttackCooldownMultiplier = 1.04f,
		},
		["identity.venom_imp"] = new CompanionIdentity
		{
			Id = "identity.venom_imp",
			PassiveKeys = new[] { "identity.passive.poison_mastery", "identity.passive.agility" },
			UniqueSkillKeys = new[] { "identity.skill.venom_spit" },
			AttackBonus = 5,
			MoveSpeedMultiplier = 1.08f,
			ElementAffinityId = "poison",
			ElementAffinityDamageMultiplier = 1.22f,
		},
		["identity.guardian"] = new CompanionIdentity
		{
			Id = "identity.guardian",
			PassiveKeys = new[] { "identity.passive.guard_oath" },
			UniqueSkillKeys = new[] { "identity.skill.guardian_stance" },
			MaxHealthBonus = 24,
			DefenseBonus = 6,
		},
		["identity.traveler"] = new CompanionIdentity
		{
			Id = "identity.traveler",
			PassiveKeys = new[] { "identity.passive.adaptable" },
			UniqueSkillKeys = new[] { "identity.skill.quick_order" },
			MaxHealthBonus = 10,
			AttackBonus = 2,
			DefenseBonus = 2,
			MoveSpeedMultiplier = 1.04f,
		},
	};

	private static readonly Dictionary<string, string> IdentityByActorName = new()
	{
		["name.monster.slime"] = "identity.water_spirit",
		["name.monster.water_spirit"] = "identity.water_spirit",
		["name.monster.wolf"] = "identity.wolf",
		["name.monster.dragon"] = "identity.dragon",
		["name.monster.redhorn"] = "identity.redhorn",
		["name.monster.imp"] = "identity.venom_imp",
		["name.npc.guard"] = "identity.guardian",
	};

	// 種族分類：把每個 identity(物種) 歸到一個上層種族群組，用於陣盤的種族羈絆加成。
	private static readonly Dictionary<string, string> RaceByIdentity = new()
	{
		["identity.traveler"] = "race.human",
		["identity.guardian"] = "race.human",
		["identity.wolf"] = "race.beast",
		["identity.redhorn"] = "race.beast",
		["identity.dragon"] = "race.dragon",
		["identity.venom_imp"] = "race.demon",
		["identity.water_spirit"] = "race.spirit",
	};

	private const string DefaultRaceId = "race.human";

	private static readonly List<EquipmentDefinition> Equipment = ItemCatalogLoader.LoadEquipment();

	private static readonly List<AttributeGemDefinition> AttributeGems = new()
	{
		new AttributeGemDefinition { Id = "gem.attribute.none", NameKey = "gem.attribute.none", SummaryKey = "gem.summary.none", ElementId = "physical", ElementNameKey = "element.physical", AttackColor = new Color(1.0f, 0.54f, 0.24f, 0.92f) },
	};

	private static readonly List<SkillGemDefinition> SkillGems = ItemCatalogLoader.LoadCoreSkills();

	private static readonly List<AttackModeDefinition> AttackModes = new()
	{
		// Command priority is the safe default and fallback: companions obey explicit
		// orders while continuing to defend the party when no target is designated.
		new AttackModeDefinition { Id = AiCommandPriority, NameKey = "attack_mode.command_priority", BehaviorId = AiCommandPriority },
		new AttackModeDefinition { Id = AiAttackNearest, NameKey = "attack_mode.independent", BehaviorId = AiAttackNearest },
		new AttackModeDefinition { Id = AiManualOnly, NameKey = "attack_mode.manual", BehaviorId = AiManualOnly },
	};

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

	// Companion/player stat calculation moved to BuildCatalog.Calculation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// ── 精煉星等（Refinement stars）──────────────────────────────────────────
	// 星等直接編碼在物品 id 尾端（例如 "equip.weapon.sword#3" = 3★），因此背包堆疊、
	// 已裝備欄位、以及存檔全是字串就能自動保存，不需改資料結構。0★ 維持原本純 id。
	public const int MaxEquipmentStars = EquipmentConfig.MaxStars;
	public const float EquipmentStarBonusPerStar = EquipmentConfig.StarBonusPerStar;
	private const char EquipmentStarSeparator = '#';

	// Equipment star id codec moved to BuildCatalog.StarCodec.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Equipped jump-power calculation moved to BuildCatalog.Calculation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Equipment star display suffix moved to BuildCatalog.StarCodec.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

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

	// Consumables (usable bag items). Town Portal Scroll returns the player to
	// the city from the wild (emergency retreat). Keyed id -> name locale key.
	public const string TownPortalScrollId = "consumable.town_portal";

	private static readonly Dictionary<string, string> Consumables = new();

	static BuildCatalog()
	{
		foreach (ConsumableDefinition item in ItemCatalogLoader.LoadConsumables())
		{
			Consumables[item.Id] = item.NameKey;
		}
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

	public const int MaxSkillGemLevel = 5;

	// --- Core slots (level-gated) ---
	// Index 0 holds the active attack core, which also defines its damage element.
	// Indices 1..6 hold behavior/stat support cores.
	public const int MainCoreUnlockLevel = CoreConfig.MainCoreUnlockLevel;

	// One fixed main attack core (index 0) plus six extension support cores (1..6).
	// The historical name is retained because this value is serialized as the skill
	// core array length throughout the existing save system.
	public const int SupportCoreSlotCount = CoreConfig.SupportCoreSlotCount;

	// Unlock levels for the core skill, then support cores 1 through 6.
	private static readonly int[] SupportCoreUnlockLevels = CoreConfig.SupportCoreUnlockLevels;

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

	// Skill-gem upgrade cost moved to BuildCatalog.Calculation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

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

	// Weapon/equipment/skill-gem stat application moved to BuildCatalog.Calculation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

}
