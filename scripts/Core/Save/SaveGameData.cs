using System.Collections.Generic;

public sealed class SaveGameData
{
	public int Version { get; set; } = 1;
	public string SavedAt { get; set; } = string.Empty;
	public string ActiveMapId { get; set; } = "city";
	public SaveVector3 PlayerPosition { get; set; } = new();
	public PlayerSaveData Player { get; set; } = new();
}

public sealed class PlayerSaveData
{
	public int Level { get; set; } = 1;
	public int Experience { get; set; }
	public int MaxHealth { get; set; } = 150;
	public int CurrentHealth { get; set; } = 150;
	public int Attack { get; set; } = 16;
	public int Defense { get; set; } = 10;
	public int Gold { get; set; }
	public string CameraMode { get; set; } = "god_view";
	public float DamageTextScale { get; set; } = 1.0f;
	public Dictionary<string, int> InventoryItems { get; set; } = new();
	public List<string> AcceptedNpcQuestNames { get; set; } = new();
	public List<string> CompletedNpcQuestNames { get; set; } = new();
	public double MercenaryNextRefreshUnix { get; set; }
	public double MerchantNextRefreshUnix { get; set; }
	public List<string> BlacksmithStockItemIds { get; set; } = new();
	public List<string> PetShopStockNameKeys { get; set; } = new();
	public List<MercenaryOfferSaveData> MercenaryOffers { get; set; } = new();
	public List<ActorSaveData> Companions { get; set; } = new();
	public List<int> ActivePartyIndexes { get; set; } = new();
}

public sealed class MercenaryOfferSaveData
{
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = "name.mercenary.vanguard";
	public string RoleNameKey { get; set; } = "role.dps";
	public string CombatRole { get; set; } = "DPS";
	public string SummaryKey { get; set; } = "mercenary.summary.duelist";
	public int Level { get; set; } = 1;
	public int Cost { get; set; } = 100;
	public int MaxHealth { get; set; } = 100;
	public int Attack { get; set; } = 10;
	public int Defense { get; set; } = 6;
}

public sealed class ActorSaveData
{
	public string ActorKind { get; set; } = "monster";
	public string DisplayName { get; set; } = "name.actor.traveler";
	public int Level { get; set; } = 1;
	public int MaxHealth { get; set; } = 100;
	public int CurrentHealth { get; set; } = 100;
	public int Attack { get; set; } = 10;
	public int Defense { get; set; } = 6;
	public float MoveSpeed { get; set; } = 7.0f;
	public int ExperienceReward { get; set; } = 6;
	public int GoldReward { get; set; } = 2;
	public int Experience { get; set; }
	public int EvolutionStage { get; set; }
	public string SpecialAbility { get; set; } = "ability.none";
	public int AbilityRank { get; set; } = 1;
	public string CombatRole { get; set; } = "DPS";
	public string Personality { get; set; } = "personality.calm";
	public string PassiveAbility { get; set; } = "ability.none";
	public int Affinity { get; set; } = 50;
	public string MoodStateId { get; set; } = string.Empty;
	public string AttackModeId { get; set; } = "command_priority";
	public CompanionBuildSaveData BuildLoadout { get; set; } = new();
}

public sealed class CompanionBuildSaveData
{
	public string HelmetId { get; set; } = "equip.helmet.traveler";
	public string WeaponId { get; set; } = "equip.weapon.sword";
	public string ArmorId { get; set; } = "equip.armor.scout";
	public string AccessoryId { get; set; } = "equip.accessory.swift_ring";
	public string AttributeGemId { get; set; } = "gem.attribute.none";
	public string[] SkillGemIds { get; set; } =
	{
		"gem.skill.none",
		"gem.skill.none",
		"gem.skill.none",
	};
	public int[] SkillGemLevels { get; set; } = { 1, 1, 1 };
}

public sealed class SaveVector3
{
	public float X { get; set; }
	public float Y { get; set; }
	public float Z { get; set; }
}
