using Godot;

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
	Consumable,
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
	public int UniqueId { get; set; }
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = string.Empty;
	public string SummaryKey { get; set; } = string.Empty;
	public EquipmentSlot Slot { get; set; }
	public int MaxHealthBonus { get; set; }
	public int AttackBonus { get; set; }
	public int DefenseBonus { get; set; }
	public float MoveSpeedBonus { get; set; }
	public int JumpPowerBonus { get; set; }
	public int AttackSpeed { get; set; }
	public float AttackCooldownReduction { get; set; }
	public float AttackRangeBonus { get; set; }
	public float CritChanceBonus { get; set; }
	// Elemental gear: grants ElementDamageBonus (+fraction of Attack) ONLY when the
	// wearer's actual attack element (set by the skill core) matches this element.
	// Empty = non-elemental. Attack elements in play: fire / ice / lightning / light.
	public string DamageElementId { get; set; } = string.Empty;
	public float ElementDamageBonus { get; set; }
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
	public int UniqueId { get; set; }
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = string.Empty;
	public string SummaryKey { get; set; } = string.Empty;
	public string DamageElementId { get; set; } = string.Empty;
	public string DamageElementNameKey { get; set; } = string.Empty;
	public Color AttackColor { get; set; } = new(1.0f, 0.54f, 0.24f, 0.92f);
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
	public bool IsSpell { get; set; }
	public bool UsesProjectile { get; set; }
	public bool IsSupportEffect { get; set; }
	public bool RequiresProjectile { get; set; }
	public float DamageMultiplier { get; set; } = 1.0f;
	public float ProjectileSpeedMultiplier { get; set; } = 1.0f;
	public float ControlChanceBonus { get; set; }

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
	public float AttackDisplayValue { get; set; }
	public float DefenseDisplayValue { get; set; }
	public float MoveSpeedMultiplier { get; set; } = 1.0f;
	public int JumpPower { get; set; } = EquipmentConfig.BaseJumpPower;
	public float AttackCooldownMultiplier { get; set; } = 1.0f;
	public float AttackRangeBonus { get; set; }
	public float DetectionRadiusBonus { get; set; }
	public float FollowDistanceMultiplier { get; set; } = 1.0f;
	public float CritChance { get; set; }
	// Multiplier applied to a critical hit's damage (1.5 = base +50%). Gear/sets add
	// to this via CritDamageBonus.
	public float CritDamageMultiplier { get; set; } = 1.5f;
	public float LifeStealPercent { get; set; }
	public float KnockbackForce { get; set; }
	public float ControlChance { get; set; }
	public float DamageMultiplier { get; set; } = 1.0f;
	public float SpellDamageMultiplier { get; set; } = 1.0f;
	public float ProjectileSpeedMultiplier { get; set; } = 1.0f;
	public float IncomingDamageMultiplier { get; set; } = 1.0f;
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

	// Four accessory (ring) slots. AccessoryId is a back-compat view of slot 0.
	public string[] AccessoryIds { get; set; } =
	{
		"equip.accessory.swift_ring",
		"equip.accessory.none",
		"equip.accessory.none",
		"equip.accessory.none",
	};

	public string AccessoryId
	{
		get => GetAccessoryId(0);
		set => SetAccessoryId(0, value);
	}

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

	public string GetAccessoryId(int index)
	{
		EnsureAccessorySlots();
		return index >= 0 && index < AccessoryIds.Length ? AccessoryIds[index] : "equip.accessory.none";
	}

	public void SetAccessoryId(int index, string id)
	{
		EnsureAccessorySlots();
		if (index >= 0 && index < AccessoryIds.Length)
		{
			AccessoryIds[index] = string.IsNullOrEmpty(id) ? "equip.accessory.none" : id;
		}
	}

	public void EnsureAccessorySlots()
	{
		int target = BuildCatalog.AccessorySlotCount;
		if (AccessoryIds != null && AccessoryIds.Length == target)
		{
			return;
		}

		string[] previous = AccessoryIds ?? System.Array.Empty<string>();
		AccessoryIds = new string[target];
		for (int index = 0; index < target; index++)
		{
			AccessoryIds[index] = index < previous.Length && !string.IsNullOrEmpty(previous[index]) ? previous[index] : "equip.accessory.none";
		}
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
