using System.Collections.Generic;

public static class ElementChart
{
	public const float StrongMultiplier = 1.5f;
	public const float WeakMultiplier = 0.75f;

	private static readonly Dictionary<string, string> StrongAgainst = new()
	{
		["fire"] = "ice",
		["ice"] = "wind",
		["wind"] = "lightning",
		["lightning"] = "water",
		["water"] = "fire",
		["poison"] = "physical",
		["light"] = "dark",
		["dark"] = "light",
	};

	public static float GetMultiplier(string attackElement, string defenseElement)
	{
		if (string.IsNullOrWhiteSpace(attackElement) || string.IsNullOrWhiteSpace(defenseElement) || attackElement == defenseElement)
		{
			return 1.0f;
		}

		if (StrongAgainst.TryGetValue(attackElement, out string? strongTarget) && strongTarget == defenseElement)
		{
			return StrongMultiplier;
		}

		if (StrongAgainst.TryGetValue(defenseElement, out string? defenderStrongTarget) && defenderStrongTarget == attackElement)
		{
			return WeakMultiplier;
		}

		return 1.0f;
	}
}
