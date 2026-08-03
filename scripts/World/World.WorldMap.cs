using Godot;
using System.Collections.Generic;

// World-map / fast-travel state. Maps the player has physically stepped onto are
// remembered (persisted) so the M-key world map can fast-travel back to them
// from the main city. The first wild map always counts as an entry point so the
// player can bootstrap into the wild before anything is visited.
public partial class World
{
	private readonly HashSet<string> _visitedMapIds = new();

	// A node on the world map: one hub city + the ordered wild maps.
	public readonly record struct WorldMapNode(string Id, string NameKey, bool Visited, bool IsCurrent, bool CanTravel);

	private void MarkMapVisited(string mapId)
	{
		if (!string.IsNullOrEmpty(mapId) && (mapId == "city" || IsWildMapId(mapId)))
		{
			_visitedMapIds.Add(mapId);
		}
	}

	public bool HasVisitedMap(string mapId)
	{
		return _visitedMapIds.Contains(mapId);
	}

	// Map adjacency matching the world-map cross (forest is the hub). Single source
	// of truth for both the in-world portals and the panel's dashed links.
	private static readonly (string A, string B)[] MapConnections =
	{
		("city", "wild_forest"),
		("wild_forest", "wild_marsh"),
		("wild_forest", "wild_snow"),
		("wild_forest", "wild_badlands"),
		("wild_marsh", "wild_skeleton"),
	};

	// Maps directly reachable on foot from mapId (both directions of each link).
	public List<string> GetAdjacentMaps(string mapId)
	{
		var result = new List<string>();
		foreach ((string a, string b) in MapConnections)
		{
			if (a == mapId)
			{
				result.Add(b);
			}
			else if (b == mapId)
			{
				result.Add(a);
			}
		}

		return result;
	}

	private static string GetMapNameKey(string mapId)
	{
		foreach (WildMapDefinition wildMap in WildMaps)
		{
			if (wildMap.Id == mapId)
			{
				return wildMap.NameKey;
			}
		}

		return mapId == "city" ? "map.city" : mapId;
	}

	// Fast-travel rule: from the main city you may travel to any visited wild map
	// (plus the first map as the always-open entry). You can NOT fast-travel back
	// to the city — returning is only via the forest's south portal or a town
	// portal scroll. You can't fast-travel to where you already are.
	public bool CanFastTravelTo(string mapId)
	{
		if (mapId == _activeMapId || mapId == "city" || !IsWildMapId(mapId))
		{
			return false;
		}

		bool reachable = _visitedMapIds.Contains(mapId) || (WildMaps.Length > 0 && mapId == WildMaps[0].Id);
		return reachable && _activeMapId == "city";
	}

	// Ordered nodes for the world-map panel: the city hub then the wild chain.
	public IReadOnlyList<WorldMapNode> GetWorldMapNodes()
	{
		var nodes = new List<WorldMapNode>
		{
			new("city", "map.city", true, _activeMapId == "city", CanFastTravelTo("city")),
		};
		foreach (WildMapDefinition wildMap in WildMaps)
		{
			nodes.Add(new WorldMapNode(
				wildMap.Id,
				wildMap.NameKey,
				_visitedMapIds.Contains(wildMap.Id),
				_activeMapId == wildMap.Id,
				CanFastTravelTo(wildMap.Id)));
		}

		return nodes;
	}
}
