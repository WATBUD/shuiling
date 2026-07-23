using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	private void RestoreNpcQuestSets(PlayerSaveData data)
	{
		_acceptedNpcQuests.Clear();
		_completedNpcQuests.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !IsInstanceValid(actor))
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

		Node3D? mapPortal = GetNearestMapPortal();
		if (mapPortal != null)
		{
			TryUseMapPortal(mapPortal);
			return;
		}

		if (GetNearestMerchantShopkeeper(out MerchantShopKind merchantShopKind) != null)
		{
			_merchantShopPanel.Open(merchantShopKind);
			UpdateMouseModeForPanels();
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
				actor.IncreaseAffinity(NpcCardExchangeAffinityReward);
				SpawnWorldCombatEffect(LocaleText.F("effect.affinity_gain", NpcCardExchangeAffinityReward), new Color(0.62f, 1.0f, 0.78f, 0.92f), actor.GlobalPosition + new Vector3(0.0f, 1.65f, 0.0f), 0.85f, 0.62f);
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

		int affinityReward = GetNpcQuestAffinityReward(questItemId, NpcRecruitQuestItemCount);
		_completedNpcQuests.Add(actor);
		actor.IncreaseAffinity(affinityReward);
		SpawnWorldCombatEffect(LocaleText.F("effect.affinity_gain", affinityReward), new Color(0.62f, 1.0f, 0.78f, 0.92f), actor.GlobalPosition + new Vector3(0.0f, 1.65f, 0.0f), 0.85f, 0.62f);
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

	private static int GetNpcQuestAffinityReward(string questItemId, int amount)
	{
		int materialDifficulty = questItemId switch
		{
			"loot.slime_mucus" => 8,
			"loot.soft_fur" => 9,
			"loot.beast_hide" => 10,
			"loot.small_bone" => 11,
			"loot.insect_wing" => 12,
			"loot.sharp_claw" => 13,
			"loot.venom_sac" => 16,
			"loot.water_core" => 18,
			"loot.cracked_core" => 20,
			"loot.red_horn" => 22,
			"loot.dragon_scale" => 28,
			_ => 10,
		};

		return Mathf.Clamp(materialDifficulty + Mathf.Max(amount - 1, 0) * 4, 8, 45);
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
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !CanInteractWithRecruitNpc(actor))
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
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !IsWarehouseKeeper(actor) || !actor.IsActiveWorldTarget)
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
