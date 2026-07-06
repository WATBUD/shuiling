using Godot;

public partial class MinimapPanel : PanelContainer
{
	[Export] public float RadarRange { get; set; } = 65.0f;

	private PlayerController? _player;
	private MinimapView _mapView = null!;
	private Label _titleLabel = null!;
	private Label _playerLegendLabel = null!;
	private Label _companionLegendLabel = null!;
	private Label _npcLegendLabel = null!;
	private Label _monsterLegendLabel = null!;

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += RefreshText;
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshText;
	}

	public override void _Process(double delta)
	{
		_mapView.QueueRedraw();
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		if (_mapView != null)
		{
			_mapView.Player = player;
		}
	}

	private void BuildPanel()
	{
		Name = "MinimapPanel";
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = 0.0f;
		AnchorRight = 0.0f;
		AnchorTop = 0.0f;
		AnchorBottom = 0.0f;
		OffsetLeft = 18.0f;
		OffsetRight = 238.0f;
		OffsetTop = 18.0f;
		OffsetBottom = 282.0f;
		CustomMinimumSize = new Vector2(220.0f, 264.0f);

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.026f, 0.032f, 0.040f, 0.82f),
			BorderColor = new Color(0.54f, 0.66f, 0.78f, 0.62f),
		};
		panelStyle.SetBorderWidthAll(1);
		panelStyle.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", panelStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 6);
		margin.AddChild(rows);

		_titleLabel = MakeLabel(14, new Color(0.90f, 0.96f, 1.0f));
		rows.AddChild(_titleLabel);

		_mapView = new MinimapView
		{
			Name = "MinimapView",
			RadarRange = RadarRange,
			CustomMinimumSize = new Vector2(178.0f, 178.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_mapView.Player = _player;
		rows.AddChild(_mapView);

		var legend = new GridContainer
		{
			Columns = 2,
		};
		legend.AddThemeConstantOverride("h_separation", 9);
		legend.AddThemeConstantOverride("v_separation", 1);
		rows.AddChild(legend);

		_playerLegendLabel = MakeLegendLabel(new Color(1.0f, 0.92f, 0.36f));
		_companionLegendLabel = MakeLegendLabel(new Color(0.30f, 1.0f, 0.78f));
		_npcLegendLabel = MakeLegendLabel(new Color(0.36f, 0.76f, 1.0f));
		_monsterLegendLabel = MakeLegendLabel(new Color(1.0f, 0.30f, 0.24f));
		legend.AddChild(_playerLegendLabel);
		legend.AddChild(_companionLegendLabel);
		legend.AddChild(_npcLegendLabel);
		legend.AddChild(_monsterLegendLabel);
		RefreshText();
	}

	private void RefreshText()
	{
		if (_titleLabel == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("minimap.title");
		_playerLegendLabel.Text = $"● {LocaleText.T("minimap.player")}";
		_companionLegendLabel.Text = $"● {LocaleText.T("minimap.companion")}";
		_npcLegendLabel.Text = $"● {LocaleText.T("minimap.npc")}";
		_monsterLegendLabel.Text = $"● {LocaleText.T("minimap.monster")}";
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.VerticalAlignment = VerticalAlignment.Center;
		return label;
	}

	private static Label MakeLegendLabel(Color color)
	{
		var label = MakeLabel(11, color);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return label;
	}
}

public partial class MinimapView : Control
{
	public PlayerController? Player { get; set; }
	public float RadarRange { get; set; } = 65.0f;

	public override void _Draw()
	{
		Vector2 center = Size * 0.5f;
		float radius = Mathf.Min(Size.X, Size.Y) * 0.5f - 6.0f;
		if (radius <= 0.0f)
		{
			return;
		}

		DrawCircle(center, radius, new Color(0.05f, 0.075f, 0.09f, 0.92f));
		DrawCircle(center, radius * 0.66f, new Color(0.10f, 0.15f, 0.18f, 0.30f));
		DrawCircle(center, radius * 0.33f, new Color(0.12f, 0.18f, 0.21f, 0.35f));
		DrawArc(center, radius, 0.0f, Mathf.Tau, 96, new Color(0.68f, 0.82f, 0.92f, 0.60f), 2.0f, true);
		DrawLine(center + new Vector2(-radius, 0.0f), center + new Vector2(radius, 0.0f), new Color(0.46f, 0.58f, 0.66f, 0.28f), 1.0f);
		DrawLine(center + new Vector2(0.0f, -radius), center + new Vector2(0.0f, radius), new Color(0.46f, 0.58f, 0.66f, 0.28f), 1.0f);

		if (Player == null || !IsInstanceValid(Player))
		{
			return;
		}

		DrawActors("npcs", new Color(0.36f, 0.76f, 1.0f, 0.95f), 3.5f, center, radius);
		DrawActors("monsters", new Color(1.0f, 0.30f, 0.24f, 0.96f), 4.0f, center, radius);
		DrawActors("captured_actors", new Color(0.30f, 1.0f, 0.78f, 0.88f), 3.0f, center, radius, true);
		DrawPlayer(center);
	}

	private void DrawActors(string groupName, Color color, float dotRadius, Vector2 center, float radius, bool activePartyOnly = false)
	{
		foreach (Node node in GetTree().GetNodesInGroup(groupName))
		{
			if (node is not SimpleActor actor || actor.IsDefeated || !actor.Visible)
			{
				continue;
			}

			if (activePartyOnly && !actor.IsInActiveParty)
			{
				continue;
			}

			Vector2 point = WorldToMap(actor.GlobalPosition, center, radius, out bool clipped);
			DrawCircle(point, clipped ? dotRadius * 0.78f : dotRadius, color);
			if (clipped)
			{
				DrawArc(point, dotRadius + 1.8f, 0.0f, Mathf.Tau, 18, color, 1.2f, true);
			}
		}
	}

	private Vector2 WorldToMap(Vector3 worldPosition, Vector2 center, float radius, out bool clipped)
	{
		if (Player == null)
		{
			clipped = false;
			return center;
		}

		Vector3 relative3 = worldPosition - Player.GlobalPosition;
		var relative = new Vector2(relative3.X, relative3.Z);
		Vector3 forward3 = Player.MinimapForward;
		var forward = new Vector2(forward3.X, forward3.Z);
		if (forward.LengthSquared() <= 0.001f)
		{
			forward = new Vector2(0.0f, -1.0f);
		}
		else
		{
			forward = forward.Normalized();
		}

		Vector2 right = new(-forward.Y, forward.X);
		Vector2 rotated = new(relative.Dot(right), -relative.Dot(forward));
		float scale = radius / Mathf.Max(RadarRange, 1.0f);
		Vector2 point = rotated * scale;
		float distance = point.Length();
		float maxDistance = radius - 5.0f;
		clipped = distance > maxDistance;
		if (clipped)
		{
			point = point.Normalized() * maxDistance;
		}

		return center + point;
	}

	private void DrawPlayer(Vector2 center)
	{
		DrawCircle(center, 6.4f, new Color(1.0f, 0.92f, 0.36f, 1.0f));
		DrawCircle(center, 3.2f, new Color(0.12f, 0.08f, 0.03f, 0.95f));
		DrawLine(center, center + new Vector2(0.0f, -12.0f), new Color(1.0f, 0.98f, 0.70f, 1.0f), 3.0f, true);
	}
}
