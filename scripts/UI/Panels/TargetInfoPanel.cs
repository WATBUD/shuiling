using Godot;

/// <summary>
/// Compact RPG-style health display for the enemy explicitly selected by the player.
/// This intentionally contains no inspection stats; those belong in dedicated detail UI.
/// </summary>
public partial class TargetInfoPanel : PanelContainer
{
	private Label _nameLabel = null!;
	private Label _healthLabel = null!;
	private ProgressBar _healthBar = null!;
	private SimpleActor? _currentActor;
	private float _refreshRemaining;

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += OnLanguageChanged;
		HideActor();
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Process(double delta)
	{
		if (_currentActor == null || !IsInstanceValid(_currentActor) || _currentActor.IsDefeated || !_currentActor.Visible)
		{
			HideActor();
			return;
		}

		_refreshRemaining -= (float)delta;
		if (_refreshRemaining > 0.0f)
		{
			return;
		}

		_refreshRemaining = PerformanceConfig.TargetHudRefreshIntervalSeconds;
		UpdateFromActor(_currentActor);
	}

	public void ShowActor(SimpleActor actor)
	{
		if (_currentActor == actor)
		{
			Visible = true;
			return;
		}

		_currentActor = actor;
		Visible = true;
		_refreshRemaining = PerformanceConfig.TargetHudRefreshIntervalSeconds;
		UpdateFromActor(actor);
	}

	public void HideActor()
	{
		if (_currentActor == null && !Visible)
		{
			return;
		}

		_currentActor = null;
		Visible = false;
	}

	private void BuildPanel()
	{
		Name = "SelectedEnemyHealthBar";
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.0f;
		AnchorBottom = 0.0f;
		OffsetLeft = -280.0f;
		OffsetRight = 280.0f;
		OffsetTop = 22.0f;
		OffsetBottom = 86.0f;
		CustomMinimumSize = new Vector2(560.0f, 64.0f);

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.028f, 0.035f, 0.82f),
			BorderColor = new Color(0.42f, 0.46f, 0.52f, 0.82f),
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.62f),
			ShadowSize = 6,
		};
		panelStyle.SetBorderWidthAll(1);
		panelStyle.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", panelStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_top", 7);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 4);
		margin.AddChild(rows);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 12);
		rows.AddChild(titleRow);

		_nameLabel = MakeLabel(18, new Color(1.0f, 0.90f, 0.82f));
		_nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleRow.AddChild(_nameLabel);

		_healthLabel = MakeLabel(15, new Color(1.0f, 0.94f, 0.92f));
		_healthLabel.HorizontalAlignment = HorizontalAlignment.Right;
		titleRow.AddChild(_healthLabel);

		_healthBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			Value = 100.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 16.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		var barBackground = new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.045f, 0.05f, 0.96f),
			BorderColor = new Color(0.38f, 0.16f, 0.18f, 0.9f),
		};
		barBackground.SetBorderWidthAll(1);
		barBackground.SetCornerRadiusAll(4);
		var barFill = new StyleBoxFlat
		{
			BgColor = new Color(0.88f, 0.10f, 0.12f, 1.0f),
			BorderColor = new Color(1.0f, 0.34f, 0.24f, 0.96f),
		};
		barFill.SetBorderWidthAll(1);
		barFill.SetCornerRadiusAll(4);
		_healthBar.AddThemeStyleboxOverride("background", barBackground);
		_healthBar.AddThemeStyleboxOverride("fill", barFill);
		rows.AddChild(_healthBar);
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label
		{
			VerticalAlignment = VerticalAlignment.Center,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.01f, 0.01f, 0.95f));
		label.AddThemeConstantOverride("outline_size", 3);
		return label;
	}

	private void UpdateFromActor(SimpleActor actor)
	{
		int maxHealth = Mathf.Max(actor.EffectiveMaxHealth, 1);
		int currentHealth = Mathf.Clamp(actor.CurrentHealth, 0, maxHealth);
		_nameLabel.Text = actor.LocalizedDisplayName;
		_nameLabel.AddThemeColorOverride(
			"font_color",
			actor.IsBoss
				? actor.IsBossEnraged ? new Color(1.0f, 0.24f, 0.10f) : new Color(1.0f, 0.78f, 0.30f)
				: new Color(1.0f, 0.90f, 0.82f));
		_healthLabel.Text = $"{currentHealth:N0} / {maxHealth:N0}";
		_healthBar.Value = currentHealth / (double)maxHealth * 100.0;
	}

	private void OnLanguageChanged()
	{
		if (_currentActor != null && IsInstanceValid(_currentActor))
		{
			UpdateFromActor(_currentActor);
		}
	}
}
