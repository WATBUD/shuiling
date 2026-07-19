using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	public int GetInventoryCount(string itemId)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return 1;
		}

		return _inventoryItems.TryGetValue(itemId, out int count) ? count : 0;
	}

	public bool HasInventoryItem(string itemId)
	{
		return BuildCatalog.IsFreeItem(itemId) || GetInventoryCount(itemId) > 0;
	}

	public void AddGold(int amount)
	{
		int gainedGold = Mathf.Max(amount, 0);
		if (gainedGold <= 0)
		{
			return;
		}

		Gold += gainedGold;
		PostSystemMessage(LocaleText.F("system.pickup.gold", gainedGold, Gold), new Color(1.0f, 0.82f, 0.26f), GameMessageChannel.Loot);
		_mercenaryShopPanel?.RefreshAll();
	}

	public void AddInventoryItem(string itemId, int amount = 1)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return;
		}

		_inventoryItems.TryGetValue(itemId, out int currentCount);
		_inventoryItems[itemId] = Mathf.Max(currentCount + amount, 0);
		PostSystemMessage(LocaleText.F("system.pickup.item", GetInventoryItemDisplayName(itemId), Mathf.Max(amount, 0)), new Color(1.0f, 0.88f, 0.48f), GameMessageChannel.Loot);
		if (_inventoryPanel != null)
		{
			_inventoryPanel.RefreshAll();
		}
	}

	// Equipping consumes one from the bag; unequipping/displacement returns one. Both are
	// silent and skip free/default (".none") items. The inventory panel refreshes itself.
	public void ConsumeInventoryItemForEquip(string itemId)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return;
		}

		RemoveInventoryItemSilently(itemId, 1);
	}

	public void ReturnInventoryItemFromUnequip(string itemId)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return;
		}

		_inventoryItems.TryGetValue(itemId, out int currentCount);
		_inventoryItems[itemId] = currentCount + 1;
	}

	public bool TryConsumeInventoryItem(string itemId, int amount = 1)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return true;
		}

		int requestedAmount = Mathf.Max(amount, 1);
		int currentCount = GetInventoryCount(itemId);
		if (currentCount < requestedAmount)
		{
			return false;
		}

		int nextCount = currentCount - requestedAmount;
		if (nextCount <= 0)
		{
			_inventoryItems.Remove(itemId);
		}
		else
		{
			_inventoryItems[itemId] = nextCount;
		}

		PostSystemMessage(LocaleText.F("system.item.used", GetInventoryItemDisplayName(itemId), requestedAmount), new Color(0.72f, 0.88f, 1.0f), GameMessageChannel.Loot);
		if (_inventoryPanel != null)
		{
			_inventoryPanel.RefreshAll();
		}

		return true;
	}

	public bool CanEvolveActor(SimpleActor actor)
	{
		return IsInstanceValid(actor)
			&& actor.CanEvolve
			&& !string.IsNullOrEmpty(actor.EvolutionMaterialId)
			&& GetInventoryCount(actor.EvolutionMaterialId) >= actor.EvolutionMaterialCount;
	}

	public bool TryEvolveActor(SimpleActor actor)
	{
		if (!CanEvolveActor(actor) || !TryConsumeInventoryItem(actor.EvolutionMaterialId, actor.EvolutionMaterialCount))
		{
			return false;
		}

		return actor.TryEvolve();
	}

	public SkillGemUpgradeCost? GetCompanionSkillGemUpgradeCost(SimpleActor actor, int slot)
	{
		if (!IsInstanceValid(actor) || slot < 0 || slot >= actor.BuildLoadout.SkillGemIds.Length)
		{
			return null;
		}

		return BuildCatalog.GetSkillGemUpgradeCost(actor.BuildLoadout.SkillGemIds[slot], actor.GetSkillGemLevel(slot));
	}

	public bool CanAffordSkillGemUpgrade(SkillGemUpgradeCost cost)
	{
		return Gold >= cost.Gold && GetInventoryCount(cost.MaterialId) >= cost.MaterialCount;
	}

	// Spend gold + loot materials to raise one companion skill gem by a level.
	public bool TryUpgradeCompanionSkillGem(SimpleActor actor, int slot)
	{
		SkillGemUpgradeCost? maybeCost = GetCompanionSkillGemUpgradeCost(actor, slot);
		if (maybeCost is not SkillGemUpgradeCost cost)
		{
			PostSystemMessage(LocaleText.T("system.gem.max_level"), new Color(1.0f, 0.82f, 0.42f), GameMessageChannel.Loot);
			return false;
		}

		if (!CanAffordSkillGemUpgrade(cost))
		{
			PostSystemMessage(
				LocaleText.F("system.gem.upgrade_not_enough", cost.Gold, cost.MaterialCount, GetInventoryItemDisplayName(cost.MaterialId)),
				new Color(1.0f, 0.62f, 0.48f),
				GameMessageChannel.Loot);
			return false;
		}

		Gold -= cost.Gold;
		TryConsumeInventoryItem(cost.MaterialId, cost.MaterialCount);
		int newLevel = actor.RaiseSkillGemLevel(slot);
		string gemName = LocaleText.T(BuildCatalog.GetSkillGem(actor.BuildLoadout.SkillGemIds[slot]).NameKey);
		PostSystemMessage(LocaleText.F("system.gem.upgraded", gemName, newLevel), new Color(0.62f, 1.0f, 0.68f), GameMessageChannel.Loot);
		_inventoryPanel?.RefreshAll();
		return true;
	}

	private void RemoveInventoryItemSilently(string itemId, int amount)
	{
		int requestedAmount = Mathf.Max(amount, 1);
		int currentCount = GetInventoryCount(itemId);
		int nextCount = currentCount - requestedAmount;
		if (nextCount <= 0)
		{
			_inventoryItems.Remove(itemId);
		}
		else
		{
			_inventoryItems[itemId] = nextCount;
		}
	}

	private void CollectNearbyWorldDrops()
	{
		foreach (Node node in GetTree().GetNodesInGroup("world_drops"))
		{
			if (node is WorldDrop drop && IsInstanceValid(drop))
			{
				drop.TryCollect(this);
			}
		}
	}

	private void UpdateNearbyWorldDropCollection(float step)
	{
		_worldDropCollectRefreshRemaining = Mathf.Max(_worldDropCollectRefreshRemaining - step, 0.0f);
		if (_worldDropCollectRefreshRemaining > 0.0f)
		{
			return;
		}

		_worldDropCollectRefreshRemaining = WorldDropCollectRefreshSeconds;
		CollectNearbyWorldDrops();
		CollectNearbyFallenCompanions();
	}

	private static string GetInventoryItemDisplayName(string itemId)
	{
		return MonsterLootCatalog.IsMonsterLoot(itemId)
			? LocaleText.T(MonsterLootCatalog.GetNameKey(itemId))
			: LocaleText.T(BuildCatalog.GetItemNameKey(itemId));
	}

	public void OpenInventoryForActor(SimpleActor actor)
	{
		SetInventoryPanelVisible(true);
		_inventoryPanel.SelectActor(actor);
	}

	private void InitializeStarterInventory()
	{
		foreach (string itemId in BuildCatalog.GetStarterInventoryItemIds())
		{
			AddInventoryItem(itemId);
		}
	}

}
