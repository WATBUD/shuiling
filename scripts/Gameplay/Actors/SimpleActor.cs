using Godot;
using System.Collections.Generic;

public partial class SimpleActor : CharacterBody3D
{
	private const float MinimumCompanionFormationDistance = 3.6f;
	private const float ExternalRootMotionStabilizeSeconds = 0.12f;
	private const int RearCompanionDustStartSlot = 4;

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
	[Export] public string AttackModeId { get; set; } = BuildCatalog.AiCommandPriority;
	[Export] public float DetectionRadius { get; set; } = 12.0f;
	[Export] public float AttackRange { get; set; } = 1.8f;
	[Export] public float AttackCooldown { get; set; } = 1.35f;
	[Export] public bool IsBoss { get; set; }
	[Export] public string BossNameKey { get; set; } = string.Empty;
	[Export] public string BossPrimaryLootId { get; set; } = string.Empty;
	// World Tier this actor was spawned at (docs/world_progression.md). Drives
	// the evolution-stage display name; stats are already baked in at spawn.
	[Export] public int WorldTier { get; set; } = 1;

	private readonly RandomNumberGenerator _rng = new();
	// Multiplayer puppet state: on clients, wild monsters are display-only
	// mirrors of the host's actors (no AI, no local damage — see World.Network.cs).
	private bool _isNetworkPuppet;
	private int _networkId = -1;
	private Vector3 _netTargetPosition;
	private float _netTargetYaw;
	private bool _isCaptured;
	private bool _isInActiveParty;
	private bool _isMountedByPlayer;
	private bool _isDefeated;
	private bool _isAwaitingRecovery;
	private string _fallenMapId = string.Empty;
	private bool _isWorldMapActive = true;
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
	private bool _bossEnraged;
	private int _bossAttackCounter;
	private Vector3 _bossLastChasePosition;
	private Vector3 _bossAvoidDirection;
	private float _bossStuckTime;
	private float _bossAvoidRemaining;
	private float _bossAvoidSide = 1.0f;

	public bool CanBeCaptured => ActorKind == "monster" && !IsBoss && !_isCaptured && !_isDefeated && !_isNetworkPuppet;
	public bool IsNetworkPuppet => _isNetworkPuppet;
	// Host & client both tag their wild monsters with the shared network id so
	// death can be broadcast/looked up without scanning (multiplayer).
	public int NetworkMonsterId { get; set; } = -1;
	public bool IsNpcRecruitCandidate => ActorKind == "npc" && !_isCaptured && !_isDefeated;
	public bool CanJoinByAffinity => IsNpcRecruitCandidate && Affinity >= 80;
	public bool IsCaptured => _isCaptured;
	public bool IsInActiveParty => _isInActiveParty;
	public float MountSeatHeight => Mathf.Max(GetVisualTopY(this) + 0.16f, 0.9f);
	public void SetMountedByPlayer(bool mounted) => _isMountedByPlayer = mounted;
	public bool IsDefeated => _isDefeated;
	public bool IsAwaitingRecovery => _isAwaitingRecovery;
	public string FallenMapId => _fallenMapId;
	public bool IsActiveWorldTarget => !_isCaptured && !_isDefeated && _isWorldMapActive && IsVisibleInTree();
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
	public string BuildAttributeGemName => LocaleText.T(BuildCatalog.GetAttributeGem(BuildLoadout.AttributeGemId).NameKey);
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
	public int ExperienceToNextLevel => 35 + Level * 18 + EvolutionStage * 20;
	public bool CanEvolve => EvolutionStage < 3 && Level >= (EvolutionStage + 1) * 5;
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

	public override void _PhysicsProcess(double delta)
	{
		float step = (float)delta;
		if (_isNetworkPuppet)
		{
			UpdateNetworkPuppet(step);
			return;
		}
		UpdateStatusEffects(step);
		_attackCooldownRemaining = Mathf.Max(_attackCooldownRemaining - step, 0.0f);
		_retaliationTargetRemaining = Mathf.Max(_retaliationTargetRemaining - step, 0.0f);
		_specialControlCooldownRemaining = Mathf.Max(_specialControlCooldownRemaining - step, 0.0f);
		_combatTargetSearchRemaining = Mathf.Max(_combatTargetSearchRemaining - step, 0.0f);
		Vector3 velocity = Velocity;

		if (_isDefeated)
		{
			Velocity = SlowToStop(velocity, step);
			MoveAndSlideWithEffects(step);
			return;
		}

		if (_isMountedByPlayer && _followTarget != null && IsInstanceValid(_followTarget))
		{
			GlobalPosition = _followTarget.GlobalPosition;
			Rotation = _followTarget.Rotation;
			Velocity = _followTarget.Velocity;
			UpdateMovementAnimation(step);
			return;
		}

		if (_stunRemaining > 0.0f)
		{
			Velocity = SlowToStop(Velocity, step);
			MoveAndSlideWithEffects(step);
			return;
		}

		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * step;
		}

		if (_isCaptured)
		{
			FollowCapturedTarget(velocity, step);
			return;
		}

		Node3D? player = GetCachedPlayerNode();
		bool chasing = false;
		Vector3 destination = _targetPosition;

		if (ActorKind == "monster" && player != null)
		{
			if (TryGetRetaliationTarget(out SimpleActor retaliationTarget))
			{
				chasing = true;
				destination = retaliationTarget.GlobalPosition;
				if (TryAttackActorTarget(retaliationTarget, velocity, step))
				{
					return;
				}
			}
			else
			{
				float distanceToPlayer = GlobalPosition.DistanceTo(player.GlobalPosition);
				if (distanceToPlayer <= ChaseRadius)
				{
					chasing = true;
					destination = player.GlobalPosition;
					if (TryAttackPlayer(player, velocity, step))
					{
						return;
					}
				}
			}
		}

		if (!chasing)
		{
			ResetBossObstacleAvoidance();
			_waitTime = Mathf.Max(_waitTime - step, 0.0f);
			if (_waitTime > 0.0f)
			{
				Velocity = SlowToStop(velocity, step);
				MoveAndSlideWithEffects(step);
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
		_isCaptured = true;
		_isDefeated = false;
		_isAwaitingRecovery = false;
		_fallenMapId = string.Empty;
		_followTarget = followTarget;
		_isInActiveParty = false;
		_waitTime = 0.0f;
		ResetSquadActivity();
		Velocity = Vector3.Zero;
		CurrentHealth = Mathf.Max(CurrentHealth, Mathf.RoundToInt(EffectiveMaxHealth * 0.45f));
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		AddToGroup("captured_actors");
		RefreshNameplate();
	}

	public void Recruit(PlayerController followTarget)
	{
		Capture(followTarget);
	}

	public void SetWorldMapActive(bool active)
	{
		if (_isCaptured)
		{
			return;
		}

		_isWorldMapActive = active;
		Visible = active;
		SetPhysicsProcess(active && !_isDefeated);
		if (active && !_isDefeated)
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

	public void CycleBuildEquipment(EquipmentSlot slot)
	{
		BuildLoadout.CycleEquipment(slot);
		MarkBuildChanged();
	}

	public void EquipBuildEquipment(EquipmentSlot slot, string equipmentId)
	{
		if (BuildCatalog.GetEquipment(equipmentId).Slot != slot)
		{
			return;
		}

		BuildLoadout.SetEquipmentId(slot, equipmentId);
		MarkBuildChanged();
	}

	public void ClearBuildLoadout()
	{
		_buildLoadout = new CompanionBuildLoadout
		{
			HelmetId = "equip.helmet.none",
			WeaponId = "equip.weapon.none",
			ArmorId = "equip.armor.none",
			BootsId = "equip.boots.none",
			AccessoryId = "equip.accessory.none",
			AttributeGemId = "gem.attribute.none",
			SkillGemIds = new[] { "gem.skill.none", "gem.skill.none", "gem.skill.none" },
		};
		_buildConfigured = true;
		MarkBuildChanged();
	}

	public void CycleAttributeGem()
	{
		BuildLoadout.CycleAttributeGem();
		MarkBuildChanged();
	}

	public void EquipAttributeGem(string gemId)
	{
		BuildLoadout.AttributeGemId = BuildCatalog.GetAttributeGem(gemId).Id;
		MarkBuildChanged();
	}

	public void CycleSkillGem(int slotIndex)
	{
		BuildLoadout.CycleSkillGem(slotIndex);
		MarkBuildChanged();
	}

	public void EquipSkillGem(int slotIndex, string gemId)
	{
		int safeSlot = Mathf.Clamp(slotIndex, 0, BuildLoadout.SkillGemIds.Length - 1);
		string validatedGemId = BuildCatalog.GetSkillGem(gemId).Id;
		if (validatedGemId == "gem.skill.none")
		{
			ClearSkillGemSlot(safeSlot);
			return;
		}
		if ((safeSlot == 0 && !BuildCatalog.IsMainAttackCore(validatedGemId))
			|| (safeSlot > 0 && !BuildCatalog.IsSupportCore(validatedGemId)))
		{
			return;
		}
		if (safeSlot > 0 && !BuildCatalog.HasMainAttackCore(BuildLoadout))
		{
			return;
		}
		if (BuildCatalog.IsProjectileSupportGem(validatedGemId) && !BuildCatalog.HasRangedActiveSkill(BuildLoadout))
		{
			return;
		}

		BuildLoadout.SkillGemIds[safeSlot] = validatedGemId;
		BuildLoadout.SkillGemLevels[safeSlot] = 1;
		if (!BuildCatalog.HasRangedActiveSkill(BuildLoadout))
		{
			for (int index = 0; index < BuildLoadout.SkillGemIds.Length; index++)
			{
				if (BuildCatalog.IsProjectileSupportGem(BuildLoadout.SkillGemIds[index]))
				{
					BuildLoadout.SkillGemIds[index] = "gem.skill.none";
					BuildLoadout.SkillGemLevels[index] = 1;
				}
			}
		}
		MarkBuildChanged();
	}

	// Empties one support core slot without the ranged-skill cascade that EquipSkillGem
	// applies, so removing the primary (fireball) core leaves the other slots untouched.
	public void ClearSkillGemSlot(int slotIndex)
	{
		int safeSlot = Mathf.Clamp(slotIndex, 0, BuildLoadout.SkillGemIds.Length - 1);
		BuildLoadout.SkillGemIds[safeSlot] = "gem.skill.none";
		BuildLoadout.SkillGemLevels[safeSlot] = 1;
		MarkBuildChanged();
	}

	// Packs slots 1..N inside the support area only. Slot 0 is the permanent main-core
	// slot and must never receive a promoted support core.
	public void CompactSupportCores()
	{
		string[] ids = BuildLoadout.SkillGemIds;
		int[] levels = BuildLoadout.SkillGemLevels;
		var packedIds = new List<string>();
		var packedLevels = new List<int>();
		for (int index = 1; index < ids.Length; index++)
		{
			if (BuildCatalog.IsSupportCore(ids[index]))
			{
				packedIds.Add(ids[index]);
				packedLevels.Add(levels[index]);
			}
		}

		for (int index = 1; index < ids.Length; index++)
		{
			int packedIndex = index - 1;
			ids[index] = packedIndex < packedIds.Count ? packedIds[packedIndex] : "gem.skill.none";
			levels[index] = packedIndex < packedLevels.Count ? packedLevels[packedIndex] : 1;
		}

		MarkBuildChanged();
	}

	public int GetSkillGemLevel(int slotIndex)
	{
		return BuildLoadout.GetSkillGemLevel(slotIndex);
	}

	public int RaiseSkillGemLevel(int slotIndex)
	{
		int safeSlot = Mathf.Clamp(slotIndex, 0, BuildLoadout.SkillGemLevels.Length - 1);
		int nextLevel = Mathf.Min(BuildLoadout.GetSkillGemLevel(safeSlot) + 1, BuildCatalog.MaxSkillGemLevel);
		BuildLoadout.SkillGemLevels[safeSlot] = nextLevel;
		MarkBuildChanged();
		return nextLevel;
	}

	public void CycleAttackMode()
	{
		AttackModeId = BuildCatalog.GetNextAttackModeId(AttackModeId);
		_combatTarget = null;
		_combatTargetSearchRemaining = 0.0f;
		MarkBuildChanged();
	}

	public void SetAttackMode(string modeId)
	{
		AttackModeId = BuildCatalog.GetAttackMode(modeId).Id;
		_combatTarget = null;
		_combatTargetSearchRemaining = 0.0f;
		MarkBuildChanged();
	}

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

	// Turns this actor into a network puppet (multiplayer client): the host
	// drives position/health; local AI and local damage application stop.
	public void SetNetworkPuppet(int networkId)
	{
		_isNetworkPuppet = true;
		_networkId = networkId;
		NetworkMonsterId = networkId;
		_netTargetPosition = GlobalPosition;
		_netTargetYaw = Rotation.Y;
	}

	public void ApplyNetworkState(Vector3 position, float yaw, int health)
	{
		_netTargetPosition = position;
		_netTargetYaw = yaw;
		int clamped = Mathf.Clamp(health, 0, EffectiveMaxHealth);
		if (clamped != CurrentHealth)
		{
			CurrentHealth = clamped;
			RefreshNameplate();
		}
	}

	private void UpdateNetworkPuppet(float step)
	{
		float weight = Mathf.Min(step * 10.0f, 1.0f);
		Vector3 toTarget = _netTargetPosition - GlobalPosition;
		if (toTarget.Length() > 12.0f)
		{
			GlobalPosition = _netTargetPosition;
			Velocity = Vector3.Zero;
		}
		else
		{
			GlobalPosition += toTarget * weight;
			Velocity = toTarget * 10.0f;
		}

		Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, _netTargetYaw, weight), 0.0f);
		UpdateMovementAnimation(step);
		Velocity = Vector3.Zero;
	}

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

	public ActorSaveData ExportSaveData()
	{
		CompanionBuildLoadout loadout = BuildLoadout;
		return new ActorSaveData
		{
			ActorKind = ActorKind,
			DisplayName = DisplayName,
			Level = Level,
			WorldTier = WorldTier,
			MaxHealth = MaxHealth,
			CurrentHealth = CurrentHealth,
			IsDefeated = _isDefeated,
			IsAwaitingRecovery = _isAwaitingRecovery,
			FallenMapId = _fallenMapId,
			WorldPosition = new SaveVector3 { X = GlobalPosition.X, Y = GlobalPosition.Y, Z = GlobalPosition.Z },
			Attack = Attack,
			Defense = Defense,
			MoveSpeed = MoveSpeed,
			ExperienceReward = ExperienceReward,
			GoldReward = GoldReward,
			Experience = Experience,
			EvolutionStage = EvolutionStage,
			SpecialAbility = SpecialAbility,
			AbilityRank = AbilityRank,
			CombatRole = CombatRole,
			Personality = Personality,
			PassiveAbility = PassiveAbility,
			Affinity = Affinity,
			MoodStateId = MoodStateId,
			AttackModeId = AttackModeId,
			BuildLoadout = new CompanionBuildSaveData
			{
				HelmetId = loadout.HelmetId,
				WeaponId = loadout.WeaponId,
				ArmorId = loadout.ArmorId,
				BootsId = loadout.BootsId,
				AccessoryId = loadout.AccessoryId,
				AttributeGemId = loadout.AttributeGemId,
				SkillGemIds = MakeSkillGemIdArray(loadout),
				SkillGemLevels = MakeSkillGemLevelArray(loadout),
			},
		};
	}

	public void ApplySaveData(ActorSaveData data)
	{
		ActorKind = data.ActorKind;
		DisplayName = data.DisplayName;
		Level = Mathf.Max(data.Level, 1);
		WorldTier = WorldTierCatalog.ClampTier(data.WorldTier);
		MaxHealth = Mathf.Max(data.MaxHealth, 1);
		_isDefeated = data.IsDefeated || data.CurrentHealth <= 0;
		_isAwaitingRecovery = _isDefeated && data.IsAwaitingRecovery;
		_fallenMapId = data.FallenMapId;
		CurrentHealth = _isDefeated ? 0 : Mathf.Clamp(data.CurrentHealth, 1, MaxHealth);
		Attack = Mathf.Max(data.Attack, 0);
		Defense = Mathf.Max(data.Defense, 0);
		MoveSpeed = Mathf.Clamp(data.MoveSpeed, 0.3f, 20.0f);
		ExperienceReward = Mathf.Max(data.ExperienceReward, 0);
		GoldReward = Mathf.Max(data.GoldReward, 0);
		Experience = Mathf.Max(data.Experience, 0);
		EvolutionStage = Mathf.Clamp(data.EvolutionStage, 0, 3);
		SpecialAbility = data.SpecialAbility;
		AbilityRank = Mathf.Max(data.AbilityRank, 1);
		CombatRole = string.IsNullOrWhiteSpace(data.CombatRole) ? "DPS" : data.CombatRole;
		Personality = string.IsNullOrWhiteSpace(data.Personality) ? "personality.calm" : data.Personality;
		PassiveAbility = string.IsNullOrWhiteSpace(data.PassiveAbility) ? "ability.none" : data.PassiveAbility;
		Affinity = Mathf.Clamp(data.Affinity, -100, 100);
		MoodStateId = data.MoodStateId;
		AttackModeId = string.IsNullOrWhiteSpace(data.AttackModeId)
			? BuildCatalog.GetDefaultAttackModeId(this)
			: BuildCatalog.GetAttackMode(data.AttackModeId).Id;
		_buildLoadout = new CompanionBuildLoadout
		{
			HelmetId = data.BuildLoadout.HelmetId,
			WeaponId = data.BuildLoadout.WeaponId,
			ArmorId = data.BuildLoadout.ArmorId,
			BootsId = string.IsNullOrWhiteSpace(data.BuildLoadout.BootsId) ? "equip.boots.traveler" : data.BuildLoadout.BootsId,
			AccessoryId = data.BuildLoadout.AccessoryId,
			AttributeGemId = data.BuildLoadout.AttributeGemId,
			SkillGemIds = data.BuildLoadout.SkillGemIds is { Length: > 0 } savedIds
				? (string[])savedIds.Clone()
				: new[] { "gem.skill.none", "gem.skill.none", "gem.skill.none" },
			SkillGemLevels = data.BuildLoadout.SkillGemLevels is { Length: > 0 } savedLevels
				? (int[])savedLevels.Clone()
				: new[] { 1, 1, 1 },
		};
		_buildLoadout.EnsureSkillSlots();
		NormalizeSkillCoreSlots();
		RemoveUnsupportedProjectileGems();
		_buildConfigured = true;
		_buildStatsDirty = true;
		RecalculateBuildStats();
		CurrentHealth = _isDefeated ? 0 : Mathf.Clamp(data.CurrentHealth, 1, EffectiveMaxHealth);
		RefreshNameplate();
	}

	private static string[] MakeSkillGemIdArray(CompanionBuildLoadout loadout)
	{
		var ids = new string[BuildCatalog.SupportCoreSlotCount];
		for (int index = 0; index < ids.Length; index++)
		{
			ids[index] = loadout.GetSkillGemId(index);
		}

		return ids;
	}

	private static int[] MakeSkillGemLevelArray(CompanionBuildLoadout loadout)
	{
		var levels = new int[BuildCatalog.SupportCoreSlotCount];
		for (int index = 0; index < levels.Length; index++)
		{
			levels[index] = loadout.GetSkillGemLevel(index);
		}

		return levels;
	}

	private void EnsureBuildLoadout()
	{
		if (_buildConfigured)
		{
			return;
		}

		_buildLoadout = BuildCatalog.CreateStarterLoadout(this);
		NormalizeSkillCoreSlots();
		RemoveUnsupportedProjectileGems();
		AttackModeId = BuildCatalog.GetAttackMode(AttackModeId).Id;
		_buildConfigured = true;
		_buildStatsDirty = true;
	}

	private void NormalizeSkillCoreSlots()
	{
		_buildLoadout.EnsureSkillSlots();
		string[] ids = _buildLoadout.SkillGemIds;
		int[] levels = _buildLoadout.SkillGemLevels;
		string mainId = "gem.skill.none";
		int mainLevel = 1;
		var supportIds = new List<string>();
		var supportLevels = new List<int>();

		for (int index = 0; index < ids.Length; index++)
		{
			if (mainId == "gem.skill.none" && BuildCatalog.IsMainAttackCore(ids[index]))
			{
				mainId = ids[index];
				mainLevel = Mathf.Max(levels[index], 1);
			}
			else if (BuildCatalog.IsSupportCore(ids[index]))
			{
				supportIds.Add(ids[index]);
				supportLevels.Add(Mathf.Max(levels[index], 1));
			}
		}

		ids[0] = mainId;
		levels[0] = mainLevel;
		for (int index = 1; index < ids.Length; index++)
		{
			int supportIndex = index - 1;
			ids[index] = supportIndex < supportIds.Count ? supportIds[supportIndex] : "gem.skill.none";
			levels[index] = supportIndex < supportLevels.Count ? supportLevels[supportIndex] : 1;
		}
	}

	private void RemoveUnsupportedProjectileGems()
	{
		if (BuildCatalog.HasRangedActiveSkill(_buildLoadout))
		{
			return;
		}

		for (int index = 0; index < _buildLoadout.SkillGemIds.Length; index++)
		{
			if (BuildCatalog.IsProjectileSupportGem(_buildLoadout.SkillGemIds[index]))
			{
				_buildLoadout.SkillGemIds[index] = "gem.skill.none";
				_buildLoadout.SkillGemLevels[index] = 1;
			}
		}
	}

	private void RecalculateBuildStats()
	{
		EnsureBuildLoadout();
		_buildStats = BuildCatalog.CalculateStats(this, _buildLoadout);
		_buildStats.Attack = Mathf.Max(Mathf.RoundToInt(_buildStats.Attack * _formationAttackMultiplier), 1);
		_buildStats.Defense = Mathf.Max(Mathf.RoundToInt(_buildStats.Defense * _formationDefenseMultiplier), 0);
		_buildStats.AttackCooldownMultiplier *= _formationCooldownMultiplier;
		_buildStats.IncomingDamageMultiplier *= _formationIncomingDamageMultiplier;
		_buildStats.AttackRangeBonus += _formationRangeBonus;
		_buildStatsDirty = false;
		CurrentHealth = Mathf.Clamp(CurrentHealth, 0, _buildStats.MaxHealth);
	}

	private void MarkBuildChanged()
	{
		_buildStatsDirty = true;
		RecalculateBuildStats();
		RefreshNameplate();
		_followTarget?.RecalculateFormationBonuses();
	}

	private void MarkBaseStatsChanged()
	{
		_buildStatsDirty = true;
		if (_buildConfigured)
		{
			RecalculateBuildStats();
		}
	}

	public void GrantTraining(int amount)
	{
		Experience += Mathf.Max(amount, 0);
		while (Experience >= ExperienceToNextLevel)
		{
			Experience -= ExperienceToNextLevel;
			LevelUp();
		}

		RefreshNameplate();
	}

	public bool TryEvolve()
	{
		if (!CanEvolve)
		{
			return false;
		}

		EvolutionStage++;
		MaxHealth += 36 + EvolutionStage * 14;
		CurrentHealth = MaxHealth;
		Attack += 8 + EvolutionStage * 2;
		Defense += 6 + EvolutionStage * 2;
		AbilityRank++;
		MarkBaseStatsChanged();
		ApplyEvolutionAppearance();
		CurrentHealth = EffectiveMaxHealth;
		RefreshNameplate();
		return true;
	}

	public void EnhanceAbility()
	{
		if (SpecialAbility == "ability.none")
		{
			SpecialAbility = ActorKind == "monster" ? "ability.monster.burst" : "ability.npc.tactics";
		}

		AbilityRank++;
		Attack += ActorKind == "monster" ? 2 : 1;
		Defense += ActorKind == "monster" ? 1 : 2;
		MarkBaseStatsChanged();
		RefreshNameplate();
	}

	public int ReceiveDamage(int rawDamage, SimpleActor? attacker)
	{
		if (_isDefeated)
		{
			return 0;
		}

		// Network puppet: the host owns this monster's health. Forward the raw
		// damage and show a local hit flash; real damage syncs back via state.
		if (_isNetworkPuppet)
		{
			NetworkManager.Instance?.SendMonsterDamageRequest(_networkId, Mathf.Max(rawDamage, 1));
			SpawnCombatEffect(rawDamage, attacker?.GetAttackColor() ?? new Color(1.0f, 0.5f, 0.22f, 0.92f));
			return 0;
		}

		float elementMultiplier = attacker == null
			? 1.0f
			: ElementChart.GetMultiplier(attacker.CurrentBuildStats.DamageElementId, CurrentBuildStats.DamageElementId);
		int elementalDamage = Mathf.Max(Mathf.RoundToInt(rawDamage * elementMultiplier * CurrentBuildStats.IncomingDamageMultiplier), 1);
		int mitigatedDamage = Mathf.Max(elementalDamage - Mathf.RoundToInt(EffectiveDefense * 0.35f), 1);
		if (CurrentBuildStats.HasShieldSkill)
		{
			mitigatedDamage = Mathf.Max(Mathf.RoundToInt(mitigatedDamage * 0.78f), 1);
			SpawnCombatEffect(string.Empty, new Color(0.35f, 0.78f, 1.0f, 0.78f), GlobalPosition + new Vector3(0.0f, 1.0f, 0.0f), 0.28f, 0.82f);
		}
		RememberAttacker(attacker);
		CurrentHealth = Mathf.Max(CurrentHealth - mitigatedDamage, 0);
		SpawnCombatEffect(mitigatedDamage, attacker?.GetAttackColor() ?? new Color(1.0f, 0.5f, 0.22f, 0.92f));
		if (IsBoss && attacker?._followTarget != null && IsInstanceValid(attacker._followTarget))
		{
			attacker._followTarget.NotifyBossCombat(this);
		}
		else if (attacker?.IsBoss == true && _followTarget != null && IsInstanceValid(_followTarget))
		{
			_followTarget.NotifyBossCombat(attacker);
		}
		if (IsBoss && !_bossEnraged && CurrentHealth > 0 && HealthRatio <= 0.50f)
		{
			TriggerBossEnrage(attacker);
		}

		if (CurrentHealth <= 0)
		{
			Defeat(attacker);
		}

		RefreshNameplate();
		return mitigatedDamage;
	}

	private void TriggerBossEnrage(SimpleActor? attacker)
	{
		_bossEnraged = true;
		Node effectParent = GetTree().CurrentScene ?? GetParent();
		SkillAttackVfx.SpawnSpecial(
			effectParent,
			SkillAttackVfx.ExplosionEvent,
			GlobalPosition + Vector3.Up * 0.35f,
			Vector3.Up,
			"gem.skill.meteor",
			"fire",
			new Color(1.0f, 0.16f, 0.04f, 0.94f),
			4.2f,
			new ProjectileBehaviorProfile());
		if (attacker?._followTarget != null && IsInstanceValid(attacker._followTarget))
		{
			attacker._followTarget.ShowBossEnraged(this);
		}
		RefreshNameplate();
	}

	public int ReceiveHealing(int rawHealing)
	{
		if (_isDefeated)
		{
			return 0;
		}

		int missingHealth = Mathf.Max(EffectiveMaxHealth - CurrentHealth, 0);
		int healing = Mathf.Min(Mathf.Max(rawHealing, 0), missingHealth);
		if (healing <= 0)
		{
			return 0;
		}

		CurrentHealth += healing;
		SpawnCombatEffect($"+{healing}", new Color(0.36f, 1.0f, 0.54f, 0.92f), GlobalPosition + new Vector3(0.0f, 1.3f, 0.0f), 0.58f, 0.46f);
		RefreshNameplate();
		return healing;
	}

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

	private void CreateNameplate()
	{
		_nameplate = new Label3D
		{
			Name = "Nameplate",
			Position = new Vector3(0.0f, 2.35f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FixedSize = false,
			NoDepthTest = false,
			FontSize = 20,
			PixelSize = 0.0075f,
			OutlineSize = 6,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Width = 320.0f,
		};
		AddChild(_nameplate);

		// Shaded so the marker reads as a real 3D ball from every angle rather
		// than a flat unshaded disc; kept self-lit via emission so it still pops
		// in dark biomes.
		_nameplateMarkerMaterial = MakeMarkerBallMaterial(new Color(1.0f, 0.28f, 0.20f, 0.92f));
		_nameplateHaloMaterial = MakeMarkerMaterial(new Color(1.0f, 0.28f, 0.20f, 0.34f), 0.35f);
		_nameplateMarker = new MeshInstance3D
		{
			Name = "NameplateMarker",
			// Full, smooth sphere (Height = 2 x Radius) — round from all 360°.
			Mesh = new SphereMesh { Radius = 0.085f, Height = 0.17f, RadialSegments = 24, Rings = 12 },
			MaterialOverride = _nameplateMarkerMaterial,
		};
		_nameplateHalo = new MeshInstance3D
		{
			Name = "NameplateHalo",
			Mesh = new TorusMesh { InnerRadius = 0.018f, OuterRadius = 0.34f },
			RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
			MaterialOverride = _nameplateHaloMaterial,
		};
		AddChild(_nameplateHalo);
		AddChild(_nameplateMarker);
		RefreshNameplate();
	}

	private void RefreshNameplate()
	{
		if (_nameplate == null)
		{
			return;
		}

		string capturedText = _isAwaitingRecovery
			? LocaleText.T("actor.nameplate.awaiting_recovery")
			: _isDefeated
			? LocaleText.T("actor.nameplate.defeated")
			: _isCaptured
			? _isInActiveParty ? LocaleText.T("actor.nameplate.active") : LocaleText.T("actor.nameplate.stored")
			: string.Empty;
		_nameplate.Text = IsBoss
			? LocaleText.F("boss.nameplate", Level, LocalizedDisplayName, capturedText)
			: $"{LocaleText.T("actor.level_prefix")}{Level} {LocalizedDisplayName}{capturedText}";
		_nameplate.FontSize = IsBoss ? 28 : 20;
		Color markerColor = GetNameplateStatusColor();
		_nameplate.Modulate = markerColor;
		_nameplate.OutlineModulate = new Color(0.02f, 0.025f, 0.03f, 0.96f);
		if (_nameplateMarkerMaterial != null)
		{
			_nameplateMarkerMaterial.AlbedoColor = markerColor;
			_nameplateMarkerMaterial.Emission = markerColor;
		}

		if (_nameplateHaloMaterial != null)
		{
			_nameplateHaloMaterial.AlbedoColor = new Color(markerColor.R, markerColor.G, markerColor.B, _isCaptured ? 0.45f : 0.30f);
			_nameplateHaloMaterial.Emission = markerColor;
		}
		if (_nameplateHalo != null)
		{
			_nameplateHalo.Visible = !IsBoss;
		}

		UpdateNameplatePosition();
	}

	private void UpdateNameplatePosition()
	{
		if (_nameplate == null)
		{
			return;
		}

		float visualTop = GetVisualTopY(this);
		float fallbackTop = ActorKind == "monster" ? 2.2f : 2.05f;
		float labelY = Mathf.Max(visualTop + 0.38f, fallbackTop);
		_nameplate.Position = new Vector3(0.0f, labelY, 0.0f);
		if (_nameplateMarker != null)
		{
			_nameplateMarker.Position = new Vector3(0.0f, labelY + 0.34f, 0.0f);
			float markerScale = IsBoss ? 1.65f : _isCaptured ? 1.18f : 1.0f;
			_nameplateMarker.Scale = Vector3.One * markerScale;
		}

		if (_nameplateHalo != null)
		{
			_nameplateHalo.Position = new Vector3(0.0f, labelY + 0.28f, 0.0f);
			float haloScale = IsBoss ? 2.15f : _isCaptured ? 1.18f : 1.0f;
			_nameplateHalo.Scale = Vector3.One * haloScale;
		}
	}

	private Color GetNameplateStatusColor()
	{
		if (_isDefeated)
		{
			return new Color(0.62f, 0.66f, 0.70f, 0.88f);
		}

		if (_isCaptured)
		{
			return _isInActiveParty
				? new Color(0.28f, 1.0f, 0.74f, 0.96f)
				: new Color(0.42f, 0.86f, 1.0f, 0.94f);
		}

		if (ActorKind != "monster")
		{
			return new Color(0.64f, 0.86f, 1.0f, 0.94f);
		}

		if (IsBoss)
		{
			return _bossEnraged
				? new Color(1.0f, 0.22f, 0.08f, 0.98f)
				: new Color(1.0f, 0.76f, 0.18f, 0.98f);
		}

		return MonsterSpeciesCatalog.Current.GetMarkerColor(DisplayName);
	}

	private static StandardMaterial3D MakeMarkerMaterial(Color color, float emissionEnergy)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = emissionEnergy,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			NoDepthTest = true,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
	}

	// Marker ball: lit (per-pixel) so its curvature is visible as a rounded
	// gradient from any direction, with mild emission so it self-illuminates.
	private static StandardMaterial3D MakeMarkerBallMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = 0.35f,
			Roughness = 0.4f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
		};
	}

	private float GetVisualTopY(Node node)
	{
		float topY = 0.0f;
		foreach (Node child in node.GetChildren())
		{
			if (child == _nameplate || child == _nameplateMarker || child == _nameplateHalo || child is CollisionShape3D)
			{
				continue;
			}

			if (child is MeshInstance3D meshInstance && meshInstance.Mesh != null)
			{
				topY = Mathf.Max(topY, GetMeshTopY(meshInstance));
			}

			topY = Mathf.Max(topY, GetVisualTopY(child));
		}

		return topY;
	}

	private float GetMeshTopY(MeshInstance3D meshInstance)
	{
		Aabb aabb = meshInstance.GetAabb();
		float topY = 0.0f;
		for (int x = 0; x <= 1; x++)
		{
			for (int y = 0; y <= 1; y++)
			{
				for (int z = 0; z <= 1; z++)
				{
					var corner = new Vector3(
						x == 0 ? aabb.Position.X : aabb.Position.X + aabb.Size.X,
						y == 0 ? aabb.Position.Y : aabb.Position.Y + aabb.Size.Y,
						z == 0 ? aabb.Position.Z : aabb.Position.Z + aabb.Size.Z
					);
					Vector3 actorLocalCorner = ToLocal(meshInstance.ToGlobal(corner));
					topY = Mathf.Max(topY, actorLocalCorner.Y);
				}
			}
		}

		return topY;
	}

	private void FollowCapturedTarget(Vector3 velocity, float step)
	{
		if (!_isInActiveParty || _followTarget == null || !IsInstanceValid(_followTarget))
		{
			SetFollowLagBubbleVisible(false);
			Velocity = SlowToStop(velocity, step);
			MoveAndSlideWithEffects(step);
			return;
		}

		float distanceToPlayer = GlobalPosition.DistanceTo(_followTarget.GlobalPosition);
		UpdatePetDialogue(distanceToPlayer, step);

		if (TryUseSupportBuild(ref velocity, step))
		{
			_squadActivity = SquadActivity.Follow;
			_squadThinkRemaining = 1.2f;
			Velocity = velocity;
			MoveAndSlideWithEffects(step);
			return;
		}

		if (TryCompanionCombat(ref velocity, step))
		{
			_squadActivity = SquadActivity.Follow;
			_squadThinkRemaining = 1.6f;
			Velocity = velocity;
			MoveAndSlideWithEffects(step);
			return;
		}

		UpdateSquadActivity(step);
		Vector3 destination = GetLivingSquadDestination();
		Vector3 toDestination = destination - GlobalPosition;
		toDestination.Y = 0.0f;

		float followSpeed = GetLivingSquadMoveSpeed(distanceToPlayer);
		float stopDistance = _squadActivity == SquadActivity.Rest ? 0.85f : 0.55f;
		if (toDestination.Length() > stopDistance)
		{
			Vector3 direction = toDestination.Normalized();
			velocity.X = Mathf.MoveToward(velocity.X, direction.X * followSpeed, followSpeed * 7.0f * step);
			velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * followSpeed, followSpeed * 7.0f * step);
			FaceDirection(direction, step);
		}
		else
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(GetLivingSquadLookDirection(destination), step);
		}

		Velocity = velocity;
		MoveAndSlideWithEffects(step);
	}

	private Vector3 GetFollowDestination()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return GlobalPosition;
		}

		return _followTarget.GlobalPosition + PlayerLocalToWorld(GetFormationLocalOffset());
	}

	private Vector3 GetLivingSquadDestination()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return GlobalPosition;
		}

		Vector3 formationOffset = GetFormationLocalOffset();
		Vector3 localOffset = _squadActivity switch
		{
			SquadActivity.Guard => formationOffset * 0.72f + _squadActivityLocalOffset * 0.28f,
			SquadActivity.Scout => _squadActivityLocalOffset,
			SquadActivity.Gather => _squadActivityLocalOffset,
			SquadActivity.Roam => _squadActivityLocalOffset,
			SquadActivity.Rest => formationOffset * 0.82f + _squadActivityLocalOffset * 0.18f,
			_ => formationOffset,
		};

		return _followTarget.GlobalPosition + PlayerLocalToWorld(localOffset);
	}

	private void UpdateSquadActivity(float step)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return;
		}

		float distanceToPlayer = GlobalPosition.DistanceTo(_followTarget.GlobalPosition);
		if (distanceToPlayer > GetFormationRegroupDistance())
		{
			_squadActivity = SquadActivity.Follow;
			_squadActivityRemaining = 1.0f;
			_squadThinkRemaining = 1.2f;
			return;
		}

		_squadActivityRemaining = Mathf.Max(_squadActivityRemaining - step, 0.0f);
		_squadThinkRemaining = Mathf.Max(_squadThinkRemaining - step, 0.0f);
		if (_squadActivityRemaining > 0.0f || _squadThinkRemaining > 0.0f)
		{
			return;
		}

		ChooseSquadActivity();
	}

	private void ChooseSquadActivity()
	{
		BuildStats stats = CurrentBuildStats;
		float roll = _rng.Randf();
		_squadActivity = ChooseLivingSquadActivity(stats, roll);
		_squadActivityLocalOffset = MakeActivityLocalOffset(_squadActivity);
		_squadActivityRemaining = (float)_rng.RandfRange(2.4f, 6.8f);
		_squadThinkRemaining = (float)_rng.RandfRange(0.3f, 1.2f);
	}

	private SquadActivity ChooseLivingSquadActivity(BuildStats stats, float roll)
	{
		// Idle behavior no longer varies by combat mode: companions loosely follow the
		// player and occasionally guard, roam, or rest.
		return roll < 0.36f
			? SquadActivity.Follow
			: roll < 0.62f
				? SquadActivity.Guard
				: roll < 0.82f
					? SquadActivity.Roam
					: SquadActivity.Rest;
	}

	private Vector3 MakeActivityLocalOffset(SquadActivity activity)
	{
		Vector3 formation = GetFormationLocalOffset();
		float side = _followSlot % 2 == 0 ? -1.0f : 1.0f;
		return activity switch
		{
			SquadActivity.Guard => formation + new Vector3(side * (float)_rng.RandfRange(0.45f, 1.15f), 0.0f, (float)_rng.RandfRange(-0.5f, 1.0f)),
			SquadActivity.Scout => new Vector3(side * (float)_rng.RandfRange(1.0f, 3.8f), 0.0f, (float)_rng.RandfRange(4.8f, 8.0f)),
			SquadActivity.Gather => RandomLocalRingOffset(3.4f, 6.4f),
			SquadActivity.Roam => RandomLocalRingOffset(3.0f, 7.2f),
			SquadActivity.Rest => formation + RandomLocalRingOffset(0.2f, 0.9f),
			_ => formation,
		};
	}

	private Vector3 RandomLocalRingOffset(float minRadius, float maxRadius)
	{
		float angle = (float)_rng.RandfRange(-Mathf.Pi, Mathf.Pi);
		float radius = (float)_rng.RandfRange(minRadius, maxRadius) * CurrentBuildStats.FollowDistanceMultiplier;
		return new Vector3(Mathf.Sin(angle) * radius, 0.0f, Mathf.Cos(angle) * radius);
	}

	private Vector3 GetFormationLocalOffset()
	{
		Vector3 offset = _followTarget != null && IsInstanceValid(_followTarget)
			? _followTarget.GetFormationLocalOffset(this)
			: Vector3.Zero;
		if (offset.LengthSquared() <= 0.001f)
		{
			offset = new Vector3(0.0f, 0.0f, MinimumCompanionFormationDistance);
		}

		return KeepFormationOffsetOutsidePlayer(offset * CurrentBuildStats.FollowDistanceMultiplier);
	}

	private static Vector3 KeepFormationOffsetOutsidePlayer(Vector3 offset)
	{
		float distance = new Vector2(offset.X, offset.Z).Length();
		if (distance >= MinimumCompanionFormationDistance || distance <= 0.001f)
		{
			return offset;
		}

		float scale = MinimumCompanionFormationDistance / distance;
		return new Vector3(offset.X * scale, offset.Y, offset.Z * scale);
	}

	private Vector3 PlayerLocalToWorld(Vector3 localOffset)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return localOffset;
		}

		Vector3 forward = -_followTarget.GlobalTransform.Basis.Z;
		forward.Y = 0.0f;
		forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;

		Vector3 right = _followTarget.GlobalTransform.Basis.X;
		right.Y = 0.0f;
		right = right.LengthSquared() > 0.001f ? right.Normalized() : Vector3.Right;
		return right * localOffset.X + forward * localOffset.Z;
	}

	private float GetLivingSquadMoveSpeed(float distanceToPlayer)
	{
		float multiplier = _squadActivity switch
		{
			SquadActivity.Scout => 1.10f,
			SquadActivity.Gather or SquadActivity.Roam => 0.92f,
			SquadActivity.Guard => 1.0f,
			SquadActivity.Rest => 0.68f,
			_ => 1.05f,
		};

		float normalSpeed = Mathf.Max(EffectiveMoveSpeed * multiplier, 4.2f);
		float catchUpBonus = Mathf.Clamp((distanceToPlayer - 6.5f) * 0.58f, 0.0f, 5.2f);
		return normalSpeed + catchUpBonus;
	}

	private void SetFollowLagBubbleVisible(bool visible)
	{
		if (!visible)
		{
			if (_followLagBubble != null)
			{
				_followLagBubble.Visible = false;
			}

			return;
		}

		if (_followLagBubble == null)
		{
			float visualTop = GetVisualTopY(this);
			_followLagBubble = new Node3D
			{
				Name = "FollowLagBubble",
				Position = new Vector3(0.0f, Mathf.Max(visualTop + 0.78f, 2.75f), 0.0f),
			};
			AddChild(_followLagBubble);
			CreateFollowLagBubbleVisual(_followLagBubble, _petDialogueText);
		}

		_followLagBubble.Visible = true;
	}

	private void UpdatePetDialogue(float distanceToPlayer, float step)
	{
		bool isInCombat = IsPetInCombat();
		if (isInCombat && _showingLagDialogue)
		{
			ClearLagDialogue();
			_nextPetDialogueDelay = 0.0f;
			return;
		}

		if (!isInCombat && distanceToPlayer > 12.0f)
		{
			_showingLagDialogue = true;
			ShowPetDialogue("主人等等我QQ....");
			_petDialogueRemaining = 0.0f;
			return;
		}

		if (_showingLagDialogue)
		{
			ClearLagDialogue();
			_nextPetDialogueDelay = (float)_rng.RandfRange(7.0f, 15.0f);
			return;
		}

		if (_petDialogueRemaining > 0.0f)
		{
			_petDialogueRemaining = Mathf.Max(_petDialogueRemaining - step, 0.0f);
			SetFollowLagBubbleVisible(_petDialogueRemaining > 0.0f);
			return;
		}

		SetFollowLagBubbleVisible(false);
		_nextPetDialogueDelay -= step;
		if (_nextPetDialogueDelay > 0.0f)
		{
			return;
		}

		string[] quotePool = isInCombat ? PetCombatQuotes : PetDailyQuotes;
		string quote = quotePool[_rng.RandiRange(0, quotePool.Length - 1)];
		ShowPetDialogue(quote);
		_petDialogueRemaining = (float)_rng.RandfRange(2.6f, 4.0f);
		_nextPetDialogueDelay = (float)_rng.RandfRange(7.0f, 15.0f);
	}

	private void ClearLagDialogue()
	{
		_showingLagDialogue = false;
		_petDialogueText = string.Empty;
		_petDialogueRemaining = 0.0f;
		SetFollowLagBubbleVisible(false);
	}

	private bool IsPetInCombat()
	{
		if (_combatTarget != null && IsInstanceValid(_combatTarget) && _combatTarget.IsHostileToPlayer)
		{
			return true;
		}

		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return false;
		}

		SimpleActor? focusedTarget = _followTarget.FocusedTarget;
		return focusedTarget != null
			&& IsInstanceValid(focusedTarget)
			&& focusedTarget.IsHostileToPlayer
			&& (GlobalPosition.DistanceTo(focusedTarget.GlobalPosition) <= Mathf.Max(EffectiveDetectionRadius * 1.85f, 18.0f)
				|| _followTarget.GlobalPosition.DistanceTo(focusedTarget.GlobalPosition) <= Mathf.Max(EffectiveDetectionRadius * 1.85f, 18.0f));
	}

	private void ShowPetDialogue(string text)
	{
		if (_petDialogueText != text)
		{
			_petDialogueText = text;
			if (_followLagBubble != null)
			{
				_followLagBubble.QueueFree();
				_followLagBubble = null;
			}
		}

		SetFollowLagBubbleVisible(true);
	}

	private static void CreateFollowLagBubbleVisual(Node3D bubble, string bubbleText)
	{
		const int fontSize = 80;
		const int horizontalPadding = 36;
		const int verticalPadding = 22;
		const int outerMargin = 3;
		const int tailHeight = 20;
		Vector2 measuredText = ThemeDB.FallbackFont.GetStringSize(bubbleText, HorizontalAlignment.Left, -1, fontSize);
		int panelWidth = Mathf.CeilToInt(measuredText.X) + horizontalPadding;
		int panelHeight = Mathf.CeilToInt(measuredText.Y) + verticalPadding;
		int viewportWidth = panelWidth + outerMargin * 2;
		int viewportHeight = panelHeight + outerMargin + tailHeight;
		float centerX = viewportWidth * 0.5f;

		var viewport = new SubViewport
		{
			Name = "BubbleViewport",
			Size = new Vector2I(viewportWidth, viewportHeight),
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
		};
		bubble.AddChild(viewport);

		var root = new Control
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(viewportWidth, viewportHeight),
		};
		viewport.AddChild(root);

		var tail = new Polygon2D
		{
			Polygon = new Vector2[]
			{
				new(centerX - 14.0f, outerMargin + panelHeight - 2.0f),
				new(centerX + 14.0f, outerMargin + panelHeight - 2.0f),
				new(centerX, viewportHeight - 1.0f),
			},
			Color = new Color(0.96f, 0.94f, 0.88f, 0.80f),
		};
		root.AddChild(tail);

		var panel = new PanelContainer
		{
			Position = new Vector2(outerMargin, outerMargin),
			Size = new Vector2(panelWidth, panelHeight),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipContents = true,
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.96f, 0.94f, 0.88f, 0.55f),
			BorderColor = new Color(0.27f, 0.22f, 0.20f, 0.80f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 13,
			CornerRadiusTopRight = 13,
			CornerRadiusBottomLeft = 13,
			CornerRadiusBottomRight = 13,
			ContentMarginLeft = 12.0f,
			ContentMarginRight = 12.0f,
			ContentMarginTop = 5.0f,
			ContentMarginBottom = 5.0f,
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		root.AddChild(panel);

		var bubbleGradient = new Gradient
		{
			Offsets = new float[] { 0.0f, 0.52f, 1.0f },
			Colors = new Color[]
			{
				new(1.0f, 0.99f, 0.95f, 0.56f),
				new(0.98f, 0.94f, 0.84f, 0.56f),
				new(0.93f, 0.82f, 0.63f, 0.56f),
			},
		};
		var gradientTexture = new GradientTexture2D
		{
			Gradient = bubbleGradient,
			Width = panelWidth,
			Height = panelHeight,
			FillFrom = new Vector2(0.5f, 0.0f),
			FillTo = new Vector2(0.5f, 1.0f),
		};
		var gradientLayer = new TextureRect
		{
			Texture = gradientTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		panel.AddChild(gradientLayer);

		var label = new Label
		{
			Text = bubbleText,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", new Color(0.20f, 0.16f, 0.15f, 1.0f));
		panel.AddChild(label);

		var sprite = new Sprite3D
		{
			Name = "BubbleSprite",
			Texture = viewport.GetTexture(),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
			PixelSize = 0.0055f,
			Position = new Vector3(0.0f, 0.0f, 0.0f),
		};
		bubble.AddChild(sprite);
	}

	private Vector3 GetLivingSquadLookDirection(Vector3 destination)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return destination - GlobalPosition;
		}

		return _squadActivity switch
		{
			SquadActivity.Guard or SquadActivity.Scout => GlobalPosition - _followTarget.GlobalPosition,
			SquadActivity.Gather or SquadActivity.Roam => destination - GlobalPosition,
			_ => GetFollowTargetFacingDirection(),
		};
	}

	private Vector3 GetFollowTargetFacingDirection()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return Vector3.Forward;
		}

		Vector3 forward = -_followTarget.GlobalTransform.Basis.Z;
		forward.Y = 0.0f;
		return forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;
	}

	private void ResetSquadActivity()
	{
		_squadActivity = SquadActivity.Follow;
		_squadActivityLocalOffset = GetFormationLocalOffset();
		_squadActivityRemaining = 0.0f;
		_squadThinkRemaining = (float)_rng.RandfRange(0.15f, 1.1f);
	}

	private float GetFormationRegroupDistance()
	{
		return 12.5f;
	}

	private void LevelUp()
	{
		Level++;
		MaxHealth += 14 + EvolutionStage * 4;
		CurrentHealth = MaxHealth;
		Attack += 3 + EvolutionStage;
		Defense += 2 + EvolutionStage;
		MarkBaseStatsChanged();
		CurrentHealth = EffectiveMaxHealth;
	}

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

		if (_combatTarget != null && IsValidCommandTarget(_combatTarget) && _combatTarget.IsHostileToPlayer && GlobalPosition.DistanceTo(_combatTarget.GlobalPosition) <= EffectiveDetectionRadius * 1.35f)
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
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
		{
			if (node is not SimpleActor actor || !actor.IsHostileToPlayer)
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

	private bool IsValidCommandTarget(SimpleActor? actor)
	{
		return actor != null && IsInstanceValid(actor) && actor.IsActiveWorldTarget && actor != this;
	}

	private void AttackActor(SimpleActor target)
	{
		if (_attackCooldownRemaining > 0.0f)
		{
			return;
		}

		// Only captured companions use the gem-driven projectile system. Wild monsters
		// (e.g. retaliating against a companion) keep the direct instant-hit path, whose
		// targeting differs from the companion "hostile monster" search.
		if (_isCaptured)
		{
			LaunchAttack(target);
		}
		else
		{
			LegacyAttackActor(target);
		}

		_attackCooldownRemaining = EffectiveAttackCooldown;
	}

	private void LegacyAttackActor(SimpleActor target)
	{
		BuildStats stats = CurrentBuildStats;
		int roleBonus = CombatRole == "DPS" ? 4 : CombatRole == "Tank" ? 1 : CombatRole == "Ranged" ? 2 : 0;
		int affinityBonus = Affinity >= 80 ? 2 : Affinity >= 55 ? 1 : 0;
		int damage = Mathf.Max(stats.Attack + roleBonus + affinityBonus, 1);
		if (_rng.Randf() < stats.CritChance)
		{
			damage = Mathf.RoundToInt(damage * 1.55f);
		}

		PlayAttackAction(target.GlobalPosition, false);
		int dealtDamage = target.ReceiveDamage(damage, this);
		if (dealtDamage > 0 && _rng.Randf() < stats.ControlChance)
		{
			target.ApplyElementStatus(stats.DamageElementId, this);
		}

		if (stats.LifeStealPercent > 0.0f && dealtDamage > 0)
		{
			ReceiveHealing(Mathf.RoundToInt(dealtDamage * stats.LifeStealPercent));
		}

		AdvanceBossAttackPattern(target);
	}

	// Base attack of the pet, shaped by whatever behavior gems the build carries.
	// Damage is no longer applied instantly; each spawned projectile carries it and
	// resolves on impact through ResolveProjectileHit.
	private void LaunchAttack(SimpleActor target)
	{
		BuildStats stats = CurrentBuildStats;
		int roleBonus = CombatRole == "DPS" ? 4 : CombatRole == "Tank" ? 1 : CombatRole == "Ranged" ? 2 : 0;
		int affinityBonus = Affinity >= 80 ? 2 : Affinity >= 55 ? 1 : 0;
		int baseDamage = Mathf.Max(stats.Attack + roleBonus + affinityBonus, 1);

		SetExternalAnimationState(GetExternalAttackAnimationState(false), 0.48f);
		AnimateAttackPose();

		bool isMelee = !UsesProjectileAttack(false);
		if (isMelee)
		{
			SpawnSwingEffect(target.GlobalPosition);
		}

		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		Vector3 forward = toTarget.LengthSquared() > 0.001f ? toTarget.Normalized() : -GlobalTransform.Basis.Z;
		string visualSkillId = BuildLoadout.GetSkillGemId(0);
		if (BuildCatalog.IsMainAttackCore(visualSkillId))
		{
			Node effectParent = GetTree().CurrentScene ?? GetParent();
			SkillAttackVfx.SpawnCast(
				effectParent,
				GlobalPosition + Vector3.Up * 1.05f + forward * 0.34f,
				forward,
				visualSkillId,
				stats.DamageElementId,
				GetAttackColor(),
				stats.Behavior,
				stats.LifeStealPercent > 0.0f);
		}

		bool usesWhirlwind = isMelee && BuildLoadout.HasSkill("gem.skill.whirlwind");
		int projectileCount = usesWhirlwind ? 3 : 1 + Mathf.Max(stats.Behavior.ExtraProjectiles, 0);
		float spreadStep = Mathf.DegToRad(usesWhirlwind ? 32.0f : 14.0f);
		for (int index = 0; index < projectileCount; index++)
		{
			float angle = (index - (projectileCount - 1) / 2.0f) * spreadStep;
			Vector3 direction = forward.Rotated(Vector3.Up, angle);
			SimpleActor? homing = Mathf.Abs(angle) < 0.001f ? target : null;
			SpawnCombatProjectile(direction, homing, baseDamage, stats, isMelee);
		}
	}

	private void SpawnCombatProjectile(Vector3 direction, SimpleActor? homingTarget, int baseDamage, BuildStats stats, bool isMelee)
	{
		Node? parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		var projectile = new CombatProjectile
		{
			Attacker = this,
			Damage = baseDamage,
			EffectColor = GetAttackColor(),
			IsMelee = isMelee,
			IsArrow = UsesArrowProjectile(false),
			VisualSkillId = BuildCatalog.IsMainAttackCore(BuildLoadout.GetSkillGemId(0))
				? BuildLoadout.GetSkillGemId(0)
				: stats.ActiveRangedSkillId,
			ElementId = stats.DamageElementId,
			HasLifeSteal = stats.LifeStealPercent > 0.0f,
			Speed = isMelee ? 26.0f : 17.0f,
			MaxRange = Mathf.Max(EffectiveAttackRange * 1.6f, isMelee ? 3.0f : 9.0f),
			HitRadius = isMelee ? 1.35f : 1.0f,
			InitialTarget = homingTarget,
			LaunchDirection = direction,
			SpawnOrigin = GlobalPosition + Vector3.Up * (isMelee ? 1.04f : 1.22f) + direction * 0.5f,
			Behavior = stats.Behavior.Clone(),
		};
		parent.AddChild(projectile);
	}

	// Called by a CombatProjectile when it strikes a target. Centralizes the crit roll,
	// elemental damage, on-hit control status, and life steal so all combat math stays here.
	public int ResolveProjectileHit(SimpleActor target, int baseDamage)
	{
		if (target == null || !IsInstanceValid(target) || !target.IsActiveWorldTarget)
		{
			return 0;
		}

		BuildStats stats = CurrentBuildStats;
		int damage = Mathf.Max(baseDamage, 1);
		if (_rng.Randf() < stats.CritChance)
		{
			damage = Mathf.RoundToInt(damage * 1.55f);
		}

		int dealtDamage = target.ReceiveDamage(damage, this);
		if (dealtDamage > 0 && _rng.Randf() < stats.ControlChance)
		{
			target.ApplyElementStatus(stats.DamageElementId, this);
		}

		if (stats.LifeStealPercent > 0.0f && dealtDamage > 0)
		{
			ReceiveHealing(Mathf.RoundToInt(dealtDamage * stats.LifeStealPercent));
		}

		return dealtDamage;
	}

	// Hostile actors within radius of a point, skipping any already struck by the
	// projectile. Used for chain retargeting, split fan-out, and explosion splash.
	public List<SimpleActor> FindProjectileTargets(Vector3 center, float radius, ICollection<SimpleActor> exclude)
	{
		var results = new List<SimpleActor>();
		float radiusSquared = radius * radius;
		center.Y = 0.0f;
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
		{
			if (node is not SimpleActor actor || !actor.IsHostileToPlayer)
			{
				continue;
			}

			if (exclude != null && exclude.Contains(actor))
			{
				continue;
			}

			Vector3 actorPosition = actor.GlobalPosition;
			actorPosition.Y = 0.0f;
			if (center.DistanceSquaredTo(actorPosition) <= radiusSquared)
			{
				results.Add(actor);
			}
		}

		return results;
	}

	private void ApplyElementStatus(string elementId, SimpleActor source)
	{
		_statusSource = source;
		switch (elementId)
		{
			case "ice":
				_slowRemaining = Mathf.Max(_slowRemaining, 3.0f);
				break;
			case "lightning":
				_stunRemaining = Mathf.Max(_stunRemaining, 0.9f);
				break;
			case "poison":
				_poisonRemaining = Mathf.Max(_poisonRemaining, 5.0f);
				break;
			case "fire":
				_burnRemaining = Mathf.Max(_burnRemaining, 4.0f);
				break;
		}
	}

	private void UpdateStatusEffects(float step)
	{
		_slowRemaining = Mathf.Max(_slowRemaining - step, 0.0f);
		_stunRemaining = Mathf.Max(_stunRemaining - step, 0.0f);
		_poisonRemaining = Mathf.Max(_poisonRemaining - step, 0.0f);
		_burnRemaining = Mathf.Max(_burnRemaining - step, 0.0f);
		if (_poisonRemaining <= 0.0f && _burnRemaining <= 0.0f)
		{
			_statusTickRemaining = 0.0f;
			return;
		}

		_statusTickRemaining -= step;
		if (_statusTickRemaining > 0.0f || _isDefeated)
		{
			return;
		}

		_statusTickRemaining = 1.0f;
		int damage = (_poisonRemaining > 0.0f ? Mathf.Max(2, Mathf.RoundToInt(EffectiveMaxHealth * 0.025f)) : 0)
			+ (_burnRemaining > 0.0f ? Mathf.Max(3, Mathf.RoundToInt(EffectiveMaxHealth * 0.035f)) : 0);
		ReceiveDamage(damage, _statusSource);
	}

	private void ApplyEvolutionAppearance()
	{
		float scale = 1.0f + EvolutionStage * 0.08f;
		Scale = Vector3.One * scale;
	}

	private bool TryAttackPlayer(Node3D player, Vector3 velocity, float step)
	{
		Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
		toPlayer.Y = 0.0f;
		if (toPlayer.Length() > EffectiveAttackRange)
		{
			return false;
		}

		Velocity = SlowToStop(velocity, step);
		FaceDirection(toPlayer, step);
		if (_attackCooldownRemaining <= 0.0f && player is PlayerController playerController)
		{
			SpawnPlayerAttackCue(player.GlobalPosition);
			PlayAttackAction(player.GlobalPosition, false);
			playerController.ReceiveDamage(EffectiveAttack, this);
			if (DisplayName == "name.monster.cave_spider"
				&& _specialControlCooldownRemaining <= 0.0f
				&& _rng.Randf() <= 0.38f
				&& playerController.TryApplySpiderWebSuspension(this))
			{
				_specialControlCooldownRemaining = 8.0f;
			}
			AdvanceBossAttackPattern(playerController);
			_attackCooldownRemaining = EffectiveAttackCooldown;
		}

		MoveAndSlideWithEffects(step);
		return true;
	}

	private void AdvanceBossAttackPattern(Node primaryTarget)
	{
		if (!IsBoss || _isDefeated)
		{
			return;
		}

		_bossAttackCounter++;
		int attackInterval = _bossEnraged ? 2 : 3;
		if (_bossAttackCounter % attackInterval != 0)
		{
			return;
		}

		const float novaRadius = 4.8f;
		Node effectParent = GetTree().CurrentScene ?? GetParent();
		SkillAttackVfx.SpawnSpecial(
			effectParent,
			SkillAttackVfx.ExplosionEvent,
			GlobalPosition + Vector3.Up * 0.24f,
			Vector3.Up,
			"gem.skill.explosion",
			"fire",
			_bossEnraged ? new Color(1.0f, 0.18f, 0.035f, 0.94f) : new Color(1.0f, 0.68f, 0.12f, 0.92f),
			novaRadius,
			new ProjectileBehaviorProfile { ExplosionRadius = novaRadius });

		int splashDamage = Mathf.Max(Mathf.RoundToInt(EffectiveAttack * (_bossEnraged ? 0.68f : 0.52f)), 1);
		foreach (Node node in GetTree().GetNodesInGroup("captured_actors"))
		{
			if (node is SimpleActor companion
				&& companion != primaryTarget
				&& companion.IsInActiveParty
				&& !companion.IsDefeated
				&& companion.GlobalPosition.DistanceTo(GlobalPosition) <= novaRadius)
			{
				companion.ReceiveDamage(splashDamage, this);
			}
		}

		Node3D? player = GetCachedPlayerNode();
		if (player is PlayerController playerController
			&& playerController != primaryTarget
			&& playerController.GlobalPosition.DistanceTo(GlobalPosition) <= novaRadius)
		{
			playerController.ReceiveDamage(Mathf.Max(Mathf.RoundToInt(splashDamage * 0.72f), 1), this);
		}
	}

	private bool TryAttackActorTarget(SimpleActor target, Vector3 velocity, float step)
	{
		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		if (toTarget.Length() > EffectiveAttackRange)
		{
			return false;
		}

		Velocity = SlowToStop(velocity, step);
		FaceDirection(toTarget, step);
		AttackActor(target);
		MoveAndSlideWithEffects(step);
		return true;
	}

	private void RememberAttacker(SimpleActor? attacker)
	{
		if (ActorKind != "monster" || !IsActiveWorldTarget || !IsValidRetaliationTarget(attacker))
		{
			return;
		}

		_retaliationTarget = attacker;
		_retaliationTargetRemaining = 8.0f;
		_combatTarget = attacker;
	}

	private bool TryGetRetaliationTarget(out SimpleActor target)
	{
		target = null!;
		SimpleActor? retaliationTarget = _retaliationTarget;
		if (_retaliationTargetRemaining <= 0.0f || !IsValidRetaliationTarget(retaliationTarget))
		{
			_retaliationTarget = null;
			return false;
		}

		if (GlobalPosition.DistanceTo(retaliationTarget!.GlobalPosition) > ChaseRadius * 1.65f)
		{
			_retaliationTarget = null;
			_retaliationTargetRemaining = 0.0f;
			return false;
		}

		target = retaliationTarget;
		return true;
	}

	private bool IsValidRetaliationTarget(SimpleActor? actor)
	{
		if (actor == null || !IsInstanceValid(actor) || actor == this || actor.IsDefeated || !actor.IsVisibleInTree())
		{
			return false;
		}

		return actor.IsInActiveParty || actor.IsActiveWorldTarget;
	}

	private void Defeat(SimpleActor? attacker)
	{
		_isDefeated = true;
		CurrentHealth = 0;
		Velocity = Vector3.Zero;
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		_retaliationTarget = null;
		_retaliationTargetRemaining = 0.0f;
		_combatTarget = null;

		if (_isCaptured)
		{
			Affinity = Mathf.Max(Affinity - 12, -100);
			UpdateNegativeMoodAfterDefeat();
			_isAwaitingRecovery = true;
			_fallenMapId = _followTarget?.GetParent() is World world ? world.ActiveMapId : string.Empty;
			_isInActiveParty = false;
			CollisionLayer = _defaultCollisionLayer;
			CollisionMask = _defaultCollisionMask;
			Visible = true;
			SetPhysicsProcess(false);
			ApplyDefeatedPose();
			SpawnCombatEffect(LocaleText.F("effect.affinity_loss", 12), new Color(1.0f, 0.28f, 0.22f, 0.92f), GlobalPosition + new Vector3(0.0f, 1.15f, 0.0f), 0.95f, 0.72f);
			RefreshNameplate();
			if (_followTarget != null && IsInstanceValid(_followTarget))
			{
				_followTarget.OnCompanionFallen(this);
			}
			return;
		}

		_isInActiveParty = false;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);

		if (attacker?._followTarget != null && IsInstanceValid(attacker._followTarget))
		{
			attacker._followTarget.PostSystemMessage(LocaleText.F("system.combat.defeated", attacker.LocalizedDisplayName, LocalizedDisplayName), new Color(1.0f, 0.70f, 0.42f), GameMessageChannel.Combat);
			attacker._followTarget.GrantCombatExperience(ExperienceReward);
			if (ActorKind == "monster")
			{
				DropMonsterLoot(attacker._followTarget);
			}
			if (IsBoss)
			{
				attacker._followTarget.ShowBossDefeated(this);
			}
		}

		// Tier unlock is per-player: only the LOCAL player's kill counts here.
		// Remote players' kills are credited on their own machine via RPC
		// (World.ApplyNetworkMonsterDamage → ClientReceiveBossDefeat).
		if (IsBoss
			&& ActorKind == "monster"
			&& attacker?._followTarget != null
			&& IsInstanceValid(attacker._followTarget)
			&& FindOwningWorld() is World bossWorld)
		{
			bossWorld.OnWildBossDefeated(this);
		}

		// Multiplayer: tell the host to broadcast this monster's removal now,
		// instead of waiting for the next periodic state sweep (removes the
		// death lag/pop clients would otherwise see).
		if (ActorKind == "monster" && NetworkMonsterId >= 0)
		{
			FindOwningWorld()?.OnNetworkMonsterDefeated(this);
		}
	}

	private World? FindOwningWorld()
	{
		Node? node = GetParent();
		while (node != null && node is not World)
		{
			node = node.GetParent();
		}

		return node as World;
	}

	private void UpdateNegativeMoodAfterDefeat()
	{
		if (Affinity >= 0)
		{
			MoodStateId = string.Empty;
			return;
		}

		if (Affinity <= -60)
		{
			MoodStateId = "actor.mood.wants_to_escape";
			return;
		}

		if (Affinity <= -30)
		{
			MoodStateId = _rng.Randf() < 0.55f ? "actor.mood.depressed" : "actor.mood.wants_to_escape";
			return;
		}

		string[] mildNegativeMoods =
		{
			"actor.mood.depressed",
			"actor.mood.afraid",
			"actor.mood.sulking",
		};
		MoodStateId = mildNegativeMoods[_rng.RandiRange(0, mildNegativeMoods.Length - 1)];
	}

	private void DropMonsterLoot(PlayerController player)
	{
		if (IsBoss)
		{
			DropBossLoot(player);
			return;
		}

		Vector3 origin = GlobalPosition;
		int goldAmount = Mathf.Max(GoldReward + _rng.RandiRange(1, Mathf.Max(Level + 2, 3)), 1);
		SpawnWorldDrop(origin + RandomDropOffset(0.45f), string.Empty, 1, goldAmount);
		string primaryLootId = MonsterLootCatalog.PickPrimaryDropForMonster(DisplayName, IsRangedCombatant, Level);
		int primaryAmount = Level >= 6 && _rng.Randf() < 0.42f ? 2 : 1;
		SpawnWorldDrop(origin + RandomDropOffset(0.78f), primaryLootId, primaryAmount, 0);

		if (_rng.Randf() < 0.34f)
		{
			string secondaryLootId = MonsterLootCatalog.PickSecondaryDropForMonster(primaryLootId, Level);
			SpawnWorldDrop(origin + RandomDropOffset(0.95f), secondaryLootId, 1, 0);
		}

		if (_rng.Randf() < 0.36f)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.18f), PickEquipmentDropId(), 1, 0);
		}

		if (_rng.Randf() < 0.22f)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.32f), PickGemDropId(), 1, 0);
		}

		player.PostSystemMessage(LocaleText.F("system.drop.loot", LocalizedDisplayName, LocaleText.T(MonsterLootCatalog.GetNameKey(primaryLootId))), new Color(1.0f, 0.86f, 0.48f), GameMessageChannel.Loot);
	}

	private void DropBossLoot(PlayerController player)
	{
		Vector3 origin = GlobalPosition;
		int goldAmount = Mathf.Max(GoldReward + _rng.RandiRange(Level * 4, Level * 8), 1);
		SpawnWorldDrop(origin + RandomDropOffset(0.55f), string.Empty, 1, goldAmount);

		string primaryLootId = string.IsNullOrWhiteSpace(BossPrimaryLootId)
			? MonsterLootCatalog.PickPrimaryDropForMonster(DisplayName, IsRangedCombatant, Level)
			: BossPrimaryLootId;
		SpawnWorldDrop(origin + RandomDropOffset(0.85f), primaryLootId, _rng.RandiRange(4, 6), 0);
		string secondaryLootId = MonsterLootCatalog.PickSecondaryDropForMonster(primaryLootId, Level + 5);
		SpawnWorldDrop(origin + RandomDropOffset(1.05f), secondaryLootId, _rng.RandiRange(2, 3), 0);

		// Bosses always drop two high-value equipment pieces and at least one core.
		SpawnWorldDrop(origin + RandomDropOffset(1.25f), PickBossEquipmentDropId(), 1, 0);
		SpawnWorldDrop(origin + RandomDropOffset(1.48f), PickBossEquipmentDropId(), 1, 0);
		SpawnWorldDrop(origin + RandomDropOffset(1.68f), PickNonFreeSkillGem(BuildCatalog.GetSkillGemDefinitions()), 1, 0);
		if (_rng.Randf() < 0.65f)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.88f), PickNonFreeSkillGem(BuildCatalog.GetSkillGemDefinitions()), 1, 0);
		}

		player.PostSystemMessage(LocaleText.F("system.drop.boss_loot", LocalizedDisplayName), new Color(1.0f, 0.78f, 0.22f), GameMessageChannel.Loot);
	}

	private void SpawnWorldDrop(Vector3 position, string itemId, int amount, int goldAmount)
	{
		var drop = new WorldDrop
		{
			ItemId = itemId,
			Amount = Mathf.Max(amount, 1),
			GoldAmount = Mathf.Max(goldAmount, 0),
		};

		Node parent = GetTree().CurrentScene ?? GetParent();
		parent.AddChild(drop);
		drop.GlobalPosition = new Vector3(position.X, 0.04f, position.Z);
	}

	private Vector3 RandomDropOffset(float radius)
	{
		float angle = (float)_rng.RandfRange(0.0f, Mathf.Tau);
		float distance = (float)_rng.RandfRange(radius * 0.35f, radius);
		return new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance);
	}

	private string PickEquipmentDropId()
	{
		EquipmentSlot[] slots =
		{
			EquipmentSlot.Helmet,
			EquipmentSlot.Weapon,
			EquipmentSlot.Armor,
			EquipmentSlot.Boots,
			EquipmentSlot.Accessory,
		};
		EquipmentSlot slot = slots[_rng.RandiRange(0, slots.Length - 1)];
		var definitions = BuildCatalog.GetEquipmentDefinitions(slot);
		return definitions[_rng.RandiRange(0, definitions.Count - 1)].Id;
	}

	private string PickBossEquipmentDropId()
	{
		EquipmentSlot[] slots =
		{
			EquipmentSlot.Helmet,
			EquipmentSlot.Weapon,
			EquipmentSlot.Armor,
			EquipmentSlot.Boots,
			EquipmentSlot.Accessory,
		};
		EquipmentSlot slot = slots[_rng.RandiRange(0, slots.Length - 1)];
		EquipmentDefinition? best = null;
		float bestScore = float.MinValue;
		foreach (EquipmentDefinition item in BuildCatalog.GetEquipmentDefinitions(slot))
		{
			if (BuildCatalog.IsFreeItem(item.Id))
			{
				continue;
			}

			float score = item.MaxHealthBonus
				+ item.AttackBonus * 3.0f
				+ item.DefenseBonus * 2.0f
				+ item.MoveSpeedBonus * 120.0f
				+ item.AttackCooldownReduction * 150.0f
				+ item.AttackRangeBonus * 12.0f
				+ item.CritChanceBonus * 180.0f
				+ item.SocketCount * 10.0f;
			if (score > bestScore)
			{
				bestScore = score;
				best = item;
			}
		}

		return best?.Id ?? PickEquipmentDropId();
	}

	private string PickGemDropId()
	{
		int kind = _rng.RandiRange(0, 1);
		if (kind == 0)
		{
			var gems = BuildCatalog.GetAttributeGemDefinitions();
			return PickNonFreeAttributeGem(gems);
		}

		var skillGems = BuildCatalog.GetSkillGemDefinitions();
		return PickNonFreeSkillGem(skillGems);
	}

	private int PickValidGemIndex(int count)
	{
		return Mathf.Clamp(_rng.RandiRange(1, Mathf.Max(count - 1, 1)), 0, Mathf.Max(count - 1, 0));
	}

	private string PickNonFreeAttributeGem(System.Collections.Generic.List<AttributeGemDefinition> gems)
	{
		for (int attempt = 0; attempt < 12; attempt++)
		{
			string id = gems[PickValidGemIndex(gems.Count)].Id;
			if (!BuildCatalog.IsFreeItem(id))
			{
				return id;
			}
		}

		return "gem.attribute.fire";
	}

	private string PickNonFreeSkillGem(System.Collections.Generic.List<SkillGemDefinition> gems)
	{
		for (int attempt = 0; attempt < 12; attempt++)
		{
			string id = gems[PickValidGemIndex(gems.Count)].Id;
			if (!BuildCatalog.IsFreeItem(id))
			{
				return id;
			}
		}

		return "gem.skill.fireball";
	}

	private void ApplyDefeatedPose()
	{
		SetExternalAnimationState("death");
		ResetAttackVisualScale();
		RotationDegrees = new Vector3(0.0f, RotationDegrees.Y, 88.0f);
		SetChildRotation("Head", ActorKind == "monster" ? new Vector3(22.0f, 0.0f, -16.0f) : new Vector3(28.0f, 0.0f, -12.0f));
		SetChildRotation("TailBase", new Vector3(82.0f, 0.0f, 0.0f));
	}

	private void ApplyLivingPose()
	{
		RotationDegrees = new Vector3(0.0f, RotationDegrees.Y, 0.0f);
		ApplyEvolutionAppearance();
		ResetAttackVisualScale();
		SetExternalAnimationState("idle");
	}

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

	private Vector3 GetBossChaseDirection(Vector3 directDirection, float step)
	{
		if (!IsBoss || directDirection.LengthSquared() <= 0.001f)
		{
			return directDirection;
		}

		Vector3 planarPosition = GlobalPosition;
		planarPosition.Y = 0.0f;
		if (_bossLastChasePosition == Vector3.Zero)
		{
			_bossLastChasePosition = planarPosition;
		}

		float progress = planarPosition.DistanceTo(_bossLastChasePosition);
		_bossLastChasePosition = planarPosition;
		_bossAvoidRemaining = Mathf.Max(_bossAvoidRemaining - step, 0.0f);
		if (progress < 0.035f)
		{
			_bossStuckTime += step;
		}
		else
		{
			_bossStuckTime = Mathf.Max(_bossStuckTime - step * 2.5f, 0.0f);
		}

		Vector3 wallNormal = GetBossBlockingWallNormal();
		bool blockedByWall = wallNormal.LengthSquared() > 0.001f;
		bool stuck = _bossStuckTime >= 0.32f;
		if ((blockedByWall && _bossAvoidRemaining <= 0.0f) || stuck)
		{
			if (blockedByWall)
			{
				Vector3 tangentA = new(-wallNormal.Z, 0.0f, wallNormal.X);
				Vector3 tangentB = -tangentA;
				float scoreA = tangentA.Dot(directDirection);
				float scoreB = tangentB.Dot(directDirection);
				if (Mathf.Abs(scoreA - scoreB) < 0.08f)
				{
					_bossAvoidDirection = _bossAvoidSide > 0.0f ? tangentA : tangentB;
				}
				else
				{
					_bossAvoidDirection = scoreA > scoreB ? tangentA : tangentB;
					_bossAvoidSide = _bossAvoidDirection == tangentA ? 1.0f : -1.0f;
				}

				// A small outward component keeps the enlarged boss collider from
				// continuously scraping the same tree or building corner.
				_bossAvoidDirection = (_bossAvoidDirection + wallNormal * 0.24f).Normalized();
			}
			else
			{
				_bossAvoidSide *= -1.0f;
				Vector3 side = new Vector3(-directDirection.Z, 0.0f, directDirection.X) * _bossAvoidSide;
				_bossAvoidDirection = (side * 0.92f + directDirection * 0.22f).Normalized();
			}

			_bossAvoidRemaining = stuck ? 0.95f : 0.72f;
			_bossStuckTime = 0.0f;
		}

		if (_bossAvoidRemaining > 0.0f && _bossAvoidDirection.LengthSquared() > 0.001f)
		{
			return (_bossAvoidDirection * 0.88f + directDirection * 0.42f).Normalized();
		}

		return directDirection;
	}

	private Vector3 GetBossBlockingWallNormal()
	{
		for (int index = 0; index < GetSlideCollisionCount(); index++)
		{
			KinematicCollision3D collision = GetSlideCollision(index);
			Vector3 normal = collision.GetNormal();
			normal.Y = 0.0f;
			if (normal.LengthSquared() > 0.16f)
			{
				return normal.Normalized();
			}
		}

		return Vector3.Zero;
	}

	private void ResetBossObstacleAvoidance()
	{
		if (!IsBoss)
		{
			return;
		}

		_bossLastChasePosition = Vector3.Zero;
		_bossAvoidDirection = Vector3.Zero;
		_bossStuckTime = 0.0f;
		_bossAvoidRemaining = 0.0f;
	}

	private void MoveAndSlideWithEffects(float step)
	{
		MoveAndSlide();
		UpdateMovementEffects(step);
		UpdateMovementAnimation(step);
		StabilizeExternalModelRootMotion();
	}

	private void UpdateMovementEffects(float step)
	{
		_footstepEffectRemaining = Mathf.Max(_footstepEffectRemaining - step, 0.0f);
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		if (_isCaptured && _followSlot >= RearCompanionDustStartSlot)
		{
			_footstepEffectRemaining = Mathf.Max(_footstepEffectRemaining, speed > EffectiveMoveSpeed * 1.65f ? 0.22f : 0.36f);
			return;
		}

		if (!IsOnFloor() || speed < 0.85f || _footstepEffectRemaining > 0.0f)
		{
			return;
		}

		bool isFastStep = speed > Mathf.Max(EffectiveMoveSpeed * 1.65f, 3.6f);
		SpawnMovementDust(planarVelocity.Normalized(), speed, isFastStep);
		_footstepEffectRemaining = isFastStep ? 0.16f : 0.28f;
	}

	private void SpawnMovementDust(Vector3 moveDirection, float speed, bool isFastStep)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		Vector3 back = -moveDirection;
		Vector3 side = new(-moveDirection.Z, 0.0f, moveDirection.X);
		float footSide = Mathf.Sin((Time.GetTicksMsec() + GetInstanceId()) * 0.016f) >= 0.0f ? 1.0f : -1.0f;
		Color color = ActorKind == "monster"
			? (isFastStep ? new Color(0.82f, 0.42f, 0.32f, 0.70f) : new Color(0.62f, 0.44f, 0.36f, 0.58f))
			: (isFastStep ? new Color(0.78f, 0.86f, 0.92f, 0.66f) : new Color(0.62f, 0.70f, 0.72f, 0.54f));

		var dust = new MovementDustEffect
		{
			DustColor = color,
			Radius = isFastStep ? 0.22f : 0.15f,
			Lifetime = isFastStep ? 0.34f : 0.42f,
			IsFastStep = isFastStep,
			DirectionYaw = Mathf.RadToDeg(Mathf.Atan2(-moveDirection.X, -moveDirection.Z)),
		};
		parent.AddChild(dust);
		dust.GlobalPosition = GlobalPosition + back * Mathf.Clamp(speed * 0.032f, 0.14f, 0.40f) + side * footSide * 0.15f + Vector3.Up * 0.04f;
	}

	private void UpdateMovementAnimation(float step)
	{
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		float speedReference = Mathf.Max(EffectiveMoveSpeed * 1.55f, 7.0f);
		float moveRatio = Mathf.Clamp(speed / speedReference, 0.0f, 1.0f);
		bool isMoving = speed > 0.18f && IsOnFloor() && !_isDefeated;
		float phaseSpeed = Mathf.Lerp(5.5f, 10.8f, moveRatio);

		if (isMoving)
		{
			_movementAnimationPhase += step * phaseSpeed;
		}
		else
		{
			_movementAnimationPhase = Mathf.Lerp(_movementAnimationPhase, 0.0f, Mathf.Min(step * 8.0f, 1.0f));
		}

		UpdateExternalMovementAnimation(step, isMoving, speed);

		if (ActorKind == "monster")
		{
			UpdateMonsterMovementAnimation(isMoving, moveRatio);
		}
		else
		{
			UpdateHumanoidMovementAnimation(isMoving, moveRatio);
		}
	}

	private void UpdateHumanoidMovementAnimation(bool isMoving, float moveRatio)
	{
		float swing = Mathf.Sin(_movementAnimationPhase);
		float counterSwing = Mathf.Sin(_movementAnimationPhase + Mathf.Pi);
		float intensity = isMoving ? Mathf.Lerp(0.38f, 1.0f, moveRatio) : 0.0f;
		float bob = Mathf.Abs(swing) * 0.035f * intensity;
		float lean = moveRatio * -3.6f;

		SetChildPosition("Body", new Vector3(0.0f, 1.02f + bob, 0.0f));
		SetChildPosition("Tunic", new Vector3(0.0f, 1.04f + bob, -0.26f));
		SetChildRotation("Body", new Vector3(lean, 0.0f, swing * 1.4f * intensity));
		SetChildRotation("Head", new Vector3(Mathf.Abs(swing) * 2.0f * intensity, 0.0f, -swing * 1.2f * intensity));

		SetChildRotation("LeftLeg", new Vector3(swing * 25.0f * intensity, 0.0f, -1.8f * intensity));
		SetChildRotation("RightLeg", new Vector3(counterSwing * 25.0f * intensity, 0.0f, 1.8f * intensity));
		SetChildPosition("LeftBoot", new Vector3(-0.14f, 0.06f + Mathf.Max(counterSwing, 0.0f) * 0.06f * intensity, -0.05f + swing * 0.045f * intensity));
		SetChildPosition("RightBoot", new Vector3(0.14f, 0.06f + Mathf.Max(swing, 0.0f) * 0.06f * intensity, -0.05f + counterSwing * 0.045f * intensity));

		SetChildRotation("LeftArm", new Vector3(counterSwing * 22.0f * intensity, 0.0f, -9.0f - swing * 4.0f * intensity));
		SetChildRotation("RightArm", new Vector3(swing * 22.0f * intensity, 0.0f, 9.0f - counterSwing * 4.0f * intensity));
		SetChildPosition("LeftGlove", new Vector3(-0.44f, 0.66f + counterSwing * 0.055f * intensity, -0.03f - counterSwing * 0.075f * intensity));
		SetChildPosition("RightGlove", new Vector3(0.44f, 0.66f + swing * 0.055f * intensity, -0.03f - swing * 0.075f * intensity));
		SetChildRotation("Cape", new Vector3(-8.0f + Mathf.Abs(swing) * 6.0f * intensity, 0.0f, -swing * 2.2f * intensity));
	}

	private void UpdateMonsterMovementAnimation(bool isMoving, float moveRatio)
	{
		float phaseA = Mathf.Sin(_movementAnimationPhase);
		float phaseB = Mathf.Sin(_movementAnimationPhase + Mathf.Pi);
		float liftA = Mathf.Max(phaseA, 0.0f);
		float liftB = Mathf.Max(phaseB, 0.0f);
		float intensity = isMoving ? Mathf.Lerp(0.42f, 1.0f, moveRatio) : 0.0f;
		float bob = Mathf.Abs(phaseA) * 0.045f * intensity;
		float lean = moveRatio * -4.8f;

		SetChildPosition("BodyCore", new Vector3(0.0f, 0.74f + bob, 0.10f));
		SetChildPosition("Head", new Vector3(0.0f, 1.18f + bob * 0.65f, -0.92f));
		SetChildRotation("BodyCore", new Vector3(lean + phaseA * 2.0f * intensity, 0.0f, phaseA * 2.2f * intensity));
		SetChildRotation("Head", new Vector3(phaseB * 3.0f * intensity, 0.0f, -phaseA * 1.5f * intensity));

		SetChildRotation("LeftForeLeg", new Vector3(7.0f + phaseA * 26.0f * intensity, 0.0f, -7.0f));
		SetChildRotation("RightForeLeg", new Vector3(7.0f + phaseB * 26.0f * intensity, 0.0f, 7.0f));
		SetChildRotation("LeftBackLeg", new Vector3(-8.0f + phaseB * 24.0f * intensity, 0.0f, -8.0f));
		SetChildRotation("RightBackLeg", new Vector3(-8.0f + phaseA * 24.0f * intensity, 0.0f, 8.0f));

		SetChildPosition("LeftFrontPaw", new Vector3(-0.42f, 0.13f + liftA * 0.08f * intensity, -0.70f + phaseA * 0.055f * intensity));
		SetChildPosition("RightFrontPaw", new Vector3(0.42f, 0.13f + liftB * 0.08f * intensity, -0.70f + phaseB * 0.055f * intensity));
		SetChildPosition("LeftBackPaw", new Vector3(-0.46f, 0.13f + liftB * 0.07f * intensity, 0.68f + phaseB * 0.05f * intensity));
		SetChildPosition("RightBackPaw", new Vector3(0.46f, 0.13f + liftA * 0.07f * intensity, 0.68f + phaseA * 0.05f * intensity));

		SetChildRotation("TailBase", new Vector3(64.0f + Mathf.Abs(phaseA) * 5.0f * intensity, phaseA * 9.0f * intensity, 0.0f));
		SetChildPosition("TailTip", new Vector3(phaseA * 0.05f * intensity, 0.38f + bob * 0.45f, 1.42f));
	}

	private void UpdateExternalMovementAnimation(float step, bool isMoving, float speed)
	{
		if (_isDefeated)
		{
			SetExternalAnimationState("death");
			return;
		}

		if (_externalAnimationLockRemaining > 0.0f)
		{
			_externalAnimationLockRemaining = Mathf.Max(_externalAnimationLockRemaining - step, 0.0f);
			return;
		}

		float runThreshold = Mathf.Max(EffectiveMoveSpeed * 1.12f, 6.4f);
		string state = isMoving
			? speed >= runThreshold ? "run" : "walk"
			: "idle";
		SetExternalAnimationState(state);
	}

	private void SetExternalAnimationState(string state, float lockDuration = 0.0f)
	{
		if (_externalAnimationState == state && lockDuration <= 0.0f)
		{
			return;
		}

		bool played = ExternalModelLibrary.TryPlayActorAnimation(this, state);
		if (played)
		{
			_externalAnimationState = state;
			StabilizeExternalModelRootMotion();
		}

		if (played && lockDuration > 0.0f)
		{
			_externalAnimationLockRemaining = lockDuration;
		}
	}

	private void SetChildPosition(string nodeName, Vector3 position)
	{
		if (GetCachedChildNode(nodeName) is Node3D node)
		{
			node.Position = position;
		}
	}

	private void SetChildRotation(string nodeName, Vector3 rotationDegrees)
	{
		if (GetCachedChildNode(nodeName) is Node3D node)
		{
			node.RotationDegrees = rotationDegrees;
		}
	}

	private Node3D? GetCachedChildNode(string nodeName)
	{
		if (_childNodeCache.TryGetValue(nodeName, out Node3D? cachedNode))
		{
			if (cachedNode == null || IsInstanceValid(cachedNode))
			{
				return cachedNode;
			}

			_childNodeCache.Remove(nodeName);
		}

		Node3D? node = GetNodeOrNull<Node3D>(nodeName);
		_childNodeCache[nodeName] = node;
		return node;
	}

	private void StabilizeExternalModelRootMotion()
	{
		Node3D? model = GetCachedExternalModelNode();
		if (model == null)
		{
			return;
		}

		ExternalModelLibrary.StabilizeRootMotion(model, Vector3.Zero, new Vector3(0.0f, 180.0f, 0.0f));
	}

	private Node3D? GetCachedExternalModelNode()
	{
		if (_externalModelNode != null)
		{
			if (IsInstanceValid(_externalModelNode))
			{
				return _externalModelNode;
			}

			_externalModelNode = null;
			_externalModelLookupAttempted = false;
		}

		if (_externalModelLookupAttempted)
		{
			return null;
		}

		_externalModelLookupAttempted = true;
		_externalModelNode = GetNodeOrNull<Node3D>("ExternalModel");
		return _externalModelNode;
	}

	private void FaceDirection(Vector3 direction, float step)
	{
		if (direction.LengthSquared() == 0.0f)
		{
			return;
		}

		float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);
		Vector3 rotation = Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetAngle, Mathf.Min(step * 8.0f, 1.0f));
		Rotation = rotation;
	}

	private Vector3 PickWanderTarget()
	{
		if (WanderRadius <= 0.05f)
		{
			return HomePosition;
		}

		float angle = (float)_rng.RandfRange(0.0f, Mathf.Tau);
		float distance = (float)_rng.RandfRange(Mathf.Min(2.0f, WanderRadius), WanderRadius);
		return HomePosition + new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance);
	}
}
