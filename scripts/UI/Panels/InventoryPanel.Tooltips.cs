using Godot;
using System.Collections.Generic;

public partial class InventoryPanel : PanelContainer
{
	public static string BuildItemTooltipBody(string itemId, string slotName)
	{
		var lines = new List<string>();
		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			lines.Add(LocaleText.T("tooltip.type.material"));
		}
		else if (BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment)
		{
			EquipmentDefinition equipment = BuildCatalog.GetEquipment(itemId);
			lines.Add(LocaleText.F("tooltip.equipment_slot", LocaleText.T(GetEquipmentSlotKey(equipment.Slot))));
			int equipmentStars = BuildCatalog.GetEquipmentStars(itemId);
			if (equipmentStars > 0)
			{
				lines.Add(LocaleText.F("tooltip.refine_stars", equipmentStars, Mathf.RoundToInt(BuildCatalog.EquipmentStarBonusPerStar * equipmentStars * 100.0f)));
			}
		}
		else if (BuildCatalog.GetItemKind(itemId) == InventoryItemKind.SkillGem)
		{
			lines.Add(itemId == "gem.skill.none"
				? slotName
				: LocaleText.F(
					"tooltip.core_role",
					LocaleText.T(BuildCatalog.IsMainAttackCore(itemId)
						? "tooltip.core_role.attack"
						: "tooltip.core_role.support")));
			int coreStars = BuildCatalog.GetSkillCoreStars(itemId);
			if (coreStars > 0)
			{
				lines.Add(LocaleText.F(
					"tooltip.core_stars",
					coreStars,
					Mathf.RoundToInt((BuildCatalog.GetSkillCoreStarMultiplier(itemId) - 1.0f) * 100.0f)));
			}
		}
		else if (BuildCatalog.GetItemKind(itemId) == InventoryItemKind.AttributeGem)
		{
			lines.Add(LocaleText.F("tooltip.core_role", LocaleText.T("tooltip.core_role.support")));
		}
		else
		{
			lines.Add(LocaleText.T(GetItemKindKey(itemId)));
		}

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			lines.Add(LocaleText.T("inventory.material_hint"));
			return FormatTooltipLines(lines);
		}

		switch (BuildCatalog.GetItemKind(itemId))
		{
			case InventoryItemKind.Equipment:
				AppendEquipmentTooltip(lines, BuildCatalog.GetEquipment(itemId), BuildCatalog.GetEquipmentStarMultiplier(itemId));
				break;
			case InventoryItemKind.AttributeGem:
				AppendAttributeGemTooltip(lines, BuildCatalog.GetAttributeGem(itemId));
				break;
			case InventoryItemKind.SkillGem:
				AppendSkillGemTooltip(lines, BuildCatalog.GetSkillGem(itemId), BuildCatalog.GetSkillCoreStarMultiplier(itemId));
				break;
		}

		return FormatTooltipLines(lines);
	}

	public static string BuildItemTooltipTitle(string itemId)
	{
		string name = GetInventoryItemName(itemId);
		int uniqueId = BuildCatalog.GetItemUniqueId(itemId);
		return uniqueId > 0 ? $"[#{uniqueId}] {name}" : name;
	}

	private static string FormatTooltipLines(List<string> lines)
	{
		if (lines.Count <= 3)
		{
			return string.Join("\n", lines);
		}

		var compactLines = new List<string>
		{
			lines[0],
			lines[1],
		};
		for (int index = 2; index < lines.Count; index += 3)
		{
			int count = Mathf.Min(3, lines.Count - index);
			compactLines.Add(string.Join("  /  ", lines.GetRange(index, count)));
		}

		return string.Join("\n", compactLines);
	}

	private static string GetItemKindKey(string itemId)
	{
		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return "tooltip.type.material";
		}

		return BuildCatalog.GetItemKind(itemId) switch
		{
			InventoryItemKind.Equipment => "tooltip.type.equipment",
			InventoryItemKind.AttributeGem => "tooltip.type.attribute",
			InventoryItemKind.SkillGem => "tooltip.type.skill",
			_ => "tooltip.type.skill",
		};
	}

	private static string GetEquipmentSlotKey(EquipmentSlot slot)
	{
		return slot switch
		{
			EquipmentSlot.Helmet => "build.slot.helmet",
			EquipmentSlot.Weapon => "build.slot.weapon",
			EquipmentSlot.Armor => "build.slot.armor",
			EquipmentSlot.Boots => "build.slot.boots",
			EquipmentSlot.Accessory => "build.slot.accessory",
			_ => "tooltip.type.equipment",
		};
	}

	// starMultiplier 反映精煉星等（每星 +8%）；插槽數不受影響。
	private static void AppendEquipmentTooltip(List<string> lines, EquipmentDefinition item, float starMultiplier = 1.0f)
	{
		AddSummaryLine(lines, item.SummaryKey);
		EquipmentSetJson? set = BuildCatalog.GetSetForEquipment(item.Id);
		if (set != null)
		{
			lines.Add(LocaleText.F("tooltip.set_member", LocaleText.T(set.NameKey)));
		}
		AddStatLine(lines, "stat.health", Mathf.RoundToInt(item.MaxHealthBonus * starMultiplier));
		AddStatLine(lines, "stat.attack", Mathf.RoundToInt(item.AttackBonus * starMultiplier));
		AddStatLine(lines, "stat.defense", Mathf.RoundToInt(item.DefenseBonus * starMultiplier));
		AddPercentLine(lines, "tooltip.move_speed", item.MoveSpeedBonus * starMultiplier);
		float jumpMultiplier = EquipmentConfig.EquipmentStarsAffectJumpPower ? starMultiplier : 1.0f;
		AddStatLine(lines, "stat.jump_power", Mathf.RoundToInt(item.JumpPowerBonus * jumpMultiplier));
		if (item.Slot == EquipmentSlot.Weapon && item.Id != "equip.weapon.none")
		{
			int attackSpeed = BuildCatalog.GetWeaponAttackSpeed(item, starMultiplier);
			lines.Add(LocaleText.F("tooltip.stat_line", LocaleText.T("stat.attack_speed"), attackSpeed));
		}
		else
		{
			AddPercentLine(lines, "stat.attack_speed", item.AttackCooldownReduction * starMultiplier);
		}
		AddDecimalLine(lines, "tooltip.attack_range", item.AttackRangeBonus * starMultiplier);
		AddPercentLine(lines, "tooltip.crit_chance", item.CritChanceBonus * starMultiplier);
	}

	private static void AppendAttributeGemTooltip(List<string> lines, AttributeGemDefinition item)
	{
		AddSummaryLine(lines, item.SummaryKey);
		lines.Add(LocaleText.F("tooltip.element", LocaleText.T(item.ElementNameKey)));
		AddStatLine(lines, "stat.attack", item.AttackBonus);
		AddStatLine(lines, "stat.defense", item.DefenseBonus);
		AddPercentLine(lines, "tooltip.move_speed", item.MoveSpeedBonus);
		AddDecimalLine(lines, "tooltip.attack_range", item.AttackRangeBonus);
		AddPercentLine(lines, "tooltip.crit_chance", item.CritChanceBonus);
		AddPercentLine(lines, "tooltip.life_steal", item.LifeStealPercent);
		AddPercentLine(lines, "tooltip.control_chance", item.ControlChance);
		AddDecimalLine(lines, "tooltip.knockback", item.KnockbackForce);
	}

	// starMultiplier 反映核心強化星等（每星 +CoreStarBonusPerStar）；與 ApplySkillGem 的
	// bonusFactor 一致，只縮放實際會被星等放大的數值（跟隨距離與投射速度不受影響）。
	private static void AppendSkillGemTooltip(List<string> lines, SkillGemDefinition item, float starMultiplier = 1.0f)
	{
		AddSummaryLine(lines, item.SummaryKey);
		// Every core is tagged with an element; cores without a damage element
		// (support cores, whirlwind, ...) read as 無屬性 via element.physical.
		string elementKey = string.IsNullOrEmpty(item.DamageElementNameKey) ? "element.physical" : item.DamageElementNameKey;
		lines.Add(LocaleText.F("tooltip.element", LocaleText.T(elementKey)));
		if (BuildCatalog.IsProjectileSupportGem(item.Id))
		{
			lines.Add(LocaleText.T("tooltip.requires_ranged_skill"));
		}
		AddStatLine(lines, "stat.health", Mathf.RoundToInt(item.MaxHealthBonus * starMultiplier));
		AddStatLine(lines, "stat.attack", Mathf.RoundToInt(item.AttackBonus * starMultiplier));
		AddStatLine(lines, "stat.defense", Mathf.RoundToInt(item.DefenseBonus * starMultiplier));
		AddPercentLine(lines, "tooltip.move_speed", item.MoveSpeedBonus * starMultiplier);
		AddPercentLine(lines, "stat.attack_speed", item.AttackCooldownReduction * starMultiplier);
		AddDecimalLine(lines, "tooltip.attack_range", item.AttackRangeBonus * starMultiplier);
		AddDecimalLine(lines, "tooltip.detection_radius", item.DetectionRadiusBonus * starMultiplier);
		AddPercentLine(lines, "tooltip.follow_distance", item.FollowDistanceMultiplier - 1.0f);
		AddPercentLine(lines, "tooltip.crit_chance", item.CritChanceBonus * starMultiplier);
		AddPercentLine(lines, "tooltip.life_steal", item.LifeStealPercent * starMultiplier);
		AddPercentLine(lines, "tooltip.damage_multiplier", (item.DamageMultiplier - 1.0f) * starMultiplier);
		AddPercentLine(lines, "tooltip.projectile_speed", item.ProjectileSpeedMultiplier - 1.0f);
		AddPercentLine(lines, "tooltip.control_chance", item.ControlChanceBonus * starMultiplier);
	}

	private static void AddSummaryLine(List<string> lines, string summaryKey)
	{
		if (!string.IsNullOrEmpty(summaryKey))
		{
			lines.Add(LocaleText.T(summaryKey));
		}
	}

	private static void AddStatLine(List<string> lines, string labelKey, int value)
	{
		if (value != 0)
		{
			lines.Add(LocaleText.F("tooltip.stat_line", LocaleText.T(labelKey), Signed(value)));
		}
	}

	private static void AddDecimalLine(List<string> lines, string labelKey, float value)
	{
		if (Mathf.Abs(value) > 0.001f)
		{
			lines.Add(LocaleText.F("tooltip.stat_line", LocaleText.T(labelKey), Signed(value, "0.0")));
		}
	}

	private static void AddPercentLine(List<string> lines, string labelKey, float value)
	{
		if (Mathf.Abs(value) > 0.001f)
		{
			lines.Add(LocaleText.F("tooltip.stat_line", LocaleText.T(labelKey), Signed(Mathf.RoundToInt(value * 100.0f)) + "%"));
		}
	}

	private static string Signed(int value)
	{
		return value > 0 ? $"+{value}" : value.ToString();
	}

	private static string Signed(float value, string format)
	{
		return value > 0.0f ? $"+{value.ToString(format)}" : value.ToString(format);
	}
}
