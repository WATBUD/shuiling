using System.Collections.Generic;

// Fixed visual identities for functional city NPCs.
public static class CityNpcConfig
{
	private static readonly Dictionary<string, string> ShopModels = new()
	{
		["name.npc.blacksmith"] = ExternalModelLibrary.KenneyBlockyRoot + "character-a.glb",
		["name.npc.refiner"] = ExternalModelLibrary.KenneyBlockyRoot + "character-i.glb",
		["name.npc.core_enhancer"] = ExternalModelLibrary.KenneyBlockyRoot + "character-i.glb",
		["name.npc.pet_trainer"] = ExternalModelLibrary.KenneyBlockyRoot + "character-m.glb",
		["name.npc.item_merchant"] = ExternalModelLibrary.KenneyBlockyRoot + "character-p.glb",
		["name.npc.warehouse_keeper"] = ExternalModelLibrary.KenneyBlockyRoot + "character-q.glb",
		["name.npc.gacha"] = ExternalModelLibrary.KenneyBlockyRoot + "character-p.glb",
	};

	public static string GetShopModel(string npcNameKey)
	{
		return ShopModels.TryGetValue(npcNameKey, out string? path) ? path : string.Empty;
	}
}
