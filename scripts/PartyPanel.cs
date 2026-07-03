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
	private Label _stateLabel = null!;
	private ProgressBar _healthBar = null!;
	private GodotObject? _selected;

	public override void _Ready()
	{
		BuildPanel();
		SetPanelVisible(false);
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
		RefreshParty();
		UpdateDetails();
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

		List<SimpleActor> followers = GetValidFollowers();
		_titleLabel.Text = $"隊伍成員 {followers.Count + 1}";
		AddMemberButton("玩家", _player.PlayerName, _selected == _player, () => SelectMember(_player));

		int index = 1;
		foreach (SimpleActor actor in followers)
		{
			string label = $"{index}. {actor.DisplayName}";
			AddMemberButton(actor.TypeName, label, _selected == actor, () => SelectMember(actor));
			index++;
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
		OffsetLeft = -360.0f;
		OffsetRight = 360.0f;
		OffsetTop = -230.0f;
		OffsetBottom = 230.0f;
		CustomMinimumSize = new Vector2(720.0f, 460.0f);

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
		listPanel.CustomMinimumSize = new Vector2(245.0f, 0.0f);
		content.AddChild(listPanel);

		_memberList = new VBoxContainer();
		_memberList.AddThemeConstantOverride("separation", 8);
		listPanel.AddChild(_memberList);

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
		_stateLabel = AddStatRow(detailRows, "狀態");
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
			_roleLabel.Text = $"{actor.TypeName} / {actor.ActorKind.ToUpperInvariant()}";
			_healthBar.Value = actor.HealthRatio * 100.0f;
			_healthLabel.Text = $"生命 {actor.CurrentHealth} / {actor.MaxHealth}";
			_levelLabel.Text = actor.Level.ToString();
			_attackLabel.Text = actor.Attack.ToString();
			_defenseLabel.Text = actor.Defense.ToString();
			_stateLabel.Text = actor.StateName;
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
		_stateLabel.Text = "可操作";
	}

	private bool IsSelectedValid()
	{
		if (_selected == _player)
		{
			return true;
		}

		return _selected is SimpleActor actor && IsInstanceValid(actor) && actor.IsCaptured;
	}

	private List<SimpleActor> GetValidFollowers()
	{
		var followers = new List<SimpleActor>();
		if (_player == null)
		{
			return followers;
		}

		foreach (SimpleActor actor in _player.CapturedActors)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				followers.Add(actor);
			}
		}

		return followers;
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
