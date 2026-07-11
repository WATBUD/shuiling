using Godot;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody3D
{
	public sealed record ContractCompanionOffer(string Id, string NameKey, string RoleNameKey, string CombatRole, string SummaryKey, int Level, int Cost, int MaxHealth, int Attack, int Defense);
	public enum MerchantShopKind
	{
		Blacksmith,
		ItemShop,
	}

	public sealed record ShopTradeEntry(string ItemId, string DisplayName, string Detail, int Price);

	private const int FormationGridSideLength = 5;
	private const int FormationCenterSlotIndex = 12;
	private const float FormationSlotSpacing = 2.35f;
	private const float PlayerVisualScale = 0.88f;
	private const int NpcRecruitQuestItemCount = 3;
	private const int NpcQuestAffinityReward = 25;
	private const int NpcRecruitAffinityRequirement = 80;
	private const float MercenaryBrokerInteractRange = 4.6f;
	private const float MerchantInteractRange = 4.6f;

	private static readonly int[] FormationFillOrder =
	{
		7, 11, 13, 17,
		6, 8, 16, 18,
		2, 10, 14, 22,
		1, 3, 5, 9, 15, 19, 21, 23,
		0, 4, 20, 24,
	};

	[Export] public float WalkSpeed { get; set; } = 7.8f;
	[Export] public float SprintSpeed { get; set; } = 12.8f;
	[Export] public float JumpVelocity { get; set; } = 5.2f;
	[Export] public float ThirdPersonDistance { get; set; } = 6.2f;
	[Export] public float ThirdPersonCameraHeight { get; set; } = 3.35f;
	[Export] public float ThirdPersonLookHeight { get; set; } = 2.2f;
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
	[Export] public int Level { get; set; } = 1;
	[Export] public int MaxHealth { get; set; } = 150;
	[Export] public int CurrentHealth { get; set; } = 150;
	[Export] public int Attack { get; set; } = 16;
	[Export] public int Defense { get; set; } = 10;
	[Export] public int Gold { get; set; }
	[Export] public int ActivePartyLimit { get; set; } = 20;
	[Export] public float DamageFlashDuration { get; set; } = 0.32f;

	private readonly List<SimpleActor> _capturedCollection = new();
	private readonly List<SimpleActor> _activeParty = new();
	private readonly Dictionary<string, int> _inventoryItems = new();
	private readonly Dictionary<int, SimpleActor> _formationActorsBySlot = new();
	private readonly Dictionary<SimpleActor, int> _formationSlotsByActor = new();
	private readonly HashSet<SimpleActor> _acceptedNpcQuests = new();
	private readonly HashSet<SimpleActor> _completedNpcQuests = new();
	private static readonly ContractCompanionOffer[] ContractCompanionOfferCatalog =
	{
		new("mercenary.offer.vanguard", "name.mercenary.vanguard", "role.tank", "Tank", "mercenary.summary.vanguard", 3, 260, 185, 18, 24),
		new("mercenary.offer.ranger", "name.mercenary.ranger", "role.ranged", "Ranged", "mercenary.summary.ranger", 4, 320, 145, 28, 15),
		new("mercenary.offer.mender", "name.mercenary.mender", "role.support", "Support", "mercenary.summary.mender", 3, 300, 132, 16, 18),
		new("mercenary.offer.duelist", "name.mercenary.duelist", "role.dps", "DPS", "mercenary.summary.duelist", 5, 420, 160, 36, 16),
		new("mercenary.offer.scout", "name.mercenary.scout", "role.gatherer", "Gatherer", "mercenary.summary.scout", 2, 180, 118, 17, 12),
	};
	private float _cameraYaw;
	private float _cameraPitch = 0.08f;
	private Vector3 _lastSafePosition = new(0.0f, 0.2f, 8.0f);
	private float _gravity;
	private float _captureCooldownRemaining;
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
	private SettingsPanel _settingsPanel = null!;
	private MinimapPanel _minimapPanel = null!;
	private SystemLogPanel _systemLogPanel = null!;
	private PanelContainer _captureAmmoPanel = null!;
	private PanelContainer _npcQuestDialog = null!;
	private PanelContainer _mapTravelDialog = null!;
	private VBoxContainer _mapTravelButtonList = null!;
	private Label _captureAmmoCaptionLabel = null!;
	private Label _captureAmmoCountLabel = null!;
	private ProgressBar _captureAmmoRechargeBar = null!;
	private ColorRect _damageFlashOverlay = null!;
	private Label _interactionPromptLabel = null!;
	private Label _npcQuestTitleLabel = null!;
	private Label _npcQuestBodyLabel = null!;
	private Label _npcQuestRewardLabel = null!;
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
	private float _damageFlashRemaining;
	private float _footstepEffectRemaining;
	private float _movementAnimationPhase;

	public IReadOnlyList<SimpleActor> CapturedCollection => _capturedCollection;
	public IReadOnlyList<SimpleActor> ActiveParty => _activeParty;
	public IReadOnlyList<ContractCompanionOffer> ContractCompanionOffers => ContractCompanionOfferCatalog;
	public SimpleActor? FocusedTarget => IsValidFocusedTarget(_focusedTarget) ? _focusedTarget : null;
	public IReadOnlyDictionary<string, int> InventoryItems => _inventoryItems;
	public int FormationGridSide => FormationGridSideLength;
	public int FormationPlayerSlotIndex => FormationCenterSlotIndex;
	public int FormationAssignedCount => _formationSlotsByActor.Count;
	public string LocalizedPlayerName => LocaleText.T(PlayerName);
	public Vector3 MinimapForward => GetCameraPlanarForward();
	public float HealthRatio => MaxHealth <= 0 ? 0.0f : Mathf.Clamp(CurrentHealth / (float)MaxHealth, 0.0f, 1.0f);

	public override void _Ready()
	{
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/Camera3D");
		_lastSafePosition = GlobalPosition + Vector3.Up * 0.2f;
		ConfigureThirdPersonCamera();
		CreatePlayerVisual();
		CreateTargetInfoPanel();
		CreateMinimapPanel();
		CreatePartyPanel();
		CreateInventoryPanel();
		CreateFormationPanel();
		CreateMerchantShopPanel();
		CreateMercenaryShopPanel();
		CreateSettingsPanel();
		InitializeStarterInventory();
		InitializeCaptureNetAmmo();
		CreateCaptureAmmoHud();
		CreateDamageFlashHud();
		CreateInteractionPromptHud();
		CreateSystemLogPanel();
		CreateNpcQuestDialog();
		CreateMapTravelDialog();

		AddToGroup("player");
		EnsureInputActions();
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Process(double delta)
	{
		UpdateCaptureNetRecharge((float)delta);
		UpdateThirdPersonCamera();
		UpdateTargetInfoPanel();
		UpdateCaptureAmmoHud();
		UpdateDamageFlash((float)delta);
		UpdateMovementAnimation((float)delta);
		UpdateFocusedTargetMarker((float)delta);
		UpdateInteractionPrompt();
		StabilizePlayerExternalModel();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
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
			else if (_partyPanel.Visible)
			{
				SetPartyPanelVisible(false);
			}
			else
			{
				SetSettingsPanelVisible(true);
			}

			return;
		}

		if (_npcQuestDialog.Visible)
		{
			return;
		}

		if (_settingsPanel.Visible || _merchantShopPanel.Visible || _mercenaryShopPanel.Visible)
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

		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_cameraYaw = Mathf.Wrap(
				_cameraYaw - mouseMotion.Relative.X * HorizontalLookSensitivity,
				-Mathf.Pi,
				Mathf.Pi
			);
			_cameraPitch = Mathf.Clamp(
				_cameraPitch + mouseMotion.Relative.Y * VerticalLookSensitivity,
				-0.42f,
				0.76f
			);
			return;
		}

		if (@event is InputEventMouseButton { Pressed: true })
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
			if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left })
			{
				TrySelectActorTarget();
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

		velocity.X = Mathf.MoveToward(velocity.X, direction.X * targetSpeed, Acceleration * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * targetSpeed, Acceleration * step);
		if (direction.LengthSquared() > 0.01f)
		{
			FaceMovementDirection(direction, step);
		}

		Velocity = velocity;
		MoveAndSlide();
		CollectNearbyWorldDrops();
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
		AddKeyAction("ui_cancel", Key.Escape);
	}

	private void ThrowCaptureNet()
	{
		if (_captureCooldownRemaining > 0.0f || _captureNetCharges <= 0)
		{
			return;
		}

		_captureCooldownRemaining = CaptureCooldown;
		_captureNetCharges = Mathf.Max(_captureNetCharges - 1, 0);
		Vector3 direction = GetCaptureThrowDirection();
		Vector3 spawnPosition = GlobalPosition + new Vector3(0.0f, 1.18f, 0.0f) + GetCameraPlanarForward() * 1.05f;
		var net = new CaptureNet
		{
			OwnerPlayer = this,
			Direction = direction,
		};

		Node projectileParent = GetTree().CurrentScene ?? GetParent();
		projectileParent.AddChild(net);
		net.GlobalPosition = spawnPosition;
		net.AlignToDirection();
		UpdateCaptureAmmoHud();
	}

	public bool CaptureActor(SimpleActor actor)
	{
		if (actor.ActorKind == "npc")
		{
			PostSystemMessage(LocaleText.F("system.npc.requires_task", actor.LocalizedDisplayName), new Color(0.82f, 0.88f, 1.0f));
			return false;
		}

		if (!actor.CanBeCaptured || _capturedCollection.Contains(actor))
		{
			return false;
		}

		_capturedCollection.Add(actor);
		actor.Capture(this);
		PostSystemMessage(LocaleText.F("system.capture.success", actor.LocalizedDisplayName), new Color(0.62f, 0.90f, 1.0f));

		if (_activeParty.Count < ActivePartyLimit)
		{
			DeployCompanion(actor, false);
		}
		else
		{
			actor.StoreInCollection();
		}

		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		return true;
	}

	private bool RecruitNpc(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !actor.IsNpcRecruitCandidate || _capturedCollection.Contains(actor))
		{
			return false;
		}

		_capturedCollection.Add(actor);
		actor.Recruit(this);
		PostSystemMessage(LocaleText.F("system.npc.joined", actor.LocalizedDisplayName), new Color(0.62f, 1.0f, 0.78f));

		if (_activeParty.Count < ActivePartyLimit)
		{
			DeployCompanion(actor, false);
		}
		else
		{
			actor.StoreInCollection();
		}

		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		return true;
	}

	public bool IsInActiveParty(SimpleActor actor)
	{
		return _activeParty.Contains(actor);
	}

	public bool DeployCompanion(SimpleActor actor, bool replaceLastIfFull)
	{
		if (!_capturedCollection.Contains(actor))
		{
			return false;
		}

		if (_activeParty.Contains(actor))
		{
			return true;
		}

		if (_activeParty.Count >= ActivePartyLimit)
		{
			if (!replaceLastIfFull || _activeParty.Count == 0)
			{
				return false;
			}

			StoreCompanion(_activeParty[_activeParty.Count - 1]);
		}

		_activeParty.Add(actor);
		actor.DeployToParty(this, _activeParty.Count - 1);
		EnsureFormationSlotForActor(actor);
		actor.OnFormationLayoutChanged();
		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		return true;
	}

	public bool TryHireContractCompanion(ContractCompanionOffer offer)
	{
		if (Gold < offer.Cost)
		{
			PostSystemMessage(LocaleText.F("system.mercenary.not_enough_gold", offer.Cost, Gold), new Color(1.0f, 0.62f, 0.48f));
			return false;
		}

		if (GetParent() is not World world)
		{
			return false;
		}

		SimpleActor actor = world.SpawnContractCompanion(offer);
		Gold = Mathf.Max(Gold - offer.Cost, 0);
		PostSystemMessage(LocaleText.F("system.mercenary.hired", LocaleText.T(offer.NameKey), offer.Cost, Gold), new Color(1.0f, 0.86f, 0.46f));
		RecruitNpc(actor);
		_inventoryPanel.RefreshAll();
		_mercenaryShopPanel.RefreshAll();
		return true;
	}

	public List<ShopTradeEntry> GetShopBuyEntries(MerchantShopKind shopKind)
	{
		var entries = new List<ShopTradeEntry>();
		if (shopKind == MerchantShopKind.Blacksmith)
		{
			foreach (EquipmentSlot slot in new[] { EquipmentSlot.Helmet, EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Accessory })
			{
				foreach (EquipmentDefinition equipment in BuildCatalog.GetEquipmentDefinitions(slot))
				{
					entries.Add(new ShopTradeEntry(equipment.Id, LocaleText.T(equipment.NameKey), LocaleText.T(equipment.SummaryKey), GetShopBuyPrice(equipment.Id)));
				}
			}
		}
		else
		{
			foreach (AttributeGemDefinition gem in BuildCatalog.GetAttributeGemDefinitions())
			{
				if (!BuildCatalog.IsFreeItem(gem.Id))
				{
					entries.Add(new ShopTradeEntry(gem.Id, LocaleText.T(gem.NameKey), LocaleText.T(gem.SummaryKey), GetShopBuyPrice(gem.Id)));
				}
			}

			foreach (SkillGemDefinition gem in BuildCatalog.GetSkillGemDefinitions())
			{
				if (!BuildCatalog.IsFreeItem(gem.Id))
				{
					entries.Add(new ShopTradeEntry(gem.Id, LocaleText.T(gem.NameKey), LocaleText.T(gem.SummaryKey), GetShopBuyPrice(gem.Id)));
				}
			}

			foreach (string materialId in GetShopMaterialIds())
			{
				entries.Add(new ShopTradeEntry(materialId, GetInventoryItemDisplayName(materialId), LocaleText.T("shop.detail.material"), GetShopBuyPrice(materialId)));
			}
		}

		return entries;
	}

	public List<ShopTradeEntry> GetShopSellEntries(MerchantShopKind shopKind)
	{
		var entries = new List<ShopTradeEntry>();
		foreach (KeyValuePair<string, int> item in _inventoryItems)
		{
			if (item.Value <= 0 || !CanTradeInShop(shopKind, item.Key))
			{
				continue;
			}

			string detail = LocaleText.F("shop.sell.count", item.Value);
			entries.Add(new ShopTradeEntry(item.Key, GetInventoryItemDisplayName(item.Key), detail, GetShopSellPrice(item.Key)));
		}

		entries.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal));
		return entries;
	}

	public bool TryBuyShopItem(MerchantShopKind shopKind, string itemId, int price)
	{
		int safePrice = Mathf.Max(price, 1);
		if (!CanTradeInShop(shopKind, itemId))
		{
			return false;
		}

		if (Gold < safePrice)
		{
			PostSystemMessage(LocaleText.F("system.shop.not_enough_gold", safePrice, Gold), new Color(1.0f, 0.62f, 0.48f));
			return false;
		}

		Gold -= safePrice;
		AddInventoryItem(itemId);
		PostSystemMessage(LocaleText.F("system.shop.bought", GetInventoryItemDisplayName(itemId), safePrice, Gold), new Color(1.0f, 0.86f, 0.46f));
		_merchantShopPanel.RefreshAll();
		return true;
	}

	public bool TrySellShopItem(MerchantShopKind shopKind, string itemId, int price)
	{
		int safePrice = Mathf.Max(price, 1);
		if (!CanTradeInShop(shopKind, itemId) || GetInventoryCount(itemId) <= 0)
		{
			return false;
		}

		RemoveInventoryItemSilently(itemId, 1);
		Gold += safePrice;
		PostSystemMessage(LocaleText.F("system.shop.sold", GetInventoryItemDisplayName(itemId), safePrice, Gold), new Color(0.86f, 1.0f, 0.62f));
		_inventoryPanel.RefreshAll();
		_merchantShopPanel.RefreshAll();
		return true;
	}

	public bool StoreCompanion(SimpleActor actor)
	{
		if (!_capturedCollection.Contains(actor))
		{
			return false;
		}

		bool removed = _activeParty.Remove(actor);
		ClearFormationAssignment(actor);
		actor.StoreInCollection();
		if (removed)
		{
			ReassignFollowSlots();
		}

		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		return true;
	}

	public PlayerSaveData ExportSaveData()
	{
		var data = new PlayerSaveData
		{
			Level = Level,
			MaxHealth = MaxHealth,
			CurrentHealth = CurrentHealth,
			Attack = Attack,
			Defense = Defense,
			Gold = Gold,
			InventoryItems = new Dictionary<string, int>(_inventoryItems),
		};

		foreach (SimpleActor actor in _acceptedNpcQuests)
		{
			if (IsInstanceValid(actor))
			{
				data.AcceptedNpcQuestNames.Add(actor.DisplayName);
			}
		}

		foreach (SimpleActor actor in _completedNpcQuests)
		{
			if (IsInstanceValid(actor))
			{
				data.CompletedNpcQuestNames.Add(actor.DisplayName);
			}
		}

		for (int index = 0; index < _capturedCollection.Count; index++)
		{
			SimpleActor actor = _capturedCollection[index];
			if (!IsInstanceValid(actor))
			{
				continue;
			}

			data.Companions.Add(actor.ExportSaveData());
			if (_activeParty.Contains(actor))
			{
				data.ActivePartyIndexes.Add(index);
			}
		}

		return data;
	}

	public void ApplySaveData(PlayerSaveData data, IReadOnlyList<SimpleActor> loadedCompanions)
	{
		Level = Mathf.Max(data.Level, 1);
		MaxHealth = Mathf.Max(data.MaxHealth, 1);
		CurrentHealth = Mathf.Clamp(data.CurrentHealth, 1, MaxHealth);
		Attack = Mathf.Max(data.Attack, 0);
		Defense = Mathf.Max(data.Defense, 0);
		Gold = Mathf.Max(data.Gold, 0);

		_inventoryItems.Clear();
		foreach (KeyValuePair<string, int> item in data.InventoryItems)
		{
			if (!BuildCatalog.IsFreeItem(item.Key) && item.Value > 0)
			{
				_inventoryItems[item.Key] = item.Value;
			}
		}

		_capturedCollection.Clear();
		_activeParty.Clear();
		_formationActorsBySlot.Clear();
		_formationSlotsByActor.Clear();
		foreach (SimpleActor actor in loadedCompanions)
		{
			if (!IsInstanceValid(actor))
			{
				continue;
			}

			_capturedCollection.Add(actor);
			actor.Capture(this);
			actor.StoreInCollection();
		}

		foreach (int companionIndex in data.ActivePartyIndexes)
		{
			if (companionIndex >= 0 && companionIndex < _capturedCollection.Count)
			{
				DeployCompanion(_capturedCollection[companionIndex], false);
			}
		}

		RestoreNpcQuestSets(data);
		_partyPanel.RefreshParty();
		_inventoryPanel.RefreshAll();
		_formationPanel.RefreshAll();
	}

	private void RestoreNpcQuestSets(PlayerSaveData data)
	{
		_acceptedNpcQuests.Clear();
		_completedNpcQuests.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !IsInstanceValid(actor))
			{
				continue;
			}

			if (data.AcceptedNpcQuestNames.Contains(actor.DisplayName))
			{
				_acceptedNpcQuests.Add(actor);
			}

			if (data.CompletedNpcQuestNames.Contains(actor.DisplayName))
			{
				_completedNpcQuests.Add(actor);
			}
		}
	}

	private void SaveCurrentGame()
	{
		if (GetParent() is not World world)
		{
			return;
		}

		if (SaveGameManager.TrySave(world.ExportSaveData(), out string error))
		{
			PostSystemMessage(LocaleText.T("system.save.success"), new Color(0.72f, 1.0f, 0.78f));
		}
		else
		{
			PostSystemMessage(LocaleText.F("system.save.failed", error), new Color(1.0f, 0.42f, 0.34f));
		}
	}

	public int ReviveDefeatedCompanions()
	{
		int revivedCount = 0;
		foreach (SimpleActor actor in _capturedCollection)
		{
			if (!IsInstanceValid(actor) || !actor.IsDefeated)
			{
				continue;
			}

			if (actor.ReviveFromCaretaker(this))
			{
				revivedCount++;
			}
		}

		if (revivedCount > 0)
		{
			ReassignFollowSlots();
			_partyPanel.RefreshParty();
			_formationPanel.RefreshAll();
			PostSystemMessage(LocaleText.F("system.revive.count", revivedCount), new Color(0.54f, 1.0f, 0.70f));
		}
		else
		{
			PostSystemMessage(LocaleText.T("system.revive.no_fallen"), new Color(0.78f, 0.88f, 1.0f));
		}

		return revivedCount;
	}

	public SimpleActor? GetFormationActor(int slotIndex)
	{
		if (!IsValidFormationSlot(slotIndex) || !_formationActorsBySlot.TryGetValue(slotIndex, out SimpleActor? actor))
		{
			return null;
		}

		if (!IsInstanceValid(actor) || !actor.IsCaptured || !actor.IsInActiveParty)
		{
			_formationActorsBySlot.Remove(slotIndex);
			_formationSlotsByActor.Remove(actor);
			return null;
		}

		return actor;
	}

	public int GetFormationSlot(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !_formationSlotsByActor.TryGetValue(actor, out int slotIndex))
		{
			return -1;
		}

		return GetFormationActor(slotIndex) == actor ? slotIndex : -1;
	}

	public bool CanAssignCompanionToFormation(SimpleActor actor, int slotIndex)
	{
		if (!IsInstanceValid(actor) || !actor.IsCaptured || !IsValidCompanionFormationSlot(slotIndex))
		{
			return false;
		}

		if (_activeParty.Contains(actor))
		{
			return true;
		}

		if (_activeParty.Count < ActivePartyLimit)
		{
			return true;
		}

		SimpleActor? target = GetFormationActor(slotIndex);
		return target != null && target != actor;
	}

	public bool AssignCompanionToFormation(SimpleActor actor, int slotIndex)
	{
		if (!CanAssignCompanionToFormation(actor, slotIndex))
		{
			return false;
		}

		SimpleActor? targetBeforeDeploy = GetFormationActor(slotIndex);
		if (!_activeParty.Contains(actor) && _activeParty.Count >= ActivePartyLimit && targetBeforeDeploy != null && targetBeforeDeploy != actor)
		{
			StoreCompanion(targetBeforeDeploy);
		}

		if (!_activeParty.Contains(actor) && !DeployCompanion(actor, false))
		{
			return false;
		}

		int previousSlot = GetFormationSlot(actor);
		SimpleActor? target = GetFormationActor(slotIndex);
		if (target == actor)
		{
			RefreshFormationViews();
			return true;
		}

		if (previousSlot >= 0)
		{
			_formationActorsBySlot.Remove(previousSlot);
		}

		if (target != null)
		{
			_formationSlotsByActor.Remove(target);
			if (previousSlot >= 0)
			{
				SetFormationAssignment(target, previousSlot);
			}

			target.OnFormationLayoutChanged();
		}

		SetFormationAssignment(actor, slotIndex);
		actor.OnFormationLayoutChanged();
		RefreshFormationViews();
		return true;
	}

	public bool ClearFormationSlot(int slotIndex)
	{
		if (!IsValidCompanionFormationSlot(slotIndex))
		{
			return false;
		}

		SimpleActor? actor = GetFormationActor(slotIndex);
		return actor != null && StoreCompanion(actor);
	}

	public Vector3 GetFormationLocalOffset(SimpleActor actor)
	{
		int slotIndex = GetFormationSlot(actor);
		if (slotIndex >= 0)
		{
			return GetFormationSlotLocalOffset(slotIndex);
		}

		return GetFallbackFormationOffset(actor);
	}

	public void TeleportPartyTo(Vector3 position)
	{
		GlobalPosition = position;
		Velocity = Vector3.Zero;
		_lastSafePosition = position + Vector3.Up * 0.18f;
		for (int index = 0; index < _activeParty.Count; index++)
		{
			SimpleActor actor = _activeParty[index];
			if (!IsInstanceValid(actor) || actor.IsDefeated)
			{
				continue;
			}

			Vector3 offset = GetFormationLocalOffset(actor);
			actor.GlobalPosition = position + new Vector3(offset.X, 0.0f, offset.Z);
			actor.Velocity = Vector3.Zero;
		}
	}

	public int GetInventoryCount(string itemId)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return 1;
		}

		return _inventoryItems.TryGetValue(itemId, out int count) ? count : 0;
	}

	public bool HasInventoryItem(string itemId)
	{
		return BuildCatalog.IsFreeItem(itemId) || GetInventoryCount(itemId) > 0;
	}

	public void AddGold(int amount)
	{
		int gainedGold = Mathf.Max(amount, 0);
		if (gainedGold <= 0)
		{
			return;
		}

		Gold += gainedGold;
		PostSystemMessage(LocaleText.F("system.pickup.gold", gainedGold, Gold), new Color(1.0f, 0.82f, 0.26f));
		_mercenaryShopPanel?.RefreshAll();
	}

	public void AddInventoryItem(string itemId, int amount = 1)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return;
		}

		_inventoryItems.TryGetValue(itemId, out int currentCount);
		_inventoryItems[itemId] = Mathf.Max(currentCount + amount, 0);
		PostSystemMessage(LocaleText.F("system.pickup.item", GetInventoryItemDisplayName(itemId), Mathf.Max(amount, 0)), new Color(1.0f, 0.88f, 0.48f));
		if (_inventoryPanel != null)
		{
			_inventoryPanel.RefreshAll();
		}
	}

	public bool TryConsumeInventoryItem(string itemId, int amount = 1)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return true;
		}

		int requestedAmount = Mathf.Max(amount, 1);
		int currentCount = GetInventoryCount(itemId);
		if (currentCount < requestedAmount)
		{
			return false;
		}

		int nextCount = currentCount - requestedAmount;
		if (nextCount <= 0)
		{
			_inventoryItems.Remove(itemId);
		}
		else
		{
			_inventoryItems[itemId] = nextCount;
		}

		PostSystemMessage(LocaleText.F("system.item.used", GetInventoryItemDisplayName(itemId), requestedAmount), new Color(0.72f, 0.88f, 1.0f));
		if (_inventoryPanel != null)
		{
			_inventoryPanel.RefreshAll();
		}

		return true;
	}

	private void RemoveInventoryItemSilently(string itemId, int amount)
	{
		int requestedAmount = Mathf.Max(amount, 1);
		int currentCount = GetInventoryCount(itemId);
		int nextCount = currentCount - requestedAmount;
		if (nextCount <= 0)
		{
			_inventoryItems.Remove(itemId);
		}
		else
		{
			_inventoryItems[itemId] = nextCount;
		}
	}

	private void CollectNearbyWorldDrops()
	{
		foreach (Node node in GetTree().GetNodesInGroup("world_drops"))
		{
			if (node is WorldDrop drop && IsInstanceValid(drop))
			{
				drop.TryCollect(this);
			}
		}
	}

	private static string GetInventoryItemDisplayName(string itemId)
	{
		return MonsterLootCatalog.IsMonsterLoot(itemId)
			? LocaleText.T(MonsterLootCatalog.GetNameKey(itemId))
			: LocaleText.T(BuildCatalog.GetItemNameKey(itemId));
	}

	private static bool CanTradeInShop(MerchantShopKind shopKind, string itemId)
	{
		if (shopKind == MerchantShopKind.Blacksmith)
		{
			return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment;
		}

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return true;
		}

		InventoryItemKind kind = BuildCatalog.GetItemKind(itemId);
		return kind is InventoryItemKind.AttributeGem or InventoryItemKind.SkillGem;
	}

	private static int GetShopBuyPrice(string itemId)
	{
		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return itemId switch
			{
				"loot.dragon_scale" => 95,
				"loot.water_core" => 70,
				"loot.red_horn" => 62,
				"loot.venom_sac" => 55,
				"loot.sharp_claw" => 42,
				"loot.beast_hide" => 34,
				"loot.slime_mucus" => 24,
				_ => 30,
			};
		}

		return BuildCatalog.GetItemKind(itemId) switch
		{
			InventoryItemKind.Equipment => 120 + GetEquipmentPriceBonus(itemId),
			InventoryItemKind.AttributeGem => 90,
			InventoryItemKind.SkillGem => 115,
			_ => 50,
		};
	}

	private static int GetShopSellPrice(string itemId)
	{
		return Mathf.Max(Mathf.RoundToInt(GetShopBuyPrice(itemId) * 0.45f), 1);
	}

	private static int GetEquipmentPriceBonus(string itemId)
	{
		EquipmentDefinition equipment = BuildCatalog.GetEquipment(itemId);
		return equipment.MaxHealthBonus * 2
			+ equipment.AttackBonus * 8
			+ equipment.DefenseBonus * 7
			+ Mathf.RoundToInt(equipment.AttackRangeBonus * 18.0f)
			+ Mathf.RoundToInt(equipment.MoveSpeedBonus * 180.0f)
			+ Mathf.RoundToInt(equipment.CritChanceBonus * 300.0f)
			+ equipment.SocketCount * 45;
	}

	private static string[] GetShopMaterialIds()
	{
		return new[]
		{
			"loot.slime_mucus",
			"loot.beast_hide",
			"loot.sharp_claw",
			"loot.red_horn",
			"loot.venom_sac",
			"loot.water_core",
			"loot.dragon_scale",
			"loot.cracked_core",
		};
	}

	private static string GetNpcQuestItemId(SimpleActor actor)
	{
		return MonsterLootCatalog.GetQuestItemIdForNpc(actor.DisplayName);
	}

	public void OpenInventoryForActor(SimpleActor actor)
	{
		SetInventoryPanelVisible(true);
		_inventoryPanel.SelectActor(actor);
	}

	public int ReceiveDamage(int rawDamage, SimpleActor? attacker = null)
	{
		int mitigatedDamage = Mathf.Max(rawDamage - Mathf.RoundToInt(Defense * 0.35f), 1);
		CurrentHealth = Mathf.Max(CurrentHealth - mitigatedDamage, 0);
		Color hitColor = attacker?.AttackFxColor ?? new Color(1.0f, 0.18f, 0.14f, 0.92f);
		SpawnWorldCombatEffect($"-{mitigatedDamage}", hitColor, GlobalPosition + new Vector3(0.0f, 1.45f, 0.0f), 0.78f, 0.88f);
		SpawnIncomingAttackCue(attacker, hitColor);
		TriggerDamageFlash();

		if (CurrentHealth <= 0)
		{
			RecoverFromKnockdown();
		}

		return mitigatedDamage;
	}

	public int ReceiveHealing(int rawHealing)
	{
		int missingHealth = Mathf.Max(MaxHealth - CurrentHealth, 0);
		int healing = Mathf.Min(Mathf.Max(rawHealing, 0), missingHealth);
		if (healing <= 0)
		{
			return 0;
		}

		CurrentHealth += healing;
		SpawnFloatingEffect($"+{healing}", new Color(0.36f, 1.0f, 0.54f, 0.92f), 0.55f, 0.48f);
		return healing;
	}

	public void GrantCombatExperience(int amount)
	{
		int experience = Mathf.Max(amount, 0);
		if (experience <= 0)
		{
			return;
		}

		foreach (SimpleActor actor in _activeParty)
		{
			if (IsInstanceValid(actor) && actor.IsInActiveParty)
			{
				actor.GrantTraining(experience);
			}
		}

		PostSystemMessage(LocaleText.F("system.exp.party_gain", experience), new Color(0.86f, 0.78f, 1.0f));
		_partyPanel.RefreshParty();
	}

	private void ReassignFollowSlots()
	{
		for (int index = 0; index < _activeParty.Count; index++)
		{
			_activeParty[index].SetFollowSlot(index);
		}
	}

	private void EnsureFormationSlotForActor(SimpleActor actor)
	{
		if (GetFormationSlot(actor) >= 0)
		{
			return;
		}

		int slotIndex = FindFirstOpenFormationSlot();
		if (slotIndex >= 0)
		{
			SetFormationAssignment(actor, slotIndex);
		}
	}

	private int FindFirstOpenFormationSlot()
	{
		foreach (int slotIndex in FormationFillOrder)
		{
			if (IsValidCompanionFormationSlot(slotIndex) && GetFormationActor(slotIndex) == null)
			{
				return slotIndex;
			}
		}

		return -1;
	}

	private void SetFormationAssignment(SimpleActor actor, int slotIndex)
	{
		if (!IsValidCompanionFormationSlot(slotIndex))
		{
			return;
		}

		ClearFormationAssignment(actor);
		if (GetFormationActor(slotIndex) is SimpleActor previousActor)
		{
			_formationSlotsByActor.Remove(previousActor);
		}

		_formationActorsBySlot[slotIndex] = actor;
		_formationSlotsByActor[actor] = slotIndex;
	}

	private void ClearFormationAssignment(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !_formationSlotsByActor.TryGetValue(actor, out int slotIndex))
		{
			return;
		}

		_formationSlotsByActor.Remove(actor);
		if (_formationActorsBySlot.TryGetValue(slotIndex, out SimpleActor? assignedActor) && assignedActor == actor)
		{
			_formationActorsBySlot.Remove(slotIndex);
		}
	}

	private bool IsValidFormationSlot(int slotIndex)
	{
		return slotIndex >= 0 && slotIndex < FormationGridSideLength * FormationGridSideLength;
	}

	private bool IsValidCompanionFormationSlot(int slotIndex)
	{
		return IsValidFormationSlot(slotIndex) && slotIndex != FormationCenterSlotIndex;
	}

	private Vector3 GetFormationSlotLocalOffset(int slotIndex)
	{
		int row = slotIndex / FormationGridSideLength;
		int column = slotIndex % FormationGridSideLength;
		int center = FormationGridSideLength / 2;
		float localX = (column - center) * FormationSlotSpacing;
		float localZ = (center - row) * FormationSlotSpacing;
		return new Vector3(localX, 0.0f, localZ);
	}

	private Vector3 GetFallbackFormationOffset(SimpleActor actor)
	{
		int index = Mathf.Max(_activeParty.IndexOf(actor), 0);
		int ring = index / 8;
		int ringSlot = index % 8;
		float radius = 3.0f + ring * 1.35f;
		float angle = -Mathf.Pi * 0.5f + ringSlot * (Mathf.Pi * 2.0f / 8.0f);
		return new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
	}

	private void RefreshFormationViews()
	{
		if (_partyPanel != null)
		{
			_partyPanel.RefreshParty();
		}

		if (_formationPanel != null)
		{
			_formationPanel.RefreshAll();
		}
	}

	private void RecoverFromKnockdown()
	{
		CurrentHealth = Mathf.Max(MaxHealth / 2, 1);
		TeleportToSafePosition();
	}

	private void SpawnDamageEffect(int damage)
	{
		SpawnFloatingEffect(damage.ToString(), new Color(1.0f, 0.18f, 0.14f, 0.92f), 0.48f, 0.62f);
	}

	private void SpawnFloatingEffect(string text, Color color, float lifetime, float radius)
	{
		SpawnWorldCombatEffect(text, color, GlobalPosition + new Vector3(0.0f, 1.15f, 0.0f), lifetime, radius);
	}

	private void SpawnIncomingAttackCue(SimpleActor? attacker, Color color)
	{
		if (attacker == null || !IsInstanceValid(attacker))
		{
			return;
		}

		Vector3 midpoint = attacker.GlobalPosition + (GlobalPosition - attacker.GlobalPosition) * 0.62f;
		midpoint.Y = Mathf.Max(attacker.GlobalPosition.Y, GlobalPosition.Y) + 1.15f;
		SpawnWorldCombatEffect("!", color, midpoint, 0.42f, 0.72f);
	}

	private void SpawnWorldCombatEffect(string text, Color color, Vector3 position, float lifetime, float radius)
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

	private void InitializeCaptureNetAmmo()
	{
		CaptureNetCapacity = Mathf.Max(CaptureNetCapacity, 1);
		_captureNetCharges = CaptureNetCapacity;
		_captureNetRechargeRemaining = CaptureNetRechargeSeconds;
	}

	private void InitializeStarterInventory()
	{
		foreach (string itemId in BuildCatalog.GetStarterInventoryItemIds())
		{
			AddInventoryItem(itemId);
		}
	}

	private void UpdateCaptureNetRecharge(float step)
	{
		float rechargeSeconds = Mathf.Max(CaptureNetRechargeSeconds, 0.05f);
		if (_captureNetCharges >= CaptureNetCapacity)
		{
			_captureNetRechargeRemaining = rechargeSeconds;
			return;
		}

		_captureNetRechargeRemaining -= step;
		while (_captureNetRechargeRemaining <= 0.0f && _captureNetCharges < CaptureNetCapacity)
		{
			_captureNetCharges++;
			_captureNetRechargeRemaining += rechargeSeconds;
		}

		if (_captureNetCharges >= CaptureNetCapacity)
		{
			_captureNetRechargeRemaining = rechargeSeconds;
		}
	}

	private void CreateCaptureAmmoHud()
	{
		var layer = new CanvasLayer
		{
			Name = "CaptureAmmoLayer",
			Layer = 24,
		};
		AddChild(layer);

		_captureAmmoPanel = new PanelContainer
		{
			Name = "CaptureAmmoHud",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 1.0f,
			AnchorRight = 1.0f,
			AnchorTop = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = -224.0f,
			OffsetRight = -28.0f,
			OffsetTop = -112.0f,
			OffsetBottom = -28.0f,
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.041f, 0.050f, 0.78f),
			BorderColor = new Color(0.62f, 0.72f, 0.82f, 0.58f),
		};
		panelStyle.SetBorderWidthAll(1);
		panelStyle.SetCornerRadiusAll(4);
		_captureAmmoPanel.AddThemeStyleboxOverride("panel", panelStyle);
		layer.AddChild(_captureAmmoPanel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		_captureAmmoPanel.AddChild(margin);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 4);
		margin.AddChild(rows);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		rows.AddChild(titleRow);

		var netLabel = MakeHudLabel(LocaleText.T("hud.net"), 13, new Color(0.68f, 0.80f, 0.90f));
		titleRow.AddChild(netLabel);

		_captureAmmoCaptionLabel = MakeHudLabel(LocaleText.T("hud.capture_net_key"), 13, new Color(0.86f, 0.92f, 0.96f));
		_captureAmmoCaptionLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_captureAmmoCaptionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(_captureAmmoCaptionLabel);

		_captureAmmoCountLabel = MakeHudLabel($"6 / {CaptureNetCapacity}", 31, new Color(1.0f, 1.0f, 1.0f));
		_captureAmmoCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
		rows.AddChild(_captureAmmoCountLabel);

		_captureAmmoRechargeBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 7.0f),
		};
		rows.AddChild(_captureAmmoRechargeBar);
		UpdateCaptureAmmoHud();
	}

	private void CreateDamageFlashHud()
	{
		var layer = new CanvasLayer
		{
			Name = "DamageFlashLayer",
			Layer = 80,
		};
		AddChild(layer);

		_damageFlashOverlay = new ColorRect
		{
			Name = "DamageFlashOverlay",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			AnchorTop = 0.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 0.0f,
			OffsetRight = 0.0f,
			OffsetTop = 0.0f,
			OffsetBottom = 0.0f,
			Color = new Color(1.0f, 0.06f, 0.02f, 0.0f),
			Visible = false,
		};
		layer.AddChild(_damageFlashOverlay);
	}

	private void CreateInteractionPromptHud()
	{
		var layer = new CanvasLayer
		{
			Name = "InteractionPromptLayer",
			Layer = 26,
		};
		AddChild(layer);

		_interactionPromptLabel = new Label
		{
			Name = "InteractionPrompt",
			Text = string.Empty,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.78f,
			AnchorBottom = 0.78f,
			OffsetLeft = -240.0f,
			OffsetRight = 240.0f,
			OffsetTop = -22.0f,
			OffsetBottom = 22.0f,
			Visible = false,
		};
		_interactionPromptLabel.AddThemeFontSizeOverride("font_size", 18);
		_interactionPromptLabel.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.88f));
		_interactionPromptLabel.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.03f, 0.025f, 0.94f));
		_interactionPromptLabel.AddThemeConstantOverride("outline_size", 6);
		layer.AddChild(_interactionPromptLabel);
	}

	private void CreateSystemLogPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "SystemLogLayer",
			Layer = 28,
		};
		AddChild(layer);

		_systemLogPanel = new SystemLogPanel();
		layer.AddChild(_systemLogPanel);
	}

	private void CreateNpcQuestDialog()
	{
		var layer = new CanvasLayer
		{
			Name = "NpcQuestDialogLayer",
			Layer = 42,
		};
		AddChild(layer);

		_npcQuestDialog = new PanelContainer
		{
			Name = "NpcQuestDialog",
			MouseFilter = Control.MouseFilterEnum.Stop,
			Visible = false,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -260.0f,
			OffsetRight = 260.0f,
			OffsetTop = -150.0f,
			OffsetBottom = 150.0f,
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.042f, 0.052f, 0.96f),
			BorderColor = new Color(0.58f, 0.70f, 0.78f, 0.95f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		_npcQuestDialog.AddThemeStyleboxOverride("panel", style);
		layer.AddChild(_npcQuestDialog);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		_npcQuestDialog.AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		_npcQuestTitleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_npcQuestTitleLabel.AddThemeFontSizeOverride("font_size", 24);
		_npcQuestTitleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.78f));
		root.AddChild(_npcQuestTitleLabel);

		_npcQuestBodyLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_npcQuestBodyLabel.AddThemeFontSizeOverride("font_size", 18);
		_npcQuestBodyLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.96f, 1.0f));
		root.AddChild(_npcQuestBodyLabel);

		_npcQuestRewardLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_npcQuestRewardLabel.AddThemeFontSizeOverride("font_size", 16);
		_npcQuestRewardLabel.AddThemeColorOverride("font_color", new Color(0.70f, 1.0f, 0.76f));
		root.AddChild(_npcQuestRewardLabel);

		var buttons = new HBoxContainer();
		buttons.AddThemeConstantOverride("separation", 12);
		root.AddChild(buttons);

		_npcQuestAcceptButton = MakeQuestDialogButton("quest.button.accept");
		_npcQuestAcceptButton.Pressed += AcceptNpcQuestDialog;
		buttons.AddChild(_npcQuestAcceptButton);

		_npcQuestDeclineButton = MakeQuestDialogButton("quest.button.decline");
		_npcQuestDeclineButton.Pressed += DeclineNpcQuestDialog;
		buttons.AddChild(_npcQuestDeclineButton);
	}

	private static Button MakeQuestDialogButton(string textKey)
	{
		var button = new Button
		{
			Text = LocaleText.T(textKey),
			CustomMinimumSize = new Vector2(130.0f, 40.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 17);
		return button;
	}

	public void PostSystemMessage(string message, Color color)
	{
		if (_systemLogPanel == null)
		{
			return;
		}

		_systemLogPanel.AddMessage(message, color);
	}

	private void UpdateInteractionPrompt()
	{
		if (_interactionPromptLabel == null)
		{
			return;
		}

		if (_npcQuestDialog != null && _npcQuestDialog.Visible)
		{
			_interactionPromptLabel.Visible = false;
			return;
		}

		Node3D? revivalNpc = GetNearestRevivalNpc();
		if (revivalNpc != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.revive_pets", "E");
			return;
		}

		Node3D? mapPortal = GetNearestMapPortal();
		if (mapPortal != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.portal", "E", GetPortalLabel(mapPortal));
			return;
		}

		SimpleActor? merchant = GetNearestMerchantShopkeeper(out MerchantShopKind merchantShopKind);
		if (merchant != null)
		{
			_interactionPromptLabel.Visible = true;
			string promptKey = merchantShopKind == MerchantShopKind.Blacksmith ? "prompt.shop.blacksmith" : "prompt.shop.item";
			_interactionPromptLabel.Text = LocaleText.F(promptKey, "E", merchant.LocalizedDisplayName);
			return;
		}

		SimpleActor? mercenaryBroker = GetNearestMercenaryBroker();
		if (mercenaryBroker != null)
		{
			_interactionPromptLabel.Visible = true;
			_interactionPromptLabel.Text = LocaleText.F("prompt.mercenary_shop", "E", mercenaryBroker.LocalizedDisplayName);
			return;
		}

		SimpleActor? recruitNpc = GetNearestRecruitableNpc();
		_interactionPromptLabel.Visible = recruitNpc != null;
		if (recruitNpc == null)
		{
			return;
		}

		string questItemId = GetNpcQuestItemId(recruitNpc);
		if (!_acceptedNpcQuests.Contains(recruitNpc))
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.accept_task", "E", recruitNpc.LocalizedDisplayName);
		}
		else if (GetInventoryCount(questItemId) >= NpcRecruitQuestItemCount)
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.deliver_task", "E", recruitNpc.LocalizedDisplayName);
		}
		else if (_completedNpcQuests.Contains(recruitNpc) && recruitNpc.Affinity >= NpcRecruitAffinityRequirement)
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.invite", "E", recruitNpc.LocalizedDisplayName);
		}
		else
		{
			_interactionPromptLabel.Text = LocaleText.F("prompt.npc.quest_progress", "E", GetInventoryCount(questItemId), NpcRecruitQuestItemCount, recruitNpc.Affinity, NpcRecruitAffinityRequirement);
		}
	}

	private void TryInteract()
	{
		if (GetNearestRevivalNpc() != null)
		{
			ShowRevivalDialog(ReviveDefeatedCompanions());
			return;
		}

		Node3D? mapPortal = GetNearestMapPortal();
		if (mapPortal != null)
		{
			TryUseMapPortal(mapPortal);
			return;
		}

		if (GetNearestMerchantShopkeeper(out MerchantShopKind merchantShopKind) != null)
		{
			_merchantShopPanel.Open(merchantShopKind);
			UpdateMouseModeForPanels();
			return;
		}

		if (GetNearestMercenaryBroker() != null)
		{
			SetMercenaryShopPanelVisible(true);
			return;
		}

		SimpleActor? recruitNpc = GetNearestRecruitableNpc();
		if (recruitNpc != null)
		{
			TryInteractWithRecruitNpc(recruitNpc);
		}
	}

	private void TryInteractWithRecruitNpc(SimpleActor actor)
	{
		if (!CanInteractWithRecruitNpc(actor))
		{
			return;
		}

		if (_completedNpcQuests.Contains(actor) && actor.Affinity >= NpcRecruitAffinityRequirement)
		{
			RecruitNpc(actor);
			return;
		}

		if (!_acceptedNpcQuests.Contains(actor))
		{
			ShowNpcQuestDialog(actor);
			return;
		}

		string questItemId = GetNpcQuestItemId(actor);
		if (!TryConsumeInventoryItem(questItemId, NpcRecruitQuestItemCount))
		{
			PostSystemMessage(LocaleText.F("system.npc.waiting_items", actor.LocalizedDisplayName, NpcRecruitQuestItemCount, GetInventoryItemDisplayName(questItemId)), new Color(0.86f, 0.84f, 0.72f));
			return;
		}

		_completedNpcQuests.Add(actor);
		actor.IncreaseAffinity(NpcQuestAffinityReward);
		SpawnWorldCombatEffect(LocaleText.F("effect.affinity_gain", NpcQuestAffinityReward), new Color(0.62f, 1.0f, 0.78f, 0.92f), actor.GlobalPosition + new Vector3(0.0f, 1.65f, 0.0f), 0.85f, 0.62f);
		PostSystemMessage(LocaleText.F("system.npc.task_complete", actor.LocalizedDisplayName, actor.Affinity, NpcRecruitAffinityRequirement), new Color(0.78f, 1.0f, 0.82f));
		if (actor.Affinity >= NpcRecruitAffinityRequirement)
		{
			RecruitNpc(actor);
		}
		else
		{
			PostSystemMessage(LocaleText.F("system.npc.need_more_tasks", actor.LocalizedDisplayName), new Color(0.82f, 0.92f, 1.0f));
		}
	}

	private void ShowNpcQuestDialog(SimpleActor actor)
	{
		if (!CanInteractWithRecruitNpc(actor) || _npcQuestDialog == null)
		{
			return;
		}

		_pendingQuestNpc = actor;
		_npcQuestDialogIsNotice = false;
		string questItemId = GetNpcQuestItemId(actor);
		_npcQuestTitleLabel.Text = LocaleText.F("quest.dialog.title", actor.LocalizedDisplayName);
		_npcQuestBodyLabel.Text = LocaleText.F("quest.dialog.body", actor.LocalizedDisplayName, NpcRecruitQuestItemCount, GetInventoryItemDisplayName(questItemId));
		_npcQuestRewardLabel.Text = LocaleText.F("quest.dialog.reward", NpcQuestAffinityReward, NpcRecruitAffinityRequirement);
		_npcQuestRewardLabel.Visible = true;
		_npcQuestAcceptButton.Text = LocaleText.T("quest.button.accept");
		_npcQuestDeclineButton.Text = LocaleText.T("quest.button.decline");
		_npcQuestDeclineButton.Visible = true;
		_npcQuestDialog.Visible = true;
		_interactionPromptLabel.Visible = false;
		UpdateMouseModeForPanels();
	}

	private void ShowRevivalDialog(int revivedCount)
	{
		if (_npcQuestDialog == null)
		{
			return;
		}

		_pendingQuestNpc = null;
		_npcQuestDialogIsNotice = true;
		_npcQuestTitleLabel.Text = LocaleText.T("revival.dialog.title");
		_npcQuestBodyLabel.Text = revivedCount > 0
			? LocaleText.F("revival.dialog.count", revivedCount)
			: LocaleText.T("revival.dialog.no_fallen");
		_npcQuestRewardLabel.Text = string.Empty;
		_npcQuestRewardLabel.Visible = false;
		_npcQuestAcceptButton.Text = LocaleText.T("dialog.button.ok");
		_npcQuestDeclineButton.Visible = false;
		_npcQuestDialog.Visible = true;
		_interactionPromptLabel.Visible = false;
		UpdateMouseModeForPanels();
	}

	private void AcceptNpcQuestDialog()
	{
		if (_npcQuestDialogIsNotice)
		{
			CloseNpcQuestDialog();
			return;
		}

		SimpleActor? actor = _pendingQuestNpc;
		if (actor == null || !CanInteractWithRecruitNpc(actor))
		{
			CloseNpcQuestDialog();
			return;
		}

		_acceptedNpcQuests.Add(actor);
		string questItemId = GetNpcQuestItemId(actor);
		PostSystemMessage(LocaleText.F("system.npc.task_posted", actor.LocalizedDisplayName, NpcRecruitQuestItemCount, GetInventoryItemDisplayName(questItemId)), new Color(0.82f, 0.92f, 1.0f));
		CloseNpcQuestDialog();
	}

	private void DeclineNpcQuestDialog()
	{
		SimpleActor? actor = _pendingQuestNpc;
		if (actor != null && IsInstanceValid(actor))
		{
			PostSystemMessage(LocaleText.F("system.npc.task_declined", actor.LocalizedDisplayName), new Color(0.82f, 0.86f, 0.92f));
		}

		CloseNpcQuestDialog();
	}

	private void CloseNpcQuestDialog()
	{
		_pendingQuestNpc = null;
		_npcQuestDialogIsNotice = false;
		if (_npcQuestDialog != null)
		{
			_npcQuestDialog.Visible = false;
		}

		if (_npcQuestRewardLabel != null)
		{
			_npcQuestRewardLabel.Visible = true;
		}

		if (_npcQuestDeclineButton != null)
		{
			_npcQuestDeclineButton.Visible = true;
		}

		UpdateMouseModeForPanels();
	}

	private void CreateMapTravelDialog()
	{
		var layer = new CanvasLayer { Name = "MapTravelDialogLayer" };
		AddChild(layer);

		_mapTravelDialog = new PanelContainer
		{
			Name = "MapTravelDialog",
			Visible = false,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -210.0f,
			OffsetRight = 210.0f,
			OffsetTop = -150.0f,
			OffsetBottom = 150.0f,
		};
		_mapTravelDialog.AddThemeStyleboxOverride("panel", MakeDialogStyle(new Color(0.05f, 0.07f, 0.09f, 0.94f), new Color(0.35f, 0.82f, 1.0f, 0.72f)));
		layer.AddChild(_mapTravelDialog);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		_mapTravelDialog.AddChild(margin);

		var root = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 10);
		margin.AddChild(root);

		var title = new Label
		{
			Text = LocaleText.T("map.travel.title"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.78f));
		root.AddChild(title);

		_mapTravelButtonList = new VBoxContainer();
		_mapTravelButtonList.AddThemeConstantOverride("separation", 8);
		root.AddChild(_mapTravelButtonList);

		var cancelButton = MakeQuestDialogButton("dialog.button.cancel");
		cancelButton.Pressed += CloseMapTravelDialog;
		root.AddChild(cancelButton);
	}

	private void ShowMapTravelDialog(World world)
	{
		ClearChildren(_mapTravelButtonList);
		foreach ((string id, string label) in world.GetWildMapTravelOptions())
		{
			var button = new Button
			{
				Text = label,
				CustomMinimumSize = new Vector2(0.0f, 42.0f),
			};
			button.Pressed += () =>
			{
				CloseMapTravelDialog();
				world.RequestMapTravel(id);
			};
			_mapTravelButtonList.AddChild(button);
		}

		_mapTravelDialog.Visible = true;
		_interactionPromptLabel.Visible = false;
		UpdateMouseModeForPanels();
	}

	private void CloseMapTravelDialog()
	{
		if (_mapTravelDialog != null)
		{
			_mapTravelDialog.Visible = false;
		}

		UpdateMouseModeForPanels();
	}

	private static StyleBoxFlat MakeDialogStyle(Color backgroundColor, Color borderColor)
	{
		var style = new StyleBoxFlat
		{
			BgColor = backgroundColor,
			BorderColor = borderColor,
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		return style;
	}

	private static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private Node3D? GetNearestRevivalNpc()
	{
		Node3D? nearest = null;
		float bestDistance = RevivalNpcInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("revival_npc"))
		{
			if (node is not Node3D npc || !IsInstanceValid(npc))
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(npc.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = npc;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private Node3D? GetNearestMapPortal()
	{
		Node3D? nearest = null;
		float bestDistance = MapPortalInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("map_portal"))
		{
			if (node is not Node3D portal || !IsInstanceValid(portal) || !portal.IsVisibleInTree())
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(portal.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = portal;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private string GetPortalLabel(Node3D portal)
	{
		if (portal.HasMeta("label"))
		{
			string label = portal.GetMeta("label").AsString();
			if (!string.IsNullOrWhiteSpace(label))
			{
				return label;
			}
		}

		return "Travel";
	}

	private void TryUseMapPortal(Node3D portal)
	{
		if (!portal.HasMeta("target_map"))
		{
			return;
		}

		string targetMapId = portal.GetMeta("target_map").AsString();
		if (GetParent() is World world)
		{
			if (targetMapId == "wild_select")
			{
				ShowMapTravelDialog(world);
				return;
			}

			world.RequestMapTravel(targetMapId);
		}
	}

	private SimpleActor? GetNearestRecruitableNpc()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		if (_selectedActor != null && CanInteractWithRecruitNpc(_selectedActor) && GlobalPosition.DistanceTo(_selectedActor.GlobalPosition) <= NpcRecruitInteractRange)
		{
			return _selectedActor;
		}

		SimpleActor? nearest = null;
		float bestDistance = NpcRecruitInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !CanInteractWithRecruitNpc(actor))
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = actor;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private bool CanInteractWithRecruitNpc(SimpleActor actor)
	{
		return IsInCityMap()
			&& IsInstanceValid(actor)
			&& !IsMerchantShopkeeper(actor)
			&& !IsMercenaryBroker(actor)
			&& actor.IsNpcRecruitCandidate
			&& actor.MapId == "city"
			&& actor.IsActiveWorldTarget;
	}

	private SimpleActor? GetNearestMercenaryBroker()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		SimpleActor? nearest = null;
		float bestDistance = MercenaryBrokerInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !IsMercenaryBroker(actor) || !actor.IsActiveWorldTarget)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = actor;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private static bool IsMercenaryBroker(SimpleActor actor)
	{
		return actor.DisplayName == "name.npc.mercenary_broker";
	}

	private SimpleActor? GetNearestMerchantShopkeeper(out MerchantShopKind shopKind)
	{
		shopKind = MerchantShopKind.ItemShop;
		if (!IsInCityMap())
		{
			return null;
		}

		SimpleActor? nearest = null;
		float bestDistance = MerchantInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !TryGetMerchantShopKind(actor, out MerchantShopKind candidateKind) || !actor.IsActiveWorldTarget)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = actor;
				shopKind = candidateKind;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private static bool IsMerchantShopkeeper(SimpleActor actor)
	{
		return TryGetMerchantShopKind(actor, out _);
	}

	private static bool TryGetMerchantShopKind(SimpleActor actor, out MerchantShopKind shopKind)
	{
		if (actor.DisplayName == "name.npc.blacksmith")
		{
			shopKind = MerchantShopKind.Blacksmith;
			return true;
		}

		if (actor.DisplayName == "name.npc.item_merchant")
		{
			shopKind = MerchantShopKind.ItemShop;
			return true;
		}

		shopKind = MerchantShopKind.ItemShop;
		return false;
	}

	private bool IsInCityMap()
	{
		return GetParent() is World world && world.ActiveMapId == "city";
	}

	private void TrySelectActorTarget()
	{
		if (TryRaycastActor(out SimpleActor actor))
		{
			SetSelectedActor(actor);
			return;
		}

		ClearSelectedActor();
	}

	private bool TryRaycastActor(out SimpleActor actor)
	{
		actor = null!;
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		Vector3 origin = _camera.GlobalPosition;
		Vector3 end = origin + GetCameraAimDirection() * TargetInfoRange;
		var query = PhysicsRayQueryParameters3D.Create(origin, end);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
		if (result.TryGetValue("collider", out Variant colliderVariant) && colliderVariant.AsGodotObject() is SimpleActor hitActor)
		{
			actor = hitActor;
			return true;
		}

		return false;
	}

	private void SetSelectedActor(SimpleActor actor)
	{
		_selectedActor = actor;
		bool isAttackCommandTarget = IsAttackCommandTarget(actor);
		_focusedTarget = isAttackCommandTarget ? actor : null;
		EnsureSelectedTargetMarker();
		UpdateSelectedTargetMarkerColors(isAttackCommandTarget);
		if (_selectedTargetMarker != null)
		{
			_selectedTargetMarker.Visible = true;
		}
	}

	private void ClearSelectedActor()
	{
		_selectedActor = null;
		_focusedTarget = null;
		if (_selectedTargetMarker != null)
		{
			_selectedTargetMarker.Visible = false;
		}
	}

	private bool IsValidFocusedTarget(SimpleActor? actor)
	{
		return actor != null && IsInstanceValid(actor) && IsAttackCommandTarget(actor) && GlobalPosition.DistanceTo(actor.GlobalPosition) <= TargetInfoRange * 1.6f;
	}

	private bool IsValidSelectedActor(SimpleActor? actor)
	{
		return actor != null && IsInstanceValid(actor) && !actor.IsDefeated && GlobalPosition.DistanceTo(actor.GlobalPosition) <= TargetInfoRange * 1.6f;
	}

	private bool IsAttackCommandTarget(SimpleActor actor)
	{
		return IsInstanceValid(actor) && actor.IsActiveWorldTarget;
	}

	private void EnsureSelectedTargetMarker()
	{
		if (_selectedTargetMarker != null && IsInstanceValid(_selectedTargetMarker))
		{
			return;
		}

		_selectedTargetMarker = new Node3D { Name = "SelectedTargetMarker", Visible = false };
		Node parent = GetTree().CurrentScene ?? GetParent();
		parent.AddChild(_selectedTargetMarker);

		_selectedTargetRingMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.78f, 0.16f, 0.78f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.35f,
		};
		_selectedTargetArrowMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.32f, 0.18f, 0.9f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.25f,
		};

		_selectedTargetOuterRing = AddMarkerMesh("SelectedRingOuter", new CylinderMesh { TopRadius = 1.15f, BottomRadius = 1.15f, Height = 0.035f, RadialSegments = 48 }, new Vector3(0.0f, 0.035f, 0.0f), Vector3.Zero, Vector3.One, _selectedTargetRingMaterial);
		_selectedTargetInnerRing = AddMarkerMesh("SelectedRingInner", new CylinderMesh { TopRadius = 0.82f, BottomRadius = 0.82f, Height = 0.04f, RadialSegments = 48 }, new Vector3(0.0f, 0.045f, 0.0f), Vector3.Zero, Vector3.One, _selectedTargetRingMaterial);
		_selectedTargetArrow = AddMarkerMesh("SelectedArrow", new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.18f, Height = 0.42f, RadialSegments = 3 }, new Vector3(0.0f, 2.65f, 0.0f), new Vector3(180.0f, 30.0f, 0.0f), Vector3.One, _selectedTargetArrowMaterial);
	}

	private MeshInstance3D? AddMarkerMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Material material)
	{
		if (_selectedTargetMarker == null)
		{
			return null;
		}

		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position * PlayerVisualScale,
			RotationDegrees = rotationDegrees,
			Scale = scale * PlayerVisualScale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		_selectedTargetMarker.AddChild(meshInstance);
		return meshInstance;
	}

	private void UpdateSelectedTargetMarkerColors(bool isHostile)
	{
		if (_selectedTargetRingMaterial == null || _selectedTargetArrowMaterial == null)
		{
			return;
		}

		_selectedTargetRingMaterial.AlbedoColor = isHostile
			? new Color(1.0f, 0.78f, 0.16f, 0.78f)
			: new Color(0.34f, 0.72f, 1.0f, 0.70f);
		_selectedTargetArrowMaterial.AlbedoColor = isHostile
			? new Color(1.0f, 0.32f, 0.18f, 0.9f)
			: new Color(0.36f, 0.92f, 1.0f, 0.86f);
	}

	private void UpdateFocusedTargetMarker(float step)
	{
		if (!IsValidSelectedActor(_selectedActor))
		{
			ClearSelectedActor();
			return;
		}

		EnsureSelectedTargetMarker();
		if (_selectedTargetMarker == null || _selectedActor == null)
		{
			return;
		}

		bool isAttackCommandTarget = IsAttackCommandTarget(_selectedActor);
		_focusedTarget = isAttackCommandTarget ? _selectedActor : null;
		UpdateSelectedTargetMarkerColors(isAttackCommandTarget);
		if (_selectedTargetInnerRing != null)
		{
			_selectedTargetInnerRing.Visible = isAttackCommandTarget;
		}

		_selectedTargetMarker.Visible = true;
		_selectedTargetMarker.GlobalPosition = _selectedActor.GlobalPosition + Vector3.Up * 0.03f;
		_selectedTargetMarker.RotationDegrees += new Vector3(0.0f, (isAttackCommandTarget ? 120.0f : 72.0f) * step, 0.0f);
		float pulse = 1.0f + Mathf.Sin(Time.GetTicksMsec() * 0.008f) * 0.08f;
		_selectedTargetMarker.Scale = new Vector3(pulse, 1.0f, pulse);
		if (_selectedTargetArrow != null)
		{
			_selectedTargetArrow.Position = new Vector3(0.0f, isAttackCommandTarget ? 2.65f : 2.45f, 0.0f);
		}
	}

	private void UpdateCaptureAmmoHud()
	{
		if (_captureAmmoCountLabel == null || _captureAmmoRechargeBar == null)
		{
			return;
		}

		int capacity = Mathf.Max(CaptureNetCapacity, 1);
		float rechargeSeconds = Mathf.Max(CaptureNetRechargeSeconds, 0.05f);
		_captureAmmoCountLabel.Text = $"{_captureNetCharges} / {capacity}";

		if (_captureNetCharges <= 0)
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.34f, 0.28f));
		}
		else if (_captureNetCharges <= 2)
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.38f));
		}
		else
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f));
		}

		bool full = _captureNetCharges >= capacity;
		float rechargeProgress = full
			? 100.0f
			: Mathf.Clamp((1.0f - _captureNetRechargeRemaining / rechargeSeconds) * 100.0f, 0.0f, 100.0f);
		_captureAmmoRechargeBar.Value = rechargeProgress;
		_captureAmmoCaptionLabel.Text = full
			? LocaleText.T("hud.capture_net_key")
			: LocaleText.F("hud.recharge_seconds", Mathf.CeilToInt(_captureNetRechargeRemaining));
	}

	private void TriggerDamageFlash()
	{
		_damageFlashRemaining = Mathf.Max(DamageFlashDuration, 0.05f);
	}

	private void UpdateDamageFlash(float step)
	{
		if (_damageFlashOverlay == null)
		{
			return;
		}

		_damageFlashRemaining = Mathf.Max(_damageFlashRemaining - step, 0.0f);
		float duration = Mathf.Max(DamageFlashDuration, 0.05f);
		float alpha = _damageFlashRemaining <= 0.0f
			? 0.0f
			: Mathf.Clamp((_damageFlashRemaining / duration) * 0.28f, 0.0f, 0.28f);
		_damageFlashOverlay.Visible = alpha > 0.01f;
		_damageFlashOverlay.Color = new Color(1.0f, 0.06f, 0.02f, alpha);
	}

	private void UpdateMovementEffects(float step, float targetSpeed)
	{
		_footstepEffectRemaining = Mathf.Max(_footstepEffectRemaining - step, 0.0f);
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		if (!IsOnFloor() || speed < 1.2f || _footstepEffectRemaining > 0.0f)
		{
			return;
		}

		bool isFastStep = speed > WalkSpeed * 1.12f || targetSpeed > WalkSpeed + 0.1f;
		SpawnMovementDust(planarVelocity.Normalized(), speed, isFastStep);
		_footstepEffectRemaining = isFastStep ? 0.13f : 0.22f;
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
		float footSide = Mathf.Sin(Time.GetTicksMsec() * 0.018f) >= 0.0f ? 1.0f : -1.0f;
		var dust = new MovementDustEffect
		{
			DustColor = isFastStep ? new Color(0.86f, 0.78f, 0.52f, 0.74f) : new Color(0.68f, 0.62f, 0.48f, 0.62f),
			Radius = isFastStep ? 0.24f : 0.17f,
			Lifetime = isFastStep ? 0.34f : 0.44f,
			IsFastStep = isFastStep,
			DirectionYaw = Mathf.RadToDeg(Mathf.Atan2(-moveDirection.X, -moveDirection.Z)),
		};
		parent.AddChild(dust);
		dust.GlobalPosition = GlobalPosition + back * Mathf.Clamp(speed * 0.035f, 0.18f, 0.45f) + side * footSide * 0.18f + Vector3.Up * 0.04f;
	}

	private void UpdateMovementAnimation(float step)
	{
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		float moveRatio = Mathf.Clamp(speed / Mathf.Max(SprintSpeed, 0.01f), 0.0f, 1.0f);
		bool isMoving = speed > 0.25f && IsOnFloor();
		if (_playerExternalModel != null)
		{
			string state = !isMoving ? "idle" : moveRatio > 0.72f ? "run" : "walk";
			SetPlayerExternalAnimationState(state);
			StabilizePlayerExternalModel();
			return;
		}

		float phaseSpeed = Mathf.Lerp(6.2f, 11.2f, moveRatio);

		if (isMoving)
		{
			_movementAnimationPhase += step * phaseSpeed;
		}
		else
		{
			_movementAnimationPhase = Mathf.Lerp(_movementAnimationPhase, 0.0f, Mathf.Min(step * 8.0f, 1.0f));
		}

		float swing = Mathf.Sin(_movementAnimationPhase);
		float counterSwing = Mathf.Sin(_movementAnimationPhase + Mathf.Pi);
		float lift = Mathf.Abs(Mathf.Cos(_movementAnimationPhase));
		float intensity = isMoving ? Mathf.Lerp(0.45f, 1.0f, moveRatio) : 0.0f;
		float bob = Mathf.Abs(swing) * 0.045f * intensity;
		float lean = Mathf.Clamp(speed / Mathf.Max(SprintSpeed, 0.01f), 0.0f, 1.0f) * -4.5f;

		SetVisualPosition("PlayerCoatBody", new Vector3(0.0f, 1.02f + bob, 0.0f));
		SetVisualPosition("PlayerChestArmor", new Vector3(0.0f, 1.28f + bob, -0.255f));
		SetVisualPosition("PlayerFrontPanel", new Vector3(0.0f, 0.98f + bob, -0.275f));
		SetVisualPosition("PlayerChestTrim", new Vector3(0.0f, 1.42f + bob, -0.30f));
		SetVisualRotation("PlayerCoatBody", new Vector3(lean, 0.0f, swing * 1.6f * intensity));

		SetVisualRotation("PlayerLeftLeg", new Vector3(swing * 28.0f * intensity, 0.0f, -2.0f * intensity));
		SetVisualRotation("PlayerRightLeg", new Vector3(counterSwing * 28.0f * intensity, 0.0f, 2.0f * intensity));
		SetVisualPosition("PlayerLeftBoot", new Vector3(-0.16f, 0.07f + Mathf.Max(counterSwing, 0.0f) * 0.07f * intensity, -0.055f + swing * 0.05f * intensity));
		SetVisualPosition("PlayerRightBoot", new Vector3(0.16f, 0.07f + Mathf.Max(swing, 0.0f) * 0.07f * intensity, -0.055f + counterSwing * 0.05f * intensity));

		SetVisualRotation("PlayerLeftSleeve", new Vector3(counterSwing * 24.0f * intensity, 0.0f, -11.0f - swing * 5.0f * intensity));
		SetVisualRotation("PlayerRightSleeve", new Vector3(swing * 24.0f * intensity, 0.0f, 11.0f - counterSwing * 5.0f * intensity));
		SetVisualPosition("PlayerLeftGlove", new Vector3(-0.48f, 0.70f + counterSwing * 0.06f * intensity, -0.03f - counterSwing * 0.08f * intensity));
		SetVisualPosition("PlayerRightGlove", new Vector3(0.48f, 0.70f + swing * 0.06f * intensity, -0.03f - swing * 0.08f * intensity));

		SetVisualRotation("PlayerCape", new Vector3(-8.0f + Mathf.Abs(swing) * 7.0f * intensity, 0.0f, -swing * 2.5f * intensity));
		SetVisualRotation("PlayerScarfTail", new Vector3(-12.0f - moveRatio * 12.0f, 0.0f, -12.0f + swing * 5.0f * intensity));
	}

	private void SetPlayerExternalAnimationState(string state)
	{
		if (_playerExternalModel == null || _playerExternalAnimationState == state)
		{
			return;
		}

		_playerExternalAnimationState = state;
		ExternalModelLibrary.TryPlayActorAnimation(_playerExternalModel, state);
		StabilizePlayerExternalModel();
	}

	private void StabilizePlayerExternalModel()
	{
		if (_playerExternalModel != null)
		{
			ExternalModelLibrary.StabilizeRootMotion(_playerExternalModel, Vector3.Zero, new Vector3(0.0f, 180.0f, 0.0f));
		}
	}

	private void SetVisualPosition(string nodeName, Vector3 position)
	{
		if (GetNodeOrNull<Node3D>(nodeName) is Node3D node)
		{
			node.Position = position;
		}
	}

	private void SetVisualRotation(string nodeName, Vector3 rotationDegrees)
	{
		if (GetNodeOrNull<Node3D>(nodeName) is Node3D node)
		{
			node.RotationDegrees = rotationDegrees;
		}
	}

	private static Label MakeHudLabel(string text, int fontSize, Color color)
	{
		var label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private Vector3 SlowPlayerToStop(Vector3 velocity, float step)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0.0f, WalkSpeed * 7.0f * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, WalkSpeed * 7.0f * step);
		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * step;
		}

		return velocity;
	}

	private void ConfigureThirdPersonCamera()
	{
		_cameraYaw = 0.0f;
		_cameraPitch = 0.08f;
		_camera.TopLevel = true;
		_camera.Fov = 58.0f;
		_camera.Near = 0.05f;
		_camera.HOffset = 0.0f;
		_camera.VOffset = 0.0f;
		_cameraPivot.Position = Vector3.Zero;
		UpdateThirdPersonCamera();
	}

	private void UpdateThirdPersonCamera()
	{
		Vector3 backward = GetCameraPlanarBackward();
		float distance = Mathf.Max(ThirdPersonDistance, 1.0f);
		float horizontalDistance = Mathf.Max(Mathf.Cos(_cameraPitch) * distance, 1.2f);
		float cameraHeight = Mathf.Max(ThirdPersonCameraHeight + Mathf.Sin(_cameraPitch) * distance, 0.85f);
		float lookHeight = Mathf.Clamp(ThirdPersonLookHeight - Mathf.Sin(_cameraPitch) * 0.45f, 1.35f, 2.75f);
		Vector3 cameraPosition = GlobalPosition + backward * horizontalDistance + Vector3.Up * cameraHeight;
		cameraPosition = ClampCameraInsideMap(cameraPosition);
		Vector3 lookTarget = GlobalPosition + Vector3.Up * lookHeight;

		_camera.LookAtFromPosition(cameraPosition, lookTarget, Vector3.Up);
	}

	private Vector3 ClampCameraInsideMap(Vector3 position)
	{
		float halfExtent = Mathf.Max(CameraWorldHalfExtent, 8.0f);
		position.X = Mathf.Clamp(position.X, -halfExtent, halfExtent);
		position.Z = Mathf.Clamp(position.Z, -halfExtent, halfExtent);
		position.Y = Mathf.Max(position.Y, GlobalPosition.Y + 2.8f);
		return position;
	}

	private Vector3 GetCameraPlanarBackward()
	{
		return new Vector3(Mathf.Sin(_cameraYaw), 0.0f, Mathf.Cos(_cameraYaw)).Normalized();
	}

	private Vector3 GetCameraPlanarForward()
	{
		return -GetCameraPlanarBackward();
	}

	private Vector3 GetCameraPlanarRight()
	{
		Vector3 forward = GetCameraPlanarForward();
		return new Vector3(-forward.Z, 0.0f, forward.X).Normalized();
	}

	private Vector3 GetCameraAimDirection()
	{
		if (_camera == null || !IsInstanceValid(_camera))
		{
			return GetCameraPlanarForward();
		}

		return -_camera.GlobalTransform.Basis.Z.Normalized();
	}

	private Vector3 GetCaptureThrowDirection()
	{
		return GetCameraPlanarForward();
	}

	private void UpdateSafeGroundPosition()
	{
		if (!IsOnFloor())
		{
			return;
		}

		if (GlobalPosition.Y < -0.2f || GetFloorNormal().Dot(Vector3.Up) < 0.65f)
		{
			return;
		}

		_lastSafePosition = GlobalPosition + Vector3.Up * 0.18f;
	}

	private void RecoverIfOutOfWorld()
	{
		if (GlobalPosition.Y > FallRespawnHeight)
		{
			return;
		}

		TeleportToSafePosition();
	}

	private void TeleportToSafePosition()
	{
		Vector3 safePosition = _lastSafePosition;
		if (safePosition.Y < 0.05f)
		{
			safePosition.Y = 0.35f;
		}

		GlobalPosition = safePosition;
		Velocity = Vector3.Zero;
	}

	private void FaceMovementDirection(Vector3 direction, float step)
	{
		float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);
		Vector3 rotation = Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetAngle, Mathf.Min(step * 12.0f, 1.0f));
		Rotation = rotation;
	}

	private void CreatePlayerVisual()
	{
		_playerExternalModel = ExternalModelLibrary.TryAddPlayerModel(this);
		if (_playerExternalModel != null)
		{
			AddPlayerExternalEquipment();
			return;
		}

		var matCoat = MakeMaterial(new Color(0.18f, 0.36f, 0.62f));
		var matCoatDark = MakeMaterial(new Color(0.08f, 0.19f, 0.32f));
		var matTrim = MakeMaterial(new Color(0.95f, 0.72f, 0.26f));
		var matScarf = MakeMaterial(new Color(0.68f, 0.10f, 0.12f));
		var matSkin = MakeMaterial(new Color(0.86f, 0.62f, 0.44f));
		var matLeather = MakeMaterial(new Color(0.22f, 0.14f, 0.09f));
		var matDark = MakeMaterial(new Color(0.06f, 0.07f, 0.08f));
		var matMetal = MakeMaterial(new Color(0.72f, 0.76f, 0.78f), 0.36f);
		var matEye = MakeMaterial(new Color(0.98f, 0.96f, 0.88f), 0.35f);

		AddVisualMesh("PlayerCoatBody", new CapsuleMesh { Radius = 0.31f, Height = 1.06f }, new Vector3(0.0f, 1.02f, 0.0f), Vector3.Zero, new Vector3(1.0f, 1.0f, 0.76f), matCoat);
		AddVisualMesh("PlayerChestArmor", new BoxMesh { Size = new Vector3(0.56f, 0.24f, 0.065f) }, new Vector3(0.0f, 1.28f, -0.255f), Vector3.Zero, Vector3.One, matMetal);
		AddVisualMesh("PlayerFrontPanel", new BoxMesh { Size = new Vector3(0.34f, 0.60f, 0.058f) }, new Vector3(0.0f, 0.98f, -0.275f), Vector3.Zero, Vector3.One, matCoatDark);
		AddVisualMesh("PlayerChestTrim", new BoxMesh { Size = new Vector3(0.60f, 0.065f, 0.066f) }, new Vector3(0.0f, 1.42f, -0.30f), Vector3.Zero, Vector3.One, matTrim);
		AddVisualMesh("PlayerSash", new BoxMesh { Size = new Vector3(0.12f, 0.80f, 0.065f) }, new Vector3(-0.12f, 1.13f, -0.315f), new Vector3(0.0f, 0.0f, -24.0f), Vector3.One, matScarf);
		AddVisualMesh("PlayerBelt", new BoxMesh { Size = new Vector3(0.70f, 0.11f, 0.13f) }, new Vector3(0.0f, 0.72f, -0.02f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerBeltBuckle", new BoxMesh { Size = new Vector3(0.15f, 0.13f, 0.055f) }, new Vector3(0.0f, 0.72f, -0.29f), Vector3.Zero, Vector3.One, matTrim);

		AddVisualMesh("PlayerHead", new SphereMesh { Radius = 0.27f, Height = 0.54f }, new Vector3(0.0f, 1.67f, 0.0f), Vector3.Zero, new Vector3(0.94f, 1.05f, 0.92f), matSkin);
		AddVisualMesh("PlayerHairCap", new SphereMesh { Radius = 0.30f, Height = 0.36f }, new Vector3(0.0f, 1.83f, 0.03f), Vector3.Zero, new Vector3(1.04f, 0.50f, 0.94f), matDark);
		AddVisualMesh("PlayerHairBangLeft", new SphereMesh { Radius = 0.10f, Height = 0.12f }, new Vector3(-0.10f, 1.75f, -0.22f), Vector3.Zero, new Vector3(1.1f, 0.55f, 0.8f), matDark);
		AddVisualMesh("PlayerHairBangRight", new SphereMesh { Radius = 0.09f, Height = 0.11f }, new Vector3(0.09f, 1.74f, -0.23f), Vector3.Zero, new Vector3(1.0f, 0.5f, 0.8f), matDark);
		AddPlayerEye("Left", new Vector3(-0.095f, 1.68f, -0.245f), 0.032f, matEye, matDark);
		AddPlayerEye("Right", new Vector3(0.095f, 1.68f, -0.245f), 0.032f, matEye, matDark);
		AddVisualMesh("PlayerNose", new CapsuleMesh { Radius = 0.022f, Height = 0.08f }, new Vector3(0.0f, 1.63f, -0.275f), new Vector3(90.0f, 0.0f, 0.0f), Vector3.One, matSkin);
		AddVisualMesh("PlayerScarfCollar", new CylinderMesh { TopRadius = 0.31f, BottomRadius = 0.33f, Height = 0.08f, RadialSegments = 24 }, new Vector3(0.0f, 1.43f, 0.0f), Vector3.Zero, new Vector3(1.0f, 1.0f, 0.82f), matScarf);
		AddVisualMesh("PlayerScarfTail", new BoxMesh { Size = new Vector3(0.14f, 0.44f, 0.055f) }, new Vector3(-0.24f, 1.18f, 0.32f), new Vector3(-12.0f, 0.0f, -12.0f), Vector3.One, matScarf);

		AddVisualMesh("PlayerLeftShoulder", new SphereMesh { Radius = 0.13f, Height = 0.16f }, new Vector3(-0.35f, 1.34f, -0.02f), Vector3.Zero, new Vector3(1.35f, 0.55f, 0.95f), matMetal);
		AddVisualMesh("PlayerRightShoulder", new SphereMesh { Radius = 0.13f, Height = 0.16f }, new Vector3(0.35f, 1.34f, -0.02f), Vector3.Zero, new Vector3(1.35f, 0.55f, 0.95f), matMetal);
		AddVisualMesh("PlayerLeftSleeve", new CapsuleMesh { Radius = 0.082f, Height = 0.54f }, new Vector3(-0.43f, 1.03f, 0.0f), new Vector3(0.0f, 0.0f, -11.0f), Vector3.One, matCoat);
		AddVisualMesh("PlayerRightSleeve", new CapsuleMesh { Radius = 0.082f, Height = 0.54f }, new Vector3(0.43f, 1.03f, 0.0f), new Vector3(0.0f, 0.0f, 11.0f), Vector3.One, matCoat);
		AddVisualMesh("PlayerLeftGlove", new SphereMesh { Radius = 0.10f, Height = 0.18f }, new Vector3(-0.48f, 0.70f, -0.03f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerRightGlove", new SphereMesh { Radius = 0.10f, Height = 0.18f }, new Vector3(0.48f, 0.70f, -0.03f), Vector3.Zero, Vector3.One, matLeather);

		AddVisualMesh("PlayerLeftLeg", new CapsuleMesh { Radius = 0.105f, Height = 0.72f }, new Vector3(-0.16f, 0.36f, 0.0f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerRightLeg", new CapsuleMesh { Radius = 0.105f, Height = 0.72f }, new Vector3(0.16f, 0.36f, 0.0f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerLeftBoot", new BoxMesh { Size = new Vector3(0.23f, 0.14f, 0.36f) }, new Vector3(-0.16f, 0.07f, -0.055f), Vector3.Zero, Vector3.One, matDark);
		AddVisualMesh("PlayerRightBoot", new BoxMesh { Size = new Vector3(0.23f, 0.14f, 0.36f) }, new Vector3(0.16f, 0.07f, -0.055f), Vector3.Zero, Vector3.One, matDark);
		AddVisualMesh("PlayerCape", new BoxMesh { Size = new Vector3(0.62f, 0.90f, 0.055f) }, new Vector3(0.0f, 1.02f, 0.38f), new Vector3(-8.0f, 0.0f, 0.0f), Vector3.One, matTrim);
		AddVisualMesh("PlayerBackBlade", new BoxMesh { Size = new Vector3(0.075f, 0.90f, 0.045f) }, new Vector3(0.44f, 1.10f, 0.36f), new Vector3(0.0f, 0.0f, -24.0f), Vector3.One, matMetal);
		AddVisualMesh("PlayerBackBladeGuard", new BoxMesh { Size = new Vector3(0.30f, 0.055f, 0.055f) }, new Vector3(0.30f, 0.73f, 0.36f), new Vector3(0.0f, 0.0f, -24.0f), Vector3.One, matTrim);
	}

	private void AddPlayerExternalEquipment()
	{
		var equipmentRoot = new Node3D
		{
			Name = "PlayerExternalEquipment",
			Position = Vector3.Zero,
			Scale = new Vector3(0.88f, 0.88f, 0.88f),
		};
		AddChild(equipmentRoot);

		bool swordAdded = ExternalModelLibrary.TryAddModel(
			equipmentRoot,
			"res://assets/models/player/sword_2handed_color.gltf",
			"BackSword",
			new Vector3(0.34f, 1.10f, 0.34f),
			new Vector3(12.0f, 0.0f, -28.0f),
			new Vector3(0.82f, 0.82f, 0.82f)
		);
		if (!swordAdded)
		{
			AddFallbackBackSword(equipmentRoot);
		}

		bool shieldAdded = ExternalModelLibrary.TryAddModel(
			equipmentRoot,
			"res://assets/models/player/shield_badge_color.gltf",
			"BackShield",
			new Vector3(-0.34f, 1.08f, 0.34f),
			new Vector3(8.0f, 180.0f, 18.0f),
			new Vector3(0.82f, 0.82f, 0.82f)
		);
		if (!shieldAdded)
		{
			AddFallbackBackShield(equipmentRoot);
		}
	}

	private void AddFallbackBackSword(Node3D parent)
	{
		var matMetal = MakeMaterial(new Color(0.72f, 0.76f, 0.78f), 0.36f);
		var matTrim = MakeMaterial(new Color(0.95f, 0.72f, 0.26f));
		AddEquipmentMesh(parent, "BackSwordBlade", new BoxMesh { Size = new Vector3(0.07f, 0.92f, 0.045f) }, new Vector3(0.34f, 1.10f, 0.34f), new Vector3(12.0f, 0.0f, -28.0f), Vector3.One, matMetal);
		AddEquipmentMesh(parent, "BackSwordGuard", new BoxMesh { Size = new Vector3(0.30f, 0.055f, 0.055f) }, new Vector3(0.23f, 0.72f, 0.28f), new Vector3(12.0f, 0.0f, -28.0f), Vector3.One, matTrim);
		AddEquipmentMesh(parent, "BackSwordGrip", new BoxMesh { Size = new Vector3(0.055f, 0.25f, 0.055f) }, new Vector3(0.16f, 0.58f, 0.25f), new Vector3(12.0f, 0.0f, -28.0f), Vector3.One, matTrim);
	}

	private void AddFallbackBackShield(Node3D parent)
	{
		var matMetal = MakeMaterial(new Color(0.58f, 0.64f, 0.70f), 0.28f);
		var matTrim = MakeMaterial(new Color(0.95f, 0.72f, 0.26f));
		AddEquipmentMesh(parent, "BackShieldPlate", new CylinderMesh { TopRadius = 0.31f, BottomRadius = 0.31f, Height = 0.075f, RadialSegments = 28 }, new Vector3(-0.34f, 1.08f, 0.34f), new Vector3(90.0f, 0.0f, 18.0f), new Vector3(0.92f, 1.18f, 0.92f), matMetal);
		AddEquipmentMesh(parent, "BackShieldBoss", new SphereMesh { Radius = 0.095f, Height = 0.12f }, new Vector3(-0.34f, 1.08f, 0.29f), new Vector3(0.0f, 0.0f, 18.0f), new Vector3(1.0f, 0.55f, 1.0f), matTrim);
		AddEquipmentMesh(parent, "BackShieldBand", new BoxMesh { Size = new Vector3(0.11f, 0.55f, 0.045f) }, new Vector3(-0.34f, 1.08f, 0.27f), new Vector3(0.0f, 0.0f, 18.0f), Vector3.One, matTrim);
	}

	private static void AddEquipmentMesh(Node3D parent, string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Material material)
	{
		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position,
			RotationDegrees = rotationDegrees,
			Scale = scale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		parent.AddChild(meshInstance);
	}

	private void AddPlayerEye(string side, Vector3 position, float radius, Material eyeMaterial, Material pupilMaterial)
	{
		AddVisualMesh($"Player{side}EyeWhite", new SphereMesh { Radius = radius, Height = radius * 2.0f }, position, Vector3.Zero, new Vector3(1.0f, 1.0f, 0.45f), eyeMaterial);
		AddVisualMesh($"Player{side}EyePupil", new SphereMesh { Radius = radius * 0.45f, Height = radius * 0.9f }, position + new Vector3(0.0f, 0.0f, -radius * 0.72f), Vector3.Zero, new Vector3(1.0f, 1.0f, 0.35f), pupilMaterial);
	}

	private void AddVisualMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Material material)
	{
		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position,
			RotationDegrees = rotationDegrees,
			Scale = scale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		AddChild(meshInstance);
	}

	private static StandardMaterial3D MakeMaterial(Color color, float roughness = 0.82f)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness,
		};
	}

	private void CreateTargetInfoPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "TargetInfoLayer",
			Layer = 20,
		};

		AddChild(layer);
		_targetInfoPanel = new TargetInfoPanel();
		layer.AddChild(_targetInfoPanel);
	}

	private void CreateMinimapPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "MinimapLayer",
			Layer = 22,
		};

		AddChild(layer);
		_minimapPanel = new MinimapPanel();
		layer.AddChild(_minimapPanel);
		_minimapPanel.Bind(this);
	}

	private void CreatePartyPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "PartyLayer",
			Layer = 30,
		};

		AddChild(layer);
		_partyPanel = new PartyPanel();
		layer.AddChild(_partyPanel);
		_partyPanel.Bind(this);
	}

	private void CreateInventoryPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "InventoryLayer",
			Layer = 34,
		};

		AddChild(layer);
		_inventoryPanel = new InventoryPanel();
		layer.AddChild(_inventoryPanel);
		_inventoryPanel.Bind(this);
		_inventoryPanel.CloseRequested = () => SetInventoryPanelVisible(false);
	}

	private void CreateFormationPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "FormationLayer",
			Layer = 36,
		};

		AddChild(layer);
		_formationPanel = new FormationPanel();
		layer.AddChild(_formationPanel);
		_formationPanel.Bind(this);
		_formationPanel.CloseRequested = () => SetFormationPanelVisible(false);
	}

	private void CreateMerchantShopPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "MerchantShopLayer",
			Layer = 39,
		};

		AddChild(layer);
		_merchantShopPanel = new MerchantShopPanel();
		layer.AddChild(_merchantShopPanel);
		_merchantShopPanel.Bind(this);
		_merchantShopPanel.CloseRequested = () => SetMerchantShopPanelVisible(false);
	}

	private void CreateMercenaryShopPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "MercenaryShopLayer",
			Layer = 40,
		};

		AddChild(layer);
		_mercenaryShopPanel = new MercenaryShopPanel();
		layer.AddChild(_mercenaryShopPanel);
		_mercenaryShopPanel.Bind(this);
		_mercenaryShopPanel.CloseRequested = () => SetMercenaryShopPanelVisible(false);
	}

	private void CreateSettingsPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "SettingsLayer",
			Layer = 45,
		};

		AddChild(layer);
		_settingsPanel = new SettingsPanel();
		layer.AddChild(_settingsPanel);
		_settingsPanel.Bind(this);
		_settingsPanel.CloseRequested = () => SetSettingsPanelVisible(false);
	}

	private void SetPartyPanelVisible(bool visible)
	{
		_partyPanel.SetPanelVisible(visible);
		if (visible)
		{
			_settingsPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetInventoryPanelVisible(bool visible)
	{
		_inventoryPanel.SetPanelVisible(visible);
		if (visible)
		{
			_partyPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetFormationPanelVisible(bool visible)
	{
		_formationPanel.SetPanelVisible(visible);
		if (visible)
		{
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetSettingsPanelVisible(bool visible)
	{
		_settingsPanel.SetPanelVisible(visible);
		if (visible)
		{
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetMerchantShopPanelVisible(bool visible)
	{
		_merchantShopPanel.SetPanelVisible(visible);
		if (visible)
		{
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetMercenaryShopPanelVisible(bool visible)
	{
		_mercenaryShopPanel.SetPanelVisible(visible);
		if (visible)
		{
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void UpdateMouseModeForPanels()
	{
		Input.MouseMode = _partyPanel.Visible || _inventoryPanel.Visible || _formationPanel.Visible || _merchantShopPanel.Visible || _mercenaryShopPanel.Visible || _settingsPanel.Visible || (_npcQuestDialog != null && _npcQuestDialog.Visible) || (_mapTravelDialog != null && _mapTravelDialog.Visible)
			? Input.MouseModeEnum.Visible
			: Input.MouseModeEnum.Captured;
	}

	private void UpdateTargetInfoPanel()
	{
		if (TryRaycastActor(out SimpleActor actor))
		{
			_targetInfoPanel.ShowActor(actor);
			return;
		}

		_targetInfoPanel.HideActor();
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
