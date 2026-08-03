using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	private void RestoreNpcQuestSets(PlayerSaveData data)
	{
		_acceptedNpcQuests.Clear();
		_completedNpcQuests.Clear();
		RemoveRecruitedVillageNpcDuplicates();
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!IsInstanceValid(actor) || actor.ActorKind == "monster" || actor.IsCaptured)
			{
				continue;
			}

			if (data.AcceptedNpcQuestNames.Contains(actor.DisplayName))
			{
				_acceptedNpcQuests.Add(actor);
			}

			if (data.CompletedNpcQuestNames.Contains(actor.DisplayName))
			{
				_completedNpcQuests.Add(actor);
			}
		}
	}

	private void RemoveRecruitedVillageNpcDuplicates()
	{
		var recruitedNpcNames = new HashSet<string>();
		foreach (SimpleActor companion in _capturedCollection)
		{
			if (IsInstanceValid(companion) && companion.ActorKind == "npc" && companion.IsCaptured)
			{
				recruitedNpcNames.Add(companion.DisplayName);
			}
		}

		if (recruitedNpcNames.Count == 0)
		{
			return;
		}

		var duplicates = new List<SimpleActor>();
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (IsInstanceValid(actor)
				&& actor.ActorKind == "npc"
				&& !actor.IsCaptured
				&& recruitedNpcNames.Contains(actor.DisplayName))
			{
				duplicates.Add(actor);
			}
		}

		foreach (SimpleActor duplicate in duplicates)
		{
			duplicate.RemoveFromGroup("npcs");
			duplicate.CallDeferred(Node.MethodName.QueueFree);
		}
	}

	private static string GetNpcQuestItemId(SimpleActor actor)
	{
		return MonsterLootCatalog.GetQuestItemIdForNpc(actor.DisplayName);
	}

	private void UpdateInteractionPrompt(float step)
	{
		if (_interactionPromptLabel == null)
		{
			return;
		}

		if (_npcQuestDialog != null && _npcQuestDialog.Visible)
		{
			_interactionPromptLabel.Visible = false;
			return;
		}

		_interactionPromptRefreshRemaining = Mathf.Max(_interactionPromptRefreshRemaining - step, 0.0f);
		if (_interactionPromptRefreshRemaining > 0.0f)
		{
			return;
		}

		_interactionPromptRefreshRemaining = InteractionPromptRefreshSeconds;

		Node3D? revivalNpc = GetNearestRevivalNpc();
		if (revivalNpc != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.revive_pets", "E");
			return;
		}

		Node3D? revivalFountain = GetNearestRevivalFountain();
		if (revivalFountain != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.fountain_revive", "E");
			return;
		}

		Node3D? mapPortal = GetNearestMapPortal();
		if (mapPortal != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.portal", "E", GetPortalLabel(mapPortal));
			return;
		}

		SimpleActor? merchant = GetNearestMerchantShopkeeper(out MerchantShopKind merchantShopKind);
		if (merchant != null)
		{
			_interactionPromptLabel.Visible = true;
			string promptKey = merchantShopKind switch
			{
				MerchantShopKind.Blacksmith => "prompt.shop.blacksmith",
				MerchantShopKind.PetShop => "prompt.shop.pet",
				_ => "prompt.shop.item",
			};
			_interactionPromptLabel.Text = LocaleText.F(promptKey, "E", merchant.LocalizedDisplayName);
			return;
		}

		SimpleActor? mercenaryBroker = GetNearestMercenaryBroker();
		if (mercenaryBroker != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.mercenary_shop", "E", mercenaryBroker.LocalizedDisplayName);
			return;
		}

		SimpleActor? warehouseKeeper = GetNearestWarehouseKeeper();
		if (warehouseKeeper != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.warehouse", "E", warehouseKeeper.LocalizedDisplayName);
			return;
		}

		SimpleActor? refiner = GetNearestRefiner();
		if (refiner != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.refiner", "E", refiner.LocalizedDisplayName);
			return;
		}

		SimpleActor? coreEnhancer = GetNearestCoreEnhancer();
		if (coreEnhancer != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.core_enhancer", "E", coreEnhancer.LocalizedDisplayName);
			return;
		}

		SimpleActor? gachaMerchant = GetNearestGachaMerchant();
		if (gachaMerchant != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.gacha", "E", gachaMerchant.LocalizedDisplayName);
			return;
		}

		SimpleActor? recruitNpc = GetNearestRecruitableNpc();
		_interactionPromptLabel.Visible = recruitNpc != null;
		if (recruitNpc == null)
		{
			return;
		}

		string questItemId = GetNpcQuestItemId(recruitNpc);
		if (!_acceptedNpcQuests.Contains(recruitNpc))
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.accept_task", "E", recruitNpc.LocalizedDisplayName);
		}
		else if (GetInventoryCount(questItemId) >= NpcRecruitQuestItemCount)
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.deliver_task", "E", recruitNpc.LocalizedDisplayName);
		}
		else if (_completedNpcQuests.Contains(recruitNpc) && recruitNpc.Affinity >= NpcRecruitAffinityRequirement)
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.invite", "E", recruitNpc.LocalizedDisplayName);
		}
		else if (recruitNpc.Affinity < NpcRecruitAffinityRequirement && HasCard(GetNpcWantedCardKey(recruitNpc)))
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.deliver_card", "E", ExternalModelLibrary.LocalizedCardName(GetNpcWantedCardKey(recruitNpc)), recruitNpc.LocalizedDisplayName);
		}
		else
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.quest_progress", "E", GetInventoryCount(questItemId), NpcRecruitQuestItemCount, recruitNpc.Affinity, NpcRecruitAffinityRequirement);
		}
	}

	private void TryInteract()
	{
		if (GetNearestRevivalNpc() != null)
		{
			ShowRevivalDialog(ReviveDefeatedCompanions());
			return;
		}

		if (GetNearestRevivalFountain() is Node3D revivalFountain)
		{
			TryFountainRevive(revivalFountain.GlobalPosition);
			return;
		}

		Node3D? mapPortal = GetNearestMapPortal();
		if (mapPortal != null)
		{
			TryUseMapPortal(mapPortal);
			return;
		}

		if (GetNearestMerchantShopkeeper(out MerchantShopKind merchantShopKind) != null)
		{
			// 夥伴招募所（原寵物店）改開統一的招募清單面板（傭兵/夥伴分頁）。
			if (merchantShopKind == MerchantShopKind.PetShop)
			{
				SetMercenaryShopPanelVisible(true);
			}
			else
			{
				_merchantShopPanel.Open(merchantShopKind);
				UpdateMouseModeForPanels();
			}

			return;
		}

		if (GetNearestMercenaryBroker() != null)
		{
			SetMercenaryShopPanelVisible(true);
			return;
		}

		if (GetNearestWarehouseKeeper() != null)
		{
			SetWarehousePanelVisible(true);
			return;
		}

		if (GetNearestRefiner() != null)
		{
			SetRefinementPanelVisible(true);
			return;
		}

		if (GetNearestCoreEnhancer() != null)
		{
			SetCoreEnhancerPanelVisible(true);
			return;
		}

		if (GetNearestGachaMerchant() != null)
		{
			SetGachaPanelVisible(true);
			return;
		}

		SimpleActor? recruitNpc = GetNearestRecruitableNpc();
		if (recruitNpc != null)
		{
			TryInteractWithRecruitNpc(recruitNpc);
		}
	}

	private void TryInteractWithRecruitNpc(SimpleActor actor)
	{
		if (!CanInteractWithRecruitNpc(actor))
		{
			return;
		}

		if (_completedNpcQuests.Contains(actor) && actor.Affinity >= NpcRecruitAffinityRequirement)
		{
			RecruitNpc(actor);
			return;
		}

		if (!_acceptedNpcQuests.Contains(actor))
		{
			ShowNpcQuestDialog(actor);
			return;
		}

		string questItemId = GetNpcQuestItemId(actor);
		if (!TryConsumeInventoryItem(questItemId, NpcRecruitQuestItemCount))
		{
			// Fallback: hand over the specific monster card this NPC collects
			// (卡片交換). Consuming it lowers the team card bonus, so it's a choice.
			string wantedCard = GetNpcWantedCardKey(actor);
			if (!string.IsNullOrEmpty(wantedCard) && TryConsumeCard(wantedCard))
			{
				_completedNpcQuests.Add(actor);
				actor.IncreaseAffinity(CardConfig.NpcExchangeAffinityReward);
				SpawnWorldCombatEffect(LocaleText.F("effect.affinity_gain", CardConfig.NpcExchangeAffinityReward), new Color(0.62f, 1.0f, 0.78f, 0.92f), actor.GlobalPosition + new Vector3(0.0f, 1.65f, 0.0f), 0.85f, 0.62f);
				PostSystemMessage(LocaleText.F("system.npc.card_accepted", ExternalModelLibrary.LocalizedCardName(wantedCard), actor.LocalizedDisplayName), new Color(0.72f, 0.92f, 1.0f), GameMessageChannel.Party);
				if (actor.Affinity >= NpcRecruitAffinityRequirement)
				{
					RecruitNpc(actor);
				}
				else
				{
					PostSystemMessage(LocaleText.F("system.npc.need_more_tasks", actor.LocalizedDisplayName), new Color(0.82f, 0.92f, 1.0f), GameMessageChannel.Party);
				}

				return;
			}

			PostSystemMessage(LocaleText.F("system.npc.waiting_items", actor.LocalizedDisplayName, NpcRecruitQuestItemCount, GetInventoryItemDisplayName(questItemId)), new Color(0.86f, 0.84f, 0.72f), GameMessageChannel.Party);
			return;
		}

		int affinityReward = _questRng.RandiRange(NpcQuestAffinityMin, NpcQuestAffinityMax);
		_completedNpcQuests.Add(actor);
		actor.IncreaseAffinity(affinityReward);
		AddGold(NpcQuestGoldReward);
		SpawnWorldCombatEffect(LocaleText.F("effect.affinity_gain", affinityReward), new Color(0.62f, 1.0f, 0.78f, 0.92f), actor.GlobalPosition + new Vector3(0.0f, 1.65f, 0.0f), 0.85f, 0.62f);
		PostSystemMessage(LocaleText.F("system.npc.quest_gold", NpcQuestGoldReward), new Color(1.0f, 0.9f, 0.5f), GameMessageChannel.Party);
		PostSystemMessage(LocaleText.F("system.npc.task_complete", actor.LocalizedDisplayName, actor.Affinity, NpcRecruitAffinityRequirement), new Color(0.78f, 1.0f, 0.82f), GameMessageChannel.Party);
		if (actor.Affinity >= NpcRecruitAffinityRequirement)
		{
			RecruitNpc(actor);
		}
		else
		{
			PostSystemMessage(LocaleText.F("system.npc.need_more_tasks", actor.LocalizedDisplayName), new Color(0.82f, 0.92f, 1.0f), GameMessageChannel.Party);
		}
	}

	private void ShowRevivalDialog(int revivedCount)
	{
		if (_npcQuestDialog == null)
		{
			return;
		}

		_pendingQuestNpc = null;
		_npcQuestDialogIsNotice = true;
		_npcQuestTitleLabel.Text = LocaleText.T("revival.dialog.title");
		_npcQuestBodyLabel.Text = revivedCount > 0
			? LocaleText.F("revival.dialog.count_paid", revivedCount, revivedCount * PetReviveGoldCost)
			: LocaleText.T("revival.dialog.no_fallen");
		_npcQuestRewardLabel.Text = string.Empty;
		_npcQuestRewardLabel.Visible = false;
		_npcQuestAcceptButton.Text = LocaleText.T("dialog.button.ok");
		_npcQuestDeclineButton.Visible = false;
		_npcQuestDialog.Visible = true;
		_interactionPromptLabel.Visible = false;
		UpdateMouseModeForPanels();
	}

	private Node3D? GetNearestRevivalNpc()
	{
		// Every map shares the same world coordinates. Without a map guard, the
		// hidden city caretaker can still be found by distance while exploring a
		// wild map and pressing E opens its dialog remotely.
		if (!IsInCityMap())
		{
			return null;
		}

		Node3D? nearest = null;
		float bestDistance = RevivalNpcInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("revival_npc"))
		{
			if (node is not Node3D npc
				|| !IsInstanceValid(npc)
				|| !npc.IsVisibleInTree()
				|| !npc.IsProcessing())
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(npc.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = npc;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private Node3D? GetNearestRevivalFountain()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		Node3D? nearest = null;
		float bestDistance = RevivalFountainInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("revival_fountain"))
		{
			if (node is not Node3D fountain || !IsInstanceValid(fountain) || !fountain.IsVisibleInTree())
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(fountain.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = fountain;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private Node3D? GetNearestMapPortal()
	{
		Node3D? nearest = null;
		float bestDistance = MapPortalInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("map_portal"))
		{
			if (node is not Node3D portal || !IsInstanceValid(portal) || !portal.IsVisibleInTree())
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(portal.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = portal;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private string GetPortalLabel(Node3D portal)
	{
		if (portal.HasMeta("label"))
		{
			string labelKey = portal.GetMeta("label").AsString();
			if (!string.IsNullOrWhiteSpace(labelKey))
			{
				return LocaleText.T(labelKey);
			}
		}

		return LocaleText.T("portal.travel_wild");
	}

	private void TryUseMapPortal(Node3D portal)
	{
		if (!portal.HasMeta("target_map"))
		{
			return;
		}

		string targetMapId = portal.GetMeta("target_map").AsString();
		if (GetParent() is World world)
		{
			if (targetMapId == "wild_select")
			{
				ShowMapTravelDialog(world);
				return;
			}

			// Leaving a wild map: offer "next tier" vs "return to city" instead of
			// going straight back to town.
			if (targetMapId == "city" && world.IsWildMap(world.ActiveMapId))
			{
				ShowWildReturnDialog(world);
				return;
			}

			world.RequestMapTravel(targetMapId);
		}
	}

	private SimpleActor? GetNearestRecruitableNpc()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		if (_selectedActor != null && CanInteractWithRecruitNpc(_selectedActor) && GlobalPosition.DistanceTo(_selectedActor.GlobalPosition) <= NpcRecruitInteractRange)
		{
			return _selectedActor;
		}

		SimpleActor? nearest = null;
		float bestDistance = NpcRecruitInteractRange;
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!CanInteractWithRecruitNpc(actor))
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = actor;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private bool CanInteractWithRecruitNpc(SimpleActor actor)
	{
		return IsInCityMap()
			&& IsInstanceValid(actor)
			&& !IsMerchantShopkeeper(actor)
			&& !IsMercenaryBroker(actor)
			&& !IsWarehouseKeeper(actor)
			&& !IsRefiner(actor)
			&& !IsCoreEnhancer(actor)
			&& !IsGachaMerchant(actor)
			&& actor.IsNpcRecruitCandidate
			&& actor.MapId == "city"
			&& actor.IsActiveWorldTarget;
	}

	private static bool IsWarehouseKeeper(SimpleActor actor)
	{
		return IsInstanceValid(actor) && actor.DisplayName == "name.npc.warehouse_keeper";
	}

	private SimpleActor? GetNearestWarehouseKeeper()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		SimpleActor? nearest = null;
		float nearestDistance = MerchantInteractRange;
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!IsWarehouseKeeper(actor) || !actor.IsActiveWorldTarget)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= nearestDistance)
			{
				nearest = actor;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

	private static bool IsRefiner(SimpleActor actor)
	{
		return IsInstanceValid(actor) && actor.DisplayName == "name.npc.refiner";
	}

	private SimpleActor? GetNearestRefiner()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		SimpleActor? nearest = null;
		float nearestDistance = MerchantInteractRange;
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!IsRefiner(actor) || !actor.IsActiveWorldTarget)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= nearestDistance)
			{
				nearest = actor;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

	private static bool IsCoreEnhancer(SimpleActor actor)
	{
		return IsInstanceValid(actor) && actor.DisplayName == "name.npc.core_enhancer";
	}

	private SimpleActor? GetNearestCoreEnhancer()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		SimpleActor? nearest = null;
		float nearestDistance = MerchantInteractRange;
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!IsCoreEnhancer(actor) || !actor.IsActiveWorldTarget)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= nearestDistance)
			{
				nearest = actor;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

	private static bool IsGachaMerchant(SimpleActor actor)
	{
		return IsInstanceValid(actor) && actor.DisplayName == "name.npc.gacha";
	}

	private SimpleActor? GetNearestGachaMerchant()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		SimpleActor? nearest = null;
		float nearestDistance = MerchantInteractRange;
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!IsGachaMerchant(actor) || !actor.IsActiveWorldTarget)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= nearestDistance)
			{
				nearest = actor;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

}
