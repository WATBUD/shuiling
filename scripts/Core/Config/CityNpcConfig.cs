using System.Collections.Generic;

// Fixed visual identities for functional city NPCs.
public static class CityNpcConfig
{
	private const string KenneyBlockyRoot =
		"res://assets/_downloads/kenney_blocky-characters_20/Models/GLB format/";

	private static readonly Dictionary<string, string> ShopModels = new()
	{
		["name.npc.blacksmith"] = KenneyBlockyRoot + "character-a.glb",
		["name.npc.refiner"] = KenneyBlockyRoot + "character-i.glb",
		["name.npc.pet_trainer"] = KenneyBlockyRoot + "character-m.glb",
		["name.npc.item_merchant"] = KenneyBlockyRoot + "character-p.glb",
		["name.npc.warehouse_keeper"] = KenneyBlockyRoot + "character-q.glb",
	};

	public static string GetShopModel(string npcNameKey)
	{
		return ShopModels.TryGetValue(npcNameKey, out string? path) ? path : string.Empty;
	}
}
