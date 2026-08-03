using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	public PlayerSaveData ExportSaveData()
	{
		EnsurePlayerAttributePoints();
		var data = new PlayerSaveData
		{
			PlayerName = PlayerName,
			PlayerModelPath = PlayerModelPath,
			Level = Level,
			Experience = Experience,
			MaxHealth = MaxHealth,
			CurrentHealth = CurrentHealth,
			Attack = Attack,
			Defense = Defense,
			UnspentAttributePoints = UnspentAttributePoints,
			HealthAttributePoints = HealthAttributePoints,
			AttackAttributePoints = AttackAttributePoints,
			DefenseAttributePoints = DefenseAttributePoints,
			MoveSpeedAttributePoints = MoveSpeedAttributePoints,
			AttackSpeedAttributePoints = AttackSpeedAttributePoints,
			CritChanceAttributePoints = CritChanceAttributePoints,
			PlayerRebirthCount = PlayerRebirthCount,
			BuildLoadout = ExportPlayerBuildLoadout(),
			Gold = Gold,
			CameraMode = CameraModeToSaveId(_cameraMode),
			DamageTextScale = DamageTextScale,
			NameplateScale = NameplateScale,
			BossAnnouncementsEnabled = BossAnnouncementsEnabled,
			BossAnnouncementOpacity = BossAnnouncementOpacity,
			ShowQuestTracker = _showQuestTracker,
			GachaMerchantLevel = _gachaMerchantLevel,
			GachaMerchantExp = _gachaMerchantExp,
			InventoryItems = new Dictionary<string, int>(_inventoryItems),
			StorageItems = new Dictionary<string, int>(_storageItems),
			MercenaryNextRefreshUnix = _mercenaryNextRefreshUnix,
			CompanionRecruitNextRefreshUnix = _companionRecruitNextRefreshUnix,
			MerchantNextRefreshUnix = _merchantNextRefreshUnix,
			BlacksmithStockItemIds = new List<string>(_blacksmithStockItemIds),
			PetShopStockNameKeys = new List<string>(_petShopStockNameKeys),
			Mailbox = ExportMailbox(),
			OwnedCards = ExportCards(),
		};

		foreach (ContractCompanionOffer offer in _contractCompanionOffers)
		{
			data.MercenaryOffers.Add(new MercenaryOfferSaveData
			{
				Id = offer.Id,
				NameKey = offer.NameKey,
				RoleNameKey = offer.RoleNameKey,
				CombatRole = offer.CombatRole,
				SummaryKey = offer.SummaryKey,
				Level = offer.Level,
				Cost = offer.Cost,
				MaxHealth = offer.MaxHealth,
				Attack = offer.Attack,
				Defense = offer.Defense,
			});
		}

		foreach (ContractCompanionOffer offer in _companionRecruitOffers)
		{
			data.CompanionRecruitOffers.Add(new MercenaryOfferSaveData
			{
				Id = offer.Id,
				NameKey = offer.NameKey,
				RoleNameKey = offer.RoleNameKey,
				CombatRole = offer.CombatRole,
				SummaryKey = offer.SummaryKey,
				Level = offer.Level,
				Cost = offer.Cost,
				MaxHealth = offer.MaxHealth,
				Attack = offer.Attack,
				Defense = offer.Defense,
			});
		}

		foreach (SimpleActor actor in _acceptedNpcQuests)
		{
			if (IsInstanceValid(actor))
			{
				data.AcceptedNpcQuestNames.Add(actor.DisplayName);
			}
		}

		foreach (SimpleActor actor in _completedNpcQuests)
		{
			if (IsInstanceValid(actor))
			{
				data.CompletedNpcQuestNames.Add(actor.DisplayName);
			}
		}

		for (int index = 0; index < _capturedCollection.Count; index++)
		{
			SimpleActor actor = _capturedCollection[index];
			if (!IsInstanceValid(actor))
			{
				continue;
			}

			data.Companions.Add(actor.ExportSaveData());
			if (_activeParty.Contains(actor))
			{
				data.ActivePartyIndexes.Add(index);
			}
		}

		return data;
	}

	public void ApplySaveData(PlayerSaveData data, IReadOnlyList<SimpleActor> loadedCompanions)
	{
		// Restore chosen character name + model; rebuild the model if it differs
		// from the one built at startup.
		if (!string.IsNullOrWhiteSpace(data.PlayerName))
		{
			PlayerName = data.PlayerName;
		}

		if (PlayerModelPath != data.PlayerModelPath)
		{
			PlayerModelPath = data.PlayerModelPath;
			RebuildPlayerExternalModel();
		}

		RefreshPlayerNameplate();

		Level = Mathf.Max(data.Level, 1);
		Experience = Mathf.Max(data.Experience, 0);
		MaxHealth = Mathf.Max(data.MaxHealth, 1);
		Attack = Mathf.Max(data.Attack, 0);
		Defense = Mathf.Max(data.Defense, 0);
		UnspentAttributePoints = Mathf.Max(data.UnspentAttributePoints, 0);
		HealthAttributePoints = Mathf.Max(data.HealthAttributePoints, 0);
		AttackAttributePoints = Mathf.Max(data.AttackAttributePoints, 0);
		DefenseAttributePoints = Mathf.Max(data.DefenseAttributePoints, 0);
		MoveSpeedAttributePoints = Mathf.Max(data.MoveSpeedAttributePoints, 0);
		AttackSpeedAttributePoints = Mathf.Max(data.AttackSpeedAttributePoints, 0);
		CritChanceAttributePoints = Mathf.Max(data.CritChanceAttributePoints, 0);
		PlayerRebirthCount = Mathf.Max(data.PlayerRebirthCount, 0);
		UnspentAttributePoints += Mathf.Max(data.Strength, 0) + Mathf.Max(data.Vitality, 0)
			+ Mathf.Max(data.Agility, 0) + Mathf.Max(data.Intelligence, 0);
		EnsurePlayerAttributePoints();
		RestorePlayerBuildLoadout(data.BuildLoadout);
		CurrentHealth = Mathf.Clamp(data.CurrentHealth, 1, EffectiveMaxHealth);
		Gold = Mathf.Max(data.Gold, 0);
		SetDamageTextScale(data.DamageTextScale);
		SetNameplateScale(data.NameplateScale);
		SetBossAnnouncementsEnabled(data.BossAnnouncementsEnabled);
		SetBossAnnouncementOpacity(data.BossAnnouncementOpacity);
		SetShowQuestTracker(data.ShowQuestTracker);
		SetGachaMerchantProgress(data.GachaMerchantLevel, data.GachaMerchantExp);
		SetCameraMode(CameraModeFromSaveId(data.CameraMode));
		RestoreMercenaryOffers(data);
		RestoreCompanionRecruitOffers(data);
		RestoreMerchantStock(data);

		_inventoryItems.Clear();
		foreach (KeyValuePair<string, int> item in data.InventoryItems)
		{
			if (!BuildCatalog.IsFreeItem(item.Key)
				&& !BuildCatalog.IsRetiredSkillCore(item.Key)
				&& !BuildCatalog.IsRetiredAttributeGem(item.Key)
				&& item.Value > 0)
			{
				_inventoryItems[item.Key] = item.Value;
			}
		}

		_storageItems.Clear();
		foreach (KeyValuePair<string, int> item in data.StorageItems)
		{
			if (!BuildCatalog.IsFreeItem(item.Key)
				&& !BuildCatalog.IsRetiredSkillCore(item.Key)
				&& !BuildCatalog.IsRetiredAttributeGem(item.Key)
				&& item.Value > 0)
			{
				_storageItems[item.Key] = item.Value;
			}
		}

		_capturedCollection.Clear();
		_activeParty.Clear();
		_formationActorsBySlot.Clear();
		_formationSlotsByActor.Clear();
		for (int index = 0; index < loadedCompanions.Count; index++)
		{
			SimpleActor actor = loadedCompanions[index];
			if (!IsInstanceValid(actor))
			{
				continue;
			}

			_capturedCollection.Add(actor);
			ActorSaveData actorData = index < data.Companions.Count ? data.Companions[index] : actor.ExportSaveData();
			actor.RestoreCapturedState(this, actorData);
		}

		foreach (int companionIndex in data.ActivePartyIndexes)
		{
			if (companionIndex >= 0 && companionIndex < _capturedCollection.Count)
			{
				DeployCompanion(_capturedCollection[companionIndex], false);
			}
		}

		RestoreNpcQuestSets(data);
		RestoreMailbox(data);
		RestoreCards(data);
		EnsureTestModeCatalogUnlocks();
		if (GetParent() is World world)
		{
			RefreshFallenCompanionMapVisibility(world.ActiveMapId);
		}
		_partyPanel.RefreshParty();
		_inventoryPanel.RefreshAll();
		_formationPanel.RefreshAll();
		_mercenaryShopPanel.RefreshAll();
		_warehousePanel.RefreshAll();
	}

	private CompanionBuildSaveData ExportPlayerBuildLoadout()
	{
		BuildLoadout.EnsureSkillSlots();
		return new CompanionBuildSaveData
		{
			HelmetId = BuildLoadout.HelmetId,
			WeaponId = BuildLoadout.WeaponId,
			ArmorId = BuildLoadout.ArmorId,
			BootsId = BuildLoadout.BootsId,
			AccessoryId = BuildLoadout.AccessoryId,
			AttributeGemId = "gem.attribute.none",
			SkillGemIds = (string[])BuildLoadout.SkillGemIds.Clone(),
			SkillGemLevels = (int[])BuildLoadout.SkillGemLevels.Clone(),
		};
	}

	private void RestorePlayerBuildLoadout(CompanionBuildSaveData? data)
	{
		data ??= new CompanionBuildSaveData();
		BuildLoadout = new CompanionBuildLoadout
		{
			HelmetId = data.HelmetId,
			WeaponId = data.WeaponId,
			ArmorId = data.ArmorId,
			BootsId = data.BootsId,
			AccessoryId = data.AccessoryId,
			// Pure attribute gems were retired; active cores now own their element.
			AttributeGemId = "gem.attribute.none",
			SkillGemIds = data.SkillGemIds is { Length: > 0 } ? (string[])data.SkillGemIds.Clone() : new[] { "gem.skill.none", "gem.skill.none", "gem.skill.none" },
			SkillGemLevels = data.SkillGemLevels is { Length: > 0 } ? (int[])data.SkillGemLevels.Clone() : new[] { 1, 1, 1 },
		};
		BuildLoadout.EnsureSkillSlots();
		for (int index = 0; index < BuildLoadout.SkillGemIds.Length; index++)
		{
			string id = BuildLoadout.SkillGemIds[index];
			bool valid = index == 0 ? BuildCatalog.IsMainAttackCore(id) : BuildCatalog.IsSupportCore(id);
			if (!valid || (BuildCatalog.IsProjectileSupportGem(id) && !BuildCatalog.HasProjectileActiveSkill(BuildLoadout)))
			{
				BuildLoadout.SkillGemIds[index] = "gem.skill.none";
				BuildLoadout.SkillGemLevels[index] = 1;
			}
		}
		MarkPlayerBuildStatsDirty();
	}

	public void SetDamageTextScale(float scale)
	{
		CombatEffect.SetDamageTextScale(scale);
	}

	// Adjust the overhead Lv+name nameplate size and re-apply to every live
	// monster/companion/NPC in the world.
	public void SetNameplateScale(float scale)
	{
		SimpleActor.SetNameplateScale(scale);
		foreach (string group in new[] { "monsters", "npcs" })
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node is SimpleActor actor && IsInstanceValid(actor))
				{
					actor.RefreshNameplateDisplay();
				}
			}
		}

		// The player's own overhead nickname scales with the same setting.
		RefreshPlayerNameplate();
	}

	private void SaveCurrentGame()
	{
		SaveGameToActiveWorld(true);
	}

	// Save the current world to its slot. announce=false suppresses the on-screen
	// message (used for the silent auto-save when a new world is first created).
	public void SaveGameToActiveWorld(bool announce)
	{
		if (GetParent() is not World world)
		{
			return;
		}

		// A multiplayer client is a transient guest on someone else's world — it
		// must not write any local save (never holds the host's world).
		if (NetworkManager.Instance is { IsClient: true })
		{
			if (announce)
			{
				PostSystemMessage(LocaleText.T("system.save.client_blocked"), new Color(1.0f, 0.72f, 0.5f));
			}

			return;
		}

		string worldId = GameLaunchOptions.ActiveWorldId;
		if (string.IsNullOrEmpty(worldId))
		{
			if (announce)
			{
				PostSystemMessage(LocaleText.T("system.save.no_world"), new Color(1.0f, 0.72f, 0.5f));
			}

			return;
		}

		if (SaveGameManager.TrySave(worldId, world.ExportSaveData(), out string error))
		{
			if (announce)
			{
				PostSystemMessage(LocaleText.T("system.save.success"), new Color(0.72f, 1.0f, 0.78f));
			}
		}
		else if (announce)
		{
			PostSystemMessage(LocaleText.F("system.save.failed", error), new Color(1.0f, 0.42f, 0.34f));
		}
	}

}
