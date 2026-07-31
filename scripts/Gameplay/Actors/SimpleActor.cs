using Godot;
using System.Collections.Generic;

public partial class SimpleActor : CharacterBody3D
{
	private const float MinimumCompanionFormationDistance = 3.6f;
	private const float ExternalRootMotionStabilizeSeconds = 0.12f;
	private const int RearCompanionDustStartSlot = 4;
	private static readonly List<SimpleActor> ActiveActorRegistry = new();

	// Allocation-free actor lookup for hot gameplay paths. SceneTree group queries
	// construct a Godot array wrapper on every call and were used by every companion
	// several times per second.
	public static IReadOnlyList<SimpleActor> ActiveActors => ActiveActorRegistry;

	private enum SquadActivity
	{
		Follow,
		Guard,
		Scout,
		Gather,
		Roam,
		Rest,
	}

	[Export] public string ActorKind { get; set; } = "npc";
	[Export] public string MapId { get; set; } = "wild_forest";
	[Export] public float MoveSpeed { get; set; } = 7.0f;
	[Export] public float WanderRadius { get; set; } = 10.0f;
	[Export] public float ChaseRadius { get; set; } = 20.0f;
	[Export] public Vector3 HomePosition { get; set; } = Vector3.Zero;
	[Export] public string DisplayName { get; set; } = "name.actor.traveler";
	[Export] public int Level { get; set; } = 1;
	[Export] public int MaxHealth { get; set; } = 100;
	[Export] public int CurrentHealth { get; set; } = 100;
	[Export] public int Attack { get; set; } = 10;
	[Export] public int Defense { get; set; } = 6;
	[Export] public int ExperienceReward { get; set; } = 6;
	[Export] public int GoldReward { get; set; } = 2;
	[Export] public int Experience { get; set; }
	[Export] public int EvolutionStage { get; set; }
	[Export] public string SpecialAbility { get; set; } = "ability.none";
	[Export] public int AbilityRank { get; set; } = 1;
	[Export] public string CombatRole { get; set; } = "DPS";
	[Export] public string Personality { get; set; } = "personality.calm";
	[Export] public string PassiveAbility { get; set; } = "ability.none";
	[Export] public int Affinity { get; set; } = 50;
	[Export] public string MoodStateId { get; set; } = string.Empty;
	[Export] public string AttackModeId { get; set; } = BuildCatalog.AiManualOnly;
	[Export] public float DetectionRadius { get; set; } = 12.0f;
	[Export] public float AttackRange { get; set; } = 1.8f;
	[Export] public float AttackCooldown { get; set; } = 1.35f;
	[Export] public bool IsBoss { get; set; }
	[Export] public string BossNameKey { get; set; } = string.Empty;
	[Export] public string BossPrimaryLootId { get; set; } = string.Empty;
	// World Tier this actor was spawned at (docs/world_progression.md). Drives
	// the evolution-stage display name; stats are already baked in at spawn.
	[Export] public int WorldTier { get; set; } = 1;

	// Party/session instance this wild actor belongs to (0 = single-player / solo).
	// Same (MapId, WorldTier, GroupId) = same hunting-ground instance, so different
	// parties/solo players never share monsters (no kill-stealing).
	[Export] public int GroupId { get; set; }

	private readonly RandomNumberGenerator _rng = new();
	// Multiplayer puppet state: on clients, wild monsters are display-only
	// mirrors of the host's actors (no AI, no local damage — see World.Network.cs).
	private bool _isNetworkPuppet;
	private int _networkId = -1;
	private Vector3 _netTargetPosition;
	private float _netTargetYaw;
	private bool _isCaptured;
	private bool _isInActiveParty;
	private bool _isInWarehouseCollection;
	private bool _isMountedByPlayer;
	private bool _isDefeated;
	private bool _isAwaitingRecovery;
	private string _fallenMapId = string.Empty;
	private bool _isWorldMapActive = true;
	// True only when this actor shares the LOCAL player's instance (same map, tier
	// and party group). The host simulates other groups' monsters invisibly for
	// network streaming, and those must never chase or attack the local player —
	// otherwise the player is hit by unseen enemies from a parallel instance.
	private bool _engagesLocalPlayer = true;
	private uint _defaultCollisionLayer;
	private uint _defaultCollisionMask;
	private PlayerController? _followTarget;
	private int _followSlot;
	private float _gravity;
	private Vector3 _targetPosition = Vector3.Zero;
	private float _waitTime;
	private float _attackCooldownRemaining;
	private float _footstepEffectRemaining;
	private float _movementAnimationPhase;
	private float _externalAnimationLockRemaining;
	private string _externalAnimationState = string.Empty;
	private Tween? _attackPoseTween;
	private Node3D? _attackPoseTarget;
	private Vector3 _attackPoseBaseScale = Vector3.One;
	// Three complete turns at twice the former angular speed:
	// old speed = 2 turns / 0.55 s, so 3 turns take 0.4125 s.
	private const float WhirlwindSpinSeconds = 0.4125f;
	private const float WhirlwindSpinRadians = Mathf.Tau * 3.0f;
	private float _whirlwindSpinBaseYaw;
	private float _whirlwindSpinAngle;
	private float _whirlwindSpinRemaining;
	private SimpleActor? _combatTarget;
	private SimpleActor? _retaliationTarget;
	private Node3D? _cachedPlayerNode;
	private readonly Dictionary<string, Node3D?> _childNodeCache = new();
	private Node3D? _externalModelNode;
	private bool _externalModelLookupAttempted;
	private float _combatTargetSearchRemaining;
	private float _externalRootMotionStabilizeRemaining;
	private float _retaliationTargetRemaining;
	private float _specialControlCooldownRemaining;
	private Label3D? _nameplate;
	private Node3D? _followLagBubble;
	private string _petDialogueText = string.Empty;
	private float _petDialogueRemaining;
	private float _nextPetDialogueDelay = 7.0f;
	private bool _showingLagDialogue;
	private MeshInstance3D? _nameplateMarker;
	private MeshInstance3D? _nameplateHalo;
	private StandardMaterial3D? _nameplateMarkerMaterial;
	private StandardMaterial3D? _nameplateHaloMaterial;
	private static readonly string[] PetDailyQuotes =
	{
		"今天也要帥氣地冒險！",
		"寶箱在哪裡？我聞到了！",
		"放心，背後交給我！",
		"打完這場要加餐喔！",
		"我不是迷路，是在偵察！",
		"主人，前面好像有好東西！",
		"這次一定會掉稀有裝備！",
		"勇者從不回頭看爆炸！",
		"先說好，寶物要平分喔！",
		"冒險的祕訣？跟緊主人！",
		"我的鼻子說前面有寶物！",
		"今天的風很適合出發！",
		"小心，我感覺到怪物了！",
		"再走一下就休息，好嗎？",
		"主人，你的背影真可靠！",
		"我會努力成為最強夥伴！",
		"這條路看起來很可疑喔！",
		"遇到危險就躲到我後面！",
		"嘿嘿，我今天狀態超好！",
		"剛才那招是不是很帥？",
		"怪物們，準備投降吧！",
		"我的肚子開始唱歌了……",
		"下一個城鎮有好吃的嗎？",
		"我想要一個閃亮亮的頭盔！",
		"別擔心，我還能再戰！",
		"前進前進，冒險不能停！",
		"這裡的空氣有故事的味道！",
		"主人，我有認真跟路喔！",
		"那朵雲好像一塊大肉排！",
		"今天也沒有迷路，完美！",
		"森林裡一定藏著祕密！",
		"山的另一邊會有什麼呢？",
		"我聽見金幣在呼喚我！",
		"快看，那邊好像會發光！",
		"勝利之後記得摸摸頭！",
		"我負責可愛，主人負責指揮！",
		"敵人好多，正好熱身！",
		"只要一起走就不會害怕！",
		"今天的幸運值肯定滿點！",
		"危險？那只是冒險的調味料！",
		"我的直覺通常都很準……吧！",
		"先休息一下也算戰術喔！",
		"雨天冒險也別有風味呢！",
		"太陽出來了，精神滿滿！",
		"晚上要一起看星星嗎？",
		"這次換我來保護主人！",
		"我已經記住敵人的弱點了！",
		"偷偷告訴你，我不怕黑！",
		"剛剛的聲音不是我肚子叫！",
		"前方道路，由我來偵察！",
		"主人，我們是最佳拍檔！",
		"再強的敵人也有破綻！",
		"寶箱會不會其實是怪物？",
		"我保證不會亂咬奇怪東西！",
		"聞起來像是稀有素材！",
		"冒險日記今天又要寫滿了！",
		"走慢一點，風景很好看呢！",
		"衝太快會錯過寶箱喔！",
		"這附近一定有隱藏道路！",
		"我剛剛看到草叢動了一下！",
		"不管去哪裡我都會跟著你！",
		"主人累了就換我帶路吧！",
		"放心，我方向感……還可以！",
		"下一戰讓我先上吧！",
		"我的必殺技正在充能！",
		"這一擊要打得漂漂亮亮！",
		"勝利姿勢我都想好了！",
		"敵人看起來也很有精神呢！",
		"要和平相處嗎？不行就開打！",
		"我的尾巴說今天會贏！",
		"有主人在，我什麼都不怕！",
		"冒險就是不斷發現驚喜！",
		"我們離傳說又近了一步！",
		"休息完要吃雙倍點心！",
		"我可以把寶石當點心嗎？",
		"這個不能吃？真可惜……",
		"聞到香味了，是營地嗎？",
		"背包裡還有零食對吧？",
		"打怪之前先補充體力嘛！",
		"主人最好了，尤其是發點心時！",
		"寶物和晚餐，我全都要！",
		"再冒險一下就開飯吧！",
		"我願意用寶箱換一頓大餐！",
		"這片草地很適合打滾耶！",
		"水面亮晶晶的，好漂亮！",
		"那棵樹看起來很有年紀！",
		"風把遠方的味道帶來了！",
		"這裡安靜得有點不尋常！",
		"腳印往那邊去了，追嗎？",
		"地圖拿反也能到目的地啦！",
		"我們好像來過這裡……吧？",
		"走錯路也可能找到驚喜！",
		"迷路是冒險家的浪漫！",
		"主人放心，我有做記號！",
		"咦？剛才的記號去哪了？",
		"只要往前走，總會到的！",
		"這條捷徑看起來很安全！",
		"如果迷路，就問問風吧！",
		"今天也一起平安回家吧！",
		"下一段旅程也請多多指教！",
	};
	private static readonly string[] PetCombatQuotes =
	{
		"好痛....",
		"不要打我！",
		"看我為主人討伐你！",
		"接招吧，壞傢伙！",
		"主人退後，交給我！",
		"這一擊是替主人打的！",
		"你挑錯對手了！",
		"別小看我的爪子！",
		"我、我才沒有害怕！",
		"痛歸痛，我還能打！",
		"等一下，你犯規啦！",
		"有本事別躲！",
		"看我的必殺技！",
		"吃我一記正義飛撲！",
		"主人正在看，我不能輸！",
		"打完你就有點心了！",
		"為了晚餐，衝啊！",
		"這招可是練習過的！",
		"你的弱點被我看穿了！",
		"再來，我還沒認真呢！",
		"可惡，差一點就很帥了！",
		"不准欺負我的主人！",
		"我們一起上，主人！",
		"勝利已經在向我招手！",
		"先投降就不咬你！",
		"這就是夥伴的力量！",
		"哎呀，這下有點疼！",
		"輪到我反擊了！",
		"你完蛋了，我生氣了！",
		"最後一擊讓我來！",
	};
	private SquadActivity _squadActivity = SquadActivity.Follow;
	private Vector3 _squadActivityLocalOffset = Vector3.Zero;
	private float _squadActivityRemaining;
	private float _squadThinkRemaining;
	private CompanionBuildLoadout _buildLoadout = new();
	private BuildStats _buildStats = new();
	private bool _buildConfigured;
	private bool _buildStatsDirty = true;
	private float _slowRemaining;
	private float _stunRemaining;
	private float _poisonRemaining;
	private float _burnRemaining;
	private float _statusTickRemaining;
	private SimpleActor? _statusSource;
	private float _formationAttackMultiplier = 1.0f;
	private float _formationDefenseMultiplier = 1.0f;
	private float _formationCooldownMultiplier = 1.0f;
	private float _formationIncomingDamageMultiplier = 1.0f;
	private float _formationRangeBonus;
	private string _formationBonusSummary = string.Empty;
	// Global team buff from the monster-card album (卡片系統 collection bonus).
	private float _cardAttackMultiplier = 1.0f;
	private float _cardDefenseMultiplier = 1.0f;
	private float _cardHealthMultiplier = 1.0f;
	private bool _bossEnraged;
	private int _bossAttackCounter;
	private Vector3 _bossLastChasePosition;
	private Vector3 _bossAvoidDirection;
	private float _bossStuckTime;
	private float _bossAvoidRemaining;
	private float _bossAvoidSide = 1.0f;
	// Capture readiness (削弱→硬直→捕捉). Stagger builds from hits (combo finisher)
	// and breaks the monster's guard; low HP also opens the capture window.
	private float _staggerValue;
	private float _staggerRemaining;
	private const float StaggerDuration = 4.0f;

	// Capture invincibility: once a capture orb lands, the target can't drop below
	// 1 HP until the attempt ends — so it can't die mid-capture. Driven on the host
	// (authoritative for HP) and mirrored to clients for the shield visual.
	private float _captureProtectionRemaining;
	private bool _captureLocked;
	private bool _captureProtectionSynced;
	private MeshInstance3D? _captureShield;
	public bool IsCaptureProtected => _captureLocked || _captureProtectionRemaining > 0.0f;

	// Mourning: when the owner player dies, their deployed companions stand on the
	// field crying, invincible and unable to fight, until the player returns.
	private bool _isMourning;
	private Label3D? _mournBubble;
	public bool IsMourning => _isMourning;

	// Passive (被動反擊): tier-1 "幼年" newbie-zone monsters never aggro on their own.
	// They wander until attacked, then fight back for a short provoked window.
	private bool _isPassive;
	private float _provokeRemaining;
	private const float PassiveProvokeSeconds = 8.0f;
	public void SetPassive(bool passive) => _isPassive = passive;

	// 訓練場稻草人：受擊只顯示傷害數字與特效，不扣血、不死亡、不可被捕捉。
	[Export] public bool IsTrainingDummy { get; set; }

	public bool CanBeCaptured => ActorKind == "monster" && !IsBoss && !_isCaptured && !_isDefeated && !_isNetworkPuppet && !IsTrainingDummy;
	public bool IsNetworkPuppet => _isNetworkPuppet;

	// ── Behaviour gates: SINGLE SOURCE OF TRUTH ────────────────────────────────
	// Every networking/combat/instancing/death cross-cutting bug we hit came from
	// each method re-deriving its OWN mix of the raw state flags (_isMourning,
	// _isPassive, _engagesLocalPlayer, _isDefeated, …). Define each behaviour gate
	// ONCE here and have every subsystem read these instead of the raw flags, so a
	// newly-added state only has to be wired into one gate — not hunted down across
	// _PhysicsProcess, ReceiveDamage, targeting and SetWorldMapState.

	// Untouchable by locally-applied damage (dead, grieving, or burrowed).
	private bool IsInvulnerable => _isDefeated || _isMourning || _isBurrowed;

	// Shares the LOCAL player's instance (same map, tier and party group). Gates
	// both visibility and whether this actor may engage the local player.
	private bool SharesLocalInstance => _engagesLocalPlayer;

	// A wild monster that actively hunts, as opposed to a passive "幼年" newbie
	// monster that only fights back for a short window after being attacked.
	private bool IsProactivelyAggressive => !_isPassive || _provokeRemaining > 0.0f;

	// HP fraction at/under which the monster can be netted; rarer = must be weaker.
	public float CaptureHealthThreshold => Mathf.Clamp(0.45f - Rarity * 0.06f, 0.18f, 0.45f);
	public float MaxStagger => Mathf.Max(EffectiveMaxHealth * 0.65f, 1.0f);
	public float StaggerRatio => Mathf.Clamp(_staggerValue / MaxStagger, 0.0f, 1.0f);
	public bool IsStaggered => _staggerRemaining > 0.0f;
	// The net only opens the capture challenge when the monster is weakened or
	// staggered — throwing at a healthy monster just chips its guard.
	public bool CaptureReady => CanBeCaptured && (IsStaggered || HealthRatio <= CaptureHealthThreshold);

	// Combo finisher: landing hits fills the stagger meter; a full meter breaks the
	// monster (力竭) into a capture window for a few seconds.
	public void AddCaptureStagger(float amount)
	{
		if (!CanBeCaptured || IsStaggered || amount <= 0.0f)
		{
			return;
		}

		_staggerValue += amount;
		if (_staggerValue >= MaxStagger)
		{
			_staggerValue = 0.0f;
			_staggerRemaining = StaggerDuration;
			SpawnCombatEffect(LocaleText.T("system.capture.stagger"), new Color(1.0f, 0.86f, 0.35f, 0.95f), GlobalPosition + new Vector3(0.0f, 1.7f, 0.0f), 1.0f, 0.7f);
			RefreshNameplate();
		}
	}

	// Capture-protection and mourning behaviour moved to SimpleActor.Capture.cs
	// (Stage-0 separation — see docs/ARCHITECTURE_REVIEW.md).

	// Overhead nameplate (Lv + name) font multiplier — 3x by default, adjustable
	// in settings. Base font is 20 (28 for bosses).
	public const float MinNameplateScale = 1.0f;
	public const float MaxNameplateScale = 6.0f;
	public static float NameplateScale { get; private set; } = 3.0f;

	// Nameplate (scale/refresh/marker) moved to SimpleActor.Nameplate.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).
	// Host & client both tag their wild monsters with the shared network id so
	// death can be broadcast/looked up without scanning (multiplayer).
	public int NetworkMonsterId { get; set; } = -1;

	// Wild-monster rarity (MonsterRarity.*). 0 = common. Kept after capture.
	[Export] public int Rarity { get; set; } = MonsterRarity.Common;

	// Progression (rarity/level/rebirth/evolution) moved to SimpleActor.Progression.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).
	public bool IsNpcRecruitCandidate => ActorKind == "npc" && !_isCaptured && !_isDefeated;
	public bool CanJoinByAffinity => IsNpcRecruitCandidate && Affinity >= 80;
	public bool IsCaptured => _isCaptured;
	public bool IsInActiveParty => _isInActiveParty;
	public bool IsInWarehouseCollection => _isInWarehouseCollection;
	public float MountSeatHeight => Mathf.Max(GetVisualTopY(this) + 0.16f, 0.9f);
	public void SetMountedByPlayer(bool mounted) => _isMountedByPlayer = mounted;
	public bool IsDefeated => _isDefeated;
	public bool IsAwaitingRecovery => _isAwaitingRecovery;
	public string FallenMapId => _fallenMapId;
	public bool IsActiveWorldTarget => !_isCaptured && !_isDefeated && !_isBurrowed && _isWorldMapActive && IsVisibleInTree();
	public bool IsHostileToPlayer => ActorKind == "monster" && IsActiveWorldTarget;
	public CompanionBuildLoadout BuildLoadout
	{
		get
		{
			EnsureBuildLoadout();
			return _buildLoadout;
		}
	}
	public BuildStats CurrentBuildStats
	{
		get
		{
			if (_buildStatsDirty)
			{
				RecalculateBuildStats();
			}

			return _buildStats;
		}
	}
	public int EffectiveMaxHealth => CurrentBuildStats.MaxHealth;
	public int EffectiveAttack => IsBoss && _bossEnraged
		? Mathf.Max(Mathf.RoundToInt(CurrentBuildStats.Attack * 1.35f), 1)
		: CurrentBuildStats.Attack;
	public int EffectiveDefense => CurrentBuildStats.Defense;
	public float EffectiveMoveSpeed => Mathf.Max(MoveSpeed * CurrentBuildStats.MoveSpeedMultiplier * (IsBoss && _bossEnraged ? 1.16f : 1.0f) * (_slowRemaining > 0.0f ? 0.55f : 1.0f), 0.3f);
	public float EffectiveAttackRange => Mathf.Max(AttackRange + CurrentBuildStats.AttackRangeBonus, 0.75f);
	public float EffectiveDetectionRadius => Mathf.Max(DetectionRadius + CurrentBuildStats.DetectionRadiusBonus, 3.0f);
	public float EffectiveAttackCooldown => Mathf.Max(AttackCooldown * CurrentBuildStats.AttackCooldownMultiplier * (IsBoss && _bossEnraged ? 0.72f : 1.0f), 0.22f);
	public string TypeName => LocaleText.T(IsBoss ? "actor.type.boss" : ActorKind == "monster" ? "actor.type.monster" : "actor.type.npc");
	public string StateName
	{
		get
		{
			return _isAwaitingRecovery
				? LocaleText.T("actor.state.awaiting_recovery")
				: _isDefeated
				? LocaleText.T("actor.state.defeated")
				: _isCaptured
					? _isInActiveParty ? LocaleText.T("actor.state.active") : LocaleText.T("actor.state.stored")
					: ActorKind == "monster" ? LocaleText.T("actor.state.hostile") : LocaleText.T("actor.state.neutral");
		}
	}
	public string MoodName => LocaleText.T(GetMoodStateKey());
	public string MoodStateKey => GetMoodStateKey();

	private string GetMoodStateKey()
	{
		if (!string.IsNullOrWhiteSpace(MoodStateId))
		{
			return MoodStateId;
		}

		return Affinity switch
		{
			<= -60 => "actor.mood.wants_to_escape",
			<= -30 => "actor.mood.depressed",
			< 0 => "actor.mood.sulking",
			< 20 => "actor.mood.wary",
			< 50 => "actor.mood.settling_in",
			< 75 => "actor.mood.trusting",
			< 90 => "actor.mood.happy",
			_ => "actor.mood.devoted",
		};
	}
	public string GrowthName => EvolutionStage <= 0
		? LocaleText.T("actor.growth.base")
		: EvolutionStage == 1
			? LocaleText.T("actor.growth.evo1")
			: EvolutionStage == 2 ? LocaleText.T("actor.growth.evo2") : LocaleText.T("actor.growth.final");
	public string CombatRoleName => CombatRole switch
	{
		"Tank" => LocaleText.T("role.tank"),
		"Ranged" => LocaleText.T("role.ranged"),
		"Support" => LocaleText.T("role.support"),
		"Gatherer" => LocaleText.T("role.gatherer"),
		"Builder" => LocaleText.T("role.builder"),
		_ => LocaleText.T("role.dps"),
	};
	public string LocalizedDisplayName
	{
		get
		{
			if (IsBoss && !string.IsNullOrWhiteSpace(BossNameKey))
			{
				return LocaleText.T(BossNameKey);
			}

			string baseName = LocaleText.T(DisplayName);
			// Monsters carry their tier evolution stage in the name (Young/Elite/...).
			return ActorKind == "monster"
				? WorldTierCatalog.FormatMonsterName(WorldTier, baseName)
				: baseName;
		}
	}
	public bool IsBossEnraged => IsBoss && _bossEnraged;
	public string LocalizedSpecialAbility => LocaleText.T(SpecialAbility);
	public string LocalizedPersonality => LocaleText.T(Personality);
	public string LocalizedPassiveAbility => LocaleText.T(PassiveAbility);
	public string TraitSummary => BuildCatalog.LocalizedList(GetTraitKeys());
	public string[] TraitKeys => GetTraitKeys();
	public string BuildEquipmentSummary => BuildCatalog.LocalizedEquipmentSet(BuildLoadout);
	public string BuildSkillSummary => BuildCatalog.LocalizedSkillGems(BuildLoadout);
	public string AttackModeName => LocaleText.T(BuildCatalog.GetAttackMode(AttackModeId).NameKey);
	public string FormationBonusSummary => _formationBonusSummary;

	// The active support cores strung together, e.g. "火球-爆炸-分裂". Only equipped,
	// already-unlocked slots are included, in slot order.
	public string SupportCoreChain
	{
		get
		{
			CompanionBuildLoadout loadout = BuildLoadout;
			int unlocked = BuildCatalog.GetUnlockedSupportCoreCount(Level);
			var names = new List<string>();
			for (int index = 0; index < unlocked && index < loadout.SkillGemIds.Length; index++)
			{
				string id = loadout.GetSkillGemId(index);
				if (id == "gem.skill.none")
				{
					continue;
				}

				names.Add(LocaleText.T(BuildCatalog.GetSkillGem(id).NameKey));
			}

			return string.Join("-", names);
		}
	}

	private string[] GetTraitKeys()
	{
		return (string[])CurrentBuildStats.TraitKeys.Clone();
	}
	public string BuildElementName => LocaleText.T(CurrentBuildStats.DamageElementNameKey);
	public bool IsRangedCombatant => CombatRole == "Ranged" || CombatRole == "Support" || EffectiveAttackRange > 3.0f;
	public string CombatRangeName => LocaleText.T(IsRangedCombatant ? "combat.range.ranged" : "combat.range.melee");
	public string CombatSummary => $"{LocaleText.F("combat.summary", CombatRoleName, LocalizedPersonality, Affinity)} / {CombatRangeName} / {LocaleText.F("stat.affinity_value", Affinity)}";
	public Color AttackFxColor => GetAttackColor();
	public int ExperienceToNextLevel => ExperienceTable.ToNextLevel(Level, EvolutionStage);
	public bool CanEvolve => EvolutionStage < 3 && Level >= (EvolutionStage + 1) * 5;

	// Rebirth (轉生): companions cap at level 100; rebirthing resets level to 1 and
	// permanently adds +5 to every base stat, stackable without limit.
	public const int MaxCompanionLevel = 100;
	public const int RebirthStatBonus = 5;
	[Export] public int RebirthCount { get; set; }
	[Export] public int LevelOneMaxHealth { get; set; }
	[Export] public int LevelOneAttack { get; set; }
	[Export] public int LevelOneDefense { get; set; }
	public bool CanRebirth => _isCaptured && Level >= MaxCompanionLevel;
	public int RebirthTotalStatBonus => Mathf.Max(RebirthCount, 0) * RebirthStatBonus;
	public int OriginalMaxHealthWithoutRebirth => Mathf.Max(MaxHealth - RebirthTotalStatBonus, 1);
	public int OriginalAttackWithoutRebirth => Mathf.Max(Attack - RebirthTotalStatBonus, 1);
	public int OriginalDefenseWithoutRebirth => Mathf.Max(Defense - RebirthTotalStatBonus, 0);
	public string EvolutionMaterialId => EvolutionStage switch
	{
		0 => "loot.cracked_core",
		1 => "loot.beast_hide",
		2 => "loot.dragon_scale",
		_ => string.Empty,
	};
	public int EvolutionMaterialCount => EvolutionStage switch { 0 => 3, 1 => 5, 2 => 2, _ => 0 };
	public float HealthRatio => _isDefeated || EffectiveMaxHealth <= 0 ? 0.0f : Mathf.Clamp(CurrentHealth / (float)EffectiveMaxHealth, 0.0f, 1.0f);

	public override void _Ready()
	{
		if (!ActiveActorRegistry.Contains(this))
		{
			ActiveActorRegistry.Add(this);
		}

		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		_rng.Seed = Time.GetTicksUsec() + GetInstanceId();
		_defaultCollisionLayer = CollisionLayer;
		_defaultCollisionMask = CollisionMask;

		if (HomePosition == Vector3.Zero)
		{
			HomePosition = GlobalPosition;
		}

		_targetPosition = PickWanderTarget();
		EnsureBuildLoadout();
		RecalculateBuildStats();
		ApplyEvolutionAppearance();
		AddToGroup(ActorKind == "monster" ? "monsters" : "npcs");
		CreateNameplate();
		LocaleText.LanguageChanged += RefreshNameplate;
	}

	public override void _ExitTree()
	{
		ActiveActorRegistry.Remove(this);
		LocaleText.LanguageChanged -= RefreshNameplate;
	}

	public override void _Process(double delta)
	{
		_externalRootMotionStabilizeRemaining = Mathf.Max(_externalRootMotionStabilizeRemaining - (float)delta, 0.0f);
		if (_externalRootMotionStabilizeRemaining > 0.0f)
		{
			return;
		}

		_externalRootMotionStabilizeRemaining = ExternalRootMotionStabilizeSeconds + (_followSlot % 4) * 0.015f;
		StabilizeExternalModelRootMotion();
	}

	// Per-frame dispatcher. Each actor state now owns exactly one handler, so the
	// behaviour for a given frame is chosen in ONE place instead of being threaded
	// through a 140-line method with interleaved early-returns. Ticks are shared;
	// then the first matching state fully handles the frame.
	public override void _PhysicsProcess(double delta)
	{
		float step = (float)delta;

		// Network puppets are display-only: driven entirely by streamed host state.
		if (_isNetworkPuppet)
		{
			UpdateNetworkPuppet(step);
			return;
		}

		TickActorTimers(step);
		Vector3 velocity = Velocity;

		// Frozen states — each ends the frame in place.
		if (_isDefeated)
		{
			StopInPlace(velocity, step);
			return;
		}

		if (_isMountedByPlayer && _followTarget != null && IsInstanceValid(_followTarget))
		{
			RunMountedFrame(step);
			return;
		}

		if (_stunRemaining > 0.0f)
		{
			StopInPlace(Velocity, step);
			return;
		}

		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * step;
		}

		// Captured companions follow their owner (or grieve when mourning); wild
		// actors run monster combat + wandering.
		if (_isCaptured)
		{
			RunCapturedFrame(velocity, step);
			return;
		}

		RunWildActorFrame(velocity, step);
	}

	// Shared per-frame bookkeeping for every non-puppet actor.
	private void TickActorTimers(float step)
	{
		UpdateStatusEffects(step);
		if (ActorKind == "monster" && !_isCaptured)
		{
			UpdateCaptureState(step);
		}
		if (ActorKind == "monster" && (IsMoleSpecies || _isBurrowed))
		{
			UpdateBurrow(step);
		}
		_attackCooldownRemaining = Mathf.Max(_attackCooldownRemaining - step, 0.0f);
		_retaliationTargetRemaining = Mathf.Max(_retaliationTargetRemaining - step, 0.0f);
		_specialControlCooldownRemaining = Mathf.Max(_specialControlCooldownRemaining - step, 0.0f);
		_combatTargetSearchRemaining = Mathf.Max(_combatTargetSearchRemaining - step, 0.0f);
		_provokeRemaining = Mathf.Max(_provokeRemaining - step, 0.0f);
		UpdateWhirlwindSpin(step);
	}

	private void StopInPlace(Vector3 velocity, float step)
	{
		Velocity = SlowToStop(velocity, step);
		MoveAndSlideWithEffects(step);
	}

	private void RunMountedFrame(float step)
	{
		GlobalPosition = _followTarget!.GlobalPosition;
		Rotation = _followTarget.Rotation;
		Velocity = _followTarget.Velocity;
		UpdateMovementAnimation(step);
	}

	private void RunCapturedFrame(Vector3 velocity, float step)
	{
		if (_isMourning)
		{
			// Grieving in place — no following, no combat.
			StopInPlace(velocity, step);
			return;
		}

		FollowCapturedTarget(velocity, step);
	}

	// Wild monster / NPC frame: resolve combat (monsters only), then wander/chase.
	private void RunWildActorFrame(Vector3 velocity, float step)
	{
		if (_isBurrowed)
		{
			// Underground: hold position, no combat or wandering until resurface.
			StopInPlace(velocity, step);
			return;
		}

		Node3D? player = GetCachedPlayerNode();
		bool chasing = false;
		Vector3 destination = _targetPosition;

		if (ActorKind == "monster" && TryRunMonsterCombat(player, velocity, ref destination, ref chasing, step))
		{
			return; // an attack consumed the frame
		}

		if (!chasing)
		{
			ResetBossObstacleAvoidance();
			_waitTime = Mathf.Max(_waitTime - step, 0.0f);
			if (_waitTime > 0.0f)
			{
				StopInPlace(velocity, step);
				return;
			}
		}

		Vector3 toDestination = destination - GlobalPosition;
		toDestination.Y = 0.0f;

		if (!chasing && toDestination.Length() < 0.8f)
		{
			_waitTime = (float)_rng.RandfRange(0.6f, 2.2f);
			_targetPosition = PickWanderTarget();
			velocity = SlowToStop(velocity, step);
		}
		else
		{
			Vector3 direction = toDestination.Normalized();
			if (chasing && IsBoss)
			{
				direction = GetBossChaseDirection(direction, step);
			}
			float activeSpeed = EffectiveMoveSpeed * (chasing ? 1.35f : 1.0f);
			velocity.X = Mathf.MoveToward(velocity.X, direction.X * activeSpeed, activeSpeed * 6.0f * step);
			velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * activeSpeed, activeSpeed * 6.0f * step);
			FaceDirection(direction, step);
		}

		Velocity = velocity;
		MoveAndSlideWithEffects(step);
	}

	// Monster combat decision: retaliate against a recent attacker, else (if not a
	// passive newbie) hunt the nearest player sharing this instance — local player
	// or, on the host, a remote player (damaged over the network). Returns true when
	// an attack fully consumed the frame; otherwise updates chasing/destination for
	// the wander/chase movement that follows.
	private bool TryRunMonsterCombat(Node3D? player, Vector3 velocity, ref Vector3 destination, ref bool chasing, float step)
	{
		if (TryGetRetaliationTarget(out SimpleActor retaliationTarget))
		{
			chasing = true;
			destination = retaliationTarget.GlobalPosition;
			return TryAttackActorTarget(retaliationTarget, velocity, step);
		}

		if (!IsProactivelyAggressive)
		{
			return false;
		}

		Node3D? target = ResolveHostileTarget(player, out bool targetIsRemote, out long remotePeerId);
		if (target == null)
		{
			return false;
		}

		if (GlobalPosition.DistanceTo(target.GlobalPosition) > ChaseRadius)
		{
			return false;
		}

		chasing = true;
		destination = target.GlobalPosition;
		return targetIsRemote
			? TryAttackRemotePlayer(target, remotePeerId, velocity, step)
			: TryAttackPlayer(target, velocity, step);
	}

	private Node3D? GetCachedPlayerNode()
	{
		if (_cachedPlayerNode != null && IsInstanceValid(_cachedPlayerNode))
		{
			return _cachedPlayerNode;
		}

		_cachedPlayerNode = GetTree().GetFirstNodeInGroup("player") as Node3D;
		return _cachedPlayerNode;
	}

	public void Capture(PlayerController followTarget)
	{
		EnsureLevelOneStats();
		_isCaptured = true;
		_isDefeated = false;
		_isAwaitingRecovery = false;
		_fallenMapId = string.Empty;
		_followTarget = followTarget;
		_isInActiveParty = false;
		AttackModeId = BuildCatalog.AiManualOnly;
		_waitTime = 0.0f;
		_captureLocked = false;
		_captureProtectionRemaining = 0.0f;
		_captureProtectionSynced = false;
		RefreshCaptureShield(false);
		ResetSquadActivity();
		Velocity = Vector3.Zero;
		CurrentHealth = Mathf.Max(CurrentHealth, Mathf.RoundToInt(EffectiveMaxHealth * 0.45f));
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		AddToGroup("captured_actors");
		RefreshNameplate();

		// Celebrate a rare capture — a real power spike worth showing off.
		if (Rarity > MonsterRarity.Common && IsInstanceValid(followTarget))
		{
			string rarity = LocaleText.T(MonsterRarity.NameKey(Rarity));
			followTarget.PostSystemMessage(LocaleText.F("system.capture.rare", rarity, LocalizedDisplayName), MonsterRarity.Color(Rarity), GameMessageChannel.Party);
			SpawnCombatEffect(LocaleText.T("system.capture.rare_pop"), MonsterRarity.Color(Rarity), GlobalPosition + new Vector3(0.0f, 1.6f, 0.0f), 1.3f, 0.9f);
		}
	}

	public void Recruit(PlayerController followTarget)
	{
		Capture(followTarget);
	}

	public void SetWorldMapActive(bool active)
	{
		SetWorldMapState(active, active);
	}

	// Simulation (physics/AI/collision) and visibility are decoupled so the host
	// can keep another group's instance running (simulate) without showing it to
	// the local player (visible). Clients never simulate (simulate=false).
	public void SetWorldMapState(bool simulate, bool visible)
	{
		if (_isCaptured)
		{
			return;
		}

		_isWorldMapActive = simulate;
		_engagesLocalPlayer = visible;
		Visible = visible;
		SetPhysicsProcess(simulate && !_isDefeated);
		if (simulate && !_isDefeated)
		{
			CollisionLayer = _defaultCollisionLayer;
			CollisionMask = _defaultCollisionMask;
		}
		else
		{
			CollisionLayer = 0;
			CollisionMask = 0;
			_combatTarget = null;
			_retaliationTarget = null;
			_retaliationTargetRemaining = 0.0f;
		}
	}

	public void DeployToParty(PlayerController followTarget, int followSlot)
	{
		_followTarget = followTarget;
		_followSlot = followSlot;
		_isInWarehouseCollection = false;
		_isInActiveParty = true;
		Visible = true;
		SetPhysicsProcess(!_isDefeated);
		CollisionLayer = _defaultCollisionLayer;
		CollisionMask = _defaultCollisionMask;
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		ResetSquadActivity();
		if (!_isDefeated)
		{
			GlobalPosition = GetFollowDestination();
			ApplyLivingPose();
		}
		Velocity = Vector3.Zero;
		RefreshNameplate();
	}

	public void StoreInCollection()
	{
		_isInActiveParty = false;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);
		RefreshNameplate();
	}

	public void SetWarehouseCollectionState(bool stored)
	{
		_isInWarehouseCollection = stored;
		if (stored)
		{
			StoreInCollection();
		}
	}

	public bool TryRecoverFallenCompanion(PlayerController followTarget, float pickupRadius)
	{
		if (!_isCaptured
			|| !_isDefeated
			|| !_isAwaitingRecovery
			|| _followTarget != followTarget
			|| followTarget.GetParent() is not World world
			|| world.ActiveMapId != _fallenMapId
			|| GlobalPosition.DistanceTo(followTarget.GlobalPosition) > pickupRadius)
		{
			return false;
		}

		_isAwaitingRecovery = false;
		_isInActiveParty = false;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);
		RefreshNameplate();
		return true;
	}

	// 開發測試用：把一隻已捕捉的夥伴直接設為「已死亡且已回收」狀態
	// （等同倒地後已被撿回），使其顯示在 U 面板「已死亡」區並可於水池復活。
	public void MarkDefeatedForTest()
	{
		_isCaptured = true;
		_isDefeated = true;
		_isAwaitingRecovery = false;
		_isInActiveParty = false;
		_fallenMapId = string.Empty;
		CurrentHealth = 0;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);
		RefreshNameplate();
	}

	public void UpdateFallenMapVisibility(string activeMapId)
	{
		if (_isCaptured && _isDefeated && _isAwaitingRecovery)
		{
			Visible = activeMapId == _fallenMapId;
		}
	}

	public void RestoreCapturedState(PlayerController followTarget, ActorSaveData data)
	{
		Capture(followTarget);
		ApplySaveData(data);
		_followTarget = followTarget;
		_isCaptured = true;
		_isInActiveParty = false;
		if (_isDefeated)
		{
			CurrentHealth = 0;
			Velocity = Vector3.Zero;
			Visible = _isAwaitingRecovery;
			CollisionLayer = _isAwaitingRecovery ? _defaultCollisionLayer : 0;
			CollisionMask = _isAwaitingRecovery ? _defaultCollisionMask : 0;
			SetPhysicsProcess(false);
			ApplyDefeatedPose();
			RefreshNameplate();
			return;
		}

		StoreInCollection();
	}

	public void SetFollowSlot(int followSlot)
	{
		_followSlot = followSlot;
		_squadThinkRemaining = Mathf.Min(_squadThinkRemaining, 0.2f);
	}

	public void OnFormationLayoutChanged()
	{
		ResetSquadActivity();
		_squadActivityRemaining = 0.0f;
		_squadThinkRemaining = 0.0f;
	}

	public void SetFormationBonuses(float attackMultiplier, float defenseMultiplier, float cooldownMultiplier, float incomingDamageMultiplier, float rangeBonus, string summary)
	{
		_formationAttackMultiplier = attackMultiplier;
		_formationDefenseMultiplier = defenseMultiplier;
		_formationCooldownMultiplier = cooldownMultiplier;
		_formationIncomingDamageMultiplier = incomingDamageMultiplier;
		_formationRangeBonus = rangeBonus;
		_formationBonusSummary = summary;
		_buildStatsDirty = true;
		if (_buildConfigured)
		{
			RecalculateBuildStats();
		}
	}

	// Applied to every deployed companion; scales with unique cards collected.
	public void SetCardCollectionBonus(float attackMultiplier, float defenseMultiplier, float healthMultiplier)
	{
		_cardAttackMultiplier = attackMultiplier;
		_cardDefenseMultiplier = defenseMultiplier;
		_cardHealthMultiplier = healthMultiplier;
		_buildStatsDirty = true;
		if (_buildConfigured)
		{
			RecalculateBuildStats();
		}
	}

	// The res:// path of the currently-instantiated external model, so the network
	// layer can tell peers which model to render for this companion (empty if the
	// actor is using the primitive fallback body).
	public string GetExternalModelPath()
	{
		Node3D? model = GetNodeOrNull<Node3D>("ExternalModel");
		return model?.SceneFilePath ?? string.Empty;
	}

	// Canonical card identity for this actor's model (one card per model), with a
	// fallback to the species DisplayName key when no external model is present.
	public string GetCardKey()
	{
		Node3D? model = GetNodeOrNull<Node3D>("ExternalModel");
		string path = model?.SceneFilePath ?? string.Empty;
		if (!string.IsNullOrEmpty(path))
		{
			string key = ExternalModelLibrary.CardKeyFromModelPath(path);
			if (!string.IsNullOrWhiteSpace(key))
			{
				return key;
			}
		}

		return DisplayName;
	}

	// Build/loadout editing moved to SimpleActor.Build.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	public int GetSkillGemLevel(int slotIndex)
	{
		return BuildLoadout.GetSkillGemLevel(slotIndex);
	}

	// Build/loadout skill-level and attack-mode editing moved to SimpleActor.Build.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	public void ConfigureStats(string displayName, int level, int maxHealth, int attack, int defense, int experienceReward, int goldReward)
	{
		DisplayName = displayName;
		Level = level;
		MaxHealth = Mathf.Max(maxHealth, 1);
		CurrentHealth = MaxHealth;
		Attack = Mathf.Max(attack, 0);
		Defense = Mathf.Max(defense, 0);
		ExperienceReward = Mathf.Max(experienceReward, 0);
		GoldReward = Mathf.Max(goldReward, 0);
		_buildConfigured = false;
		_buildStatsDirty = true;
		RefreshNameplate();
	}

	// Network-puppet lifecycle/sync (SetNetworkPuppet/ApplyNetworkState/
	// UpdateNetworkPuppet) moved to SimpleActor.Network.cs
	// (Stage-0 separation — see docs/ARCHITECTURE_REVIEW.md).

	public void ConfigureBoss(string bossNameKey, string primaryLootId)
	{
		IsBoss = true;
		BossNameKey = bossNameKey;
		BossPrimaryLootId = primaryLootId;
		_bossEnraged = false;
		_bossAttackCounter = 0;
		ResetBossObstacleAvoidance();
		ChaseRadius = Mathf.Max(ChaseRadius, 28.0f);
		WanderRadius = Mathf.Max(WanderRadius, 14.0f);
		RefreshNameplate();
	}

	public void ConfigureGrowth(string specialAbility, int abilityRank)
	{
		SpecialAbility = specialAbility;
		AbilityRank = Mathf.Max(abilityRank, 1);
	}

	public void ConfigureCombatProfile(string combatRole, string personality, string passiveAbility, int affinity)
	{
		CombatRole = combatRole;
		Personality = personality;
		PassiveAbility = passiveAbility;
		Affinity = Mathf.Clamp(affinity, -100, 100);

		switch (CombatRole)
		{
			case "Tank":
				Defense += 5 + Level;
				MaxHealth += 24 + Level * 3;
				CurrentHealth = MaxHealth;
				AttackRange = 1.7f;
				DetectionRadius = 13.0f;
				AttackCooldown = 1.55f;
				break;
			case "Ranged":
				Attack += 2 + Mathf.CeilToInt(Level * 0.5f);
				AttackRange = 6.0f;
				DetectionRadius = 16.0f;
				AttackCooldown = 1.65f;
				break;
			case "Support":
				Defense += 3;
				AttackRange = 4.0f;
				DetectionRadius = 14.0f;
				AttackCooldown = 1.75f;
				break;
			case "Gatherer":
			case "Builder":
				Defense += 2;
				AttackRange = 2.2f;
				DetectionRadius = 10.0f;
				AttackCooldown = 1.45f;
				break;
			default:
				CombatRole = "DPS";
				Attack += 4 + Level;
				AttackRange = 2.0f;
				DetectionRadius = 14.0f;
				AttackCooldown = 1.20f;
				break;
		}

		AttackModeId = BuildCatalog.GetDefaultAttackModeId(this);
		_buildConfigured = false;
		EnsureBuildLoadout();
		RecalculateBuildStats();
		CurrentHealth = EffectiveMaxHealth;
	}

	public void IncreaseAffinity(int amount)
	{
		Affinity = Mathf.Clamp(Affinity + amount, -100, 100);
		if (Affinity >= 0)
		{
			MoodStateId = string.Empty;
		}
		RefreshNameplate();
	}

	// Save moved to SimpleActor.Save.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Build (loadout resolution/normalisation/stat recalculation/change markers) moved to SimpleActor.Build.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Progression (level-one stats/training/rebirth/evolve/enhance) moved to SimpleActor.Progression.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Combat (damage/heal) moved to SimpleActor.Combat.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	public bool ReviveFromCaretaker(PlayerController followTarget)
	{
		if (!_isCaptured || !_isDefeated || _isAwaitingRecovery)
		{
			return false;
		}

		_isDefeated = false;
		_isAwaitingRecovery = false;
		_fallenMapId = string.Empty;
		_followTarget = followTarget;
		CurrentHealth = Mathf.Max(Mathf.RoundToInt(EffectiveMaxHealth * 0.65f), 1);
		Velocity = Vector3.Zero;
		Visible = _isInActiveParty;
		CollisionLayer = _isInActiveParty ? _defaultCollisionLayer : 0;
		CollisionMask = _isInActiveParty ? _defaultCollisionMask : 0;
		SetPhysicsProcess(_isInActiveParty);
		ApplyLivingPose();
		if (_isInActiveParty)
		{
			GlobalPosition = GetFollowDestination();
		}

		RefreshNameplate();
		return true;
	}

	// Nameplate (create/refresh/position/status colour/markers/visual-top) moved to SimpleActor.Nameplate.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Squad/follow behaviour moved to SimpleActor.Squad.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Progression (level-up + per-level growth) moved to SimpleActor.Progression.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private bool TryUseSupportBuild(ref Vector3 velocity, float step)
	{
		BuildStats stats = CurrentBuildStats;
		// A companion with a heal skill auto-heals allies while in auto mode; manual mode
		// suppresses all automatic behavior so it only acts on the player's command.
		if (!stats.HasHealSkill || stats.AiBehaviorId == BuildCatalog.AiManualOnly || _followTarget == null || !IsInstanceValid(_followTarget) || _attackCooldownRemaining > 0.0f)
		{
			return false;
		}

		int healing = Mathf.Max(Mathf.RoundToInt(stats.Attack * 0.58f + stats.Defense * 0.18f), 8);
		if (_followTarget.CurrentHealth < Mathf.RoundToInt(_followTarget.MaxHealth * 0.72f))
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(_followTarget.GlobalPosition - GlobalPosition, step);
			if (_followTarget.ReceiveHealing(healing) > 0)
			{
				PlayAttackAction(_followTarget.GlobalPosition, true);
				_attackCooldownRemaining = EffectiveAttackCooldown;
				return true;
			}
		}

		foreach (SimpleActor ally in _followTarget.ActiveParty)
		{
			if (ally == this || !IsInstanceValid(ally) || !ally.IsInActiveParty || ally.HealthRatio >= 0.68f)
			{
				continue;
			}

			velocity = SlowToStop(velocity, step);
			FaceDirection(ally.GlobalPosition - GlobalPosition, step);
			if (ally.ReceiveHealing(healing) > 0)
			{
				PlayAttackAction(ally.GlobalPosition, true);
				_attackCooldownRemaining = EffectiveAttackCooldown;
				return true;
			}
		}

		return false;
	}

	private bool TryCompanionCombat(ref Vector3 velocity, float step)
	{
		SimpleActor? target = GetCombatTarget();
		if (target == null)
		{
			_combatTarget = null;
			return false;
		}

		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		float distance = toTarget.Length();
		if (distance > EffectiveDetectionRadius * 1.25f)
		{
			_combatTarget = null;
			return false;
		}

		if (distance <= EffectiveAttackRange)
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(toTarget, step);
			AttackActor(target);
			return true;
		}

		Vector3 direction = toTarget.Normalized();
		float combatSpeed = Mathf.Max(EffectiveMoveSpeed * 2.05f, 4.2f);
		velocity.X = Mathf.MoveToward(velocity.X, direction.X * combatSpeed, combatSpeed * 8.0f * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * combatSpeed, combatSpeed * 8.0f * step);
		FaceDirection(direction, step);
		return true;
	}

	private SimpleActor? GetCombatTarget()
	{
		string behaviorId = CurrentBuildStats.AiBehaviorId;
		bool acceptsPlayerCommand = behaviorId is BuildCatalog.AiCommandPriority or BuildCatalog.AiManualOnly;
		if (acceptsPlayerCommand && _followTarget != null && IsInstanceValid(_followTarget))
		{
			SimpleActor? focusedTarget = _followTarget.FocusedTarget;
			if (focusedTarget != null && IsValidCommandTarget(focusedTarget))
			{
				float distanceFromSelf = GlobalPosition.DistanceTo(focusedTarget.GlobalPosition);
				float distanceFromPlayer = _followTarget.GlobalPosition.DistanceTo(focusedTarget.GlobalPosition);
				float commandRadius = Mathf.Max(EffectiveDetectionRadius * 1.85f, 18.0f);
				if (distanceFromSelf <= commandRadius || distanceFromPlayer <= commandRadius)
				{
					_combatTarget = focusedTarget;
					return focusedTarget;
				}
			}
		}

		// Manual mode never auto-acquires: it fights only the player's designated target
		// handled above. Independent mode intentionally skips the command branch, while
		// command-priority mode reaches the same automatic fallback used below.
		if (behaviorId == BuildCatalog.AiManualOnly)
		{
			_combatTarget = null;
			return null;
		}

		if (_combatTarget != null
			&& IsValidCommandTarget(_combatTarget)
			&& _combatTarget.IsHostileToPlayer
			&& (!_combatTarget.IsTrainingDummy || IsPlayerFocusedTarget(_combatTarget))
			&& GlobalPosition.DistanceTo(_combatTarget.GlobalPosition) <= EffectiveDetectionRadius * 1.35f)
		{
			return _combatTarget;
		}

		if (_combatTargetSearchRemaining > 0.0f)
		{
			return null;
		}

		_combatTargetSearchRemaining = 0.18f + (_followSlot % 4) * 0.035f;

		// Auto mode: pick the nearest hostile within detection range.
		float searchRadius = EffectiveDetectionRadius;
		SimpleActor? selected = null;
		float bestDistance = float.MaxValue;
		foreach (SimpleActor actor in ActiveActorRegistry)
		{
			if (!IsInstanceValid(actor) || !actor.IsHostileToPlayer || actor.IsTrainingDummy)
			{
				continue;
			}

			float distanceFromSelf = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distanceFromSelf > searchRadius || distanceFromSelf >= bestDistance)
			{
				continue;
			}

			selected = actor;
			bestDistance = distanceFromSelf;
		}

		_combatTarget = selected;
		return selected;
	}

	private bool IsPlayerFocusedTarget(SimpleActor actor)
	{
		return _followTarget != null
			&& IsInstanceValid(_followTarget)
			&& _followTarget.FocusedTarget == actor;
	}

	private bool IsValidCommandTarget(SimpleActor? actor)
	{
		return actor != null && IsInstanceValid(actor) && actor.IsActiveWorldTarget && actor != this;
	}

	// Combat (attack execution) moved to SimpleActor.Combat.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Status effects moved to SimpleActor.Status.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Progression (evolution appearance) moved to SimpleActor.Progression.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Combat (targeting/retaliation) moved to SimpleActor.Combat.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Death/defeat moved to SimpleActor.Death.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Loot/drops moved to SimpleActor.Loot.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private Color GetAttackColor()
	{
		BuildStats stats = CurrentBuildStats;
		if (stats.DamageElementId != "physical")
		{
			return stats.AttackColor;
		}

		return CombatRole switch
		{
			"Tank" => new Color(1.0f, 0.78f, 0.28f, 0.9f),
			"Ranged" => new Color(0.34f, 0.78f, 1.0f, 0.9f),
			"Support" => new Color(0.42f, 1.0f, 0.62f, 0.9f),
			"Gatherer" => new Color(0.72f, 0.92f, 0.38f, 0.9f),
			"Builder" => new Color(0.94f, 0.64f, 0.36f, 0.9f),
			_ => new Color(1.0f, 0.42f, 0.16f, 0.92f),
		};
	}

	private void SpawnSwingEffect(Vector3 targetPosition)
	{
		Vector3 position = GlobalPosition + (targetPosition - GlobalPosition) * 0.5f;
		position.Y = Mathf.Max(GlobalPosition.Y, targetPosition.Y) + 0.95f;
		SpawnCombatEffect(string.Empty, GetAttackColor(), position, 0.34f, 0.36f);
	}

	private void PlayAttackAction(Vector3 targetPosition, bool isHealing)
	{
		SetExternalAnimationState(GetExternalAttackAnimationState(isHealing), 0.48f);
		AnimateAttackPose();
		SpawnAttackProjectile(targetPosition, isHealing);
		if (!UsesProjectileAttack(isHealing))
		{
			SpawnSwingEffect(targetPosition);
		}
	}

	private string GetExternalAttackAnimationState(bool isHealing)
	{
		if (isHealing || CombatRole == "Support")
		{
			return "cast";
		}

		return UsesArrowProjectile(false) ? "shoot" : "attack";
	}

	private void AnimateAttackPose()
	{
		if (_attackPoseTween != null && IsInstanceValid(_attackPoseTween))
		{
			_attackPoseTween.Kill();
		}

		ResetAttackVisualScale();
		Node3D? visualTarget = GetAttackVisualTarget();
		if (visualTarget == null)
		{
			return;
		}

		_attackPoseTarget = visualTarget;
		_attackPoseBaseScale = visualTarget.Scale;
		_attackPoseTween = CreateTween();
		_attackPoseTween.SetTrans(Tween.TransitionType.Sine);
		_attackPoseTween.SetEase(Tween.EaseType.Out);
		_attackPoseTween.TweenProperty(visualTarget, "scale", _attackPoseBaseScale * new Vector3(1.12f, 0.90f, 1.20f), 0.075f);
		_attackPoseTween.TweenProperty(visualTarget, "scale", _attackPoseBaseScale * new Vector3(0.94f, 1.08f, 0.92f), 0.085f);
		_attackPoseTween.TweenProperty(visualTarget, "scale", _attackPoseBaseScale, 0.13f);
	}

	// Combat (whirlwind spin) moved to SimpleActor.Combat.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private Node3D? GetAttackVisualTarget()
	{
		return GetNodeOrNull<Node3D>("ExternalModel")
			?? GetNodeOrNull<Node3D>("BodyCore")
			?? GetNodeOrNull<Node3D>("Torso")
			?? GetNodeOrNull<Node3D>("Head");
	}

	private void ResetAttackVisualScale()
	{
		if (_attackPoseTarget != null && IsInstanceValid(_attackPoseTarget))
		{
			_attackPoseTarget.Scale = _attackPoseBaseScale;
		}

		_attackPoseTarget = null;
		_attackPoseBaseScale = Vector3.One;
	}

	private void SpawnAttackProjectile(Vector3 targetPosition, bool isHealing)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		bool isMelee = !UsesProjectileAttack(isHealing);
		Vector3 toTarget = targetPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		Vector3 forward = toTarget.LengthSquared() > 0.001f ? toTarget.Normalized() : -GlobalTransform.Basis.Z;
		Color color = isHealing ? new Color(0.36f, 1.0f, 0.54f, 0.92f) : GetAttackColor();
		float travelDistance = GlobalPosition.DistanceTo(targetPosition);

		var projectile = new AttackProjectile
		{
			StartPosition = GlobalPosition + Vector3.Up * (isMelee ? 1.04f : 1.22f) + forward * 0.44f,
			EndPosition = targetPosition + Vector3.Up * (isMelee ? 1.02f : 1.16f),
			EffectColor = color,
			IsMelee = isMelee,
			IsHealing = isHealing,
			IsArrow = UsesArrowProjectile(isHealing),
			Radius = isMelee ? 0.24f : 0.20f,
			Lifetime = isMelee
				? 0.16f
				: Mathf.Clamp(travelDistance / 18.0f, 0.24f, 0.48f),
		};
		parent.AddChild(projectile);
	}

	private bool UsesProjectileAttack(bool isHealing)
	{
		return isHealing || IsRangedCombatant || BuildCatalog.HasRangedActiveSkill(BuildLoadout);
	}

	private bool UsesArrowProjectile(bool isHealing)
	{
		return !isHealing && ActorKind == "npc" && CombatRole == "Ranged";
	}

	private void SpawnPlayerAttackCue(Vector3 playerPosition)
	{
		Color color = GetAttackColor();
		SpawnCombatEffect("!", color, GlobalPosition + new Vector3(0.0f, 1.75f, 0.0f), 0.48f, 0.68f);
		SpawnCombatEffect(string.Empty, color, playerPosition + new Vector3(0.0f, 1.12f, 0.0f), 0.42f, 0.72f);
	}

	private void SpawnCombatEffect(int damage, Color color)
	{
		SpawnCombatEffect(damage.ToString(), color, GlobalPosition + new Vector3(0.0f, 1.1f, 0.0f), 0.52f, 0.55f);
	}

	private void SpawnCombatEffect(string text, Color color, Vector3 position, float lifetime, float radius)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		var effect = new CombatEffect
		{
			Text = text,
			EffectColor = color,
			Lifetime = lifetime,
			Radius = radius,
		};
		parent.AddChild(effect);
		effect.GlobalPosition = position;
	}

	private Vector3 SlowToStop(Vector3 velocity, float step)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0.0f, MoveSpeed * 5.0f * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, MoveSpeed * 5.0f * step);
		return velocity;
	}

	// Boss obstacle-avoidance movement moved to SimpleActor.BossMovement.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private void MoveAndSlideWithEffects(float step)
	{
		MoveAndSlide();
		UpdateMovementEffects(step);
		UpdateMovementAnimation(step);
		StabilizeExternalModelRootMotion();
		ApplyWhirlwindSpinRotation();
	}

	// Movement effects, procedural body animation, external-model animation glue,
	// and the cached-child / wander helpers moved to SimpleActor.Animation.cs
	// (Stage-0 separation — see docs/ARCHITECTURE_REVIEW.md).
}
