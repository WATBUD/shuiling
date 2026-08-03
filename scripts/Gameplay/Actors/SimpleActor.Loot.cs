using Godot;

public partial class SimpleActor : CharacterBody3D
{
	private void DropMonsterLoot(PlayerController player)
	{
		if (IsBoss)
		{
			DropBossLoot(player);
			return;
		}

		Vector3 origin = GlobalPosition;
		int goldAmount = Mathf.Max(
			GoldReward + _rng.RandiRange(
				MonsterConfig.GoldRandomMinimum,
				Mathf.Max(Level + MonsterConfig.GoldRandomLevelOffset, MonsterConfig.GoldRandomMinimumMaximum)),
			1);
		SpawnWorldDrop(origin + RandomDropOffset(0.45f), string.Empty, 1, goldAmount);
		string primaryLootId = MonsterLootCatalog.PickPrimaryDropForMonster(DisplayName, IsRangedCombatant, Level);
		int primaryAmount = Level >= MonsterConfig.BonusPrimaryLootMinimumLevel
			&& _rng.Randf() < MonsterConfig.BonusPrimaryLootChance ? 2 : 1;
		SpawnWorldDrop(origin + RandomDropOffset(0.78f), primaryLootId, primaryAmount, 0);

		// 所有怪物都會掉一顆對應自身世界階級的強化水晶（精煉材料）。
		string crystalId = MonsterLootCatalog.GetEnhanceCrystalId(WorldTierCatalog.ClampTier(WorldTier));
		SpawnWorldDrop(origin + RandomDropOffset(0.62f), crystalId, MonsterConfig.GuaranteedCrystalAmount, 0);

		if (_rng.Randf() < MonsterConfig.SecondaryLootChance)
		{
			string secondaryLootId = MonsterLootCatalog.PickSecondaryDropForMonster(primaryLootId, Level);
			SpawnWorldDrop(origin + RandomDropOffset(0.95f), secondaryLootId, 1, 0);
		}

		if (_rng.Randf() < EquipmentConfig.MonsterDropChance)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.18f), PickEquipmentDropId(), 1, 0);
		}

		if (_rng.Randf() < CoreConfig.MainSkillMonsterDropChance)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.32f), PickSkillCoreDropId(false), 1, 0);
		}

		if (_rng.Randf() < CoreConfig.SupportMonsterDropChance)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.45f), PickSkillCoreDropId(true), 1, 0);
		}

		player.PostSystemMessage(LocaleText.F("system.drop.loot", LocalizedDisplayName, LocaleText.T(MonsterLootCatalog.GetNameKey(primaryLootId))), new Color(1.0f, 0.86f, 0.48f), GameMessageChannel.Loot);
	}

	// Chance for a defeated monster to drop its exclusive name card as a physical
	// card-shaped pickup. Skipped when the player already owns the card.
	private void MaybeDropMonsterCard(PlayerController player)
	{
		if (player == null || !IsInstanceValid(player))
		{
			return;
		}

		string cardKey = GetCardKey();
		if (!ExternalModelLibrary.IsValidCardKey(cardKey) || player.HasCard(cardKey))
		{
			return;
		}

		float dropChance = IsBoss ? CardConfig.BossDropChance : CardConfig.NormalMonsterDropChance;
		if (_rng.Randf() >= dropChance)
		{
			return;
		}

		Node parent = GetTree().CurrentScene ?? GetParent();
		Vector3 position = GlobalPosition + RandomDropOffset(0.6f);
		WorldDropFactory.SpawnCard(parent, position, cardKey);
	}

	private void DropBossLoot(PlayerController player)
	{
		Vector3 origin = GlobalPosition;
		int goldAmount = Mathf.Max(
			GoldReward + _rng.RandiRange(
				Level * BossConfig.GoldLevelMultiplierMinimum,
				Level * BossConfig.GoldLevelMultiplierMaximum),
			1);
		SpawnWorldDrop(origin + RandomDropOffset(0.55f), string.Empty, 1, goldAmount);

		string primaryLootId = string.IsNullOrWhiteSpace(BossPrimaryLootId)
			? MonsterLootCatalog.PickPrimaryDropForMonster(DisplayName, IsRangedCombatant, Level)
			: BossPrimaryLootId;
		SpawnWorldDrop(origin + RandomDropOffset(0.85f), primaryLootId, _rng.RandiRange(BossConfig.PrimaryLootMinimum, BossConfig.PrimaryLootMaximum), 0);
		string secondaryLootId = MonsterLootCatalog.PickSecondaryDropForMonster(primaryLootId, Level + BossConfig.SecondaryLootLevelBonus);
		SpawnWorldDrop(origin + RandomDropOffset(1.05f), secondaryLootId, _rng.RandiRange(BossConfig.SecondaryLootMinimum, BossConfig.SecondaryLootMaximum), 0);

		// Boss 掉多顆對應階級的強化水晶。
		string bossCrystalId = MonsterLootCatalog.GetEnhanceCrystalId(WorldTierCatalog.ClampTier(WorldTier));
		SpawnWorldDrop(origin + RandomDropOffset(0.95f), bossCrystalId, _rng.RandiRange(BossConfig.CrystalMinimum, BossConfig.CrystalMaximum), 0);

		// Bosses have a 20% chance to drop equipment; when they do, 1..6 pieces.
		if (_rng.Randf() < EquipmentConfig.BossDropChance)
		{
			int dropCount = _rng.RandiRange(1, EquipmentConfig.BossMaxDropCount);
			for (int index = 0; index < dropCount; index++)
			{
				SpawnWorldDrop(origin + RandomDropOffset(1.25f + index * 0.23f), PickBossEquipmentDropId(), 1, 0);
			}
		}
		for (int index = 0; index < CoreConfig.BossGuaranteedCoreCount; index++)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.68f + index * 0.20f), PickNonFreeSkillGem(BuildCatalog.GetSkillGemDefinitions()), 1, 0);
		}
		if (_rng.Randf() < CoreConfig.BossAdditionalCoreChance)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.88f), PickNonFreeSkillGem(BuildCatalog.GetSkillGemDefinitions()), 1, 0);
		}

		player.PostSystemMessage(LocaleText.F("system.drop.boss_loot", LocalizedDisplayName), new Color(1.0f, 0.78f, 0.22f), GameMessageChannel.Loot);
	}

	private void SpawnWorldDrop(Vector3 position, string itemId, int amount, int goldAmount)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (goldAmount > 0)
		{
			WorldDropFactory.SpawnGold(parent, position, goldAmount);
		}
		else
		{
			WorldDropFactory.SpawnItem(parent, position, itemId, amount);
		}
	}

	private Vector3 RandomDropOffset(float radius)
	{
		float angle = (float)_rng.RandfRange(0.0f, Mathf.Tau);
		float distance = (float)_rng.RandfRange(radius * 0.35f, radius);
		return new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance);
	}

	private string PickEquipmentDropId()
	{
		EquipmentSlot[] slots =
		{
			EquipmentSlot.Helmet,
			EquipmentSlot.Weapon,
			EquipmentSlot.Armor,
			EquipmentSlot.Boots,
			EquipmentSlot.Accessory,
		};
		EquipmentSlot slot = slots[_rng.RandiRange(0, slots.Length - 1)];
		var definitions = BuildCatalog.GetEquipmentDefinitions(slot);
		return definitions[_rng.RandiRange(0, definitions.Count - 1)].Id;
	}

	private string PickBossEquipmentDropId()
	{
		EquipmentSlot[] slots =
		{
			EquipmentSlot.Helmet,
			EquipmentSlot.Weapon,
			EquipmentSlot.Armor,
			EquipmentSlot.Boots,
			EquipmentSlot.Accessory,
		};
		EquipmentSlot slot = slots[_rng.RandiRange(0, slots.Length - 1)];
		EquipmentDefinition? best = null;
		float bestScore = float.MinValue;
		foreach (EquipmentDefinition item in BuildCatalog.GetEquipmentDefinitions(slot))
		{
			if (BuildCatalog.IsFreeItem(item.Id))
			{
				continue;
			}

			float score = item.MaxHealthBonus
				+ item.AttackBonus * EquipmentConfig.AttackScoreWeight
				+ item.DefenseBonus * EquipmentConfig.DefenseScoreWeight
				+ item.MoveSpeedBonus * EquipmentConfig.MoveSpeedScoreWeight
				+ (BuildCatalog.GetWeaponAttackSpeed(item) - EquipmentConfig.NeutralWeaponAttackSpeed)
					* EquipmentConfig.WeaponAttackSpeedToCooldownReduction
					* EquipmentConfig.AttackSpeedScoreWeight
				+ item.AttackCooldownReduction * EquipmentConfig.AttackSpeedScoreWeight
				+ item.AttackRangeBonus * EquipmentConfig.AttackRangeScoreWeight
				+ item.CritChanceBonus * EquipmentConfig.CriticalChanceScoreWeight;
			if (score > bestScore)
			{
				bestScore = score;
				best = item;
			}
		}

		return best?.Id ?? PickEquipmentDropId();
	}

	private string PickSkillCoreDropId(bool supportCore)
	{
		var candidates = new System.Collections.Generic.List<SkillGemDefinition>();
		foreach (SkillGemDefinition gem in BuildCatalog.GetSkillGemDefinitions())
		{
			if (BuildCatalog.IsFreeItem(gem.Id))
			{
				continue;
			}

			bool isRequestedType = supportCore
				? BuildCatalog.IsSupportCore(gem.Id)
				: BuildCatalog.IsMainAttackCore(gem.Id);
			if (isRequestedType)
			{
				candidates.Add(gem);
			}
		}

		return candidates.Count > 0
			? candidates[_rng.RandiRange(0, candidates.Count - 1)].Id
			: "gem.skill.none";
	}

	private int PickValidGemIndex(int count)
	{
		return Mathf.Clamp(_rng.RandiRange(1, Mathf.Max(count - 1, 1)), 0, Mathf.Max(count - 1, 0));
	}

	private string PickNonFreeSkillGem(System.Collections.Generic.List<SkillGemDefinition> gems)
	{
		for (int attempt = 0; attempt < 12; attempt++)
		{
			string id = gems[PickValidGemIndex(gems.Count)].Id;
			if (!BuildCatalog.IsFreeItem(id))
			{
				return id;
			}
		}

		return "gem.skill.fireball";
	}
}
