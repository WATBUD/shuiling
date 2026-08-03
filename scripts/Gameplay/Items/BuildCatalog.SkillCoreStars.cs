using Godot;

// Star enhancement for SKILL CORES, mirroring the equipment star codec but with
// its own tunable bonus (CoreEnhanceConfig). Stars ride on the item id as a "#N"
// suffix (same separator as equipment) so an enhanced core keeps its stars while
// sitting unequipped in the bag, when equipped into a loadout slot, and across
// save/load — no schema change. The existing per-slot SkillGemLevels system is
// untouched; stars are an independent, persistent axis.
public static partial class BuildCatalog
{
	public static int GetSkillCoreStars(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return 0;
		}

		int index = id.IndexOf(EquipmentStarSeparator);
		if (index < 0 || index + 1 >= id.Length)
		{
			return 0;
		}

		return int.TryParse(id.Substring(index + 1), out int stars)
			? Mathf.Clamp(stars, 0, CoreEnhanceConfig.MaxCoreStars)
			: 0;
	}

	public static string GetBaseSkillCoreId(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return id;
		}

		int index = id.IndexOf(EquipmentStarSeparator);
		return index < 0 ? id : id.Substring(0, index);
	}

	public static string MakeStarredSkillCoreId(string baseId, int stars)
	{
		string root = GetBaseSkillCoreId(baseId);
		int clamped = Mathf.Clamp(stars, 0, CoreEnhanceConfig.MaxCoreStars);
		return clamped <= 0 ? root : $"{root}{EquipmentStarSeparator}{clamped}";
	}

	// Multiplier applied to a core's stat bonuses: 1 + stars * per-star bonus.
	public static float GetSkillCoreStarMultiplier(string id)
	{
		return 1.0f + GetSkillCoreStars(id) * CoreEnhanceConfig.CoreStarBonusPerStar;
	}
}
