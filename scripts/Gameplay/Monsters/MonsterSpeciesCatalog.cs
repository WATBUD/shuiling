using Godot;
using System.Collections.Generic;

public readonly record struct MonsterSpeciesDefinition(
	string NameKey,
	string DefaultRole,
	string PrimaryLootId,
	Color MarkerColor,
	string[] ModelPaths,
	string[] MapIds
);

public interface IMonsterSpeciesCatalog
{
	IReadOnlyList<MonsterSpeciesDefinition> All { get; }
	string[] GetNamePool(string mapId);
	string GetDefaultRole(string nameKey);
	string GetPrimaryLootId(string nameKey, bool isRangedCombatant, int level);
	Color GetMarkerColor(string nameKey);
	string[]? GetModelPaths(string nameKey);
}

public sealed class DefaultMonsterSpeciesCatalog : IMonsterSpeciesCatalog
{
	private static readonly string[] ForestMap = { "wild_forest" };
	private static readonly string[] MarshMap = { "wild_marsh" };
	private static readonly string[] BadlandsMap = { "wild_badlands" };

	public static DefaultMonsterSpeciesCatalog Instance { get; } = new();

	private readonly MonsterSpeciesDefinition[] _all =
	{
		new("name.monster.slime", "Support", "loot.slime_mucus", new Color(0.20f, 0.96f, 0.82f, 0.94f), new[]
		{
			"res://assets/models/monsters/slime_enemy_poly_pizza.glb",
			"res://assets/models/monsters/slime.gltf",
		}, MarshMap),
		new("name.monster.water_spirit", "Ranged", "loot.water_core", new Color(0.32f, 0.76f, 1.0f, 0.94f), System.Array.Empty<string>(), MarshMap),
		new("name.monster.redhorn", "Tank", "loot.red_horn", new Color(1.0f, 0.34f, 0.26f, 0.94f), System.Array.Empty<string>(), BadlandsMap),
		new("name.monster.hunter", "DPS", "loot.beast_hide", new Color(1.0f, 0.34f, 0.26f, 0.94f), System.Array.Empty<string>(), ForestMap),
		new("name.monster.wolf", "DPS", "loot.beast_hide", new Color(1.0f, 0.34f, 0.26f, 0.94f), System.Array.Empty<string>(), ForestMap),
		new("name.monster.imp", "Ranged", "loot.venom_sac", new Color(1.0f, 0.34f, 0.26f, 0.94f), System.Array.Empty<string>(), BadlandsMap),
		new("name.monster.dragon", "Ranged", "loot.dragon_scale", new Color(1.0f, 0.34f, 0.26f, 0.94f), System.Array.Empty<string>(), BadlandsMap),
		new("name.monster.rat", "DPS", "loot.soft_fur", new Color(0.62f, 0.48f, 0.36f, 0.94f), new[]
		{
			"res://assets/models/monsters/street_rat/street_rat_1k.gltf",
		}, new[] { "wild_forest", "wild_marsh" }),
		new("name.monster.fox", "DPS", "loot.beast_hide", new Color(1.0f, 0.48f, 0.20f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-fox.glb",
		}, ForestMap),
		new("name.monster.deer", "DPS", "loot.soft_fur", new Color(0.78f, 0.58f, 0.34f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-deer.glb",
		}, ForestMap),
		new("name.monster.bunny", "DPS", "loot.soft_fur", new Color(0.96f, 0.86f, 0.78f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-bunny.glb",
		}, ForestMap),
		new("name.monster.beaver", "DPS", "loot.soft_fur", new Color(0.58f, 0.34f, 0.18f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-beaver.glb",
		}, ForestMap),
		new("name.monster.boar", "DPS", "loot.beast_hide", new Color(0.58f, 0.34f, 0.18f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-hog.glb",
		}, ForestMap),
		new("name.monster.crab", "Tank", "loot.small_bone", new Color(1.0f, 0.34f, 0.26f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-crab.glb",
		}, MarshMap),
		new("name.monster.fish", "Ranged", "loot.small_bone", new Color(0.28f, 0.82f, 1.0f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-fish.glb",
		}, MarshMap),
		new("name.monster.caterpillar", "Support", "loot.insect_wing", new Color(0.44f, 0.92f, 0.28f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-caterpillar.glb",
		}, MarshMap),
		new("name.monster.bee", "Ranged", "loot.insect_wing", new Color(1.0f, 0.82f, 0.22f, 0.96f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-bee.glb",
		}, MarshMap),
		new("name.monster.lion", "DPS", "loot.beast_hide", new Color(0.95f, 0.66f, 0.26f, 0.96f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-lion.glb",
		}, BadlandsMap),
		new("name.monster.tiger", "DPS", "loot.beast_hide", new Color(1.0f, 0.42f, 0.12f, 0.96f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-tiger.glb",
		}, BadlandsMap),
		new("name.monster.bear", "Tank", "loot.beast_hide", new Color(0.84f, 0.92f, 1.0f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-polar.glb",
		}, BadlandsMap),
		new("name.monster.elephant", "Tank", "loot.small_bone", new Color(0.62f, 0.68f, 0.74f, 0.94f), new[]
		{
			"res://assets/models/pets/cube_pets/animal-elephant.glb",
		}, BadlandsMap),
	};

	private readonly Dictionary<string, MonsterSpeciesDefinition> _byName = new();

	private DefaultMonsterSpeciesCatalog()
	{
		foreach (MonsterSpeciesDefinition species in _all)
		{
			_byName[species.NameKey] = species;
		}
	}

	public IReadOnlyList<MonsterSpeciesDefinition> All => _all;

	public string[] GetNamePool(string mapId)
	{
		var names = new List<string>();
		foreach (MonsterSpeciesDefinition species in _all)
		{
			if (MatchesMap(species, mapId))
			{
				names.Add(species.NameKey);
			}
		}

		return names.Count > 0 ? names.ToArray() : GetAllNames();
	}

	public string GetDefaultRole(string nameKey)
	{
		return _byName.TryGetValue(nameKey, out MonsterSpeciesDefinition species)
			? species.DefaultRole
			: "DPS";
	}

	public string GetPrimaryLootId(string nameKey, bool isRangedCombatant, int level)
	{
		if (_byName.TryGetValue(nameKey, out MonsterSpeciesDefinition species))
		{
			return species.PrimaryLootId switch
			{
				"loot.soft_fur" => level >= 6 ? "loot.small_bone" : "loot.soft_fur",
				"loot.small_bone" => level >= 7 ? "loot.cracked_core" : "loot.small_bone",
				_ => species.PrimaryLootId,
			};
		}

		return isRangedCombatant ? "loot.venom_sac" : level % 2 == 0 ? "loot.sharp_claw" : "loot.beast_hide";
	}

	public Color GetMarkerColor(string nameKey)
	{
		return _byName.TryGetValue(nameKey, out MonsterSpeciesDefinition species)
			? species.MarkerColor
			: new Color(1.0f, 0.34f, 0.26f, 0.94f);
	}

	public string[]? GetModelPaths(string nameKey)
	{
		if (!_byName.TryGetValue(nameKey, out MonsterSpeciesDefinition species) || species.ModelPaths == null || species.ModelPaths.Length == 0)
		{
			return null;
		}

		return species.ModelPaths;
	}

	private string[] GetAllNames()
	{
		var names = new string[_all.Length];
		for (int index = 0; index < _all.Length; index++)
		{
			names[index] = _all[index].NameKey;
		}

		return names;
	}

	private static bool MatchesMap(MonsterSpeciesDefinition species, string mapId)
	{
		if (species.MapIds == null || species.MapIds.Length == 0)
		{
			return false;
		}

		foreach (string candidate in species.MapIds)
		{
			if (candidate == "*" || candidate == mapId)
			{
				return true;
			}
		}

		return false;
	}
}

public static class MonsterSpeciesCatalog
{
	public static IMonsterSpeciesCatalog Current { get; set; } = DefaultMonsterSpeciesCatalog.Instance;
}
