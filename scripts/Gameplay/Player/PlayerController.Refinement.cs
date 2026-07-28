using Godot;
using System.Collections.Generic;

// 精煉系統：花費金幣 + 對應階級的強化水晶，把「背包內未裝備的裝備」提升星等（最高 10★）。
// 星等直接編碼在物品 id 尾端（見 BuildCatalog），因此背包堆疊與存檔全用字串自動保存。
public partial class PlayerController
{
	private readonly RandomNumberGenerator _refineRng = new();

	public readonly struct RefinementQuote
	{
		public bool CanRefine { get; init; }
		public string ItemId { get; init; }
		public string BaseId { get; init; }
		public int CurrentStars { get; init; }
		public int TargetStars { get; init; }
		public string CrystalId { get; init; }
		public int CrystalCount { get; init; }
		public int Gold { get; init; }
		public int SuccessPercent { get; init; }
	}

	private static int RefineGoldCost(int targetStars)
	{
		return 200 * Mathf.Max(targetStars, 1);
	}

	// 列出背包內所有「裝備類」的堆疊 id（含不同星等各自成堆）。已裝備的不在背包中，自然不會列出。
	public List<string> GetRefinableBagEquipmentIds()
	{
		var ids = new List<string>();
		foreach (KeyValuePair<string, int> entry in _inventoryItems)
		{
			if (entry.Value > 0 && BuildCatalog.GetItemKind(entry.Key) == InventoryItemKind.Equipment)
			{
				ids.Add(entry.Key);
			}
		}

		ids.Sort(System.StringComparer.Ordinal);
		return ids;
	}

	public RefinementQuote GetRefinementQuote(string itemId)
	{
		int currentStars = BuildCatalog.GetEquipmentStars(itemId);
		bool isEquipment = BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment;
		bool canRefine = isEquipment && currentStars < BuildCatalog.MaxEquipmentStars;
		int targetStars = currentStars + 1;
		return new RefinementQuote
		{
			CanRefine = canRefine,
			ItemId = itemId,
			BaseId = BuildCatalog.GetBaseEquipmentId(itemId),
			CurrentStars = currentStars,
			TargetStars = targetStars,
			CrystalId = MonsterLootCatalog.GetEnhanceCrystalId(targetStars),
			CrystalCount = targetStars,
			Gold = RefineGoldCost(targetStars),
			// 成功率 = 100 - 目前星等×10：1★=100%、2★=90%…10★=10%。
			SuccessPercent = Mathf.Clamp(100 - currentStars * 10, 10, 100),
		};
	}

	public bool CanAffordRefinement(RefinementQuote quote)
	{
		return quote.CanRefine
			&& GetInventoryCount(quote.ItemId) > 0
			&& Gold >= quote.Gold
			&& GetInventoryCount(quote.CrystalId) >= quote.CrystalCount;
	}

	// 精煉一件背包裝備：無論成敗都消耗金幣與水晶；成功 +1★，失敗維持原星等（不降階）。
	public bool TryRefineBagEquipment(string itemId)
	{
		RefinementQuote quote = GetRefinementQuote(itemId);
		if (!quote.CanRefine)
		{
			PostSystemMessage(LocaleText.T("system.refine.max"), new Color(1.0f, 0.82f, 0.42f), GameMessageChannel.Loot);
			return false;
		}

		if (GetInventoryCount(quote.ItemId) <= 0)
		{
			return false;
		}

		if (Gold < quote.Gold || GetInventoryCount(quote.CrystalId) < quote.CrystalCount)
		{
			PostSystemMessage(
				LocaleText.F("system.refine.not_enough", quote.Gold, quote.CrystalCount, GetInventoryItemDisplayName(quote.CrystalId)),
				new Color(1.0f, 0.62f, 0.48f),
				GameMessageChannel.Loot);
			return false;
		}

		Gold -= quote.Gold;
		TryConsumeInventoryItem(quote.CrystalId, quote.CrystalCount);
		RemoveInventoryItemSilently(quote.ItemId, 1);

		bool success = _refineRng.Randf() * 100.0f < quote.SuccessPercent;
		string resultId = success
			? BuildCatalog.MakeRefinedEquipmentId(quote.BaseId, quote.TargetStars)
			: quote.ItemId;
		AddInventoryItemSilently(resultId, 1);

		string baseName = LocaleText.T(BuildCatalog.GetItemNameKey(quote.BaseId));
		if (success)
		{
			PostSystemMessage(LocaleText.F("system.refine.success", baseName, quote.TargetStars), new Color(0.62f, 1.0f, 0.68f), GameMessageChannel.Loot);
		}
		else
		{
			PostSystemMessage(LocaleText.F("system.refine.fail", baseName, quote.CurrentStars), new Color(1.0f, 0.62f, 0.48f), GameMessageChannel.Loot);
		}

		_inventoryPanel?.RefreshAll();
		_refinementPanel?.RefreshAll();
		return success;
	}

	// 靜默加入背包（不跳「撿到」訊息），用於精煉產出的結果堆疊。
	private void AddInventoryItemSilently(string itemId, int amount)
	{
		if (BuildCatalog.IsFreeItem(itemId)
			|| BuildCatalog.IsRetiredSkillCore(itemId)
			|| BuildCatalog.IsRetiredAttributeGem(itemId))
		{
			return;
		}

		_inventoryItems.TryGetValue(itemId, out int current);
		_inventoryItems[itemId] = Mathf.Max(current + amount, 0);
	}
}
