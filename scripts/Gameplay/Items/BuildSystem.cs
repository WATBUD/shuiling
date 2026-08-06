using Godot;
using System.Collections.Generic;

// Build definition types moved to BuildDefinitions.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

public static partial class BuildCatalog
{
	// Three distinct combat behaviors: fully independent auto targeting, player-command
	// priority with automatic fallback, and manual designated-target-only combat.
	public const string AiManualOnly = "manual";
	public const string AiAttackNearest = "independent";
	public const string AiCommandPriority = "command_priority";
	private const string LegacyAiAttackNearest = "attack_nearest";

	private static readonly Dictionary<string, CompanionIdentity> Identities = new()
	{
		["identity.water_spirit"] = new CompanionIdentity
		{
			Id = "identity.water_spirit",
			PassiveKeys = new[] { "identity.passive.water_damage", "identity.passive.water_aoe", "identity.passive.vitality" },
			UniqueSkillKeys = new[] { "identity.skill.water_cannon" },
			MaxHealthBonus = 18,
			AttackMultiplier = 1.04f,
			DefenseBonus = 3,
			AttackRangeBonus = 0.8f,
			ElementAffinityId = "water",
			ElementAffinityDamageMultiplier = 1.30f,
		},
		["identity.wolf"] = new CompanionIdentity
		{
			Id = "identity.wolf",
			PassiveKeys = new[] { "identity.passive.move_speed", "identity.passive.crit_rate" },
			UniqueSkillKeys = new[] { "identity.skill.bite", "identity.skill.howl" },
			AttackBonus = 4,
			MoveSpeedMultiplier = 1.20f,
			CritChanceBonus = 0.10f,
			AttackCooldownMultiplier = 0.94f,
		},
		["identity.dragon"] = new CompanionIdentity
		{
			Id = "identity.dragon",
			PassiveKeys = new[] { "identity.passive.fire_damage", "identity.passive.vitality", "identity.passive.attack_range" },
			UniqueSkillKeys = new[] { "identity.skill.dragon_breath" },
			MaxHealthMultiplier = 1.18f,
			AttackMultiplier = 1.12f,
			DefenseBonus = 8,
			AttackRangeBonus = 1.2f,
			DetectionRadiusBonus = 4.0f,
			ElementAffinityId = "fire",
			ElementAffinityDamageMultiplier = 1.25f,
		},
		["identity.redhorn"] = new CompanionIdentity
		{
			Id = "identity.redhorn",
			PassiveKeys = new[] { "identity.passive.power_strike", "identity.passive.thick_hide" },
			UniqueSkillKeys = new[] { "identity.skill.horn_crash" },
			MaxHealthMultiplier = 1.12f,
			DefenseMultiplier = 1.12f,
			AttackBonus = 3,
			AttackCooldownMultiplier = 1.04f,
		},
		["identity.venom_imp"] = new CompanionIdentity
		{
			Id = "identity.venom_imp",
			PassiveKeys = new[] { "identity.passive.poison_mastery", "identity.passive.agility" },
			UniqueSkillKeys = new[] { "identity.skill.venom_spit" },
			AttackBonus = 5,
			MoveSpeedMultiplier = 1.08f,
			ElementAffinityId = "poison",
			ElementAffinityDamageMultiplier = 1.22f,
		},
		["identity.guardian"] = new CompanionIdentity
		{
			Id = "identity.guardian",
			PassiveKeys = new[] { "identity.passive.guard_oath" },
			UniqueSkillKeys = new[] { "identity.skill.guardian_stance" },
			MaxHealthBonus = 24,
			DefenseBonus = 6,
		},
		["identity.traveler"] = new CompanionIdentity
		{
			Id = "identity.traveler",
			PassiveKeys = new[] { "identity.passive.adaptable" },
			UniqueSkillKeys = new[] { "identity.skill.quick_order" },
			MaxHealthBonus = 10,
			AttackBonus = 2,
			DefenseBonus = 2,
			MoveSpeedMultiplier = 1.04f,
		},
	};

	private static readonly Dictionary<string, string> IdentityByActorName = new()
	{
		["name.monster.slime"] = "identity.water_spirit",
		["name.monster.water_spirit"] = "identity.water_spirit",
		["name.monster.wolf"] = "identity.wolf",
		["name.monster.dragon"] = "identity.dragon",
		["name.monster.redhorn"] = "identity.redhorn",
		["name.monster.imp"] = "identity.venom_imp",
		["name.npc.guard"] = "identity.guardian",
	};

	// 種族分類：把每個 identity(物種) 歸到一個上層種族群組，用於陣盤的種族羈絆加成。
	private static readonly Dictionary<string, string> RaceByIdentity = new()
	{
		["identity.traveler"] = "race.human",
		["identity.guardian"] = "race.human",
		["identity.wolf"] = "race.beast",
		["identity.redhorn"] = "race.beast",
		["identity.dragon"] = "race.dragon",
		["identity.venom_imp"] = "race.demon",
		["identity.water_spirit"] = "race.spirit",
	};

	private const string DefaultRaceId = "race.human";

	private static readonly List<EquipmentDefinition> Equipment = ItemCatalogLoader.LoadEquipment();

	private static readonly List<AttributeGemDefinition> AttributeGems = new()
	{
		new AttributeGemDefinition { Id = "gem.attribute.none", NameKey = "gem.attribute.none", SummaryKey = "gem.summary.none", ElementId = "physical", ElementNameKey = "element.physical", AttackColor = new Color(1.0f, 0.54f, 0.24f, 0.92f) },
	};

	private static readonly List<SkillGemDefinition> SkillGems = ItemCatalogLoader.LoadCoreSkills();

	private static readonly List<AttackModeDefinition> AttackModes = new()
	{
		// Command priority is the safe default and fallback: companions obey explicit
		// orders while continuing to defend the party when no target is designated.
		new AttackModeDefinition { Id = AiCommandPriority, NameKey = "attack_mode.command_priority", BehaviorId = AiCommandPriority },
		new AttackModeDefinition { Id = AiAttackNearest, NameKey = "attack_mode.independent", BehaviorId = AiAttackNearest },
		new AttackModeDefinition { Id = AiManualOnly, NameKey = "attack_mode.manual", BehaviorId = AiManualOnly },
	};

	// Identity/race/element lookups and starter-loadout creation moved to BuildCatalog.Data.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Companion/player stat calculation moved to BuildCatalog.Calculation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// ── 精煉星等（Refinement stars）──────────────────────────────────────────
	// 星等直接編碼在物品 id 尾端（例如 "equip.weapon.sword#3" = 3★），因此背包堆疊、
	// 已裝備欄位、以及存檔全是字串就能自動保存，不需改資料結構。0★ 維持原本純 id。
	public const int MaxEquipmentStars = EquipmentConfig.MaxStars;
	public const float EquipmentStarBonusPerStar = EquipmentConfig.StarBonusPerStar;
	private const char EquipmentStarSeparator = '#';

	// Equipment star id codec moved to BuildCatalog.StarCodec.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Equipped jump-power calculation moved to BuildCatalog.Calculation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Equipment star display suffix moved to BuildCatalog.StarCodec.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Equipment/gem/attack-mode definition lookups and item-catalogue queries moved to BuildCatalog.Data.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Consumables (usable bag items). Town Portal Scroll returns the player to
	// the city from the wild (emergency retreat). Keyed id -> name locale key.
	public const string TownPortalScrollId = "consumable.town_portal";

	private static readonly Dictionary<string, string> Consumables = new();

	static BuildCatalog()
	{
		foreach (ConsumableDefinition item in ItemCatalogLoader.LoadConsumables())
		{
			Consumables[item.Id] = item.NameKey;
		}
	}

	// Item unique-id, name-key, and kind lookups plus equipment/gem cyclers moved to BuildCatalog.Data.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	public const int MaxSkillGemLevel = 5;

	// --- Core slots (level-gated) ---
	// Index 0 holds the active attack core, which also defines its damage element.
	// Indices 1..6 hold behavior/stat support cores.
	public const int MainCoreUnlockLevel = CoreConfig.MainCoreUnlockLevel;

	// One fixed main attack core (index 0) plus six extension support cores (1..6).
	// The historical name is retained because this value is serialized as the skill
	// core array length throughout the existing save system.
	public const int SupportCoreSlotCount = CoreConfig.SupportCoreSlotCount;
	public const int AccessorySlotCount = 4;

	// Unlock levels for the core skill, then support cores 1 through 6.
	private static readonly int[] SupportCoreUnlockLevels = CoreConfig.SupportCoreUnlockLevels;

	// Core-slot unlock queries and skill-gem role predicates moved to BuildCatalog.Data.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Skill-gem upgrade cost moved to BuildCatalog.Calculation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Skill-gem/attack-mode cyclers and localized build summaries moved to BuildCatalog.Data.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Weapon/equipment/skill-gem stat application moved to BuildCatalog.Calculation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

}
