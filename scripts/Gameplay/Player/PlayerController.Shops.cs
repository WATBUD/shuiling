using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	public bool IsMerchantShopRefreshable(MerchantShopKind shopKind)
	{
		return shopKind is MerchantShopKind.Blacksmith or MerchantShopKind.PetShop;
	}

	public string GetMerchantRefreshCountdownText()
	{
		double remaining = Mathf.Max((float)(_merchantNextRefreshUnix - Time.GetUnixTimeFromSystem()), 0.0f);
		int totalSeconds = Mathf.CeilToInt((float)remaining);
		int hours = totalSeconds / 3600;
		int minutes = (totalSeconds % 3600) / 60;
		int seconds = totalSeconds % 60;
		return LocaleText.F("mercenary.refresh.countdown", hours, minutes, seconds);
	}

	public bool TryRefreshMerchantShopManually(MerchantShopKind shopKind)
	{
		if (!IsMerchantShopRefreshable(shopKind))
		{
			return false;
		}

		if (Gold < MerchantRefreshCost)
		{
			PostSystemMessage(LocaleText.F("system.shop.refresh_not_enough_gold", MerchantRefreshCost, Gold), new Color(1.0f, 0.62f, 0.48f));
			return false;
		}

		Gold -= MerchantRefreshCost;
		GenerateMerchantStock();
		PostSystemMessage(LocaleText.F("system.shop.refreshed", MerchantRefreshCost, Gold), new Color(0.82f, 0.94f, 1.0f));
		_inventoryPanel.RefreshAll();
		_merchantShopPanel.RefreshAll();
		return true;
	}

	private void EnsureMerchantStock()
	{
		if (_blacksmithStockItemIds.Count == 0 || _petShopStockNameKeys.Count == 0 || _merchantNextRefreshUnix <= 0.0 || Time.GetUnixTimeFromSystem() >= _merchantNextRefreshUnix)
		{
			GenerateMerchantStock();
		}
	}

	private void UpdateMerchantStockRefresh()
	{
		if (_merchantNextRefreshUnix > 0.0 && Time.GetUnixTimeFromSystem() >= _merchantNextRefreshUnix)
		{
			GenerateMerchantStock();
			PostSystemMessage(LocaleText.T("system.shop.auto_refreshed"), new Color(0.82f, 0.94f, 1.0f));
			_merchantShopPanel?.RefreshAll();
		}
	}

	private void GenerateMerchantStock()
	{
		_blacksmithStockItemIds.Clear();
		var equipmentIds = new List<string>();
		foreach (EquipmentSlot slot in new[] { EquipmentSlot.Helmet, EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Accessory })
		{
			foreach (EquipmentDefinition equipment in BuildCatalog.GetEquipmentDefinitions(slot))
			{
				if (!BuildCatalog.IsFreeItem(equipment.Id))
				{
					equipmentIds.Add(equipment.Id);
				}
			}
		}
		AddRandomItems(equipmentIds, _blacksmithStockItemIds, BlacksmithStockCount);

		_petShopStockNameKeys.Clear();
		var petKeys = new List<string>();
		foreach (PetShopOffer offer in PetShopOffers)
		{
			petKeys.Add(offer.MonsterNameKey);
		}
		AddRandomItems(petKeys, _petShopStockNameKeys, PetShopStockCount);

		_merchantNextRefreshUnix = Time.GetUnixTimeFromSystem() + MercenaryRefreshSeconds;
	}

	private void AddRandomItems(List<string> source, List<string> target, int count)
	{
		var available = new List<string>(source);
		for (int index = 0; index < count && available.Count > 0; index++)
		{
			int randomIndex = _mercenaryRng.RandiRange(0, available.Count - 1);
			target.Add(available[randomIndex]);
			available.RemoveAt(randomIndex);
		}
	}

	public List<ShopTradeEntry> GetShopBuyEntries(MerchantShopKind shopKind)
	{
		var entries = new List<ShopTradeEntry>();
		if (shopKind == MerchantShopKind.Blacksmith)
		{
			EnsureMerchantStock();
			foreach (string itemId in _blacksmithStockItemIds)
			{
				EquipmentDefinition equipment = BuildCatalog.GetEquipment(itemId);
				entries.Add(new ShopTradeEntry(equipment.Id, LocaleText.T(equipment.NameKey), LocaleText.T(equipment.SummaryKey), GetShopBuyPrice(equipment.Id)));
			}
		}
		else if (shopKind == MerchantShopKind.PetShop)
		{
			EnsureMerchantStock();
			foreach (string monsterNameKey in _petShopStockNameKeys)
			{
				PetShopOffer offer = GetPetShopOffer(monsterNameKey);
				if (string.IsNullOrWhiteSpace(offer.MonsterNameKey))
				{
					continue;
				}

				entries.Add(new ShopTradeEntry(
					offer.MonsterNameKey,
					LocaleText.T(offer.MonsterNameKey),
					GetPetShopDetail(offer),
					offer.Price,
					offer.Level,
					offer.MaxHealth,
					offer.Attack,
					offer.Defense));
			}
		}
		else
		{
			foreach (AttributeGemDefinition gem in BuildCatalog.GetAttributeGemDefinitions())
			{
				if (!BuildCatalog.IsFreeItem(gem.Id))
				{
					entries.Add(new ShopTradeEntry(gem.Id, LocaleText.T(gem.NameKey), LocaleText.T(gem.SummaryKey), GetShopBuyPrice(gem.Id)));
				}
			}

			foreach (SkillGemDefinition gem in BuildCatalog.GetSkillGemDefinitions())
			{
				if (!BuildCatalog.IsFreeItem(gem.Id))
				{
					entries.Add(new ShopTradeEntry(gem.Id, LocaleText.T(gem.NameKey), LocaleText.T(gem.SummaryKey), GetShopBuyPrice(gem.Id)));
				}
			}

			foreach (string materialId in GetShopMaterialIds())
			{
				entries.Add(new ShopTradeEntry(materialId, GetInventoryItemDisplayName(materialId), LocaleText.T("shop.detail.material"), GetShopBuyPrice(materialId)));
			}
		}

		return entries;
	}

	public List<ShopTradeEntry> GetShopSellEntries(MerchantShopKind shopKind)
	{
		var entries = new List<ShopTradeEntry>();
		foreach (KeyValuePair<string, int> item in _inventoryItems)
		{
			if (item.Value <= 0 || !CanTradeInShop(shopKind, item.Key))
			{
				continue;
			}

			string detail = LocaleText.F("shop.sell.count", item.Value);
			entries.Add(new ShopTradeEntry(item.Key, GetInventoryItemDisplayName(item.Key), detail, GetShopSellPrice(item.Key)));
		}

		entries.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal));
		return entries;
	}

	public bool TryBuyShopItem(MerchantShopKind shopKind, string itemId, int price)
	{
		int safePrice = Mathf.Max(price, 1);
		if (!CanTradeInShop(shopKind, itemId))
		{
			return false;
		}

		if (Gold < safePrice)
		{
			PostSystemMessage(LocaleText.F("system.shop.not_enough_gold", safePrice, Gold), new Color(1.0f, 0.62f, 0.48f));
			return false;
		}

		Gold -= safePrice;
		if (shopKind == MerchantShopKind.PetShop)
		{
			if (!TryReceivePurchasedPet(itemId, safePrice))
			{
				Gold += safePrice;
				return false;
			}

			_inventoryPanel.RefreshAll();
			_merchantShopPanel.RefreshAll();
			return true;
		}

		AddInventoryItem(itemId);
		PostSystemMessage(LocaleText.F("system.shop.bought", GetInventoryItemDisplayName(itemId), safePrice, Gold), new Color(1.0f, 0.86f, 0.46f));
		_merchantShopPanel.RefreshAll();
		return true;
	}

	private bool TryReceivePurchasedPet(string monsterNameKey, int price)
	{
		PetShopOffer offer = GetPetShopOffer(monsterNameKey);
		if (string.IsNullOrWhiteSpace(offer.MonsterNameKey))
		{
			return false;
		}

		if (GetParent() is not World world)
		{
			return false;
		}

		SimpleActor actor = world.SpawnPurchasedPet(offer.MonsterNameKey, offer.Level, offer.MaxHealth, offer.Attack, offer.Defense);
		actor.ClearBuildLoadout();
		if (!_capturedCollection.Contains(actor))
		{
			_capturedCollection.Add(actor);
		}

		actor.Capture(this);
		PostSystemMessage(LocaleText.F("system.shop.bought_pet", actor.LocalizedDisplayName, price, Gold), new Color(0.64f, 1.0f, 0.82f));

		if (_activeParty.Count < ActivePartyLimit)
		{
			DeployCompanion(actor, false);
		}
		else
		{
			actor.StoreInCollection();
		}

		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		return true;
	}

	public bool TrySellShopItem(MerchantShopKind shopKind, string itemId, int price)
	{
		int safePrice = Mathf.Max(price, 1);
		if (!CanTradeInShop(shopKind, itemId) || GetInventoryCount(itemId) <= 0)
		{
			return false;
		}

		RemoveInventoryItemSilently(itemId, 1);
		Gold += safePrice;
		PostSystemMessage(LocaleText.F("system.shop.sold", GetInventoryItemDisplayName(itemId), safePrice, Gold), new Color(0.86f, 1.0f, 0.62f));
		_inventoryPanel.RefreshAll();
		_merchantShopPanel.RefreshAll();
		return true;
	}

	private void RestoreMerchantStock(PlayerSaveData data)
	{
		_blacksmithStockItemIds.Clear();
		foreach (string itemId in data.BlacksmithStockItemIds)
		{
			if (!string.IsNullOrWhiteSpace(itemId) && BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment && !_blacksmithStockItemIds.Contains(itemId))
			{
				_blacksmithStockItemIds.Add(itemId);
			}
		}

		_petShopStockNameKeys.Clear();
		foreach (string nameKey in data.PetShopStockNameKeys)
		{
			if (!string.IsNullOrWhiteSpace(nameKey) && IsPetShopItem(nameKey) && !_petShopStockNameKeys.Contains(nameKey))
			{
				_petShopStockNameKeys.Add(nameKey);
			}
		}

		_merchantNextRefreshUnix = data.MerchantNextRefreshUnix;
		EnsureMerchantStock();
	}

	private static bool CanTradeInShop(MerchantShopKind shopKind, string itemId)
	{
		if (shopKind == MerchantShopKind.PetShop)
		{
			return IsPetShopItem(itemId);
		}

		if (shopKind == MerchantShopKind.Blacksmith)
		{
			return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment;
		}

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return true;
		}

		InventoryItemKind kind = BuildCatalog.GetItemKind(itemId);
		return kind is InventoryItemKind.AttributeGem or InventoryItemKind.SkillGem;
	}

	private static int GetShopBuyPrice(string itemId)
	{
		if (IsPetShopItem(itemId))
		{
			foreach (PetShopOffer offer in PetShopOffers)
			{
				if (offer.MonsterNameKey == itemId)
				{
					return offer.Price;
				}
			}
		}

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return itemId switch
			{
				"loot.dragon_scale" => 95,
				"loot.water_core" => 70,
				"loot.red_horn" => 62,
				"loot.venom_sac" => 55,
				"loot.sharp_claw" => 42,
				"loot.beast_hide" => 34,
				"loot.small_bone" => 28,
				"loot.slime_mucus" => 24,
				"loot.soft_fur" => 22,
				"loot.insect_wing" => 30,
				_ => 30,
			};
		}

		return BuildCatalog.GetItemKind(itemId) switch
		{
			InventoryItemKind.Equipment => 120 + GetEquipmentPriceBonus(itemId),
			InventoryItemKind.AttributeGem => 90,
			InventoryItemKind.SkillGem => 115,
			_ => 50,
		};
	}

	private static int GetShopSellPrice(string itemId)
	{
		return Mathf.Max(Mathf.RoundToInt(GetShopBuyPrice(itemId) * 0.45f), 1);
	}

	private static int GetEquipmentPriceBonus(string itemId)
	{
		EquipmentDefinition equipment = BuildCatalog.GetEquipment(itemId);
		return equipment.MaxHealthBonus * 2
			+ equipment.AttackBonus * 8
			+ equipment.DefenseBonus * 7
			+ Mathf.RoundToInt(equipment.AttackRangeBonus * 18.0f)
			+ Mathf.RoundToInt(equipment.MoveSpeedBonus * 180.0f)
			+ Mathf.RoundToInt(equipment.CritChanceBonus * 300.0f)
			+ equipment.SocketCount * 45;
	}

	private static string[] GetShopMaterialIds()
	{
		return new[]
		{
			"loot.slime_mucus",
			"loot.beast_hide",
			"loot.sharp_claw",
			"loot.soft_fur",
			"loot.small_bone",
			"loot.insect_wing",
			"loot.red_horn",
			"loot.venom_sac",
			"loot.water_core",
			"loot.dragon_scale",
			"loot.cracked_core",
		};
	}

	private static bool IsPetShopItem(string itemId)
	{
		foreach (PetShopOffer offer in PetShopOffers)
		{
			if (offer.MonsterNameKey == itemId)
			{
				return true;
			}
		}

		return false;
	}

	private static PetShopOffer GetPetShopOffer(string itemId)
	{
		foreach (PetShopOffer offer in PetShopOffers)
		{
			if (offer.MonsterNameKey == itemId)
			{
				return offer;
			}
		}

		return default;
	}

	private static string GetPetShopDetail(PetShopOffer offer)
	{
		string combatRole = MonsterSpeciesCatalog.Current.GetDefaultRole(offer.MonsterNameKey);
		string roleKey = combatRole switch
		{
			"Tank" => "role.tank",
			"Ranged" => "role.ranged",
			"Support" => "role.support",
			_ => "role.dps",
		};
		string rangeKey = combatRole is "Ranged" or "Support" ? "shop.pet.range.ranged" : "shop.pet.range.melee";
		return LocaleText.F(
			"shop.detail.pet_stats",
			offer.Level,
			LocaleText.T(roleKey),
			offer.MaxHealth,
			offer.Attack,
			offer.Defense,
			LocaleText.T(rangeKey));
	}

	private SimpleActor? GetNearestMerchantShopkeeper(out MerchantShopKind shopKind)
	{
		shopKind = MerchantShopKind.ItemShop;
		if (!IsInCityMap())
		{
			return null;
		}

		SimpleActor? nearest = null;
		float bestDistance = MerchantInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !TryGetMerchantShopKind(actor, out MerchantShopKind candidateKind) || !actor.IsActiveWorldTarget)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = actor;
				shopKind = candidateKind;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private static bool IsMerchantShopkeeper(SimpleActor actor)
	{
		return TryGetMerchantShopKind(actor, out _);
	}

	private static bool TryGetMerchantShopKind(SimpleActor actor, out MerchantShopKind shopKind)
	{
		if (actor.DisplayName == "name.npc.blacksmith")
		{
			shopKind = MerchantShopKind.Blacksmith;
			return true;
		}

		if (actor.DisplayName == "name.npc.item_merchant")
		{
			shopKind = MerchantShopKind.ItemShop;
			return true;
		}

		if (actor.DisplayName == "name.npc.pet_trainer")
		{
			shopKind = MerchantShopKind.PetShop;
			return true;
		}

		shopKind = MerchantShopKind.ItemShop;
		return false;
	}

}
