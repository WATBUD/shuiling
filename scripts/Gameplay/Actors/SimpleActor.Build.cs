using Godot;
using System.Collections.Generic;

public partial class SimpleActor : CharacterBody3D
{
	public void CycleBuildEquipment(EquipmentSlot slot)
	{
		BuildLoadout.CycleEquipment(slot);
		MarkBuildChanged();
	}

	public void EquipBuildEquipment(EquipmentSlot slot, string equipmentId)
	{
		if (BuildCatalog.GetEquipment(equipmentId).Slot != slot)
		{
			return;
		}

		BuildLoadout.SetEquipmentId(slot, equipmentId);
		MarkBuildChanged();
	}

	public void ClearBuildLoadout()
	{
		_buildLoadout = new CompanionBuildLoadout
		{
			HelmetId = "equip.helmet.none",
			WeaponId = "equip.weapon.none",
			ArmorId = "equip.armor.none",
			BootsId = "equip.boots.none",
			AccessoryId = "equip.accessory.none",
			AttributeGemId = "gem.attribute.none",
			SkillGemIds = new[] { "gem.skill.none", "gem.skill.none", "gem.skill.none" },
		};
		_buildConfigured = true;
		MarkBuildChanged();
	}

	public void CycleAttributeGem()
	{
		BuildLoadout.CycleAttributeGem();
		MarkBuildChanged();
	}

	public void EquipAttributeGem(string gemId)
	{
		BuildLoadout.AttributeGemId = BuildCatalog.GetAttributeGem(gemId).Id;
		MarkBuildChanged();
	}

	public void CycleSkillGem(int slotIndex)
	{
		BuildLoadout.CycleSkillGem(slotIndex);
		MarkBuildChanged();
	}

	public void EquipSkillGem(int slotIndex, string gemId)
	{
		int safeSlot = Mathf.Clamp(slotIndex, 0, BuildLoadout.SkillGemIds.Length - 1);
		string validatedGemId = BuildCatalog.GetSkillGem(gemId).Id;
		if (validatedGemId == "gem.skill.none")
		{
			ClearSkillGemSlot(safeSlot);
			return;
		}
		if ((safeSlot == 0 && !BuildCatalog.IsMainAttackCore(validatedGemId))
			|| (safeSlot > 0 && !BuildCatalog.IsSupportCore(validatedGemId)))
		{
			return;
		}
		if (safeSlot > 0 && !BuildCatalog.HasMainAttackCore(BuildLoadout))
		{
			return;
		}
		if (BuildCatalog.IsProjectileSupportGem(validatedGemId) && !BuildCatalog.HasProjectileActiveSkill(BuildLoadout))
		{
			return;
		}

		BuildLoadout.SkillGemIds[safeSlot] = validatedGemId;
		BuildLoadout.SkillGemLevels[safeSlot] = 1;
		if (!BuildCatalog.HasProjectileActiveSkill(BuildLoadout))
		{
			for (int index = 0; index < BuildLoadout.SkillGemIds.Length; index++)
			{
				if (BuildCatalog.IsProjectileSupportGem(BuildLoadout.SkillGemIds[index]))
				{
					BuildLoadout.SkillGemIds[index] = "gem.skill.none";
					BuildLoadout.SkillGemLevels[index] = 1;
				}
			}
		}
		MarkBuildChanged();
	}

	// Empties one support core slot without the ranged-skill cascade that EquipSkillGem
	// applies, so removing the primary (fireball) core leaves the other slots untouched.
	public void ClearSkillGemSlot(int slotIndex)
	{
		int safeSlot = Mathf.Clamp(slotIndex, 0, BuildLoadout.SkillGemIds.Length - 1);
		BuildLoadout.SkillGemIds[safeSlot] = "gem.skill.none";
		BuildLoadout.SkillGemLevels[safeSlot] = 1;
		MarkBuildChanged();
	}

	// Packs slots 1..N inside the support area only. Slot 0 is the permanent main-core
	// slot and must never receive a promoted support core.
	public void CompactSupportCores()
	{
		string[] ids = BuildLoadout.SkillGemIds;
		int[] levels = BuildLoadout.SkillGemLevels;
		var packedIds = new List<string>();
		var packedLevels = new List<int>();
		for (int index = 1; index < ids.Length; index++)
		{
			if (BuildCatalog.IsSupportCore(ids[index]))
			{
				packedIds.Add(ids[index]);
				packedLevels.Add(levels[index]);
			}
		}

		for (int index = 1; index < ids.Length; index++)
		{
			int packedIndex = index - 1;
			ids[index] = packedIndex < packedIds.Count ? packedIds[packedIndex] : "gem.skill.none";
			levels[index] = packedIndex < packedLevels.Count ? packedLevels[packedIndex] : 1;
		}

		MarkBuildChanged();
	}

	public int RaiseSkillGemLevel(int slotIndex)
	{
		int safeSlot = Mathf.Clamp(slotIndex, 0, BuildLoadout.SkillGemLevels.Length - 1);
		int nextLevel = Mathf.Min(BuildLoadout.GetSkillGemLevel(safeSlot) + 1, BuildCatalog.MaxSkillGemLevel);
		BuildLoadout.SkillGemLevels[safeSlot] = nextLevel;
		MarkBuildChanged();
		return nextLevel;
	}

	public void CycleAttackMode()
	{
		AttackModeId = BuildCatalog.GetNextAttackModeId(AttackModeId);
		_combatTarget = null;
		_combatTargetSearchRemaining = 0.0f;
		MarkBuildChanged();
	}

	public void SetAttackMode(string modeId)
	{
		AttackModeId = BuildCatalog.GetAttackMode(modeId).Id;
		_combatTarget = null;
		_combatTargetSearchRemaining = 0.0f;
		MarkBuildChanged();
	}

	private void EnsureBuildLoadout()
	{
		if (_buildConfigured)
		{
			return;
		}

		_buildLoadout = BuildCatalog.CreateStarterLoadout(this);
		NormalizeSkillCoreSlots();
		RemoveUnsupportedProjectileGems();
		AttackModeId = BuildCatalog.GetAttackMode(AttackModeId).Id;
		_buildConfigured = true;
		_buildStatsDirty = true;
	}

	private void NormalizeSkillCoreSlots()
	{
		_buildLoadout.EnsureSkillSlots();
		string[] ids = _buildLoadout.SkillGemIds;
		int[] levels = _buildLoadout.SkillGemLevels;
		string mainId = "gem.skill.none";
		int mainLevel = 1;
		var supportIds = new List<string>();
		var supportLevels = new List<int>();

		for (int index = 0; index < ids.Length; index++)
		{
			if (mainId == "gem.skill.none" && BuildCatalog.IsMainAttackCore(ids[index]))
			{
				mainId = ids[index];
				mainLevel = Mathf.Max(levels[index], 1);
			}
			else if (BuildCatalog.IsSupportCore(ids[index]))
			{
				supportIds.Add(ids[index]);
				supportLevels.Add(Mathf.Max(levels[index], 1));
			}
		}

		ids[0] = mainId;
		levels[0] = mainLevel;
		for (int index = 1; index < ids.Length; index++)
		{
			int supportIndex = index - 1;
			ids[index] = supportIndex < supportIds.Count ? supportIds[supportIndex] : "gem.skill.none";
			levels[index] = supportIndex < supportLevels.Count ? supportLevels[supportIndex] : 1;
		}
	}

	private void RemoveUnsupportedProjectileGems()
	{
		if (BuildCatalog.HasRangedActiveSkill(_buildLoadout))
		{
			return;
		}

		for (int index = 0; index < _buildLoadout.SkillGemIds.Length; index++)
		{
			if (BuildCatalog.IsProjectileSupportGem(_buildLoadout.SkillGemIds[index]))
			{
				_buildLoadout.SkillGemIds[index] = "gem.skill.none";
				_buildLoadout.SkillGemLevels[index] = 1;
			}
		}
	}

	private void RecalculateBuildStats()
	{
		EnsureBuildLoadout();
		_buildStats = BuildCatalog.CalculateStats(this, _buildLoadout);
		_buildStats.Attack = Mathf.Max(Mathf.RoundToInt(_buildStats.Attack * _formationAttackMultiplier * _cardAttackMultiplier), 1);
		_buildStats.Defense = Mathf.Max(Mathf.RoundToInt(_buildStats.Defense * _formationDefenseMultiplier * _cardDefenseMultiplier), 0);
		_buildStats.MaxHealth = Mathf.Max(Mathf.RoundToInt(_buildStats.MaxHealth * _cardHealthMultiplier), 1);
		_buildStats.AttackCooldownMultiplier *= _formationCooldownMultiplier;
		_buildStats.IncomingDamageMultiplier *= _formationIncomingDamageMultiplier;
		_buildStats.AttackRangeBonus += _formationRangeBonus;
		_buildStatsDirty = false;
		CurrentHealth = Mathf.Clamp(CurrentHealth, 0, _buildStats.MaxHealth);
	}

	private void MarkBuildChanged()
	{
		_buildStatsDirty = true;
		RecalculateBuildStats();
		RefreshNameplate();
		_followTarget?.RecalculateFormationBonuses();
	}

	private void MarkBaseStatsChanged()
	{
		_buildStatsDirty = true;
		if (_buildConfigured)
		{
			RecalculateBuildStats();
		}
	}
}
