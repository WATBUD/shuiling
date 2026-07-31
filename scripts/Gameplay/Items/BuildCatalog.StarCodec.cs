using Godot;

public static partial class BuildCatalog
{
	public static int GetEquipmentStars(string id)
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

		return int.TryParse(id.Substring(index + 1), out int stars) ? Mathf.Clamp(stars, 0, MaxEquipmentStars) : 0;
	}

	public static string GetBaseEquipmentId(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return id;
		}

		int index = id.IndexOf(EquipmentStarSeparator);
		return index < 0 ? id : id.Substring(0, index);
	}

	public static string MakeRefinedEquipmentId(string baseId, int stars)
	{
		string root = GetBaseEquipmentId(baseId);
		int clamped = Mathf.Clamp(stars, 0, MaxEquipmentStars);
		return clamped <= 0 ? root : $"{root}{EquipmentStarSeparator}{clamped}";
	}

	public static float GetEquipmentStarMultiplier(string id)
	{
		return 1.0f + GetEquipmentStars(id) * EquipmentStarBonusPerStar;
	}

	// 顯示用的星等後綴，例如 " ★3"；0★ 回傳空字串。
	public static string GetStarSuffix(string id)
	{
		int stars = GetEquipmentStars(id);
		return stars > 0 ? $" ★{stars}" : string.Empty;
	}
}
