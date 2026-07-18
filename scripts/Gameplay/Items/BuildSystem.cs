using Godot;
using System.Collections.Generic;

public enum EquipmentSlot
{
	Helmet,
	Weapon,
	Armor,
	Boots,
	Accessory,
}

public enum InventoryItemKind
{
	Equipment,
	AttributeGem,
	SkillGem,
}

public sealed class CompanionIdentity
{
	public string Id { get; set; } = "identity.traveler";
	public string[] PassiveKeys { get; set; } = System.Array.Empty<string>();
	public string[] UniqueSkillKeys { get; set; } = System.Array.Empty<string>();
	public int MaxHealthBonus { get; set; }
	public int AttackBonus { get; set; }
	public int DefenseBonus { get; set; }
	public float MaxHealthMultiplier { get; set; } = 1.0f;
	public float AttackMultiplier { get; set; } = 1.0f;
	public float DefenseMultiplier { get; set; } = 1.0f;
	public float MoveSpeedMultiplier { get; set; } = 1.0f;
	public float AttackCooldownMultiplier { get; set; } = 1.0f;
	public float AttackRangeBonus { get; set; }
	public float DetectionRadiusBonus { get; set; }
	public float CritChanceBonus { get; set; }
	public string ElementAffinityId { get; set; } = string.Empty;
	public float ElementAffinityDamageMultiplier { get; set; } = 1.0f;
}

public sealed class EquipmentDefinition
{
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = string.Empty;
	public string SummaryKey { get; set; } = string.Empty;
	public EquipmentSlot Slot { get; set; }
	public int MaxHealthBonus { get; set; }
	public int AttackBonus { get; set; }
	public int DefenseBonus { get; set; }
	public float MoveSpeedBonus { get; set; }
	public float AttackCooldownReduction { get; set; }
	public float AttackRangeBonus { get; set; }
	public float CritChanceBonus { get; set; }
	public int SocketCount { get; set; }
}

public sealed class AttributeGemDefinition
{
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = string.Empty;
	public string SummaryKey { get; set; } = string.Empty;
	public string ElementId { get; set; } = "physical";
	public string ElementNameKey { get; set; } = "element.physical";
	public Color AttackColor { get; set; } = new(1.0f, 0.54f, 0.24f, 0.92f);
	public int AttackBonus { get; set; }
	public int DefenseBonus { get; set; }
	public float MoveSpeedBonus { get; set; }
	public float AttackRangeBonus { get; set; }
	public float CritChanceBonus { get; set; }
	public float LifeStealPercent { get; set; }
	public float ControlChance { get; set; }
	public float KnockbackForce { get; set; }
}

public sealed class SkillGemDefinition
{
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = string.Empty;
	public string SummaryKey { get; set; } = string.Empty;
	public int MaxHealthBonus { get; set; }
	public int AttackBonus { get; set; }
	public int DefenseBonus { get; set; }
	public float MoveSpeedBonus { get; set; }
	public float AttackCooldownReduction { get; set; }
	public float AttackRangeBonus { get; set; }
	public float DetectionRadiusBonus { get; set; }
	public float FollowDistanceMultiplier { get; set; } = 1.0f;
	public float CritChanceBonus { get; set; }
	public float LifeStealPercent { get; set; }
	public bool EnablesHeal { get; set; }
	public bool EnablesShield { get; set; }
	public bool IsRangedActiveSkill { get; set; }

	// PoE-style attack behavior. A gem either just tweaks stats (BehaviorId == None)
	// or attaches a projectile behavior that shapes how the base attack plays out.
	public string BehaviorId { get; set; } = ProjectileBehavior.None;

	// Base magnitude at gem level 1. Meaning depends on BehaviorId:
	//  Multi   -> extra projectiles fired at cast
	//  Split   -> child projectiles spawned on first hit
	//  Chain   -> number of bounces to new targets
	//  Pierce  -> number of enemies passed through
	//  Explosion -> unused (radius drives it)
	public int BehaviorMagnitude { get; set; }

	// Base explosion / area radius at gem level 1 (Explosion behavior only).
	public float BehaviorRadius { get; set; }

	// Loot material consumed (alongside gold) to raise this gem's level.
	public string UpgradeMaterialId { get; set; } = string.Empty;
}

public readonly record struct SkillGemUpgradeCost(int NextLevel, int Gold, string MaterialId, int MaterialCount);

public static class ProjectileBehavior
{
	public const string None = "none";
	public const string Multi = "multi";
	public const string Split = "split";
	public const string Chain = "chain";
	public const string Pierce = "pierce";
	public const string Explosion = "explosion";
}

// Aggregated behavior for one companion's current build. Combines every equipped
// behavior gem (and their levels) into the counts the projectile actually consumes.
public sealed class ProjectileBehaviorProfile
{
	public int ExtraProjectiles { get; set; }   // Multi: fired together at cast
	public int SplitCount { get; set; }          // Split: children spawned on first hit
	public int ChainBounces { get; set; }        // Chain: hops to fresh targets
	public int PierceCount { get; set; }          // Pierce: enemies passed through
	public float ExplosionRadius { get; set; }   // Explosion: AoE radius on hit

	public bool HasAny =>
		ExtraProjectiles > 0 || SplitCount > 0 || ChainBounces > 0 || PierceCount > 0 || ExplosionRadius > 0.0f;

	public ProjectileBehaviorProfile Clone()
	{
		return new ProjectileBehaviorProfile
		{
			ExtraProjectiles = ExtraProjectiles,
			SplitCount = SplitCount,
			ChainBounces = ChainBounces,
			PierceCount = PierceCount,
			ExplosionRadius = ExplosionRadius,
		};
	}
}

public sealed class AttackModeDefinition
{
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = string.Empty;
	public string BehaviorId { get; set; } = BuildCatalog.AiAttackNearest;
}

public sealed class BuildStats
{
	public int MaxHealth { get; set; }
	public int Attack { get; set; }
	public int Defense { get; set; }
	public float MoveSpeedMultiplier { get; set; } = 1.0f;
	public float AttackCooldownMultiplier { get; set; } = 1.0f;
	public float AttackRangeBonus { get; set; }
	public float DetectionRadiusBonus { get; set; }
	public float FollowDistanceMultiplier { get; set; } = 1.0f;
	public float CritChance { get; set; }
	public float LifeStealPercent { get; set; }
	public float KnockbackForce { get; set; }
	public float ControlChance { get; set; }
	public float IncomingDamageMultiplier { get; set; } = 1.0f;
	public int EquipmentSocketCount { get; set; }
	public bool HasHealSkill { get; set; }
	public bool HasShieldSkill { get; set; }
	public string ActiveRangedSkillId { get; set; } = string.Empty;
	public string IdentityId { get; set; } = string.Empty;
	public string DamageElementId { get; set; } = "physical";
	public string DamageElementNameKey { get; set; } = "element.physical";
	public string AiBehaviorId { get; set; } = BuildCatalog.AiAttackNearest;
	public Color AttackColor { get; set; } = new(1.0f, 0.54f, 0.24f, 0.92f);
	public string[] TraitKeys { get; set; } = System.Array.Empty<string>();
	public ProjectileBehaviorProfile Behavior { get; set; } = new();
}

public sealed class CompanionBuildLoadout
{
	public string HelmetId { get; set; } = "equip.helmet.traveler";
	public string WeaponId { get; set; } = "equip.weapon.sword";
	public string ArmorId { get; set; } = "equip.armor.scout";
	public string BootsId { get; set; } = "equip.boots.traveler";
	public string AccessoryId { get; set; } = "equip.accessory.swift_ring";
	public string AttributeGemId { get; set; } = "gem.attribute.none";
	public string[] SkillGemIds { get; set; } =
	{
		"gem.skill.none",
		"gem.skill.none",
		"gem.skill.none",
	};

	// Parallel to SkillGemIds. Level scales a behavior gem's magnitude/radius.
	public int[] SkillGemLevels { get; set; } = { 1, 1, 1 };

	public int GetSkillGemLevel(int index)
	{
		EnsureSkillSlots();
		if (index < 0 || index >= SkillGemLevels.Length)
		{
			return 1;
		}

		return Mathf.Max(SkillGemLevels[index], 1);
	}

	public string GetSkillGemId(int index)
	{
		EnsureSkillSlots();
		return index >= 0 && index < SkillGemIds.Length ? SkillGemIds[index] : "gem.skill.none";
	}

	public string GetEquipmentId(EquipmentSlot slot)
	{
		return slot switch
		{
			EquipmentSlot.Helmet => HelmetId,
			EquipmentSlot.Weapon => WeaponId,
			EquipmentSlot.Armor => ArmorId,
			EquipmentSlot.Boots => BootsId,
			_ => AccessoryId,
		};
	}

	public void SetEquipmentId(EquipmentSlot slot, string id)
	{
		switch (slot)
		{
			case EquipmentSlot.Helmet:
				HelmetId = id;
				break;
			case EquipmentSlot.Weapon:
				WeaponId = id;
				break;
			case EquipmentSlot.Armor:
				ArmorId = id;
				break;
			case EquipmentSlot.Boots:
				BootsId = id;
				break;
			default:
				AccessoryId = id;
				break;
		}
	}

	public void CycleEquipment(EquipmentSlot slot)
	{
		SetEquipmentId(slot, BuildCatalog.GetNextEquipmentId(slot, GetEquipmentId(slot)));
	}

	public void CycleAttributeGem()
	{
		AttributeGemId = BuildCatalog.GetNextAttributeGemId(AttributeGemId);
	}

	public void CycleSkillGem(int index)
	{
		EnsureSkillSlots();
		int slot = Mathf.Clamp(index, 0, SkillGemIds.Length - 1);
		SkillGemIds[slot] = BuildCatalog.GetNextSkillGemId(SkillGemIds[slot]);
	}

	public bool HasSkill(string skillId)
	{
		EnsureSkillSlots();
		foreach (string equippedSkillId in SkillGemIds)
		{
			if (equippedSkillId == skillId)
			{
				return true;
			}
		}

		return false;
	}

	public void EnsureSkillSlots()
	{
		int target = BuildCatalog.SupportCoreSlotCount;
		if (SkillGemIds.Length != target)
		{
			string[] previous = SkillGemIds;
			SkillGemIds = new string[target];
			for (int index = 0; index < target; index++)
			{
				SkillGemIds[index] = index < previous.Length && !string.IsNullOrEmpty(previous[index]) ? previous[index] : "gem.skill.none";
			}
		}

		if (SkillGemLevels.Length != target)
		{
			int[] previousLevels = SkillGemLevels;
			SkillGemLevels = new int[target];
			for (int index = 0; index < target; index++)
			{
				SkillGemLevels[index] = index < previousLevels.Length ? Mathf.Max(previousLevels[index], 1) : 1;
			}
		}
	}
}

public static class BuildCatalog
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

	private static readonly List<EquipmentDefinition> Equipment = new()
	{
		new EquipmentDefinition { Id = "equip.helmet.none", NameKey = "equipment.none", SummaryKey = "gem.summary.none", Slot = EquipmentSlot.Helmet },
		new EquipmentDefinition { Id = "equip.weapon.none", NameKey = "equipment.none", SummaryKey = "gem.summary.none", Slot = EquipmentSlot.Weapon },
		new EquipmentDefinition { Id = "equip.armor.none", NameKey = "equipment.none", SummaryKey = "gem.summary.none", Slot = EquipmentSlot.Armor },
		new EquipmentDefinition { Id = "equip.boots.none", NameKey = "equipment.none", SummaryKey = "gem.summary.none", Slot = EquipmentSlot.Boots },
		new EquipmentDefinition { Id = "equip.accessory.none", NameKey = "equipment.none", SummaryKey = "gem.summary.none", Slot = EquipmentSlot.Accessory },
		new EquipmentDefinition { Id = "equip.helmet.traveler", NameKey = "equip.helmet.traveler", SummaryKey = "equip.summary.traveler_helmet", Slot = EquipmentSlot.Helmet, MaxHealthBonus = 10, DefenseBonus = 3, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.helmet.guardian", NameKey = "equip.helmet.guardian", SummaryKey = "equip.summary.guardian_helmet", Slot = EquipmentSlot.Helmet, MaxHealthBonus = 26, DefenseBonus = 8, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.helmet.mystic", NameKey = "equip.helmet.mystic", SummaryKey = "equip.summary.mystic_hood", Slot = EquipmentSlot.Helmet, MaxHealthBonus = 12, DefenseBonus = 4, AttackCooldownReduction = 0.07f, AttackRangeBonus = 0.6f, SocketCount = 2 },

		new EquipmentDefinition { Id = "equip.weapon.sword", NameKey = "equip.weapon.sword", SummaryKey = "equip.summary.sword", Slot = EquipmentSlot.Weapon, AttackBonus = 10, AttackCooldownReduction = 0.04f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.weapon.short_sword", NameKey = "equip.weapon.short_sword", SummaryKey = "equip.summary.short_sword", Slot = EquipmentSlot.Weapon, AttackBonus = 8, AttackCooldownReduction = 0.10f, CritChanceBonus = 0.03f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.weapon.dagger", NameKey = "equip.weapon.dagger", SummaryKey = "equip.summary.dagger", Slot = EquipmentSlot.Weapon, AttackBonus = 6, AttackCooldownReduction = 0.18f, CritChanceBonus = 0.10f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.weapon.longbow", NameKey = "equip.weapon.longbow", SummaryKey = "equip.summary.longbow", Slot = EquipmentSlot.Weapon, AttackBonus = 9, AttackRangeBonus = 3.2f, AttackCooldownReduction = 0.02f, CritChanceBonus = 0.04f, SocketCount = 2 },
		new EquipmentDefinition { Id = "equip.weapon.spear", NameKey = "equip.weapon.spear", SummaryKey = "equip.summary.spear", Slot = EquipmentSlot.Weapon, AttackBonus = 12, DefenseBonus = 3, AttackRangeBonus = 1.4f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.weapon.warhammer", NameKey = "equip.weapon.warhammer", SummaryKey = "equip.summary.warhammer", Slot = EquipmentSlot.Weapon, AttackBonus = 20, DefenseBonus = 4, AttackCooldownReduction = -0.12f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.weapon.scepter", NameKey = "equip.weapon.scepter", SummaryKey = "equip.summary.scepter", Slot = EquipmentSlot.Weapon, AttackBonus = 8, DefenseBonus = 5, AttackRangeBonus = 1.6f, AttackCooldownReduction = 0.05f, SocketCount = 2 },
		new EquipmentDefinition { Id = "equip.weapon.staff", NameKey = "equip.weapon.staff", SummaryKey = "equip.summary.staff", Slot = EquipmentSlot.Weapon, AttackBonus = 7, AttackRangeBonus = 2.0f, AttackCooldownReduction = 0.08f, SocketCount = 2 },
		new EquipmentDefinition { Id = "equip.weapon.great_axe", NameKey = "equip.weapon.great_axe", SummaryKey = "equip.summary.great_axe", Slot = EquipmentSlot.Weapon, AttackBonus = 18, DefenseBonus = 2, AttackCooldownReduction = -0.08f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.weapon.claws", NameKey = "equip.weapon.claws", SummaryKey = "equip.summary.claws", Slot = EquipmentSlot.Weapon, AttackBonus = 8, AttackCooldownReduction = 0.16f, CritChanceBonus = 0.06f, SocketCount = 1 },

		new EquipmentDefinition { Id = "equip.armor.scout", NameKey = "equip.armor.scout", SummaryKey = "equip.summary.scout_armor", Slot = EquipmentSlot.Armor, MaxHealthBonus = 18, DefenseBonus = 5, MoveSpeedBonus = 0.05f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.armor.plate", NameKey = "equip.armor.plate", SummaryKey = "equip.summary.plate_armor", Slot = EquipmentSlot.Armor, MaxHealthBonus = 44, DefenseBonus = 16, MoveSpeedBonus = -0.05f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.armor.spirit_robe", NameKey = "equip.armor.spirit_robe", SummaryKey = "equip.summary.spirit_robe", Slot = EquipmentSlot.Armor, MaxHealthBonus = 24, DefenseBonus = 7, AttackCooldownReduction = 0.10f, AttackRangeBonus = 0.7f, SocketCount = 2 },

		new EquipmentDefinition { Id = "equip.boots.traveler", NameKey = "equip.boots.traveler", SummaryKey = "equip.summary.traveler_shoes", Slot = EquipmentSlot.Boots, DefenseBonus = 1, MoveSpeedBonus = 0.07f },
		new EquipmentDefinition { Id = "equip.boots.reinforced", NameKey = "equip.boots.reinforced", SummaryKey = "equip.summary.reinforced_boots", Slot = EquipmentSlot.Boots, MaxHealthBonus = 10, DefenseBonus = 5, MoveSpeedBonus = 0.03f },
		new EquipmentDefinition { Id = "equip.boots.windrunner", NameKey = "equip.boots.windrunner", SummaryKey = "equip.summary.windrunner_boots", Slot = EquipmentSlot.Boots, DefenseBonus = 2, MoveSpeedBonus = 0.15f, AttackCooldownReduction = 0.03f },

		new EquipmentDefinition { Id = "equip.accessory.swift_ring", NameKey = "equip.accessory.swift_ring", SummaryKey = "equip.summary.swift_ring", Slot = EquipmentSlot.Accessory, MoveSpeedBonus = 0.12f, AttackCooldownReduction = 0.05f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.accessory.crit_charm", NameKey = "equip.accessory.crit_charm", SummaryKey = "equip.summary.crit_charm", Slot = EquipmentSlot.Accessory, AttackBonus = 4, CritChanceBonus = 0.12f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.accessory.turtle_amulet", NameKey = "equip.accessory.turtle_amulet", SummaryKey = "equip.summary.turtle_amulet", Slot = EquipmentSlot.Accessory, MaxHealthBonus = 34, DefenseBonus = 8, MoveSpeedBonus = -0.03f, SocketCount = 1 },
		new EquipmentDefinition { Id = "equip.accessory.focus_lens", NameKey = "equip.accessory.focus_lens", SummaryKey = "equip.summary.focus_lens", Slot = EquipmentSlot.Accessory, AttackRangeBonus = 1.4f, CritChanceBonus = 0.06f, SocketCount = 1 },
	};

	private static readonly List<AttributeGemDefinition> AttributeGems = new()
	{
		new AttributeGemDefinition { Id = "gem.attribute.none", NameKey = "gem.attribute.none", SummaryKey = "gem.summary.none", ElementId = "physical", ElementNameKey = "element.physical", AttackColor = new Color(1.0f, 0.54f, 0.24f, 0.92f) },
		new AttributeGemDefinition { Id = "gem.attribute.fire", NameKey = "gem.attribute.fire", SummaryKey = "gem.summary.fire", ElementId = "fire", ElementNameKey = "element.fire", AttackBonus = 5, ControlChance = 0.18f, AttackColor = new Color(1.0f, 0.28f, 0.08f, 0.94f) },
		new AttributeGemDefinition { Id = "gem.attribute.water", NameKey = "gem.attribute.water", SummaryKey = "gem.summary.water", ElementId = "water", ElementNameKey = "element.water", AttackBonus = 2, AttackRangeBonus = 0.8f, AttackColor = new Color(0.20f, 0.70f, 1.0f, 0.94f) },
		new AttributeGemDefinition { Id = "gem.attribute.lightning", NameKey = "gem.attribute.lightning", SummaryKey = "gem.summary.lightning", ElementId = "lightning", ElementNameKey = "element.lightning", AttackBonus = 3, CritChanceBonus = 0.06f, ControlChance = 0.16f, AttackColor = new Color(0.95f, 0.88f, 0.20f, 0.95f) },
		new AttributeGemDefinition { Id = "gem.attribute.ice", NameKey = "gem.attribute.ice", SummaryKey = "gem.summary.ice", ElementId = "ice", ElementNameKey = "element.ice", DefenseBonus = 3, ControlChance = 0.18f, AttackColor = new Color(0.58f, 0.88f, 1.0f, 0.95f) },
		new AttributeGemDefinition { Id = "gem.attribute.poison", NameKey = "gem.attribute.poison", SummaryKey = "gem.summary.poison", ElementId = "poison", ElementNameKey = "element.poison", AttackBonus = 2, ControlChance = 0.22f, AttackColor = new Color(0.45f, 0.95f, 0.28f, 0.94f) },
		new AttributeGemDefinition { Id = "gem.attribute.wind", NameKey = "gem.attribute.wind", SummaryKey = "gem.summary.wind", ElementId = "wind", ElementNameKey = "element.wind", MoveSpeedBonus = 0.08f, KnockbackForce = 2.8f, AttackColor = new Color(0.72f, 1.0f, 0.76f, 0.94f) },
		new AttributeGemDefinition { Id = "gem.attribute.dark", NameKey = "gem.attribute.dark", SummaryKey = "gem.summary.dark", ElementId = "dark", ElementNameKey = "element.dark", AttackBonus = 4, LifeStealPercent = 0.10f, AttackColor = new Color(0.72f, 0.34f, 1.0f, 0.94f) },
		new AttributeGemDefinition { Id = "gem.attribute.light", NameKey = "gem.attribute.light", SummaryKey = "gem.summary.light", ElementId = "light", ElementNameKey = "element.light", DefenseBonus = 2, AttackRangeBonus = 0.5f, AttackColor = new Color(1.0f, 0.95f, 0.58f, 0.94f) },
	};

	private static readonly List<SkillGemDefinition> SkillGems = new()
	{
		new SkillGemDefinition { Id = "gem.skill.none", NameKey = "gem.skill.none", SummaryKey = "gem.skill.summary.none" },
		new SkillGemDefinition { Id = "gem.skill.fireball", NameKey = "gem.skill.fireball", SummaryKey = "gem.skill.summary.fireball", AttackBonus = 5, AttackRangeBonus = 2.0f, IsRangedActiveSkill = true },
		new SkillGemDefinition { Id = "gem.skill.heal", NameKey = "gem.skill.heal", SummaryKey = "gem.skill.summary.heal", DefenseBonus = 3, EnablesHeal = true },
		new SkillGemDefinition { Id = "gem.skill.shield", NameKey = "gem.skill.shield", SummaryKey = "gem.skill.summary.shield", MaxHealthBonus = 20, DefenseBonus = 10, EnablesShield = true },
		new SkillGemDefinition { Id = "gem.skill.whirlwind", NameKey = "gem.skill.whirlwind", SummaryKey = "gem.skill.summary.whirlwind", AttackBonus = 4, DefenseBonus = 2, AttackCooldownReduction = 0.04f },
		new SkillGemDefinition { Id = "gem.skill.meteor", NameKey = "gem.skill.meteor", SummaryKey = "gem.skill.summary.meteor", AttackBonus = 12, AttackRangeBonus = 1.2f, AttackCooldownReduction = -0.08f, IsRangedActiveSkill = true },
		new SkillGemDefinition { Id = "gem.skill.dash", NameKey = "gem.skill.dash", SummaryKey = "gem.skill.summary.dash", MoveSpeedBonus = 0.18f, AttackCooldownReduction = 0.08f },
		new SkillGemDefinition { Id = "gem.skill.laser", NameKey = "gem.skill.laser", SummaryKey = "gem.skill.summary.laser", AttackBonus = 6, AttackRangeBonus = 3.2f, DetectionRadiusBonus = 2.0f, IsRangedActiveSkill = true },
		new SkillGemDefinition { Id = "gem.skill.chain", NameKey = "gem.skill.chain", SummaryKey = "gem.skill.summary.chain", AttackBonus = 3, DetectionRadiusBonus = 2.0f, BehaviorId = ProjectileBehavior.Chain, BehaviorMagnitude = 2, UpgradeMaterialId = "loot.water_core" },
		new SkillGemDefinition { Id = "gem.skill.explosion", NameKey = "gem.skill.explosion", SummaryKey = "gem.skill.summary.explosion", AttackBonus = 7, AttackCooldownReduction = -0.04f, BehaviorId = ProjectileBehavior.Explosion, BehaviorRadius = 3.0f, UpgradeMaterialId = "loot.red_horn" },
		new SkillGemDefinition { Id = "gem.skill.piercing", NameKey = "gem.skill.piercing", SummaryKey = "gem.skill.summary.piercing", AttackBonus = 2, AttackRangeBonus = 2.0f, BehaviorId = ProjectileBehavior.Pierce, BehaviorMagnitude = 2, UpgradeMaterialId = "loot.small_bone" },
		new SkillGemDefinition { Id = "gem.skill.life_steal", NameKey = "gem.skill.life_steal", SummaryKey = "gem.skill.summary.life_steal", LifeStealPercent = 0.08f, DefenseBonus = 2 },
		new SkillGemDefinition { Id = "gem.skill.split", NameKey = "gem.skill.split", SummaryKey = "gem.skill.summary.split", AttackBonus = 2, BehaviorId = ProjectileBehavior.Split, BehaviorMagnitude = 2, UpgradeMaterialId = "loot.sharp_claw" },
		new SkillGemDefinition { Id = "gem.skill.multishot", NameKey = "gem.skill.multishot", SummaryKey = "gem.skill.summary.multishot", AttackBonus = 1, AttackCooldownReduction = -0.03f, BehaviorId = ProjectileBehavior.Multi, BehaviorMagnitude = 2, UpgradeMaterialId = "loot.insect_wing" },
	};

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

	public static CompanionBuildLoadout CreateStarterLoadout(SimpleActor actor)
	{
		var loadout = new CompanionBuildLoadout();
		string identityId = GetIdentity(actor).Id;

		if (actor.CombatRole == "Support")
		{
			loadout.WeaponId = "equip.weapon.staff";
			loadout.ArmorId = "equip.armor.spirit_robe";
			loadout.AttributeGemId = "gem.attribute.light";
			loadout.SkillGemIds = new[] { "gem.skill.heal", "gem.skill.shield", "gem.skill.none" };
		}
		else if (actor.CombatRole == "Tank")
		{
			loadout.HelmetId = "equip.helmet.guardian";
			loadout.WeaponId = "equip.weapon.great_axe";
			loadout.ArmorId = "equip.armor.plate";
			loadout.AccessoryId = "equip.accessory.turtle_amulet";
			loadout.AttributeGemId = "gem.attribute.ice";
			loadout.SkillGemIds = new[] { "gem.skill.shield", "gem.skill.whirlwind", "gem.skill.none" };
		}
		else if (actor.CombatRole == "Ranged")
		{
			loadout.HelmetId = "equip.helmet.mystic";
			loadout.WeaponId = "equip.weapon.staff";
			loadout.ArmorId = "equip.armor.spirit_robe";
			loadout.AccessoryId = "equip.accessory.focus_lens";
			loadout.AttributeGemId = "gem.attribute.lightning";
			loadout.SkillGemIds = new[] { "gem.skill.laser", "gem.skill.chain", "gem.skill.none" };
		}
		else if (identityId == "identity.wolf")
		{
			loadout.WeaponId = "equip.weapon.claws";
			loadout.AccessoryId = "equip.accessory.crit_charm";
			loadout.AttributeGemId = "gem.attribute.lightning";
			loadout.SkillGemIds = new[] { "gem.skill.dash", "gem.skill.chain", "gem.skill.none" };
		}
		else if (identityId == "identity.dragon")
		{
			loadout.HelmetId = "equip.helmet.guardian";
			loadout.WeaponId = "equip.weapon.great_axe";
			loadout.ArmorId = "equip.armor.plate";
			loadout.AttributeGemId = "gem.attribute.fire";
			loadout.SkillGemIds = new[] { "gem.skill.meteor", "gem.skill.explosion", "gem.skill.none" };
		}
		else if (identityId == "identity.water_spirit")
		{
			loadout.WeaponId = "equip.weapon.staff";
			loadout.ArmorId = "equip.armor.spirit_robe";
			loadout.AttributeGemId = "gem.attribute.ice";
			loadout.SkillGemIds = new[] { "gem.skill.heal", "gem.skill.piercing", "gem.skill.none" };
		}
		else if (identityId == "identity.venom_imp")
		{
			loadout.WeaponId = "equip.weapon.claws";
			loadout.AttributeGemId = "gem.attribute.poison";
			loadout.SkillGemIds = new[] { "gem.skill.life_steal", "gem.skill.dash", "gem.skill.none" };
		}

		if (actor.ActorKind == "monster")
		{
			loadout.AttributeGemId = actor.MapId switch
			{
				"wild_marsh" => "gem.attribute.water",
				"wild_badlands" => "gem.attribute.fire",
				"wild_forest" => "gem.attribute.wind",
				"wild_snow" => "gem.attribute.ice",
				_ => loadout.AttributeGemId,
			};
		}

		return loadout;
	}

	public static BuildStats CalculateStats(SimpleActor actor, CompanionBuildLoadout loadout)
	{
		loadout.EnsureSkillSlots();
		CompanionIdentity identity = GetIdentity(actor);
		var stats = new BuildStats
		{
			IdentityId = identity.Id,
			TraitKeys = CombineTraitKeys(identity),
			MaxHealth = Mathf.Max(Mathf.RoundToInt(actor.MaxHealth * identity.MaxHealthMultiplier) + identity.MaxHealthBonus, 1),
			Attack = Mathf.Max(Mathf.RoundToInt(actor.Attack * identity.AttackMultiplier) + identity.AttackBonus, 1),
			Defense = Mathf.Max(Mathf.RoundToInt(actor.Defense * identity.DefenseMultiplier) + identity.DefenseBonus, 0),
			MoveSpeedMultiplier = identity.MoveSpeedMultiplier,
			AttackCooldownMultiplier = identity.AttackCooldownMultiplier,
			AttackRangeBonus = identity.AttackRangeBonus,
			DetectionRadiusBonus = identity.DetectionRadiusBonus,
			CritChance = identity.CritChanceBonus,
		};

		ApplyEquipment(stats, GetEquipment(loadout.HelmetId));
		ApplyEquipment(stats, GetEquipment(loadout.WeaponId));
		ApplyEquipment(stats, GetEquipment(loadout.ArmorId));
		ApplyEquipment(stats, GetEquipment(loadout.BootsId));
		ApplyEquipment(stats, GetEquipment(loadout.AccessoryId));

		// Main core (attack core) only takes effect once unlocked; before that the
		// creature attacks with the default physical core.
		if (IsMainCoreUnlocked(actor.Level))
		{
			AttributeGemDefinition attributeGem = GetAttributeGem(loadout.AttributeGemId);
			stats.DamageElementId = attributeGem.ElementId;
			stats.DamageElementNameKey = attributeGem.ElementNameKey;
			stats.AttackColor = attributeGem.AttackColor;
			stats.Attack += attributeGem.AttackBonus;
			stats.Defense += attributeGem.DefenseBonus;
			stats.MoveSpeedMultiplier += attributeGem.MoveSpeedBonus;
			stats.AttackRangeBonus += attributeGem.AttackRangeBonus;
			stats.CritChance += attributeGem.CritChanceBonus;
			stats.LifeStealPercent += attributeGem.LifeStealPercent;
			stats.ControlChance += attributeGem.ControlChance;
			stats.KnockbackForce += attributeGem.KnockbackForce;
		}

		// Support cores only contribute up to the number of unlocked support slots.
		int unlockedSupportCores = GetUnlockedSupportCoreCount(actor.Level);
		bool hasRangedActiveSkill = HasRangedActiveSkill(loadout);
		bool hasMainAttackCore = HasMainAttackCore(loadout);
		for (int slot = 0; slot < loadout.SkillGemIds.Length; slot++)
		{
			if (slot >= unlockedSupportCores)
			{
				break;
			}

			SkillGemDefinition gem = GetSkillGem(loadout.SkillGemIds[slot]);
			if ((slot == 0 && !IsMainAttackCore(gem.Id)) || (slot > 0 && !IsSupportCore(gem.Id)))
			{
				continue;
			}
			if (slot > 0 && !hasMainAttackCore)
			{
				continue;
			}
			if (IsProjectileSupportGem(gem.Id) && !hasRangedActiveSkill)
			{
				continue;
			}
			if (gem.IsRangedActiveSkill && string.IsNullOrEmpty(stats.ActiveRangedSkillId))
			{
				stats.ActiveRangedSkillId = gem.Id;
			}
			ApplySkillGem(stats, gem);
			AccumulateBehavior(stats.Behavior, gem, loadout.GetSkillGemLevel(slot));
		}

		stats.AiBehaviorId = GetAttackMode(actor.AttackModeId).BehaviorId;

		if (!string.IsNullOrEmpty(identity.ElementAffinityId) && identity.ElementAffinityId == stats.DamageElementId)
		{
			stats.Attack = Mathf.RoundToInt(stats.Attack * identity.ElementAffinityDamageMultiplier);
		}

		stats.MoveSpeedMultiplier = Mathf.Clamp(stats.MoveSpeedMultiplier, 0.55f, 2.4f);
		stats.AttackCooldownMultiplier = Mathf.Clamp(stats.AttackCooldownMultiplier, 0.42f, 1.85f);
		stats.CritChance = Mathf.Clamp(stats.CritChance, 0.0f, 0.75f);
		stats.LifeStealPercent = Mathf.Clamp(stats.LifeStealPercent, 0.0f, 0.45f);
		stats.MaxHealth = Mathf.Max(stats.MaxHealth, 1);
		stats.Attack = Mathf.Max(stats.Attack, 1);
		stats.Defense = Mathf.Max(stats.Defense, 0);
		return stats;
	}

	private static string[] CombineTraitKeys(CompanionIdentity identity)
	{
		var keys = new List<string>();
		keys.AddRange(identity.PassiveKeys);
		return keys.ToArray();
	}

	public static EquipmentDefinition GetEquipment(string id)
	{
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Id == id)
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

	public static List<string> GetStarterInventoryItemIds()
	{
		var ids = new List<string>();
		foreach (EquipmentDefinition equipment in Equipment)
		{
			ids.Add(equipment.Id);
		}

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

	public static bool IsFreeItem(string id)
	{
		return id is "gem.attribute.none" or "gem.skill.none"
			|| id.EndsWith(".none", System.StringComparison.Ordinal);
	}

	public static string GetItemNameKey(string id)
	{
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Id == id)
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

		return id;
	}

	public static InventoryItemKind GetItemKind(string id)
	{
		foreach (EquipmentDefinition equipment in Equipment)
		{
			if (equipment.Id == id)
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

		for (int index = 0; index < matching.Count; index++)
		{
			if (matching[index].Id == currentId)
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
	// The core-skill slot holds the attack core (attribute gem, decides attack element).
	// Support core slots hold support/trait cores (skill gems). Both unlock as the
	// creature grows, per the Core System design: 0 cores at low level, then main, then
	// support slots one by one.
	public const int MainCoreUnlockLevel = 3;

	// One fixed main attack core (index 0) plus six extension support cores (1..6).
	// The historical name is retained because this value is serialized as the skill
	// core array length throughout the existing save system.
	public const int SupportCoreSlotCount = 7;

	// Unlock levels for the core skill, then support cores 1 through 6.
	private static readonly int[] SupportCoreUnlockLevels = { 6, 10, 15, 21, 28, 36, 45 };

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

	public static bool IsProjectileSupportGem(string gemId) => GetSkillGem(gemId).BehaviorId != ProjectileBehavior.None;

	public static bool IsMainAttackCore(string gemId)
	{
		return IsRangedActiveSkillGem(gemId) || gemId == "gem.skill.whirlwind";
	}

	public static bool IsSupportCore(string gemId)
	{
		return gemId != "gem.skill.none" && !IsMainAttackCore(gemId);
	}

	public static bool HasMainAttackCore(CompanionBuildLoadout loadout)
	{
		return IsMainAttackCore(loadout.GetSkillGemId(0));
	}

	public static string GetSkillGemCategoryKey(string gemId)
	{
		SkillGemDefinition gem = GetSkillGem(gemId);
		if (IsProjectileSupportGem(gemId))
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

	// Cost to raise a behavior gem from its current level to the next one, or null if
	// the gem has no behavior to scale or is already at the maximum level.
	public static SkillGemUpgradeCost? GetSkillGemUpgradeCost(string gemId, int currentLevel)
	{
		SkillGemDefinition gem = GetSkillGem(gemId);
		if (gem.BehaviorId == ProjectileBehavior.None)
		{
			return null;
		}

		int level = Mathf.Clamp(currentLevel, 1, MaxSkillGemLevel);
		if (level >= MaxSkillGemLevel)
		{
			return null;
		}

		int nextLevel = level + 1;
		string materialId = string.IsNullOrEmpty(gem.UpgradeMaterialId) ? "loot.cracked_core" : gem.UpgradeMaterialId;
		return new SkillGemUpgradeCost(nextLevel, 90 * nextLevel * nextLevel, materialId, level);
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
		return AiCommandPriority;
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

	private static void ApplyEquipment(BuildStats stats, EquipmentDefinition equipment)
	{
		stats.MaxHealth += equipment.MaxHealthBonus;
		stats.Attack += equipment.AttackBonus;
		stats.Defense += equipment.DefenseBonus;
		stats.MoveSpeedMultiplier += equipment.MoveSpeedBonus;
		stats.AttackCooldownMultiplier -= equipment.AttackCooldownReduction;
		stats.AttackRangeBonus += equipment.AttackRangeBonus;
		stats.CritChance += equipment.CritChanceBonus;
		stats.EquipmentSocketCount += equipment.SocketCount;
	}

	private static void ApplySkillGem(BuildStats stats, SkillGemDefinition gem)
	{
		stats.MaxHealth += gem.MaxHealthBonus;
		stats.Attack += gem.AttackBonus;
		stats.Defense += gem.DefenseBonus;
		stats.MoveSpeedMultiplier += gem.MoveSpeedBonus;
		stats.AttackCooldownMultiplier -= gem.AttackCooldownReduction;
		stats.AttackRangeBonus += gem.AttackRangeBonus;
		stats.DetectionRadiusBonus += gem.DetectionRadiusBonus;
		stats.CritChance += gem.CritChanceBonus;
		stats.LifeStealPercent += gem.LifeStealPercent;
		stats.HasHealSkill |= gem.EnablesHeal;
		stats.HasShieldSkill |= gem.EnablesShield;
	}

	private static void AccumulateBehavior(ProjectileBehaviorProfile profile, SkillGemDefinition gem, int level)
	{
		if (gem.BehaviorId == ProjectileBehavior.None)
		{
			return;
		}

		int levelBonus = Mathf.Max(level, 1) - 1;
		int magnitude = Mathf.Max(gem.BehaviorMagnitude + levelBonus, 0);
		switch (gem.BehaviorId)
		{
			case ProjectileBehavior.Multi:
				profile.ExtraProjectiles += magnitude;
				break;
			case ProjectileBehavior.Split:
				profile.SplitCount += magnitude;
				break;
			case ProjectileBehavior.Chain:
				profile.ChainBounces += magnitude;
				break;
			case ProjectileBehavior.Pierce:
				profile.PierceCount += magnitude;
				break;
			case ProjectileBehavior.Explosion:
				profile.ExplosionRadius += gem.BehaviorRadius + levelBonus * 0.6f;
				break;
		}
	}

}
