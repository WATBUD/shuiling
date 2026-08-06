using System.Collections.Generic;

// Equipment set (套裝) lookups. A set is identified by a "theme" — the material
// token in an item id (equip.helmet.iron → "iron", equip.weapon.iron_blade →
// "iron"). Wearing five pieces that all share the same themed set grants the
// set's bonus. Set data lives in configs/items/equipment_sets.json.
public static partial class BuildCatalog
{
	private static Dictionary<string, EquipmentSetJson>? _setsByTheme;

	private static Dictionary<string, EquipmentSetJson> SetsByTheme
	{
		get
		{
			if (_setsByTheme == null)
			{
				_setsByTheme = new Dictionary<string, EquipmentSetJson>(System.StringComparer.Ordinal);
				foreach (EquipmentSetJson set in ItemCatalogLoader.LoadEquipmentSets())
				{
					if (!string.IsNullOrEmpty(set.Theme))
					{
						_setsByTheme[set.Theme] = set;
					}
				}
			}

			return _setsByTheme;
		}
	}

	// The material/theme token of an equipment id (star suffix tolerated). Empty for
	// ids that don't follow the equip.<slot>.<theme>[_<noun>] shape or are ".none".
	public static string GetEquipmentSetTheme(string equipmentId)
	{
		string baseId = GetBaseEquipmentId(equipmentId);
		string[] parts = baseId.Split('.');
		if (parts.Length < 3 || parts[0] != "equip" || parts[2] == "none")
		{
			return string.Empty;
		}

		return parts[2].Split('_')[0];
	}

	// The active set for a loadout: non-null only when all five equipped pieces
	// share one theme that has a defined set.
	public static EquipmentSetJson? GetActiveEquipmentSet(CompanionBuildLoadout loadout)
	{
		(EquipmentSetJson? set, int count) = GetEquipmentSetProgress(loadout);
		return count >= EquipmentSetSize ? set : null;
	}

	// Locale name key of the active set, or empty when no full set is worn.
	public static string GetActiveEquipmentSetNameKey(CompanionBuildLoadout loadout)
	{
		return GetActiveEquipmentSet(loadout)?.NameKey ?? string.Empty;
	}

	// A full set is five slot types: helmet, weapon, armor, boots, and accessory.
	// The four accessory slots count as ONE piece (matched if any ring matches).
	public const int EquipmentSetSize = 5;

	// Progress toward the best (most-worn) set: the dominant set and how many of the
	// five slot types belong to it (0 if none).
	public static (EquipmentSetJson? Set, int Count) GetEquipmentSetProgress(CompanionBuildLoadout loadout)
	{
		var candidates = new HashSet<string>(System.StringComparer.Ordinal);
		foreach (string id in new[] { loadout.HelmetId, loadout.WeaponId, loadout.ArmorId, loadout.BootsId })
		{
			string theme = GetEquipmentSetTheme(id);
			if (!string.IsNullOrEmpty(theme) && SetsByTheme.ContainsKey(theme))
			{
				candidates.Add(theme);
			}
		}

		loadout.EnsureAccessorySlots();
		foreach (string id in loadout.AccessoryIds)
		{
			string theme = GetEquipmentSetTheme(id);
			if (!string.IsNullOrEmpty(theme) && SetsByTheme.ContainsKey(theme))
			{
				candidates.Add(theme);
			}
		}

		string bestTheme = string.Empty;
		int bestCount = 0;
		foreach (string theme in candidates)
		{
			int count = GetWornSetPieceCount(loadout, theme);
			if (count > bestCount)
			{
				bestCount = count;
				bestTheme = theme;
			}
		}

		return bestCount == 0 ? (null, 0) : (SetsByTheme[bestTheme], bestCount);
	}

	// Set-piece count for a theme: one per matching base slot (helmet/weapon/armor/
	// boots) plus one if ANY of the four accessory slots matches (accessories are a
	// single set piece however many rings you wear). Max EquipmentSetSize.
	public static int GetWornSetPieceCount(CompanionBuildLoadout loadout, string theme)
	{
		if (string.IsNullOrEmpty(theme))
		{
			return 0;
		}

		int count = 0;
		foreach (string id in new[] { loadout.HelmetId, loadout.WeaponId, loadout.ArmorId, loadout.BootsId })
		{
			if (GetEquipmentSetTheme(id) == theme)
			{
				count++;
			}
		}

		loadout.EnsureAccessorySlots();
		foreach (string id in loadout.AccessoryIds)
		{
			if (GetEquipmentSetTheme(id) == theme)
			{
				count++;
				break;
			}
		}

		return count;
	}

	// The set an individual equipment piece belongs to (for tooltips), or null.
	public static EquipmentSetJson? GetSetForEquipment(string equipmentId)
	{
		string theme = GetEquipmentSetTheme(equipmentId);
		return string.IsNullOrEmpty(theme) ? null : (SetsByTheme.TryGetValue(theme, out EquipmentSetJson? set) ? set : null);
	}
}
