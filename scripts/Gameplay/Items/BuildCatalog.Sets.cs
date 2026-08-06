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
		string theme = GetEquipmentSetTheme(loadout.HelmetId);
		if (string.IsNullOrEmpty(theme))
		{
			return null;
		}

		string[] worn = { loadout.HelmetId, loadout.WeaponId, loadout.ArmorId, loadout.BootsId, loadout.AccessoryId };
		foreach (string id in worn)
		{
			if (GetEquipmentSetTheme(id) != theme)
			{
				return null;
			}
		}

		return SetsByTheme.TryGetValue(theme, out EquipmentSetJson? set) ? set : null;
	}

	// Locale name key of the active set, or empty when no full set is worn.
	public static string GetActiveEquipmentSetNameKey(CompanionBuildLoadout loadout)
	{
		return GetActiveEquipmentSet(loadout)?.NameKey ?? string.Empty;
	}

	// A full set is five matching pieces.
	public const int EquipmentSetSize = 5;

	// Progress toward the best (most-worn) set in this loadout: the dominant set and
	// how many of the five slots belong to it (0 if no set pieces are worn).
	public static (EquipmentSetJson? Set, int Count) GetEquipmentSetProgress(CompanionBuildLoadout loadout)
	{
		string[] worn = { loadout.HelmetId, loadout.WeaponId, loadout.ArmorId, loadout.BootsId, loadout.AccessoryId };
		var counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
		foreach (string id in worn)
		{
			string theme = GetEquipmentSetTheme(id);
			if (!string.IsNullOrEmpty(theme) && SetsByTheme.ContainsKey(theme))
			{
				counts.TryGetValue(theme, out int n);
				counts[theme] = n + 1;
			}
		}

		string bestTheme = string.Empty;
		int bestCount = 0;
		foreach (KeyValuePair<string, int> pair in counts)
		{
			if (pair.Value > bestCount)
			{
				bestCount = pair.Value;
				bestTheme = pair.Key;
			}
		}

		return bestCount == 0 ? (null, 0) : (SetsByTheme[bestTheme], bestCount);
	}

	// The set an individual equipment piece belongs to (for tooltips), or null.
	public static EquipmentSetJson? GetSetForEquipment(string equipmentId)
	{
		string theme = GetEquipmentSetTheme(equipmentId);
		return string.IsNullOrEmpty(theme) ? null : (SetsByTheme.TryGetValue(theme, out EquipmentSetJson? set) ? set : null);
	}
}
