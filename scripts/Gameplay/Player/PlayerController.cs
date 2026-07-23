using Godot;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody3D
{

	public enum CameraViewMode
	{
		ThirdPerson,
		GodView,
	}

	public sealed record ContractCompanionOffer(string Id, string NameKey, string RoleNameKey, string CombatRole, string SummaryKey, int Level, int Cost, int MaxHealth, int Attack, int Defense);
	public enum MerchantShopKind
	{
		Blacksmith,
		ItemShop,
		PetShop,
	}

	public sealed record ShopTradeEntry(
		string ItemId,
		string DisplayName,
		string Detail,
		int Price,
		int PetLevel = 0,
		int PetMaxHealth = 0,
		int PetAttack = 0,
		int PetDefense = 0)
	{
		public bool IsPetOffer => PetLevel > 0;
	}

	private readonly record struct PetShopOffer(string MonsterNameKey, int Level, int Price, int MaxHealth, int Attack, int Defense);

	private const float PlayerVisualScale = 0.88f;
	private const int NpcRecruitQuestItemCount = 3;
	private const int NpcRecruitAffinityRequirement = 80;
	private const float MercenaryBrokerInteractRange = 4.6f;
	private const float MerchantInteractRange = 4.6f;
	private const int MercenaryRefreshCost = 5000;
	private const int MercenaryOfferCount = 5;
	private const double MercenaryRefreshSeconds = 6.0 * 60.0 * 60.0;
	private const int MerchantRefreshCost = 5000;
	private const int BlacksmithStockCount = 6;
	private const int PetShopStockCount = 4;
	private const int PetReviveGoldCost = 40;
	private const float InteractionPromptRefreshSeconds = 0.12f;
	private const float WorldDropCollectRefreshSeconds = 0.10f;
	private const float FallenCompanionPickupRadius = 1.85f;
	private const string ThirdPersonCameraModeId = "third_person";
	private const string GodViewCameraModeId = "god_view";

	private static readonly PetShopOffer[] PetShopOffers =
	{
		new("name.monster.rat", 1, 120, 120, 16, 9),
		new("name.monster.bunny", 1, 130, 118, 15, 9),
		new("name.monster.fox", 1, 210, 130, 18, 10),
		new("name.monster.crab", 1, 240, 150, 15, 16),
		new("name.monster.bee", 1, 260, 124, 17, 10),
		new("name.monster.lion", 1, 420, 150, 20, 12),
	};

	[Export] public float WalkSpeed { get; set; } = 7.8f;
	[Export] public float SprintSpeed { get; set; } = 12.8f;
	[Export] public float JumpVelocity { get; set; } = 5.2f;
	[Export] public float ThirdPersonDistance { get; set; } = 6.2f;
	[Export] public float ThirdPersonCameraHeight { get; set; } = 3.35f;
	[Export] public float ThirdPersonLookHeight { get; set; } = 2.2f;
	[Export] public float GodViewDistance { get; set; } = 28.0f;
	[Export] public float GodViewCameraHeight { get; set; } = 15.5f;
	[Export] public float GodViewMinZoom { get; set; } = 7.0f;
	[Export] public float GodViewMaxZoom { get; set; } = 28.0f;
	[Export] public float GodViewWheelZoomStep { get; set; } = 1.5f;
	[Export] public float GodViewMouseZoomSensitivity { get; set; } = 0.025f;
	[Export] public float HorizontalLookSensitivity { get; set; } = 0.0026f;
	[Export] public float VerticalLookSensitivity { get; set; } = 0.0022f;
	[Export] public float CameraWorldHalfExtent { get; set; } = 68.5f;
	[Export] public float FallRespawnHeight { get; set; } = -8.0f;
	[Export] public float Acceleration { get; set; } = 18.0f;
	[Export] public float CaptureCooldown { get; set; } = 0.55f;
	[Export] public int CaptureNetCapacity { get; set; } = 6;
	[Export] public float CaptureNetRechargeSeconds { get; set; } = 5.0f;
	[Export] public float TargetInfoRange { get; set; } = 30.0f;
	[Export] public float RevivalNpcInteractRange { get; set; } = 4.2f;
	[Export] public float NpcRecruitInteractRange { get; set; } = 4.8f;
	[Export] public float MapPortalInteractRange { get; set; } = 5.2f;
	[Export] public string PlayerName { get; set; } = "player.default_name";
	[Export] public string PlayerModelPath { get; set; } = string.Empty;
	[Export] public int Level { get; set; } = 1;
	[Export] public int Experience { get; set; }
	[Export] public int MaxHealth { get; set; } = 150;
	[Export] public int CurrentHealth { get; set; } = 150;
	[Export] public int Attack { get; set; } = 16;
	[Export] public int Defense { get; set; } = 10;
	[Export] public int Gold { get; set; } = 5000;
	[Export] public float AttackRange { get; set; } = 2.2f;
	[Export] public float DetectionRadius { get; set; } = 18.0f;
	[Export] public float CritChance { get; set; } = 0.05f;
	[Export] public float AttackCooldown { get; set; } = 1.0f;
	[Export] public int ActivePartyLimit { get; set; } = 20;
	[Export] public float DamageFlashDuration { get; set; } = 0.32f;

	private readonly List<SimpleActor> _capturedCollection = new();
	private readonly List<SimpleActor> _activeParty = new();
	private SimpleActor? _mountedCompanion;
	private readonly Dictionary<string, int> _inventoryItems = new();
	private readonly Dictionary<string, int> _storageItems = new();
	private readonly HashSet<SimpleActor> _acceptedNpcQuests = new();
	private readonly HashSet<SimpleActor> _completedNpcQuests = new();
	private readonly List<ContractCompanionOffer> _contractCompanionOffers = new();
	private readonly List<string> _blacksmithStockItemIds = new();
	private readonly List<string> _petShopStockNameKeys = new();
	private readonly RandomNumberGenerator _mercenaryRng = new();
	private double _mercenaryNextRefreshUnix;
	private double _merchantNextRefreshUnix;
	private static readonly ContractCompanionOffer[] ContractCompanionOfferTemplates =
	{
		new("mercenary.offer.vanguard", "name.mercenary.vanguard", "role.tank", "Tank", "mercenary.summary.vanguard", 3, 260, 185, 18, 24),
		new("mercenary.offer.ranger", "name.mercenary.ranger", "role.ranged", "Ranged", "mercenary.summary.ranger", 4, 320, 145, 28, 15),
		new("mercenary.offer.arcane_healer", "name.mercenary.arcane_healer", "role.support", "Support", "mercenary.summary.arcane_healer", 3, 300, 132, 16, 18),
		new("mercenary.offer.duelist", "name.mercenary.duelist", "role.dps", "DPS", "mercenary.summary.duelist", 5, 420, 160, 36, 16),
		new("mercenary.offer.scout", "name.mercenary.scout", "role.gatherer", "Gatherer", "mercenary.summary.scout", 2, 180, 118, 17, 12),
	};
	private float _cameraYaw;
	private float _thirdPersonCameraYaw;
	private float _godViewCameraYaw;
	private float _cameraPitch = 0.08f;
	// Elevation angle of the top-down camera; adjustable by right-drag vertical motion.
	private const float GodViewMinPitch = 0.30f;
	private const float GodViewMaxPitch = 1.40f;
	private float _godViewPitch = 0.62f;
	private bool _isRightMouseLookActive;
	private CameraViewMode _cameraMode = CameraViewMode.GodView;
	private Vector3 _lastSafePosition = new(0.0f, 0.2f, 8.0f);
	private float _gravity;
	private float _captureCooldownRemaining;
	private float _interactionPromptRefreshRemaining;
	private float _worldDropCollectRefreshRemaining;
	private int _captureNetCharges;
	private float _captureNetRechargeRemaining;
	private Node3D _cameraPivot = null!;
	private Camera3D _camera = null!;
	private TargetInfoPanel _targetInfoPanel = null!;
	private PartyPanel _partyPanel = null!;
	private InventoryPanel _inventoryPanel = null!;
	private FormationPanel _formationPanel = null!;
	private MerchantShopPanel _merchantShopPanel = null!;
	private MercenaryShopPanel _mercenaryShopPanel = null!;
	private WarehousePanel _warehousePanel = null!;
	private MailboxPanel _mailboxPanel = null!;
	private ComposePanel _composePanel = null!;
	private Button _mailboxHudButton = null!;
	private Panel _mailboxBadge = null!;
	private SettingsPanel _settingsPanel = null!;
	private PanelContainer _pauseMenuPanel = null!;
	private MinimapPanel _minimapPanel = null!;
	private CaptureRhythmPanel _captureRhythmPanel = null!;
	private SystemLogPanel _systemLogPanel = null!;
	private PanelContainer _bossAnnouncementPanel = null!;
	private PanelContainer _bossHudPanel = null!;
	private PanelContainer _bossWorldStatusPanel = null!;
	private PanelContainer _captureAmmoPanel = null!;
	private PanelContainer _npcQuestDialog = null!;
	private PanelContainer _mapTravelDialog = null!;
	private VBoxContainer _mapTravelButtonList = null!;
	private Label _mapTravelTitleLabel = null!;
	private bool _mapTravelGuideOnly;
	private PanelContainer _wildReturnDialog = null!;
	private VBoxContainer _wildReturnButtonList = null!;
	private Label _captureAmmoCaptionLabel = null!;
	private Label _captureAmmoCountLabel = null!;
	private ProgressBar _captureAmmoRechargeBar = null!;
	private ColorRect _damageFlashOverlay = null!;
	private Label _interactionPromptLabel = null!;
	private Label _npcQuestTitleLabel = null!;
	private Label _npcQuestBodyLabel = null!;
	private Label _npcQuestRewardLabel = null!;
	private Label _bossAnnouncementTitleLabel = null!;
	private Label _bossAnnouncementBodyLabel = null!;
	private Label _bossWorldStatusTitleLabel = null!;
	private Label _bossWorldStatusEntryLabel = null!;
	private Label _bossHudNameLabel = null!;
	private Label _bossHudHealthLabel = null!;
	private ProgressBar _bossHudHealthBar = null!;
	private Button _npcQuestAcceptButton = null!;
	private Button _npcQuestDeclineButton = null!;
	private Node3D? _selectedTargetMarker;
	private MeshInstance3D? _selectedTargetOuterRing;
	private MeshInstance3D? _selectedTargetInnerRing;
	private MeshInstance3D? _selectedTargetArrow;
	private StandardMaterial3D? _selectedTargetRingMaterial;
	private StandardMaterial3D? _selectedTargetArrowMaterial;
	private SimpleActor? _selectedActor;
	private SimpleActor? _focusedTarget;
	private SimpleActor? _pendingQuestNpc;
	private bool _npcQuestDialogIsNotice;
	private Node3D? _playerExternalModel;
	private string _playerExternalAnimationState = string.Empty;
	private readonly Dictionary<string, Node3D?> _playerVisualNodeCache = new();
	private Node3D? _playerVisualRoot;
	private Label3D? _playerNameLabel;
	private float _damageFlashRemaining;
	private float _footstepEffectRemaining;
	private float _movementAnimationPhase;
	private SimpleActor? _activeBoss;
	private float _bossHudCombatVisibleRemaining;
	private Tween? _bossAnnouncementTween;
	private Tween? _bossWorldStatusTween;
	private float _bossWorldStatusRefreshRemaining;
	private bool _bossAnnouncementsEnabled = true;
	private float _bossAnnouncementOpacity = 0.90f;
	private string _bossWorldStatusSignature = string.Empty;

	public IReadOnlyList<SimpleActor> CapturedCollection => _capturedCollection;
	public IReadOnlyList<SimpleActor> ActiveParty => _activeParty;
	public int AvailableCompanionCount
	{
		get
		{
			int count = 0;
			foreach (SimpleActor actor in _capturedCollection)
			{
				if (IsInstanceValid(actor) && !actor.IsAwaitingRecovery)
				{
					count++;
				}
			}
			return count;
		}
	}
	public IReadOnlyList<ContractCompanionOffer> ContractCompanionOffers => _contractCompanionOffers;
	public int MercenaryManualRefreshCost => MercenaryRefreshCost;
	public int MerchantManualRefreshCost => MerchantRefreshCost;
	public int PetReviveCostPerCompanion => PetReviveGoldCost;
	public IReadOnlyDictionary<string, int> InventoryItems => _inventoryItems;
	public string LocalizedPlayerName => LocaleText.T(PlayerName);
	public CameraViewMode CameraMode => _cameraMode;
	public float DamageTextScale => CombatEffect.DamageTextScale;
	public float NameplateScale => SimpleActor.NameplateScale;
	public bool BossAnnouncementsEnabled => _bossAnnouncementsEnabled;
	public float BossAnnouncementOpacity => _bossAnnouncementOpacity;
	public float HealthRatio => MaxHealth <= 0 ? 0.0f : Mathf.Clamp(CurrentHealth / (float)MaxHealth, 0.0f, 1.0f);
	public int ExperienceToNextLevel => 60 + Level * 30;

	public override void _Ready()
	{
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/Camera3D");
		_lastSafePosition = GlobalPosition + Vector3.Up * 0.2f;
		_mercenaryRng.Seed = Time.GetTicksUsec() ^ (ulong)GetInstanceId();
		EnsureMercenaryOffers();
		EnsureMerchantStock();
		ConfigureThirdPersonCamera();
		ApplyNewGameCharacterChoice();
		CreatePlayerVisual();
		CreatePlayerNameplate();
		CreateTargetInfoPanel();
		CreateMinimapPanel();
		CreateCaptureRhythmPanel();
		CreatePartyPanel();
		CreateInventoryPanel();
		CreateFormationPanel();
		CreateMerchantShopPanel();
		CreateMercenaryShopPanel();
		CreateWarehousePanel();
		CreateMailboxPanel();
		CreateComposePanel();
		CreateSettingsPanel();
		CreatePauseMenuPanel();
		InitializeStarterInventory();
		if (!GameLaunchOptions.LoadSaveOnWorldReady)
		{
			CallDeferred(nameof(GrantStarterBunny));
			GrantStarterTownPortalScrolls();
		}
		InitializeCaptureNetAmmo();
		CreateCaptureAmmoHud();
		CreateMailboxHud();
		CreateDamageFlashHud();
		CreateInteractionPromptHud();
		CreateSystemLogPanel();
		CreateBossHud();
		CreateNpcQuestDialog();
		CreateMapTravelDialog();
		CreateWildReturnDialog();

		AddToGroup("player");
		CallDeferred(nameof(PrewarmTownPortalCastEffect));
		EnsureInputActions();
		Input.MouseMode = _cameraMode == CameraViewMode.GodView
			? Input.MouseModeEnum.Visible
			: Input.MouseModeEnum.Captured;
	}

	public override void _Process(double delta)
	{
		if (_mountedCompanion != null && !IsMountedCompanionValid())
		{
			if (IsInstanceValid(_mountedCompanion))
			{
				_mountedCompanion.SetMountedByPlayer(false);
			}
			_mountedCompanion = null;
			UpdateMountedVisualOffset();
		}
		else if (_mountedCompanion != null)
		{
			UpdateMountedVisualOffset();
		}
		UpdateCaptureNetRecharge((float)delta);
		UpdateCamera();
		UpdateTargetInfoPanel();
		UpdateCaptureAmmoHud();
		UpdateDamageFlash((float)delta);
		UpdateMovementAnimation((float)delta);
		UpdateFocusedTargetMarker((float)delta);
		UpdateMercenaryOfferRefresh();
		UpdateMerchantStockRefresh();
		UpdateInteractionPrompt((float)delta);
		UpdateBossHud((float)delta);
		UpdateBossWorldStatusHud((float)delta);
		UpdateTownPortalChannel((float)delta);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_captureRhythmPanel != null && _captureRhythmPanel.IsChallengeActive)
		{
			return;
		}

		if (@event.IsActionPressed("ui_cancel"))
		{
			if (_mapTravelDialog != null && _mapTravelDialog.Visible)
			{
				CloseMapTravelDialog();
			}
			else if (_npcQuestDialog.Visible)
			{
				CloseNpcQuestDialog();
			}
			else if (_settingsPanel.Visible)
			{
				SetSettingsPanelVisible(false);
			}
			else if (_pauseMenuPanel.Visible)
			{
				SetPauseMenuVisible(false);
			}
			else if (_inventoryPanel.Visible)
			{
				SetInventoryPanelVisible(false);
			}
			else if (_formationPanel.Visible)
			{
				SetFormationPanelVisible(false);
			}
			else if (_merchantShopPanel.Visible)
			{
				SetMerchantShopPanelVisible(false);
			}
			else if (_mercenaryShopPanel.Visible)
			{
				SetMercenaryShopPanelVisible(false);
			}
			else if (_warehousePanel.Visible)
			{
				SetWarehousePanelVisible(false);
			}
			else if (_composePanel.Visible)
			{
				SetComposePanelVisible(false);
			}
			else if (_mailboxPanel.Visible)
			{
				SetMailboxPanelVisible(false);
			}
			else if (_partyPanel.Visible)
			{
				SetPartyPanelVisible(false);
			}
			else
			{
				SetPauseMenuVisible(true);
			}

			return;
		}

		if (_npcQuestDialog.Visible)
		{
			return;
		}

		if (_pauseMenuPanel.Visible || _settingsPanel.Visible || _merchantShopPanel.Visible || _mercenaryShopPanel.Visible || _warehousePanel.Visible || _mailboxPanel.Visible || _composePanel.Visible)
		{
			return;
		}

		if (@event.IsActionPressed("inventory_panel"))
		{
			SetInventoryPanelVisible(!_inventoryPanel.Visible);
			return;
		}

		if (@event.IsActionPressed("formation_panel"))
		{
			SetFormationPanelVisible(!_formationPanel.Visible);
			return;
		}

		if (_inventoryPanel.Visible || _formationPanel.Visible)
		{
			return;
		}

		if (@event.IsActionPressed("party_panel"))
		{
			SetPartyPanelVisible(!_partyPanel.Visible);
			return;
		}

		if (_partyPanel.Visible)
		{
			return;
		}

		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right } rightMouseButton)
		{
			_isRightMouseLookActive = rightMouseButton.Pressed;
			Input.MouseMode = rightMouseButton.Pressed
				? Input.MouseModeEnum.Captured
				: _cameraMode == CameraViewMode.GodView ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
			return;
		}

		if (@event is InputEventMouseMotion godViewMouseMotion
			&& _cameraMode == CameraViewMode.GodView
			&& (godViewMouseMotion.ButtonMask & MouseButtonMask.Middle) != 0)
		{
			AdjustGodViewZoom(godViewMouseMotion.Relative.Y * GodViewMouseZoomSensitivity);
			return;
		}

		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured && (_cameraMode == CameraViewMode.ThirdPerson || _isRightMouseLookActive))
		{
			_cameraYaw = Mathf.Wrap(
				_cameraYaw - mouseMotion.Relative.X * HorizontalLookSensitivity,
				-Mathf.Pi,
				Mathf.Pi
			);
			if (_cameraMode == CameraViewMode.ThirdPerson)
			{
				Rotation = new Vector3(Rotation.X, _cameraYaw, Rotation.Z);
				_cameraPitch = Mathf.Clamp(
					_cameraPitch + mouseMotion.Relative.Y * VerticalLookSensitivity,
					-0.42f,
					0.76f
				);
			}
			else
			{
				// Top-down: vertical drag tilts the camera's elevation angle. Dragging
				// down looks more overhead, dragging up looks toward the horizon.
				_godViewPitch = Mathf.Clamp(
					_godViewPitch + mouseMotion.Relative.Y * VerticalLookSensitivity,
					GodViewMinPitch,
					GodViewMaxPitch
				);
			}
			return;
		}

		if (@event is InputEventMouseButton { Pressed: true })
		{
			if (_cameraMode == CameraViewMode.GodView && @event is InputEventMouseButton zoomButton)
			{
				if (zoomButton.ButtonIndex == MouseButton.WheelUp)
				{
					AdjustGodViewZoom(-GodViewWheelZoomStep);
					return;
				}

				if (zoomButton.ButtonIndex == MouseButton.WheelDown)
				{
					AdjustGodViewZoom(GodViewWheelZoomStep);
					return;
				}
			}

			if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left })
			{
				if (_cameraMode == CameraViewMode.GodView && @event is InputEventMouseButton selectButton)
				{
					TrySelectActorTarget(selectButton.Position);
					ShowGodViewClickIndicator(selectButton.Position);
				}
				else
				{
					Input.MouseMode = Input.MouseModeEnum.Captured;
					TrySelectActorTarget();
				}
				return;
			}
		}

		if (@event.IsActionPressed("capture_net"))
		{
			ThrowCaptureNet();
		}

		if (@event.IsActionPressed("save_game"))
		{
			SaveCurrentGame();
			return;
		}

		if (@event.IsActionPressed("interact"))
		{
			TryInteract();
		}

		if (@event.IsActionPressed("town_portal"))
		{
			TryUseTownPortalScroll();
		}

		// M opens the world map guide (same tiers/locks/boss view as the portal),
		// usable from anywhere as a quick-travel + overview screen.
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.M })
		{
			ToggleWorldMapGuide();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_settingsPanel.Visible || _partyPanel.Visible || _inventoryPanel.Visible || _formationPanel.Visible || _npcQuestDialog.Visible)
		{
			Velocity = SlowPlayerToStop(Velocity, (float)delta);
			MoveAndSlide();
			UpdateSafeGroundPosition();
			RecoverIfOutOfWorld();
			return;
		}

		float step = (float)delta;
		if (UpdateSpiderWebSuspension(step))
		{
			UpdateSafeGroundPosition();
			return;
		}
		_captureCooldownRemaining = Mathf.Max(_captureCooldownRemaining - step, 0.0f);
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * step;
		}

		if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		Vector2 inputDirection = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 forward = GetCameraPlanarForward();
		Vector3 right = new(-forward.Z, 0.0f, forward.X);
		Vector3 direction = (right * inputDirection.X + forward * -inputDirection.Y).Normalized();
		float targetSpeed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
		if (MountedCompanion is SimpleActor mount)
		{
			targetSpeed = mount.EffectiveMoveSpeed;
			if (Input.IsActionPressed("sprint"))
			{
				targetSpeed *= SprintSpeed / Mathf.Max(WalkSpeed, 0.01f);
			}
		}

		velocity.X = Mathf.MoveToward(velocity.X, direction.X * targetSpeed, Acceleration * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * targetSpeed, Acceleration * step);
		if (direction.LengthSquared() > 0.01f)
		{
			FaceMovementDirection(direction, step);
		}

		Velocity = velocity;
		MoveAndSlide();
		UpdateNearbyWorldDropCollection(step);
		UpdateMovementEffects(step, targetSpeed);
		UpdateSafeGroundPosition();
		RecoverIfOutOfWorld();
	}

	private static void EnsureInputActions()
	{
		AddKeyAction("move_forward", Key.W);
		AddKeyAction("move_back", Key.S);
		AddKeyAction("move_left", Key.A);
		AddKeyAction("move_right", Key.D);
		AddKeyAction("jump", Key.Space);
		AddKeyAction("sprint", Key.Shift);
		AddKeyAction("capture_net", Key.R);
		AddKeyAction("interact", Key.E);
		AddKeyAction("save_game", Key.F5);
		AddKeyAction("party_panel", Key.P);
		AddKeyAction("inventory_panel", Key.I);
		AddKeyAction("formation_panel", Key.F);
		AddKeyAction("town_portal", Key.T);
		AddKeyAction("ui_cancel", Key.Escape);
	}

	private bool IsInCityMap()
	{
		return GetParent() is World world && world.ActiveMapId == "city";
	}

	private static void AddKeyAction(StringName actionName, Key physicalKeycode)
	{
		if (!InputMap.HasAction(actionName))
		{
			InputMap.AddAction(actionName);
		}

		foreach (InputEvent inputEvent in InputMap.ActionGetEvents(actionName))
		{
			if (inputEvent is InputEventKey keyEvent && keyEvent.PhysicalKeycode == physicalKeycode)
			{
				return;
			}
		}

		var keyEventToAdd = new InputEventKey { PhysicalKeycode = physicalKeycode };
		InputMap.ActionAddEvent(actionName, keyEventToAdd);
	}
}
