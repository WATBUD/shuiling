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

	// The next wild biome in the fixed chain, or "" past the last one.
	public string GetNextWildMapId(string mapId)
	{
		for (int index = 0; index < WildMaps.Length - 1; index++)
		{
			if (WildMaps[index].Id == mapId)
			{
				return WildMaps[index + 1].Id;
			}
		}

		return string.Empty;
	}

	// City fast-travel rule: from the main city you may travel to any visited wild
	// map (plus the first map as the always-open entry). Returning to the city is
	// always allowed from anywhere. You can't fast-travel to where you already are.
	public bool CanFastTravelTo(string mapId)
	{
		if (mapId == _activeMapId)
		{
			return false;
		}

		if (mapId == "city")
		{
			return true;
		}

		if (!IsWildMapId(mapId))
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
