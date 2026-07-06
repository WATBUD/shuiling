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
	private Button _toggleActiveButton = null!;
	private Button _trainButton = null!;
	private Button _evolveButton = null!;
	private Button _abilityButton = null!;
	private ProgressBar _healthBar = null!;
	private GodotObject? _selected;

	public override void _Ready()
	{
		BuildPanel();
		SetPanelVisible(false);

		if (_player != null)
		{
			RefreshParty();
			UpdateDetails();
		}
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

		_titleLabel.Text = $"隊伍 {_player.ActiveParty.Count}/{_player.ActivePartyLimit}  收藏 {_player.CapturedCollection.Count}";
		AddMemberButton("玩家", _player.PlayerName, _selected == _player, () => SelectMember(_player));

		AddHeader("出戰");
		int activeIndex = 1;
		foreach (SimpleActor actor in GetActiveCompanions())
		{
			AddMemberButton(actor.TypeName, $"{activeIndex}. {actor.DisplayName}", _selected == actor, () => SelectMember(actor));
			activeIndex++;
		}

		AddHeader("收藏");
		int storedIndex = 1;
		foreach (SimpleActor actor in GetStoredCompanions())
		{
			AddMemberButton(actor.TypeName, $"{storedIndex}. {actor.DisplayName}", _selected == actor, () => SelectMember(actor));
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
		OffsetLeft = -430.0f;
		OffsetRight = 430.0f;
		OffsetTop = -270.0f;
		OffsetBottom = 270.0f;
		CustomMinimumSize = new Vector2(860.0f, 540.0f);

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

		var detailRows = new VBoxContainer();
		detailRows.AddThemeConstantOverride("separation", 10);
		detailPanel.AddChild(detailRows);

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

		_levelLabel = AddStatRow(detailRows, "等級");
		_attackLabel = AddStatRow(detailRows, "攻擊");
		_defenseLabel = AddStatRow(detailRows, "防禦");
		_combatRoleLabel = AddStatRow(detailRows, "定位");
		_personalityLabel = AddStatRow(detailRows, "個性");
		_passiveLabel = AddStatRow(detailRows, "被動");
		_affinityLabel = AddStatRow(detailRows, "親密");
		_growthLabel = AddStatRow(detailRows, "進化");
		_experienceLabel = AddStatRow(detailRows, "經驗");
		_abilityLabel = AddStatRow(detailRows, "能力");
		_stateLabel = AddStatRow(detailRows, "狀態");

		var actionRow = new HBoxContainer();
		actionRow.AddThemeConstantOverride("separation", 8);
		detailRows.AddChild(actionRow);

		_toggleActiveButton = MakeActionButton("出戰");
		_toggleActiveButton.Pressed += OnToggleActivePressed;
		actionRow.AddChild(_toggleActiveButton);

		_trainButton = MakeActionButton("培養 +25 EXP");
		_trainButton.Pressed += OnTrainPressed;
		actionRow.AddChild(_trainButton);

		_evolveButton = MakeActionButton("進化");
		_evolveButton.Pressed += OnEvolvePressed;
		actionRow.AddChild(_evolveButton);

		_abilityButton = MakeActionButton("技能強化");
		_abilityButton.Pressed += OnAbilityPressed;
		actionRow.AddChild(_abilityButton);
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
			_nameLabel.Text = actor.DisplayName;
			_roleLabel.Text = $"{actor.TypeName} / {actor.ActorKind.ToUpperInvariant()} / {actor.CombatSummary}";
			_healthBar.Value = actor.HealthRatio * 100.0f;
			_healthLabel.Text = $"生命 {actor.CurrentHealth} / {actor.MaxHealth}";
			_levelLabel.Text = actor.Level.ToString();
			_attackLabel.Text = actor.Attack.ToString();
			_defenseLabel.Text = actor.Defense.ToString();
			_combatRoleLabel.Text = actor.CombatRoleName;
			_personalityLabel.Text = actor.Personality;
			_passiveLabel.Text = actor.PassiveAbility;
			_affinityLabel.Text = $"{actor.Affinity} / 100";
			_growthLabel.Text = actor.GrowthName;
			_experienceLabel.Text = $"{actor.Experience} / {actor.ExperienceToNextLevel}";
			_abilityLabel.Text = $"{actor.SpecialAbility} Lv.{actor.AbilityRank}";
			_stateLabel.Text = actor.StateName;
			UpdateActorButtons(actor);
			return;
		}

		_selected = _player;
		_nameLabel.Text = _player.PlayerName;
		_roleLabel.Text = "玩家 / 隊伍領隊";
		_healthBar.Value = _player.HealthRatio * 100.0f;
		_healthLabel.Text = $"生命 {_player.CurrentHealth} / {_player.MaxHealth}";
		_levelLabel.Text = _player.Level.ToString();
		_attackLabel.Text = _player.Attack.ToString();
		_defenseLabel.Text = _player.Defense.ToString();
		_combatRoleLabel.Text = "領隊";
		_personalityLabel.Text = "-";
		_passiveLabel.Text = "-";
		_affinityLabel.Text = "-";
		_growthLabel.Text = "-";
		_experienceLabel.Text = "-";
		_abilityLabel.Text = "-";
		_stateLabel.Text = "可操作";
		SetActorButtonsDisabled(true);
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
			? "收回收藏"
			: _player.ActiveParty.Count >= _player.ActivePartyLimit ? "替換出戰" : "加入出戰";
		_trainButton.Disabled = false;
		_evolveButton.Disabled = !actor.CanEvolve;
		_abilityButton.Disabled = false;
	}

	private void SetActorButtonsDisabled(bool disabled)
	{
		_toggleActiveButton.Disabled = disabled;
		_trainButton.Disabled = disabled;
		_evolveButton.Disabled = disabled;
		_abilityButton.Disabled = disabled;
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

	private static Label AddStatRow(VBoxContainer rows, string title)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		rows.AddChild(row);

		var titleLabel = MakeLabel(15, new Color(0.66f, 0.74f, 0.80f));
		titleLabel.Text = title;
		titleLabel.CustomMinimumSize = new Vector2(72.0f, 0.0f);
		row.AddChild(titleLabel);

		var valueLabel = MakeLabel(15, new Color(0.96f, 0.98f, 1.0f));
		valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(valueLabel);
		return valueLabel;
	}
}
