using Godot;
using System.Collections.Generic;

public static partial class BuildCatalog
{
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

		ApplyEquipment(stats, GetEquipment(loadout.HelmetId), GetEquipmentStarMultiplier(loadout.HelmetId));
		ApplyEquipment(stats, GetEquipment(loadout.WeaponId), GetEquipmentStarMultiplier(loadout.WeaponId));
		ApplyEquipment(stats, GetEquipment(loadout.ArmorId), GetEquipmentStarMultiplier(loadout.ArmorId));
		ApplyEquipment(stats, GetEquipment(loadout.BootsId), GetEquipmentStarMultiplier(loadout.BootsId));
		ApplyAccessories(stats, loadout);
		ApplyEquipmentSetBonus(stats, loadout);

		// The active core owns its damage type; support cores only alter compatible
		// behavior and stats up to the number of unlocked support slots.
		int unlockedSupportCores = GetUnlockedSupportCoreCount(actor.Level);
		bool hasProjectileActiveSkill = HasProjectileActiveSkill(loadout);
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
			if (IsProjectileSupportGem(gem.Id) && !hasProjectileActiveSkill)
			{
				continue;
			}
			if (gem.IsRangedActiveSkill && string.IsNullOrEmpty(stats.ActiveRangedSkillId))
			{
				stats.ActiveRangedSkillId = gem.Id;
			}
			ApplySkillGem(stats, gem, GetSkillCoreStarMultiplier(loadout.SkillGemIds[slot]));
			AccumulateBehavior(stats.Behavior, gem, loadout.GetSkillGemLevel(slot));
		}

		stats.AiBehaviorId = GetAttackMode(actor.AttackModeId).BehaviorId;

		if (!string.IsNullOrEmpty(identity.ElementAffinityId) && identity.ElementAffinityId == stats.DamageElementId)
		{
			stats.Attack = Mathf.RoundToInt(stats.Attack * identity.ElementAffinityDamageMultiplier);
		}

		ApplyEquipmentElementDamage(stats, loadout);
		stats.Attack = Mathf.Max(Mathf.RoundToInt(stats.Attack * stats.DamageMultiplier), 1);
		stats.MoveSpeedMultiplier = Mathf.Clamp(stats.MoveSpeedMultiplier, 0.55f, 2.4f);
		stats.AttackCooldownMultiplier = Mathf.Clamp(stats.AttackCooldownMultiplier, 0.42f, 1.85f);
		stats.CritChance = Mathf.Clamp(stats.CritChance, 0.0f, 1.0f);
		stats.LifeStealPercent = Mathf.Clamp(stats.LifeStealPercent, 0.0f, 0.45f);
		stats.ControlChance = Mathf.Clamp(stats.ControlChance, 0.0f, 0.75f);
		stats.ProjectileSpeedMultiplier = Mathf.Clamp(stats.ProjectileSpeedMultiplier, 0.65f, 2.5f);
		stats.MaxHealth = Mathf.Max(stats.MaxHealth, 1);
		stats.Attack = Mathf.Max(stats.Attack, 1);
		stats.Defense = Mathf.Max(stats.Defense, 0);
		return stats;
	}

	public static BuildStats CalculatePlayerStats(PlayerController player, CompanionBuildLoadout loadout)
	{
		loadout.EnsureSkillSlots();
		var stats = new BuildStats
		{
			MaxHealth = Mathf.Max(player.MaxHealth + player.HealthAttributePoints * PlayerController.HealthPerPoint, 1),
			Attack = Mathf.Max(player.Attack, 1),
			Defense = Mathf.Max(player.Defense, 0),
			CritChance = player.CritChance + player.CritChanceAttributePoints * PlayerController.CritChancePercentPerPoint / 100.0f,
		};

		ApplyEquipment(stats, GetEquipment(loadout.HelmetId), GetEquipmentStarMultiplier(loadout.HelmetId));
		ApplyEquipment(stats, GetEquipment(loadout.WeaponId), GetEquipmentStarMultiplier(loadout.WeaponId));
		ApplyEquipment(stats, GetEquipment(loadout.ArmorId), GetEquipmentStarMultiplier(loadout.ArmorId));
		ApplyEquipment(stats, GetEquipment(loadout.BootsId), GetEquipmentStarMultiplier(loadout.BootsId));
		ApplyAccessories(stats, loadout);
		ApplyEquipmentSetBonus(stats, loadout);

		int unlocked = GetUnlockedSupportCoreCount(player.Level);
		bool hasProjectile = HasProjectileActiveSkill(loadout);
		bool hasMain = HasMainAttackCore(loadout);
		for (int slot = 0; slot < loadout.SkillGemIds.Length && slot < unlocked; slot++)
		{
			SkillGemDefinition gem = GetSkillGem(loadout.SkillGemIds[slot]);
			if ((slot == 0 && !IsMainAttackCore(gem.Id)) || (slot > 0 && !IsSupportCore(gem.Id))
				|| (slot > 0 && !hasMain) || (IsProjectileSupportGem(gem.Id) && !hasProjectile))
			{
				continue;
			}
			if (gem.IsRangedActiveSkill && string.IsNullOrEmpty(stats.ActiveRangedSkillId))
			{
				stats.ActiveRangedSkillId = gem.Id;
			}
			ApplySkillGem(stats, gem, GetSkillCoreStarMultiplier(loadout.SkillGemIds[slot]));
			AccumulateBehavior(stats.Behavior, gem, loadout.GetSkillGemLevel(slot));
		}

		ApplyEquipmentElementDamage(stats, loadout);
		stats.Attack = Mathf.Max(Mathf.RoundToInt(stats.Attack * stats.DamageMultiplier), 1);
		stats.AttackDisplayValue = stats.Attack + player.AttackAttributePoints * PlayerController.AttackPerPoint;
		stats.DefenseDisplayValue = stats.Defense + player.DefenseAttributePoints * PlayerController.DefensePerPoint;
		stats.Attack = Mathf.Max(Mathf.RoundToInt(stats.AttackDisplayValue), 1);
		stats.Defense = Mathf.Max(Mathf.RoundToInt(stats.DefenseDisplayValue), 0);
		float equippedMoveSpeed = player.WalkSpeed * stats.MoveSpeedMultiplier;
		stats.MoveSpeedMultiplier = (equippedMoveSpeed + player.MoveSpeedAttributePoints * PlayerController.MoveSpeedPerPoint)
			/ Mathf.Max(player.WalkSpeed, 0.01f);
		float equippedAttackSpeed = 1.0f / Mathf.Max(player.AttackCooldown * stats.AttackCooldownMultiplier, 0.01f);
		float allocatedAttackSpeed = equippedAttackSpeed + player.AttackSpeedAttributePoints * PlayerController.AttackSpeedPerPoint;
		stats.AttackCooldownMultiplier = 1.0f / Mathf.Max(player.AttackCooldown * allocatedAttackSpeed, 0.01f);
		stats.MoveSpeedMultiplier = Mathf.Clamp(stats.MoveSpeedMultiplier, 0.55f, 2.4f);
		stats.AttackCooldownMultiplier = Mathf.Clamp(stats.AttackCooldownMultiplier, 0.08f, 1.85f);
		stats.CritChance = Mathf.Clamp(stats.CritChance, 0.0f, 1.0f);
		stats.LifeStealPercent = Mathf.Clamp(stats.LifeStealPercent, 0.0f, 0.45f);
		stats.ControlChance = Mathf.Clamp(stats.ControlChance, 0.0f, 0.75f);
		stats.ProjectileSpeedMultiplier = Mathf.Clamp(stats.ProjectileSpeedMultiplier, 0.65f, 2.5f);
		return stats;
	}

	private static string[] CombineTraitKeys(CompanionIdentity identity)
	{
		var keys = new List<string>();
		keys.AddRange(identity.PassiveKeys);
		return keys.ToArray();
	}

	public static int CalculateEquippedJumpPower(CompanionBuildLoadout loadout)
	{
		EquipmentDefinition boots = GetEquipment(loadout.BootsId);
		float starMultiplier = EquipmentConfig.EquipmentStarsAffectJumpPower
			? GetEquipmentStarMultiplier(loadout.BootsId)
			: 1.0f;
		int bootsBonus = boots.Slot == EquipmentSlot.Boots
			? Mathf.RoundToInt(boots.JumpPowerBonus * starMultiplier)
			: 0;
		return Mathf.Clamp(
			EquipmentConfig.BaseJumpPower + bootsBonus,
			EquipmentConfig.BaseJumpPower,
			EquipmentConfig.MaximumPlayerJumpPower);
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

	// bonusMultiplier 由精煉星等提供（每星 +8%）；插槽數不受星等影響。
	public static int GetWeaponAttackSpeed(EquipmentDefinition equipment, float bonusMultiplier = 1.0f)
	{
		if (equipment.Slot != EquipmentSlot.Weapon || equipment.Id == "equip.weapon.none")
		{
			return EquipmentConfig.NeutralWeaponAttackSpeed;
		}

		float speed = EquipmentConfig.NeutralWeaponAttackSpeed
			+ (equipment.AttackSpeed - EquipmentConfig.NeutralWeaponAttackSpeed) * bonusMultiplier;
		return Mathf.Clamp(
			Mathf.RoundToInt(speed),
			EquipmentConfig.MinimumWeaponAttackSpeed,
			EquipmentConfig.MaximumWeaponAttackSpeed);
	}

	// Full-set (套裝) bonus: flat stats added when all five worn pieces share a set
	// theme. Data lives in configs/items/equipment_sets.json. Added like equipment
	// (before the final damage multiplier), so the set's Attack scales consistently.
	private static void ApplyEquipmentSetBonus(BuildStats stats, CompanionBuildLoadout loadout)
	{
		EquipmentSetJson? set = GetActiveEquipmentSet(loadout);
		if (set == null)
		{
			return;
		}

		EquipmentSetBonusJson b = set.Bonus;
		stats.MaxHealth += b.MaxHealthBonus;
		stats.Attack += b.AttackBonus;
		stats.Defense += b.DefenseBonus;
		stats.MoveSpeedMultiplier += b.MoveSpeedBonus;
		stats.SprintSpeedMultiplier += b.SprintSpeedBonus;
		stats.MaxStamina += b.MaxStaminaBonus;
		stats.JumpPower += b.JumpPowerBonus;
		stats.CritChance += b.CritChanceBonus;
		stats.CritDamageMultiplier += b.CritDamageBonus;
		stats.AttackRangeBonus += b.AttackRangeBonus;
	}

	// Elemental gear pays off only when its element matches the wielder's actual
	// attack element (set by the skill core) — e.g. a lightning ring does nothing
	// on an ice-shard build. Sums each matching piece's bonus (scaled by its refine
	// stars) and boosts Attack. Call AFTER cores have set stats.DamageElementId.
	private static void ApplyEquipmentElementDamage(BuildStats stats, CompanionBuildLoadout loadout)
	{
		if (string.IsNullOrEmpty(stats.DamageElementId))
		{
			return;
		}

		loadout.EnsureAccessorySlots();
		var worn = new System.Collections.Generic.List<string> { loadout.HelmetId, loadout.WeaponId, loadout.ArmorId, loadout.BootsId };
		worn.AddRange(loadout.AccessoryIds);
		float bonus = 0.0f;
		foreach (string id in worn)
		{
			EquipmentDefinition equipment = GetEquipment(id);
			if (!string.IsNullOrEmpty(equipment.DamageElementId) && equipment.DamageElementId == stats.DamageElementId)
			{
				bonus += equipment.ElementDamageBonus * GetEquipmentStarMultiplier(id);
			}
		}

		if (bonus > 0.0f)
		{
			stats.Attack = Mathf.Max(Mathf.RoundToInt(stats.Attack * (1.0f + bonus)), 1);
		}
	}

	private static void ApplyAccessories(BuildStats stats, CompanionBuildLoadout loadout)
	{
		loadout.EnsureAccessorySlots();
		foreach (string id in loadout.AccessoryIds)
		{
			ApplyEquipment(stats, GetEquipment(id), GetEquipmentStarMultiplier(id));
		}
	}

	private static void ApplyEquipment(BuildStats stats, EquipmentDefinition equipment, float bonusMultiplier = 1.0f)
	{
		stats.MaxHealth += Mathf.RoundToInt(equipment.MaxHealthBonus * bonusMultiplier);
		stats.Attack += Mathf.RoundToInt(equipment.AttackBonus * bonusMultiplier);
		stats.Defense += Mathf.RoundToInt(equipment.DefenseBonus * bonusMultiplier);
		stats.MoveSpeedMultiplier += equipment.MoveSpeedBonus * bonusMultiplier;
		float jumpBonusMultiplier = EquipmentConfig.EquipmentStarsAffectJumpPower ? bonusMultiplier : 1.0f;
		stats.JumpPower += Mathf.RoundToInt(equipment.JumpPowerBonus * jumpBonusMultiplier);
		if (equipment.Slot == EquipmentSlot.Weapon && equipment.Id != "equip.weapon.none")
		{
			int attackSpeed = GetWeaponAttackSpeed(equipment, bonusMultiplier);
			float speedDifference = attackSpeed - EquipmentConfig.NeutralWeaponAttackSpeed;
			stats.AttackCooldownMultiplier -= speedDifference
				* EquipmentConfig.WeaponAttackSpeedToCooldownReduction;
		}
		stats.AttackCooldownMultiplier -= equipment.AttackCooldownReduction * bonusMultiplier;
		stats.AttackRangeBonus += equipment.AttackRangeBonus * bonusMultiplier;
		stats.CritChance += equipment.CritChanceBonus * bonusMultiplier;
	}

	// bonusFactor scales the core's contribution by its star enhancement
	// (1.0 = unstarred, so behavior is unchanged for a 0-star core).
	private static void ApplySkillGem(BuildStats stats, SkillGemDefinition gem, float bonusFactor = 1.0f)
	{
		if (!string.IsNullOrEmpty(gem.DamageElementId))
		{
			stats.DamageElementId = gem.DamageElementId;
			stats.DamageElementNameKey = gem.DamageElementNameKey;
			stats.AttackColor = gem.AttackColor;
		}

		stats.MaxHealth += Mathf.RoundToInt(gem.MaxHealthBonus * bonusFactor);
		stats.Attack += Mathf.RoundToInt(gem.AttackBonus * bonusFactor);
		stats.Defense += Mathf.RoundToInt(gem.DefenseBonus * bonusFactor);
		stats.MoveSpeedMultiplier += gem.MoveSpeedBonus * bonusFactor;
		stats.AttackCooldownMultiplier -= gem.AttackCooldownReduction * bonusFactor;
		stats.AttackRangeBonus += gem.AttackRangeBonus * bonusFactor;
		stats.DetectionRadiusBonus += gem.DetectionRadiusBonus * bonusFactor;
		stats.CritChance += gem.CritChanceBonus * bonusFactor;
		stats.LifeStealPercent += gem.LifeStealPercent * bonusFactor;
		stats.ControlChance += gem.ControlChanceBonus * bonusFactor;
		stats.DamageMultiplier *= 1.0f + (gem.DamageMultiplier - 1.0f) * bonusFactor;
		stats.ProjectileSpeedMultiplier *= gem.ProjectileSpeedMultiplier;
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
