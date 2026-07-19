using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	public PlayerSaveData ExportSaveData()
	{
		var data = new PlayerSaveData
		{
			Level = Level,
			Experience = Experience,
			MaxHealth = MaxHealth,
			CurrentHealth = CurrentHealth,
			Attack = Attack,
			Defense = Defense,
			Gold = Gold,
			CameraMode = CameraModeToSaveId(_cameraMode),
			DamageTextScale = DamageTextScale,
			BossAnnouncementsEnabled = BossAnnouncementsEnabled,
			BossAnnouncementOpacity = BossAnnouncementOpacity,
			InventoryItems = new Dictionary<string, int>(_inventoryItems),
			MercenaryNextRefreshUnix = _mercenaryNextRefreshUnix,
			MerchantNextRefreshUnix = _merchantNextRefreshUnix,
			BlacksmithStockItemIds = new List<string>(_blacksmithStockItemIds),
			PetShopStockNameKeys = new List<string>(_petShopStockNameKeys),
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
		Level = Mathf.Max(data.Level, 1);
		Experience = Mathf.Max(data.Experience, 0);
		MaxHealth = Mathf.Max(data.MaxHealth, 1);
		CurrentHealth = Mathf.Clamp(data.CurrentHealth, 1, MaxHealth);
		Attack = Mathf.Max(data.Attack, 0);
		Defense = Mathf.Max(data.Defense, 0);
		Gold = Mathf.Max(data.Gold, 0);
		SetDamageTextScale(data.DamageTextScale);
		SetBossAnnouncementsEnabled(data.BossAnnouncementsEnabled);
		SetBossAnnouncementOpacity(data.BossAnnouncementOpacity);
		SetCameraMode(CameraModeFromSaveId(data.CameraMode));
		RestoreMercenaryOffers(data);
		RestoreMerchantStock(data);

		_inventoryItems.Clear();
		foreach (KeyValuePair<string, int> item in data.InventoryItems)
		{
			if (!BuildCatalog.IsFreeItem(item.Key) && item.Value > 0)
			{
				_inventoryItems[item.Key] = item.Value;
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
		if (GetParent() is World world)
		{
			RefreshFallenCompanionMapVisibility(world.ActiveMapId);
		}
		_partyPanel.RefreshParty();
		_inventoryPanel.RefreshAll();
		_formationPanel.RefreshAll();
		_mercenaryShopPanel.RefreshAll();
	}

	public void SetDamageTextScale(float scale)
	{
		CombatEffect.SetDamageTextScale(scale);
	}

	private void SaveCurrentGame()
	{
		if (GetParent() is not World world)
		{
			return;
		}

		if (SaveGameManager.TrySave(world.ExportSaveData(), out string error))
		{
			PostSystemMessage(LocaleText.T("system.save.success"), new Color(0.72f, 1.0f, 0.78f));
		}
		else
		{
			PostSystemMessage(LocaleText.F("system.save.failed", error), new Color(1.0f, 0.42f, 0.34f));
		}
	}

}
