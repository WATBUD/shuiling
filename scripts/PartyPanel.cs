using Godot;
using System.Collections.Generic;

public partial class PartyPanel : PanelContainer
{
	private PlayerController? _player;
	private VBoxContainer _memberList = null!;
	private Label _titleLabel = null!;
	private Label _nameLabel = null!;
	private Label _roleLabel = null!;
	private Label _healthLabel = null!;
	private Label _levelLabel = null!;
	private Label _attackLabel = null!;
	private Label _defenseLabel = null!;
	private Label _growthLabel = null!;
	private Label _experienceLabel = null!;
	private Label _abilityLabel = null!;
	private Label _combatRoleLabel = null!;
	private Label _personalityLabel = null!;
	private Label _passiveLabel = null!;
	private Label _affinityLabel = null!;
	private Label _stateLabel = null!;
	private Label _identityLabel = null!;
	private Label _identityPassiveLabel = null!;
	private Label _identitySkillLabel = null!;
	private Label _buildPowerLabel = null!;
	private Label _elementLabel = null!;
	private Label _equipmentLabel = null!;
	private Label _skillGemsLabel = null!;
	private Label _aiGemLabel = null!;
	private Button _toggleActiveButton = null!;
	private Button _trainButton = null!;
	private Button _evolveButton = null!;
	private Button _abilityButton = null!;
	private Button _helmetButton = null!;
	private Button _weaponButton = null!;
	private Button _armorButton = null!;
	private Button _accessoryButton = null!;
	private Button _attributeGemButton = null!;
	private Button _skillGem1Button = null!;
	private Button _skillGem2Button = null!;
	private Button _skillGem3Button = null!;
	private Button _aiGemButton = null!;
	private ProgressBar _healthBar = null!;
	private GodotObject? _selected;

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += OnLanguageChanged;
		SetPanelVisible(false);

		if (_player != null)
		{
			RefreshParty();
			UpdateDetails();
		}
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Process(double delta)
	{
		if (!Visible)
		{
			return;
		}

		UpdateDetails();
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		_selected = player;

		if (_memberList != null)
		{
			RefreshParty();
			UpdateDetails();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshParty();
			UpdateDetails();
		}
	}

	public void RefreshParty()
	{
		if (_player == null || _memberList == null)
		{
			return;
		}

		foreach (Node child in _memberList.GetChildren())
		{
			_memberList.RemoveChild(child);
			child.QueueFree();
		}

		_titleLabel.Text = LocaleText.F("party.title", _player.ActiveParty.Count, _player.ActivePartyLimit, _player.CapturedCollection.Count);
		AddMemberButton(LocaleText.T("party.player"), _player.LocalizedPlayerName, _selected == _player, () => SelectMember(_player));

		AddHeader(LocaleText.T("party.active"));
		int activeIndex = 1;
		foreach (SimpleActor actor in GetActiveCompanions())
		{
			AddMemberButton(actor.TypeName, $"{activeIndex}. {actor.LocalizedDisplayName}", _selected == actor, () => SelectMember(actor));
			activeIndex++;
		}

		AddHeader(LocaleText.T("party.collection"));
		int storedIndex = 1;
		foreach (SimpleActor actor in GetStoredCompanions())
		{
			AddMemberButton(actor.TypeName, $"{storedIndex}. {actor.LocalizedDisplayName}", _selected == actor, () => SelectMember(actor));
			storedIndex++;
		}

		if (!IsSelectedValid())
		{
			SelectMember(_player);
		}
	}

	private void BuildPanel()
	{
		Name = "PartyPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -520.0f;
		OffsetRight = 520.0f;
		OffsetTop = -330.0f;
		OffsetBottom = 330.0f;
		CustomMinimumSize = new Vector2(1040.0f, 660.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.94f),
			BorderColor = new Color(0.34f, 0.46f, 0.58f, 0.95f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		_titleLabel = MakeLabel(22, new Color(0.96f, 0.98f, 1.0f));
		root.AddChild(_titleLabel);

		var content = new HBoxContainer();
		content.SizeFlagsVertical = SizeFlags.ExpandFill;
		content.AddThemeConstantOverride("separation", 14);
		root.AddChild(content);

		var listPanel = MakeSection();
		listPanel.CustomMinimumSize = new Vector2(300.0f, 0.0f);
		content.AddChild(listPanel);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		listPanel.AddChild(scroll);

		_memberList = new VBoxContainer();
		_memberList.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_memberList);

		var detailPanel = MakeSection();
		detailPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		content.AddChild(detailPanel);

		var detailScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		detailPanel.AddChild(detailScroll);

		var detailRows = new VBoxContainer();
		detailRows.AddThemeConstantOverride("separation", 10);
		detailRows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		detailScroll.AddChild(detailRows);

		_nameLabel = MakeLabel(24, new Color(1.0f, 1.0f, 1.0f));
		detailRows.AddChild(_nameLabel);

		_roleLabel = MakeLabel(14, new Color(0.72f, 0.82f, 0.92f));
		detailRows.AddChild(_roleLabel);

		_healthBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 16.0f),
		};
		detailRows.AddChild(_healthBar);

		_healthLabel = MakeLabel(14, new Color(0.94f, 0.94f, 0.94f));
		detailRows.AddChild(_healthLabel);

		_levelLabel = AddStatRow(detailRows, "stat.level");
		_attackLabel = AddStatRow(detailRows, "stat.attack");
		_defenseLabel = AddStatRow(detailRows, "stat.defense");
		_combatRoleLabel = AddStatRow(detailRows, "stat.role");
		_personalityLabel = AddStatRow(detailRows, "stat.personality");
		_passiveLabel = AddStatRow(detailRows, "stat.passive");
		_affinityLabel = AddStatRow(detailRows, "stat.affinity");
		_growthLabel = AddStatRow(detailRows, "stat.growth");
		_experienceLabel = AddStatRow(detailRows, "stat.experience");
		_abilityLabel = AddStatRow(detailRows, "stat.ability");
		_stateLabel = AddStatRow(detailRows, "stat.state");
		_identityLabel = AddStatRow(detailRows, "build.identity");
		_identityPassiveLabel = AddStatRow(detailRows, "build.identity_passives");
		_identitySkillLabel = AddStatRow(detailRows, "build.identity_skills");
		_buildPowerLabel = AddStatRow(detailRows, "build.power");
		_elementLabel = AddStatRow(detailRows, "build.element");
		_equipmentLabel = AddStatRow(detailRows, "build.equipment");
		_skillGemsLabel = AddStatRow(detailRows, "build.skill_gems");
		_aiGemLabel = AddStatRow(detailRows, "build.ai_gem");

		var actionRow = new HBoxContainer();
		actionRow.AddThemeConstantOverride("separation", 8);
		detailRows.AddChild(actionRow);

		_toggleActiveButton = MakeActionButton(LocaleText.T("button.deploy"));
		_toggleActiveButton.Pressed += OnToggleActivePressed;
		actionRow.AddChild(_toggleActiveButton);

		_trainButton = MakeActionButton(LocaleText.T("button.train"));
		_trainButton.Pressed += OnTrainPressed;
		actionRow.AddChild(_trainButton);

		_evolveButton = MakeActionButton(LocaleText.T("button.evolve"));
		_evolveButton.Pressed += OnEvolvePressed;
		actionRow.AddChild(_evolveButton);

		_abilityButton = MakeActionButton(LocaleText.T("button.enhance_ability"));
		_abilityButton.Pressed += OnAbilityPressed;
		actionRow.AddChild(_abilityButton);

		var buildButtonGrid = new GridContainer
		{
			Columns = 3,
		};
		buildButtonGrid.AddThemeConstantOverride("h_separation", 8);
		buildButtonGrid.AddThemeConstantOverride("v_separation", 8);
		detailRows.AddChild(buildButtonGrid);

		_helmetButton = AddBuildButton(buildButtonGrid, OnHelmetPressed);
		_weaponButton = AddBuildButton(buildButtonGrid, OnWeaponPressed);
		_armorButton = AddBuildButton(buildButtonGrid, OnArmorPressed);
		_accessoryButton = AddBuildButton(buildButtonGrid, OnAccessoryPressed);
		_attributeGemButton = AddBuildButton(buildButtonGrid, OnAttributeGemPressed);
		_aiGemButton = AddBuildButton(buildButtonGrid, OnAiGemPressed);
		_skillGem1Button = AddBuildButton(buildButtonGrid, () => OnSkillGemPressed(0));
		_skillGem2Button = AddBuildButton(buildButtonGrid, () => OnSkillGemPressed(1));
		_skillGem3Button = AddBuildButton(buildButtonGrid, () => OnSkillGemPressed(2));
	}

	private static MarginContainer MakeSection()
	{
		var section = new MarginContainer();
		section.AddThemeConstantOverride("margin_left", 12);
		section.AddThemeConstantOverride("margin_right", 12);
		section.AddThemeConstantOverride("margin_top", 12);
		section.AddThemeConstantOverride("margin_bottom", 12);
		return section;
	}

	private void AddHeader(string text)
	{
		var label = MakeLabel(13, new Color(0.62f, 0.70f, 0.76f));
		label.Text = text;
		_memberList.AddChild(label);
	}

	private void AddMemberButton(string tag, string text, bool selected, System.Action onPressed)
	{
		var button = new Button
		{
			Text = $"{tag}  {text}",
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0.0f, 38.0f),
		};
		button.AddThemeFontSizeOverride("font_size", 14);
		button.AddThemeColorOverride("font_color", selected ? new Color(1.0f, 0.94f, 0.68f) : new Color(0.9f, 0.94f, 0.98f));
		button.Pressed += onPressed;
		_memberList.AddChild(button);
	}

	private static Button MakeActionButton(string text)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(116.0f, 36.0f),
		};
		button.AddThemeFontSizeOverride("font_size", 13);
		return button;
	}

	private static Button AddBuildButton(GridContainer parent, System.Action onPressed)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(0.0f, 36.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 12);
		button.Pressed += onPressed;
		parent.AddChild(button);
		return button;
	}

	private void SelectMember(GodotObject member)
	{
		_selected = member;
		RefreshParty();
		UpdateDetails();
	}

	private void UpdateDetails()
	{
		if (_player == null)
		{
			return;
		}

		if (_selected is SimpleActor actor && IsInstanceValid(actor))
		{
			_nameLabel.Text = actor.LocalizedDisplayName;
			_roleLabel.Text = $"{actor.TypeName} / {actor.CombatSummary}";
			_healthBar.Value = actor.HealthRatio * 100.0f;
			_healthLabel.Text = LocaleText.F("stat.health_value", actor.CurrentHealth, actor.EffectiveMaxHealth);
			_levelLabel.Text = actor.Level.ToString();
			_attackLabel.Text = LocaleText.F("build.effective_stat", actor.EffectiveAttack, actor.Attack);
			_defenseLabel.Text = LocaleText.F("build.effective_stat", actor.EffectiveDefense, actor.Defense);
			_combatRoleLabel.Text = actor.CombatRoleName;
			_personalityLabel.Text = actor.LocalizedPersonality;
			_passiveLabel.Text = actor.LocalizedPassiveAbility;
			_affinityLabel.Text = $"{actor.Affinity} / 100";
			_growthLabel.Text = actor.GrowthName;
			_experienceLabel.Text = $"{actor.Experience} / {actor.ExperienceToNextLevel}";
			_abilityLabel.Text = $"{actor.LocalizedSpecialAbility} {LocaleText.T("actor.level_prefix")}{actor.AbilityRank}";
			_stateLabel.Text = actor.StateName;
			_identityLabel.Text = actor.IdentityName;
			_identityPassiveLabel.Text = actor.IdentityPassives;
			_identitySkillLabel.Text = actor.IdentityUniqueSkills;
			_buildPowerLabel.Text = actor.CurrentBuildStats.BuildPower.ToString();
			_elementLabel.Text = $"{actor.BuildElementName} / {actor.BuildRareComboName}";
			_equipmentLabel.Text = LocaleText.F("build.equipment_summary", actor.BuildEquipmentSummary, actor.CurrentBuildStats.EquipmentSocketCount);
			_skillGemsLabel.Text = actor.BuildSkillSummary;
			_aiGemLabel.Text = actor.BuildAiGemName;
			UpdateActorButtons(actor);
			UpdateBuildButtons(actor);
			return;
		}

		_selected = _player;
		_nameLabel.Text = _player.LocalizedPlayerName;
		_roleLabel.Text = LocaleText.T("party.player_role");
		_healthBar.Value = _player.HealthRatio * 100.0f;
		_healthLabel.Text = LocaleText.F("stat.health_value", _player.CurrentHealth, _player.MaxHealth);
		_levelLabel.Text = _player.Level.ToString();
		_attackLabel.Text = _player.Attack.ToString();
		_defenseLabel.Text = _player.Defense.ToString();
		_combatRoleLabel.Text = LocaleText.T("party.leader");
		_personalityLabel.Text = "-";
		_passiveLabel.Text = "-";
		_affinityLabel.Text = "-";
		_growthLabel.Text = "-";
		_experienceLabel.Text = "-";
		_abilityLabel.Text = "-";
		_stateLabel.Text = LocaleText.T("party.playable");
		_identityLabel.Text = "-";
		_identityPassiveLabel.Text = "-";
		_identitySkillLabel.Text = "-";
		_buildPowerLabel.Text = "-";
		_elementLabel.Text = "-";
		_equipmentLabel.Text = "-";
		_skillGemsLabel.Text = "-";
		_aiGemLabel.Text = "-";
		SetActorButtonsDisabled(true);
		SetBuildButtonsDisabled(true);
	}

	private void UpdateActorButtons(SimpleActor actor)
	{
		if (_player == null)
		{
			SetActorButtonsDisabled(true);
			return;
		}

		bool active = _player.IsInActiveParty(actor);
		_toggleActiveButton.Disabled = false;
		_toggleActiveButton.Text = active
			? LocaleText.T("button.store")
			: _player.ActiveParty.Count >= _player.ActivePartyLimit ? LocaleText.T("button.replace_deploy") : LocaleText.T("button.add_deploy");
		_trainButton.Disabled = false;
		_evolveButton.Disabled = !actor.CanEvolve;
		_abilityButton.Disabled = false;
	}

	private void UpdateBuildButtons(SimpleActor actor)
	{
		SetBuildButtonsDisabled(false);
		CompanionBuildLoadout loadout = actor.BuildLoadout;
		_helmetButton.Text = BuildButtonText("build.slot.helmet", LocaleText.T(BuildCatalog.GetEquipment(loadout.HelmetId).NameKey));
		_weaponButton.Text = BuildButtonText("build.slot.weapon", LocaleText.T(BuildCatalog.GetEquipment(loadout.WeaponId).NameKey));
		_armorButton.Text = BuildButtonText("build.slot.armor", LocaleText.T(BuildCatalog.GetEquipment(loadout.ArmorId).NameKey));
		_accessoryButton.Text = BuildButtonText("build.slot.accessory", LocaleText.T(BuildCatalog.GetEquipment(loadout.AccessoryId).NameKey));
		_attributeGemButton.Text = BuildButtonText("build.slot.attribute", LocaleText.T(BuildCatalog.GetAttributeGem(loadout.AttributeGemId).NameKey));
		_aiGemButton.Text = BuildButtonText("build.slot.ai", LocaleText.T(BuildCatalog.GetAiGem(loadout.AiGemId).NameKey));
		_skillGem1Button.Text = BuildButtonText("build.slot.skill1", LocaleText.T(BuildCatalog.GetSkillGem(loadout.SkillGemIds[0]).NameKey));
		_skillGem2Button.Text = BuildButtonText("build.slot.skill2", LocaleText.T(BuildCatalog.GetSkillGem(loadout.SkillGemIds[1]).NameKey));
		_skillGem3Button.Text = BuildButtonText("build.slot.skill3", LocaleText.T(BuildCatalog.GetSkillGem(loadout.SkillGemIds[2]).NameKey));
	}

	private static string BuildButtonText(string slotKey, string value)
	{
		return LocaleText.F("build.button.slot", LocaleText.T(slotKey), value);
	}

	private void SetActorButtonsDisabled(bool disabled)
	{
		_toggleActiveButton.Disabled = disabled;
		_trainButton.Disabled = disabled;
		_evolveButton.Disabled = disabled;
		_abilityButton.Disabled = disabled;
	}

	private void SetBuildButtonsDisabled(bool disabled)
	{
		_helmetButton.Disabled = disabled;
		_weaponButton.Disabled = disabled;
		_armorButton.Disabled = disabled;
		_accessoryButton.Disabled = disabled;
		_attributeGemButton.Disabled = disabled;
		_skillGem1Button.Disabled = disabled;
		_skillGem2Button.Disabled = disabled;
		_skillGem3Button.Disabled = disabled;
		_aiGemButton.Disabled = disabled;

		if (disabled)
		{
			_helmetButton.Text = "-";
			_weaponButton.Text = "-";
			_armorButton.Text = "-";
			_accessoryButton.Text = "-";
			_attributeGemButton.Text = "-";
			_skillGem1Button.Text = "-";
			_skillGem2Button.Text = "-";
			_skillGem3Button.Text = "-";
			_aiGemButton.Text = "-";
		}
	}

	private void OnToggleActivePressed()
	{
		if (_player == null || _selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		if (_player.IsInActiveParty(actor))
		{
			_player.StoreCompanion(actor);
		}
		else
		{
			_player.DeployCompanion(actor, true);
		}

		RefreshParty();
		UpdateDetails();
	}

	private void OnTrainPressed()
	{
		if (_selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		actor.GrantTraining(25);
		RefreshParty();
		UpdateDetails();
	}

	private void OnEvolvePressed()
	{
		if (_selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		actor.TryEvolve();
		RefreshParty();
		UpdateDetails();
	}

	private void OnAbilityPressed()
	{
		if (_selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		actor.EnhanceAbility();
		RefreshParty();
		UpdateDetails();
	}

	private void OnHelmetPressed()
	{
		CycleEquipmentSlot(EquipmentSlot.Helmet);
	}

	private void OnWeaponPressed()
	{
		CycleEquipmentSlot(EquipmentSlot.Weapon);
	}

	private void OnArmorPressed()
	{
		CycleEquipmentSlot(EquipmentSlot.Armor);
	}

	private void OnAccessoryPressed()
	{
		CycleEquipmentSlot(EquipmentSlot.Accessory);
	}

	private void OnAttributeGemPressed()
	{
		if (_selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		actor.CycleAttributeGem();
		RefreshParty();
		UpdateDetails();
	}

	private void OnSkillGemPressed(int slotIndex)
	{
		if (_selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		actor.CycleSkillGem(slotIndex);
		RefreshParty();
		UpdateDetails();
	}

	private void OnAiGemPressed()
	{
		if (_selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		actor.CycleAiGem();
		RefreshParty();
		UpdateDetails();
	}

	private void CycleEquipmentSlot(EquipmentSlot slot)
	{
		if (_selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		actor.CycleBuildEquipment(slot);
		RefreshParty();
		UpdateDetails();
	}

	private void OnLanguageChanged()
	{
		bool wasVisible = Visible;
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		BuildPanel();
		Visible = wasVisible;
		if (_player != null)
		{
			RefreshParty();
			UpdateDetails();
		}
	}

	private bool IsSelectedValid()
	{
		if (_selected == _player)
		{
			return true;
		}

		return _selected is SimpleActor actor && IsInstanceValid(actor) && IsCaptured(actor);
	}

	private bool IsCaptured(SimpleActor actor)
	{
		return actor.IsCaptured;
	}

	private List<SimpleActor> GetActiveCompanions()
	{
		var companions = new List<SimpleActor>();
		if (_player == null)
		{
			return companions;
		}

		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				companions.Add(actor);
			}
		}

		return companions;
	}

	private List<SimpleActor> GetStoredCompanions()
	{
		var companions = new List<SimpleActor>();
		if (_player == null)
		{
			return companions;
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !_player.IsInActiveParty(actor))
			{
				companions.Add(actor);
			}
		}

		return companions;
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		return label;
	}

	private static Label AddStatRow(VBoxContainer rows, string titleKey)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		rows.AddChild(row);

		var titleLabel = MakeLabel(15, new Color(0.66f, 0.74f, 0.80f));
		titleLabel.Text = LocaleText.T(titleKey);
		titleLabel.CustomMinimumSize = new Vector2(72.0f, 0.0f);
		row.AddChild(titleLabel);

		var valueLabel = MakeLabel(15, new Color(0.96f, 0.98f, 1.0f));
		valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(valueLabel);
		return valueLabel;
	}
}
