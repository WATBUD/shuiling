using Godot;
using System.Collections.Generic;

public partial class CompanionInfoCard : PanelContainer
{
	private Label _title = null!;
	private ProgressBar _experienceBar = null!;
	private Label _experience = null!;
	private Label _stats = null!;
	private Label _meta = null!;
	private Label _mode = null!;
	private Label _mood = null!;
	private Label _traitsTitle = null!;
	private Label _equipmentTitle = null!;
	private Label _skillGemsTitle = null!;
	private HFlowContainer _traitFlow = null!;
	private HFlowContainer _equipmentFlow = null!;
	private HFlowContainer _skillGemFlow = null!;
	private VBoxContainer _playerAttributeSection = null!;
	private Label _attributePointLabel = null!;
	private readonly Dictionary<PlayerAttribute, Button> _attributeButtons = new();
	private readonly Dictionary<PlayerAttribute, Button> _attributeMinusButtons = new();
	private readonly Dictionary<PlayerAttribute, Label> _attributeValueLabels = new();
	private readonly Dictionary<PlayerAttribute, int> _pendingAttributePoints = new();
	private HBoxContainer _attributeCommitActions = null!;
	private Button _attributeConfirmButton = null!;
	private Button _attributeCancelButton = null!;
	private PlayerAttribute? _heldAttribute;
	private int _heldAttributeDirection;
	private float _attributeHoldRepeatRemaining;
	private const float AttributeHoldInitialDelay = 0.42f;
	private const float AttributeHoldRepeatInterval = 0.075f;
	private FloatingTooltip _tooltip = null!;
	private SimpleActor? _actor;
	private PlayerController? _player;
	private string _detailSignature = string.Empty;

	public override void _Ready()
	{
		var style = new StyleBoxFlat { BgColor = new Color(0.055f, 0.065f, 0.075f, 0.88f), BorderColor = new Color(0.34f, 0.46f, 0.58f, 0.90f) };
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);
		var rows = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
		rows.AddThemeConstantOverride("separation", 3);
		margin.AddChild(rows);

		var header = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		header.AddThemeConstantOverride("separation", 8);
		rows.AddChild(header);
		_title = MakeLabel(14, new Color(1.0f, 0.92f, 0.58f));
		_title.AutowrapMode = TextServer.AutowrapMode.Off;
		_title.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		header.AddChild(_title);
		_experience = MakeLabel(14, new Color(1.0f, 0.92f, 0.58f));
		_experience.AutowrapMode = TextServer.AutowrapMode.Off;
		_experience.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		header.AddChild(_experience);
		_attributeCommitActions = new HBoxContainer
		{
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		_attributeCommitActions.AddThemeConstantOverride("separation", 4);
		header.AddChild(_attributeCommitActions);
		_attributeConfirmButton = MakeAttributeCommitButton("button.confirm", new Color(0.56f, 1.0f, 0.68f));
		_attributeConfirmButton.Pressed += ConfirmPendingAttributeAllocation;
		_attributeCommitActions.AddChild(_attributeConfirmButton);
		_attributeCancelButton = MakeAttributeCommitButton("dialog.button.cancel", new Color(1.0f, 0.68f, 0.58f));
		_attributeCancelButton.Pressed += DiscardPendingAttributeAllocation;
		_attributeCommitActions.AddChild(_attributeCancelButton);
		_experienceBar = new ProgressBar { MinValue = 0.0, MaxValue = 1.0, ShowPercentage = false, CustomMinimumSize = new Vector2(0.0f, 14.0f), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		rows.AddChild(_experienceBar);

		var columns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		columns.AddThemeConstantOverride("separation", 12);
		rows.AddChild(columns);
		var statColumn = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		statColumn.AddThemeConstantOverride("separation", 1);
		columns.AddChild(statColumn);
		_stats = MakeLabel(12, new Color(0.80f, 0.87f, 0.93f));
		_stats.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		statColumn.AddChild(_stats);
		var metaRows = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		metaRows.AddThemeConstantOverride("separation", 0);
		columns.AddChild(metaRows);
		_mode = AddInteractiveLabel(metaRows, new Color(0.88f, 0.95f, 0.72f));
		_mode.GuiInput += OnModeGuiInput;
		_mood = MakeLabel(12, new Color(1.0f, 0.78f, 0.82f));
		metaRows.AddChild(_mood);
		_meta = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f));
		metaRows.AddChild(_meta);
		_traitsTitle = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f), "build.traits");
		metaRows.AddChild(_traitsTitle);
		_traitFlow = AddFlow(metaRows);
		_equipmentTitle = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f), "build.equipment");
		metaRows.AddChild(_equipmentTitle);
		_equipmentFlow = AddFlow(metaRows);
		_skillGemsTitle = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f), "build.skill_gems");
		metaRows.AddChild(_skillGemsTitle);
		_skillGemFlow = AddFlow(metaRows);

		_playerAttributeSection = new VBoxContainer
		{
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_playerAttributeSection.AddThemeConstantOverride("separation", 0);
		statColumn.AddChild(_playerAttributeSection);
		statColumn.MoveChild(_playerAttributeSection, 0);
		_attributePointLabel = MakeLabel(12, new Color(1.0f, 0.84f, 0.38f));
		_attributePointLabel.Visible = false;
		_playerAttributeSection.AddChild(_attributePointLabel);
		AddAttributeButton(_playerAttributeSection, PlayerAttribute.Health);
		AddAttributeButton(_playerAttributeSection, PlayerAttribute.Attack);
		AddAttributeButton(_playerAttributeSection, PlayerAttribute.Defense);
		AddAttributeButton(_playerAttributeSection, PlayerAttribute.MoveSpeed);
		AddAttributeButton(_playerAttributeSection, PlayerAttribute.AttackSpeed);
		AddAttributeButton(_playerAttributeSection, PlayerAttribute.CritChance);

		_tooltip = new FloatingTooltip
		{
			Name = "CompanionDetailTooltip",
			TopLevel = true,
			ZIndex = 100,
			MaxWidth = 720.0f,
			MaxWidthRatio = 0.70f,
			MaxHeightRatio = 0.72f,
		};
		AddChild(_tooltip);
		SetActor(_actor);
	}

	public override void _Process(double delta)
	{
		UpdateHeldAttributeAdjustment((float)delta);
		if (_tooltip != null && _tooltip.Visible)
		{
			_tooltip.PositionNearMouse(this);
		}
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
		{
			StopHeldAttributeAdjustment();
		}

		if (_tooltip == null || !_tooltip.Visible || inputEvent is not InputEventMouseButton { Pressed: true } mouseButton)
		{
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			_tooltip.ScrollDetail(-48);
			GetViewport().SetInputAsHandled();
		}
		else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			_tooltip.ScrollDetail(48);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnModeGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			CycleMode();
			AcceptEvent();
		}
	}

	public void SetActor(SimpleActor? actor)
	{
		ClearPendingAttributeAllocation();
		_player = null;
		_actor = actor != null && IsInstanceValid(actor) ? actor : null;
		if (_title == null) return;
		if (_actor == null)
		{
			_title.Text = LocaleText.T("inventory.companion_info");
			_experienceBar.Value = 0.0;
			_experience.Text = LocaleText.T("inventory.no_companions");
			_stats.Text = _meta.Text = _mood.Text = string.Empty;
			ClearFlow(_traitFlow);
			ClearFlow(_equipmentFlow);
			ClearFlow(_skillGemFlow);
			_detailSignature = string.Empty;
			_mode.Visible = false;
			_mood.Visible = false;
			_playerAttributeSection.Visible = false;
			_tooltip?.HideTooltip();
			return;
		}

		BuildStats stats = _actor.CurrentBuildStats;
		_playerAttributeSection.Visible = false;
		SetPetSectionsVisible(true);
		_traitsTitle.Text = LocaleText.T("build.traits");
		_equipmentTitle.Text = LocaleText.T("build.equipment");
		_skillGemsTitle.Text = LocaleText.T("build.skill_gems");
		_experienceBar.Visible = true;
		string rebirthTag = _actor.RebirthCount > 0 ? $"  ✦x{_actor.RebirthCount}" : string.Empty;
		_title.Text = $"{_actor.LocalizedDisplayName} - {LocaleText.F("inventory.info_header", _actor.Level)}{rebirthTag}";
		_experienceBar.MaxValue = Mathf.Max(_actor.ExperienceToNextLevel, 1);
		_experienceBar.Value = Mathf.Clamp(_actor.Experience, 0, _actor.ExperienceToNextLevel);
		_experience.Text = $"{LocaleText.T("stat.experience")} {_actor.Experience}/{_actor.ExperienceToNextLevel}";
		int rebirthBonus = _actor.RebirthTotalStatBonus;
		_stats.Text = string.Join("\n",
			BuildHealthStatText(_actor, rebirthBonus),
			BuildRebirthStatText("stat.attack", _actor.EffectiveAttack, _actor.Attack, _actor.OriginalAttackWithoutRebirth, rebirthBonus),
			BuildRebirthStatText("stat.defense", _actor.EffectiveDefense, _actor.Defense, _actor.OriginalDefenseWithoutRebirth, rebirthBonus),
			$"{LocaleText.T("stat.move_speed")} {_actor.EffectiveMoveSpeed:0.0}",
			LocaleText.F("stat.attack_speed_value", GetAttackSpeed(_actor.EffectiveAttackCooldown).ToString("0.00")),
			$"{LocaleText.T("tooltip.attack_range")} {_actor.EffectiveAttackRange:0.0}",
			$"{LocaleText.T("tooltip.detection_radius")} {_actor.EffectiveDetectionRadius:0.0}",
			$"{LocaleText.T("tooltip.crit_chance")} {stats.CritChance * 100.0f:0.#}%",
			$"{LocaleText.T("stat.growth")} {_actor.GrowthName}",
			$"{LocaleText.T("stat.state")} {_actor.StateName}");
		var metaLines = new List<string>
		{
			$"{LocaleText.T("stat.race")} {LocaleText.T(BuildCatalog.GetRaceNameKey(BuildCatalog.GetRaceId(_actor)))} / {LocaleText.T("stat.element")} {_actor.BuildElementName}",
			$"{LocaleText.T("stat.affinity")} {_actor.Affinity} / 100",
		};
		if (rebirthBonus > 0)
		{
			metaLines.Add(BuildRebirthSummary(_actor, rebirthBonus));
		}
		_meta.Text = string.Join("\n", metaLines);
		_mood.Text = $"{LocaleText.T("stat.mood")}：{_actor.MoodName}";
		_mood.Visible = true;
		string detailSignature = BuildDetailSignature(_actor);
		if (_detailSignature != detailSignature)
		{
			_detailSignature = detailSignature;
			RebuildDetailTerms();
		}
		_mode.Text = $"{LocaleText.T("build.slot.attack_mode")}: {_actor.AttackModeName}";
		_mode.Visible = true;
	}

	public void SetPlayer(PlayerController? player)
	{
		PlayerController? nextPlayer = player != null && IsInstanceValid(player) ? player : null;
		if (_player != nextPlayer)
		{
			ClearPendingAttributeAllocation();
		}
		_actor = null;
		_player = nextPlayer;
		if (_title == null) return;
		if (_player == null)
		{
			SetActor(null);
			return;
		}

		string playerRebirthTag = _player.PlayerRebirthCount > 0 ? $"  ✦x{_player.PlayerRebirthCount}" : string.Empty;
		_title.Text = $"{_player.LocalizedPlayerName} - {LocaleText.F("inventory.info_header", _player.Level)}{playerRebirthTag}";
		_attributeConfirmButton.Text = LocaleText.T("button.confirm");
		_attributeCancelButton.Text = LocaleText.T("dialog.button.cancel");
		_player.EnsurePlayerAttributePoints();
		_experience.Text = $"{LocaleText.T("stat.experience")} {_player.Experience}/{_player.ExperienceToNextLevel}";
		_experienceBar.MaxValue = Mathf.Max(_player.ExperienceToNextLevel, 1);
		_experienceBar.Value = Mathf.Clamp(_player.Experience, 0, _player.ExperienceToNextLevel);
		_experienceBar.Visible = true;
		BuildStats playerStats = _player.CurrentBuildStats;
		_stats.Text = string.Join("\n",
			$"{LocaleText.T("tooltip.attack_range")} {_player.AttackRange + playerStats.AttackRangeBonus:0.0}",
			$"{LocaleText.T("tooltip.detection_radius")} {_player.DetectionRadius:0.0}",
			$"{LocaleText.T("stat.state")} {LocaleText.T("party.playable")}");
		int pendingTotal = GetPendingAttributePointTotal();
		_meta.Text = string.Join("\n",
			LocaleText.F("player.attribute.points", Mathf.Max(_player.UnspentAttributePoints - pendingTotal, 0)),
			LocaleText.F("inventory.gold", _player.Gold),
			LocaleText.F("party.title", _player.ActiveParty.Count, _player.ActivePartyLimit, _player.AvailableCompanionCount));
		_mode.Visible = false;
		_mood.Visible = false;
		RefreshPlayerAttributes();
		SetPetSectionsVisible(true);
		_traitsTitle.Text = LocaleText.T("build.traits");
		_equipmentTitle.Text = LocaleText.T("build.equipment");
		_skillGemsTitle.Text = LocaleText.T("build.skill_gems");
		CompanionBuildLoadout loadout = _player.BuildLoadout;
		string playerSignature = $"player|{_player.Level}|{_player.UnspentAttributePoints}|{_player.HealthAttributePoints}|{_player.AttackAttributePoints}|{_player.DefenseAttributePoints}|{_player.MoveSpeedAttributePoints}|{_player.AttackSpeedAttributePoints}|{_player.CritChanceAttributePoints}|{loadout.HelmetId}|{loadout.WeaponId}|{loadout.ArmorId}|{loadout.BootsId}|{loadout.AccessoryId}|{string.Join(",", loadout.SkillGemIds)}|{string.Join(",", loadout.SkillGemLevels)}";
		if (_detailSignature != playerSignature)
		{
			_detailSignature = playerSignature;
			RebuildPlayerTerms();
		}
	}

	private void AddAttributeButton(VBoxContainer parent, PlayerAttribute attribute)
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		row.AddThemeConstantOverride("separation", 2);
		parent.AddChild(row);
		var valueLabel = MakeLabel(12, new Color(0.80f, 0.87f, 0.93f));
		valueLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		valueLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		valueLabel.VerticalAlignment = VerticalAlignment.Center;
		valueLabel.AutowrapMode = TextServer.AutowrapMode.Off;
		row.AddChild(valueLabel);
		Button plusButton = MakeAttributeAdjustButton("+", new Color(1.0f, 0.84f, 0.38f));
		BindAttributeAdjustButton(plusButton, attribute, 1);
		row.AddChild(plusButton);
		Button minusButton = MakeAttributeAdjustButton("−", new Color(1.0f, 0.68f, 0.58f));
		BindAttributeAdjustButton(minusButton, attribute, -1);
		row.AddChild(minusButton);
		_attributeMinusButtons[attribute] = minusButton;
		_attributeButtons[attribute] = plusButton;
		_attributeValueLabels[attribute] = valueLabel;
	}

	private static Button MakeAttributeAdjustButton(string text, Color color)
	{
		var button = new Button
		{
			Text = text,
			Flat = true,
			CustomMinimumSize = new Vector2(22.0f, 22.0f),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.None,
		};
		button.AddThemeFontSizeOverride("font_size", 14);
		button.AddThemeColorOverride("font_color", color);
		button.AddThemeColorOverride("font_hover_color", color.Lightened(0.2f));
		button.AddThemeColorOverride("font_pressed_color", color.Lightened(0.1f));
		return button;
	}

	private static Button MakeAttributeCommitButton(string textKey, Color color)
	{
		var button = new Button
		{
			Text = LocaleText.T(textKey),
			Flat = false,
			CustomMinimumSize = new Vector2(58.0f, 28.0f),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.None,
		};
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", color);
		button.AddThemeColorOverride("font_hover_color", color.Lightened(0.15f));
		return button;
	}

	private void BindAttributeAdjustButton(Button button, PlayerAttribute attribute, int direction)
	{
		button.ButtonDown += () => BeginHeldAttributeAdjustment(attribute, direction);
		button.ButtonUp += StopHeldAttributeAdjustment;
	}

	private void BeginHeldAttributeAdjustment(PlayerAttribute attribute, int direction)
	{
		_heldAttribute = attribute;
		_heldAttributeDirection = direction;
		_attributeHoldRepeatRemaining = AttributeHoldInitialDelay;
		AdjustPendingAttribute(attribute, direction);
	}

	private void StopHeldAttributeAdjustment()
	{
		_heldAttribute = null;
		_heldAttributeDirection = 0;
		_attributeHoldRepeatRemaining = 0.0f;
	}

	private void UpdateHeldAttributeAdjustment(float step)
	{
		if (_heldAttribute is not PlayerAttribute attribute || _heldAttributeDirection == 0 || _player == null)
		{
			return;
		}

		_attributeHoldRepeatRemaining -= step;
		while (_attributeHoldRepeatRemaining <= 0.0f)
		{
			if (!AdjustPendingAttribute(attribute, _heldAttributeDirection))
			{
				StopHeldAttributeAdjustment();
				return;
			}
			_attributeHoldRepeatRemaining += AttributeHoldRepeatInterval;
		}
	}

	private bool AdjustPendingAttribute(PlayerAttribute attribute, int direction)
	{
		if (_player == null)
		{
			return false;
		}

		int currentPending = GetPendingAttributePoints(attribute);
		if (direction > 0)
		{
			if (GetPendingAttributePointTotal() >= _player.UnspentAttributePoints)
			{
				return false;
			}
			_pendingAttributePoints[attribute] = currentPending + 1;
		}
		else if (direction < 0)
		{
			if (currentPending <= 0)
			{
				return false;
			}
			_pendingAttributePoints[attribute] = currentPending - 1;
		}
		else
		{
			return false;
		}

		RefreshPendingAttributePreview();
		return true;
	}

	private int GetPendingAttributePoints(PlayerAttribute attribute)
	{
		return _pendingAttributePoints.TryGetValue(attribute, out int count) ? count : 0;
	}

	private int GetPendingAttributePointTotal()
	{
		int total = 0;
		foreach (int count in _pendingAttributePoints.Values)
		{
			total += Mathf.Max(count, 0);
		}
		return total;
	}

	private void ConfirmPendingAttributeAllocation()
	{
		StopHeldAttributeAdjustment();
		if (_player == null)
		{
			ClearPendingAttributeAllocation();
			return;
		}

		int total = GetPendingAttributePointTotal();
		if (total <= 0 || total > _player.UnspentAttributePoints)
		{
			return;
		}

		foreach (PlayerAttribute attribute in System.Enum.GetValues<PlayerAttribute>())
		{
			int amount = GetPendingAttributePoints(attribute);
			if (amount > 0)
			{
				_player.AllocateAttributePoints(attribute, amount);
			}
		}

		ClearPendingAttributeAllocation();
		SetPlayer(_player);
	}

	public void DiscardPendingAttributeAllocation()
	{
		PlayerController? player = _player;
		ClearPendingAttributeAllocation();
		if (player != null && IsInstanceValid(player) && _title != null)
		{
			SetPlayer(player);
		}
	}

	private void ClearPendingAttributeAllocation()
	{
		StopHeldAttributeAdjustment();
		_pendingAttributePoints.Clear();
		if (_attributeCommitActions != null)
		{
			_attributeCommitActions.Visible = false;
		}
	}

	private void RefreshPendingAttributePreview()
	{
		if (_player == null)
		{
			return;
		}

		SetPlayer(_player);
	}

	private void RefreshPlayerAttributes()
	{
		if (_player == null)
		{
			_playerAttributeSection.Visible = false;
			return;
		}

		_playerAttributeSection.Visible = true;
		int pendingTotal = GetPendingAttributePointTotal();
		_attributePointLabel.Visible = true;
		_attributePointLabel.Text = LocaleText.F("player.attribute.points", Mathf.Max(_player.UnspentAttributePoints - pendingTotal, 0));
		_attributeCommitActions.Visible = pendingTotal > 0;
		BuildStats stats = _player.CurrentBuildStats;
		int pendingHealth = GetPendingAttributePoints(PlayerAttribute.Health) * PlayerController.HealthPerPoint;
		int projectedMaxHealth = stats.MaxHealth + pendingHealth;
		int projectedHealth = Mathf.Min(_player.CurrentHealth + pendingHealth, projectedMaxHealth);
		SetAttributeButton(PlayerAttribute.Health, "stat.health", $"{projectedHealth} / {projectedMaxHealth}", "player.attribute.health.detail");
		SetAttributeButton(PlayerAttribute.Attack, "stat.attack", (stats.AttackDisplayValue + GetPendingAttributePoints(PlayerAttribute.Attack) * PlayerController.AttackPerPoint).ToString("0.0"), "player.attribute.attack.detail");
		SetAttributeButton(PlayerAttribute.Defense, "stat.defense", (stats.DefenseDisplayValue + GetPendingAttributePoints(PlayerAttribute.Defense) * PlayerController.DefensePerPoint).ToString("0.0"), "player.attribute.defense.detail");
		SetAttributeButton(PlayerAttribute.MoveSpeed, "stat.move_speed", (_player.WalkSpeed * stats.MoveSpeedMultiplier + GetPendingAttributePoints(PlayerAttribute.MoveSpeed) * PlayerController.MoveSpeedPerPoint).ToString("0.00"), "player.attribute.move_speed.detail");
		SetAttributeButton(PlayerAttribute.AttackSpeed, "stat.attack_speed", (GetAttackSpeed(_player.AttackCooldown * stats.AttackCooldownMultiplier) + GetPendingAttributePoints(PlayerAttribute.AttackSpeed) * PlayerController.AttackSpeedPerPoint).ToString("0.00"), "player.attribute.attack_speed.detail");
		SetAttributeButton(PlayerAttribute.CritChance, "tooltip.crit_chance", $"{stats.CritChance * 100.0f + GetPendingAttributePoints(PlayerAttribute.CritChance) * PlayerController.CritChancePercentPerPoint:0.0}%", "player.attribute.crit_chance.detail");
	}

	private void SetAttributeButton(PlayerAttribute attribute, string nameKey, string value, string detailKey)
	{
		Button button = _attributeButtons[attribute];
		int pending = GetPendingAttributePoints(attribute);
		string pendingText = pending > 0 ? $"  (+{pending})" : string.Empty;
		_attributeValueLabels[attribute].Text = $"{LocaleText.T(nameKey)} {value}{pendingText}";
		button.Disabled = _player == null || GetPendingAttributePointTotal() >= _player.UnspentAttributePoints;
		_attributeMinusButtons[attribute].Disabled = _player == null || pending <= 0;
		button.TooltipText = LocaleText.T(detailKey);
		_attributeMinusButtons[attribute].TooltipText = LocaleText.T(detailKey);
	}

	private void RebuildPlayerTerms()
	{
		ClearFlow(_traitFlow);
		ClearFlow(_equipmentFlow);
		ClearFlow(_skillGemFlow);
		if (_player == null) return;

		CompanionBuildLoadout loadout = _player.BuildLoadout;
		foreach (EquipmentSlot slot in new[] { EquipmentSlot.Helmet, EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Boots, EquipmentSlot.Accessory })
		{
			EquipmentDefinition equipment = BuildCatalog.GetEquipment(loadout.GetEquipmentId(slot));
			string name = LocaleText.T(equipment.NameKey);
			AddTerm(_equipmentFlow, name, () => (name, LocaleText.T(equipment.SummaryKey)));
		}

		for (int index = 0; index < loadout.SkillGemIds.Length; index++)
		{
			SkillGemDefinition gem = BuildCatalog.GetSkillGem(loadout.GetSkillGemId(index));
			string gemName = LocaleText.T(gem.NameKey);
			int gemLevel = loadout.GetSkillGemLevel(index);
			string displayedName = BuildCatalog.IsUpgradeableSkillGem(gem.Id)
				? LocaleText.F("inventory.gem_level", gemName, gemLevel)
				: gemName;
			AddTerm(_skillGemFlow, displayedName, () => (displayedName, LocaleText.T(gem.SummaryKey)));
		}
	}

	private void SetPetSectionsVisible(bool visible)
	{
		_traitsTitle.Visible = visible;
		_traitFlow.Visible = visible;
		_equipmentTitle.Visible = visible;
		_equipmentFlow.Visible = visible;
		_skillGemsTitle.Visible = visible;
		_skillGemFlow.Visible = visible;
	}

	private static string BuildHealthStatText(SimpleActor actor, int rebirthBonus)
	{
		int currentHealth = actor.IsDefeated ? 0 : actor.CurrentHealth;
		if (rebirthBonus <= 0)
		{
			return $"HP {currentHealth} / {actor.EffectiveMaxHealth}";
		}

		return LocaleText.F(
			"build.rebirth_health_stat",
			currentHealth,
			actor.EffectiveMaxHealth,
			actor.OriginalMaxHealthWithoutRebirth,
			rebirthBonus);
	}

	private static string BuildRebirthStatText(string labelKey, int effectiveValue, int baseValue, int originalValue, int rebirthBonus)
	{
		string label = LocaleText.T(labelKey);
		if (rebirthBonus <= 0)
		{
			return $"{label} {LocaleText.F("build.effective_stat", effectiveValue, baseValue)}";
		}

		return $"{label} {LocaleText.F("build.rebirth_effective_stat", effectiveValue, originalValue, rebirthBonus)}";
	}

	private static string BuildRebirthSummary(SimpleActor actor, int rebirthBonus)
	{
		return LocaleText.F("stat.rebirth_summary", actor.RebirthCount, rebirthBonus, rebirthBonus, rebirthBonus);
	}

	private void CycleMode()
	{
		if (_actor == null || !IsInstanceValid(_actor)) return;
		_actor.CycleAttackMode();
		SetActor(_actor);
	}

	private void RebuildDetailTerms()
	{
		ClearFlow(_traitFlow);
		ClearFlow(_equipmentFlow);
		ClearFlow(_skillGemFlow);
		if (_actor == null) return;

		foreach (string traitKey in _actor.TraitKeys)
		{
			string capturedKey = traitKey;
			AddTerm(_traitFlow, LocaleText.T(capturedKey), () => BuildSingleTraitTooltip(capturedKey));
		}

		CompanionBuildLoadout loadout = _actor.BuildLoadout;
		var equipmentItems = new (string Id, string Slot)[]
		{
			(loadout.HelmetId, LocaleText.T("build.slot.helmet")),
			(loadout.WeaponId, LocaleText.T("build.slot.weapon")),
			(loadout.ArmorId, LocaleText.T("build.slot.armor")),
			(loadout.BootsId, LocaleText.T("build.slot.boots")),
			(loadout.AccessoryId, LocaleText.T("build.slot.accessory")),
		};
		foreach ((string id, string slot) in equipmentItems)
		{
			string capturedId = id;
			string capturedSlot = slot;
			string name = LocaleText.T(BuildCatalog.GetEquipment(id).NameKey) + BuildCatalog.GetStarSuffix(id);
			AddTerm(_equipmentFlow, name, () => (InventoryPanel.BuildItemTooltipTitle(capturedId), InventoryPanel.BuildItemTooltipBody(capturedId, capturedSlot)));
		}

		int unlockedCores = BuildCatalog.GetUnlockedSupportCoreCount(_actor.Level);
		for (int index = 0; index < loadout.SkillGemIds.Length && index < unlockedCores; index++)
		{
			string id = loadout.GetSkillGemId(index);
			if (id == "gem.skill.none")
			{
				continue;
			}

			string slot = index == 0 ? LocaleText.T("build.slot.main_core") : LocaleText.T("build.slot.support_core_plain");
			SkillGemDefinition gem = BuildCatalog.GetSkillGem(id);
			string name = $"{LocaleText.T(gem.NameKey)} Lv.{loadout.GetSkillGemLevel(index)}";
			AddTerm(_skillGemFlow, name, () => (InventoryPanel.BuildItemTooltipTitle(id), InventoryPanel.BuildItemTooltipBody(id, slot)));
		}
	}

	private static string BuildDetailSignature(SimpleActor actor)
	{
		CompanionBuildLoadout loadout = actor.BuildLoadout;
		return string.Join("|",
			actor.GetInstanceId(),
			actor.RebirthCount,
			actor.MaxHealth,
			actor.Attack,
			actor.Defense,
			string.Join(",", actor.TraitKeys),
			loadout.HelmetId,
			loadout.WeaponId,
			loadout.ArmorId,
			loadout.BootsId,
			loadout.AccessoryId,
			string.Join(",", loadout.SkillGemIds),
			string.Join(",", loadout.SkillGemLevels));
	}

	private (string, string) BuildSingleTraitTooltip(string traitKey)
	{
		if (_actor == null) return (string.Empty, string.Empty);
		CompanionIdentity identity = BuildCatalog.GetIdentity(_actor);
		string value = traitKey switch
		{
			"identity.passive.move_speed" => $"{LocaleText.T("stat.move_speed")} +{(identity.MoveSpeedMultiplier - 1.0f) * 100.0f:0.#}%\n{_actor.MoveSpeed:0.0} -> {_actor.MoveSpeed * identity.MoveSpeedMultiplier:0.0}",
			"identity.passive.crit_rate" => $"{LocaleText.T("tooltip.crit_chance")} +{identity.CritChanceBonus * 100.0f:0.#}%\n{LocaleText.T("tooltip.attack_cooldown")} {(1.0f - identity.AttackCooldownMultiplier) * 100.0f:0.#}%",
			"identity.passive.water_damage" or "identity.passive.fire_damage" => $"{LocaleText.T($"element.{identity.ElementAffinityId}")} +{(identity.ElementAffinityDamageMultiplier - 1.0f) * 100.0f:0.#}%\n{LocaleText.T("stat.attack")} x{identity.AttackMultiplier:0.00}",
			"identity.passive.water_aoe" or "identity.passive.attack_range" => $"{LocaleText.T("tooltip.attack_range")} +{identity.AttackRangeBonus:0.0}\n{_actor.AttackRange:0.0} -> {_actor.AttackRange + identity.AttackRangeBonus:0.0}",
			"identity.passive.vitality" => BuildHealthTraitText(identity),
			"identity.passive.power_strike" => $"{LocaleText.T("stat.attack")} +{identity.AttackBonus}\n{_actor.Attack} -> {_actor.Attack + identity.AttackBonus}",
			"identity.passive.thick_hide" => $"HP +{(identity.MaxHealthMultiplier - 1.0f) * 100.0f:0.#}%\n{LocaleText.T("stat.defense")} +{(identity.DefenseMultiplier - 1.0f) * 100.0f:0.#}%",
			"identity.passive.poison_mastery" => $"{LocaleText.T("stat.attack")} +{identity.AttackBonus}\n{LocaleText.T("element.poison")} +{(identity.ElementAffinityDamageMultiplier - 1.0f) * 100.0f:0.#}%",
			"identity.passive.agility" => $"{LocaleText.T("stat.move_speed")} +{(identity.MoveSpeedMultiplier - 1.0f) * 100.0f:0.#}%\n{_actor.MoveSpeed:0.0} -> {_actor.MoveSpeed * identity.MoveSpeedMultiplier:0.0}",
			"identity.passive.guard_oath" => $"HP +{identity.MaxHealthBonus}\n{LocaleText.T("stat.defense")} +{identity.DefenseBonus}",
			"identity.passive.adaptable" => $"HP +{identity.MaxHealthBonus}\n{LocaleText.T("stat.attack")} +{identity.AttackBonus}\n{LocaleText.T("stat.defense")} +{identity.DefenseBonus}\n{LocaleText.T("stat.move_speed")} +{(identity.MoveSpeedMultiplier - 1.0f) * 100.0f:0.#}%",
			_ => LocaleText.T(traitKey),
		};
		return (LocaleText.T(traitKey), value);
	}

	private static string BuildHealthTraitText(CompanionIdentity identity)
	{
		var lines = new List<string>();
		if (identity.MaxHealthBonus != 0) lines.Add($"HP +{identity.MaxHealthBonus}");
		if (!Mathf.IsEqualApprox(identity.MaxHealthMultiplier, 1.0f)) lines.Add($"HP +{(identity.MaxHealthMultiplier - 1.0f) * 100.0f:0.#}%");
		return string.Join("\n", lines);
	}

	private void AddTerm(HFlowContainer flow, string text, System.Func<(string Title, string Body)> factory)
	{
		Label label = MakeLabel(12, new Color(0.88f, 0.95f, 0.72f));
		label.Text = $"[{text}]";
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.MouseFilter = MouseFilterEnum.Stop;
		label.MouseDefaultCursorShape = CursorShape.PointingHand;
		flow.AddChild(label);
		BindTooltip(label, factory);
	}

	private static HFlowContainer AddFlow(VBoxContainer parent)
	{
		var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		flow.AddThemeConstantOverride("h_separation", 5);
		flow.AddThemeConstantOverride("v_separation", 1);
		parent.AddChild(flow);
		return flow;
	}

	private static void ClearFlow(HFlowContainer flow)
	{
		foreach (Node child in flow.GetChildren())
		{
			flow.RemoveChild(child);
			child.QueueFree();
		}
	}

	private Label AddInteractiveLabel(VBoxContainer parent, Color? color = null)
	{
		Label label = MakeLabel(12, color ?? new Color(0.82f, 0.92f, 0.76f));
		label.MouseFilter = MouseFilterEnum.Stop;
		label.MouseDefaultCursorShape = CursorShape.PointingHand;
		parent.AddChild(label);
		return label;
	}

	private void BindTooltip(Control control, System.Func<(string Title, string Body)> factory)
	{
		control.MouseEntered += () =>
		{
			bool hasActor = _actor != null && IsInstanceValid(_actor);
			bool hasPlayer = _player != null && IsInstanceValid(_player);
			if (!hasActor && !hasPlayer) return;
			(string title, string body) = factory();
			_tooltip.ShowTooltip(title, body, this);
		};
		control.MouseExited += () => _tooltip.HideTooltip();
	}

	private (string, string) BuildTraitsTooltip()
	{
		if (_actor == null) return (string.Empty, string.Empty);
		BuildStats stats = _actor.CurrentBuildStats;
		return (LocaleText.T("build.traits"), string.Join("\n",
			_actor.TraitSummary,
			$"HP {_actor.MaxHealth} -> {_actor.EffectiveMaxHealth}",
			$"{LocaleText.T("stat.attack")} {_actor.Attack} -> {_actor.EffectiveAttack}",
			$"{LocaleText.T("stat.defense")} {_actor.Defense} -> {_actor.EffectiveDefense}",
			$"{LocaleText.T("stat.move_speed")} {_actor.MoveSpeed:0.0} -> {_actor.EffectiveMoveSpeed:0.0}",
			$"{LocaleText.T("stat.attack_speed")} {GetAttackSpeed(_actor.AttackCooldown):0.00} -> {GetAttackSpeed(_actor.EffectiveAttackCooldown):0.00}",
			$"{LocaleText.T("tooltip.crit_chance")} {stats.CritChance * 100.0f:0.#}%",
			$"{LocaleText.T("tooltip.life_steal")} {stats.LifeStealPercent * 100.0f:0.#}%",
			$"{LocaleText.T("tooltip.control_chance")} {stats.ControlChance * 100.0f:0.#}%"));
	}

	private (string, string) BuildEquipmentTooltip()
	{
		if (_actor == null) return (string.Empty, string.Empty);
		CompanionBuildLoadout loadout = _actor.BuildLoadout;
		var items = new (string Id, string Slot)[]
		{
			(loadout.HelmetId, LocaleText.T("build.slot.helmet")),
			(loadout.WeaponId, LocaleText.T("build.slot.weapon")),
			(loadout.ArmorId, LocaleText.T("build.slot.armor")),
			(loadout.BootsId, LocaleText.T("build.slot.boots")),
			(loadout.AccessoryId, LocaleText.T("build.slot.accessory")),
		};
		var blocks = new List<string>();
		foreach ((string id, string slot) in items)
		{
			string name = LocaleText.T(BuildCatalog.GetEquipment(id).NameKey);
			blocks.Add($"[{slot}] {name}\n{InventoryPanel.BuildItemTooltipBody(id, slot)}");
		}
		return (LocaleText.T("build.equipment"), string.Join("\n\n", blocks));
	}

	private (string, string) BuildSkillGemTooltip()
	{
		if (_actor == null) return (string.Empty, string.Empty);
		CompanionBuildLoadout loadout = _actor.BuildLoadout;
		var blocks = new List<string>();
		int unlockedCores = BuildCatalog.GetUnlockedSupportCoreCount(_actor.Level);
		for (int index = 0; index < loadout.SkillGemIds.Length && index < unlockedCores; index++)
		{
			string id = loadout.GetSkillGemId(index);
			if (id == "gem.skill.none")
			{
				continue;
			}

			SkillGemDefinition gem = BuildCatalog.GetSkillGem(id);
			string slot = index == 0 ? LocaleText.T("build.slot.main_core") : LocaleText.T("build.slot.support_core_plain");
			blocks.Add($"{LocaleText.T(gem.NameKey)} Lv.{loadout.GetSkillGemLevel(index)}\n{InventoryPanel.BuildItemTooltipBody(id, slot)}");
		}
		return (LocaleText.T("build.skill_gems"), string.Join("\n\n", blocks));
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label { VerticalAlignment = VerticalAlignment.Top, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static float GetAttackSpeed(float attackCooldown)
	{
		return 1.0f / Mathf.Max(attackCooldown, 0.01f);
	}

	private static Label MakeLabel(int fontSize, Color color, string textKey)
	{
		Label label = MakeLabel(fontSize, color);
		label.Text = LocaleText.T(textKey);
		return label;
	}
}
