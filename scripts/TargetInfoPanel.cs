using Godot;

public partial class TargetInfoPanel : PanelContainer
{
	private Label _nameLabel = null!;
	private Label _typeLabel = null!;
	private Label _levelLabel = null!;
	private Label _healthLabel = null!;
	private Label _attackLabel = null!;
	private Label _defenseLabel = null!;
	private Label _rewardLabel = null!;
	private Label _stateLabel = null!;
	private ProgressBar _healthBar = null!;
	private SimpleActor? _currentActor;

	public override void _Ready()
	{
		BuildPanel();
		HideActor();
	}

	public override void _Process(double delta)
	{
		if (_currentActor == null || !IsInstanceValid(_currentActor))
		{
			HideActor();
			return;
		}

		UpdateFromActor(_currentActor);
	}

	public void ShowActor(SimpleActor actor)
	{
		_currentActor = actor;
		Visible = true;
		UpdateFromActor(actor);
	}

	public void HideActor()
	{
		_currentActor = null;
		Visible = false;
	}

	private void BuildPanel()
	{
		Name = "TargetInfoPanel";
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = 1.0f;
		AnchorRight = 1.0f;
		AnchorTop = 0.0f;
		AnchorBottom = 0.0f;
		OffsetLeft = -348.0f;
		OffsetRight = -24.0f;
		OffsetTop = 24.0f;
		OffsetBottom = 220.0f;
		CustomMinimumSize = new Vector2(324.0f, 196.0f);

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.055f, 0.065f, 0.075f, 0.88f),
			BorderColor = new Color(0.46f, 0.55f, 0.62f, 0.9f),
		};
		panelStyle.SetBorderWidthAll(2);
		panelStyle.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", panelStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		AddChild(margin);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 6);
		margin.AddChild(rows);

		_nameLabel = MakeLabel(18, new Color(0.96f, 0.98f, 1.0f));
		rows.AddChild(_nameLabel);

		_typeLabel = MakeLabel(13, new Color(0.72f, 0.82f, 0.92f));
		rows.AddChild(_typeLabel);

		_healthBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			Value = 100.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 14.0f),
		};
		rows.AddChild(_healthBar);

		_healthLabel = MakeLabel(13, new Color(0.94f, 0.94f, 0.94f));
		rows.AddChild(_healthLabel);

		_levelLabel = AddStatRow(rows, "等級");
		_attackLabel = AddStatRow(rows, "攻擊");
		_defenseLabel = AddStatRow(rows, "防禦");
		_rewardLabel = AddStatRow(rows, "獎勵");
		_stateLabel = AddStatRow(rows, "狀態");
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
		row.AddThemeConstantOverride("separation", 8);
		rows.AddChild(row);

		var titleLabel = MakeLabel(13, new Color(0.68f, 0.74f, 0.78f));
		titleLabel.Text = title;
		titleLabel.CustomMinimumSize = new Vector2(48.0f, 0.0f);
		row.AddChild(titleLabel);

		var valueLabel = MakeLabel(13, new Color(0.96f, 0.96f, 0.96f));
		valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(valueLabel);
		return valueLabel;
	}

	private void UpdateFromActor(SimpleActor actor)
	{
		_nameLabel.Text = actor.DisplayName;
		_typeLabel.Text = $"{actor.TypeName}  /  {actor.ActorKind.ToUpperInvariant()}";
		_healthBar.Value = actor.HealthRatio * 100.0f;
		_healthLabel.Text = $"生命 {actor.CurrentHealth} / {actor.MaxHealth}";
		_levelLabel.Text = actor.Level.ToString();
		_attackLabel.Text = actor.Attack.ToString();
		_defenseLabel.Text = actor.Defense.ToString();
		_rewardLabel.Text = $"{actor.ExperienceReward} EXP  /  {actor.GoldReward} 金幣";
		_stateLabel.Text = actor.StateName;
	}
}
