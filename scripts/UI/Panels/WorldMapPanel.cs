using Godot;
using System.Collections.Generic;

// World-map panel opened with the M key: draws the six maps (main city + five wild
// biomes) as a connected node chain over a winding overworld path and lets the player
// fast-travel to any reachable node. Travel rules live on World (GetWorldMapNodes /
// RequestMapTravel); this panel is purely the view + input. Shell/lifecycle mirror
// CoreEnhancerPanel. World is reached via the player's parent (same as MinimapPanel).
public partial class WorldMapPanel : PanelContainer
{
	// Fixed normalized layout (x,y in 0..1, y down): a cross centred on the forest
	// hub — skeleton/marsh north, snow west, badlands east, city directly south.
	private static readonly (string Id, Vector2 Pos)[] NodeLayout =
	{
		("wild_skeleton", new Vector2(0.50f, 0.12f)),
		("wild_marsh", new Vector2(0.50f, 0.31f)),
		("wild_snow", new Vector2(0.24f, 0.50f)),
		("wild_forest", new Vector2(0.50f, 0.50f)),
		("wild_badlands", new Vector2(0.76f, 0.50f)),
		("city", new Vector2(0.50f, 0.82f)),
	};

	// Dashed links between nodes (forest is the hub; marsh continues north to skeleton).
	private static readonly (string A, string B)[] NodeEdges =
	{
		("wild_forest", "wild_marsh"),
		("wild_marsh", "wild_skeleton"),
		("wild_forest", "wild_snow"),
		("wild_forest", "wild_badlands"),
		("wild_forest", "city"),
	};

	// Per-node lit state (visited or current) — drives which nodes/routes glow.
	private readonly Dictionary<string, bool> _litById = new();

	private PlayerController? _player;
	private Label _titleLabel = null!;
	private Label _hintLabel = null!;
	private WorldMapCanvas _mapArea = null!;
	private Button _closeButton = null!;
	private readonly Dictionary<string, Button> _nodeButtons = new();

	public System.Action? CloseRequested { get; set; }

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += RefreshAll;
		SetPanelVisible(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshAll;
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		if (_mapArea != null)
		{
			RefreshAll();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshAll();
		}
	}

	public void RefreshAll()
	{
		if (_mapArea == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("worldmap.title");
		_closeButton.Text = LocaleText.T("ui.close");

		World? world = _player?.GetParent() as World;
		if (world == null)
		{
			foreach (Button button in _nodeButtons.Values)
			{
				button.Visible = false;
			}
			_hintLabel.Text = string.Empty;
			LayoutNodes();
			return;
		}

		_litById.Clear();
		foreach (World.WorldMapNode node in world.GetWorldMapNodes())
		{
			_litById[node.Id] = node.Visited || node.IsCurrent;
			if (!_nodeButtons.TryGetValue(node.Id, out Button? button))
			{
				continue;
			}

			button.Visible = true;
			string label = LocaleText.T(node.NameKey);
			if (node.IsCurrent)
			{
				label += "\n" + LocaleText.T("worldmap.here");
			}
			else if (!node.Visited)
			{
				label += "\n" + LocaleText.T("worldmap.locked");
			}
			button.Text = label;
			ApplyNodeStyle(button, node);
		}

		_hintLabel.Text = world.ActiveMapId != "city"
			? LocaleText.T("worldmap.city_only")
			: LocaleText.T("worldmap.hint");

		LayoutNodes();
	}

	// Buttons live directly on the canvas, so they must be re-centered on their node
	// point whenever the canvas resizes (or on refresh). Position = point*size - size/2.
	private void LayoutNodes()
	{
		if (_mapArea == null)
		{
			return;
		}

		Vector2 areaSize = _mapArea.Size;
		var pointById = new Dictionary<string, Vector2>();
		var nodePoints = new List<(Vector2 P, bool Lit)>();
		foreach ((string id, Vector2 normalized) in NodeLayout)
		{
			Vector2 point = normalized * areaSize;
			pointById[id] = point;
			nodePoints.Add((point, _litById.GetValueOrDefault(id)));
			if (_nodeButtons.TryGetValue(id, out Button? button))
			{
				button.Position = point - button.Size * 0.5f;
			}
		}

		var edges = new List<(Vector2 A, Vector2 B, bool Lit)>();
		foreach ((string a, string b) in NodeEdges)
		{
			if (pointById.TryGetValue(a, out Vector2 pa) && pointById.TryGetValue(b, out Vector2 pb))
			{
				bool lit = _litById.GetValueOrDefault(a) && _litById.GetValueOrDefault(b);
				edges.Add((pa, pb, lit));
			}
		}

		_mapArea.Connectors = edges;
		_mapArea.NodePoints = nodePoints;
		_mapArea.QueueRedraw();
	}

	private void BuildPanel()
	{
		Name = "WorldMapPanel";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.10f;
		AnchorTop = 0.10f;
		AnchorRight = 0.90f;
		AnchorBottom = 0.90f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.97f),
			BorderColor = new Color(0.58f, 0.72f, 0.95f, 0.96f),
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

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		root.AddChild(header);

		_titleLabel = MakeLabel(24, new Color(0.82f, 0.90f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_closeButton = new Button
		{
			Text = LocaleText.T("ui.close"),
			CustomMinimumSize = new Vector2(96.0f, 36.0f),
		};
		_closeButton.Pressed += () => CloseRequested?.Invoke();
		header.AddChild(_closeButton);

		_mapArea = new WorldMapCanvas
		{
			Name = "WorldMapCanvas",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		_mapArea.Resized += LayoutNodes;
		root.AddChild(_mapArea);

		foreach ((string id, Vector2 _) in NodeLayout)
		{
			// View-only nodes: the world map only shows layout / visited state; actual
			// travel is chosen at the main-city portal. Non-interactive on purpose.
			var button = new Button
			{
				CustomMinimumSize = new Vector2(150.0f, 46.0f),
				Size = new Vector2(150.0f, 46.0f),
				AutowrapMode = TextServer.AutowrapMode.Off,
				MouseFilter = MouseFilterEnum.Ignore,
				FocusMode = FocusModeEnum.None,
			};
			button.AddThemeFontSizeOverride("font_size", 13);
			_mapArea.AddChild(button);
			_nodeButtons[id] = button;
		}

		_hintLabel = MakeLabel(14, new Color(0.74f, 0.83f, 0.90f));
		_hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
		root.AddChild(_hintLabel);
	}

	private static void ApplyNodeStyle(Button button, World.WorldMapNode node)
	{
		Color background;
		Color border;
		Color fontColor;
		int borderWidth;

		if (node.IsCurrent)
		{
			background = new Color(0.13f, 0.19f, 0.16f, 0.98f);
			border = new Color(1.0f, 0.86f, 0.38f, 1.0f);
			fontColor = new Color(1.0f, 0.96f, 0.78f);
			borderWidth = 3;
		}
		else if (node.Visited)
		{
			background = new Color(0.10f, 0.14f, 0.20f, 0.98f);
			border = new Color(0.58f, 0.78f, 0.98f, 0.94f);
			fontColor = new Color(0.94f, 0.98f, 1.0f);
			borderWidth = 2;
		}
		else
		{
			background = new Color(0.07f, 0.08f, 0.095f, 0.96f);
			border = new Color(0.30f, 0.34f, 0.40f, 0.80f);
			fontColor = new Color(0.56f, 0.60f, 0.66f);
			borderWidth = 1;
		}

		button.AddThemeStyleboxOverride("normal", MakeNodeStyle(background, border, borderWidth));
		button.AddThemeStyleboxOverride("hover", MakeNodeStyle(background.Lightened(0.08f), border, borderWidth));
		button.AddThemeStyleboxOverride("pressed", MakeNodeStyle(background.Lightened(0.14f), border, borderWidth));
		button.AddThemeStyleboxOverride("disabled", MakeNodeStyle(background.Darkened(0.2f), border, borderWidth));
		button.AddThemeColorOverride("font_color", fontColor);
		button.AddThemeColorOverride("font_disabled_color", fontColor);
	}

	private static StyleBoxFlat MakeNodeStyle(Color background, Color border, int borderWidth)
	{
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
		};
		style.SetBorderWidthAll(borderWidth);
		style.SetCornerRadiusAll(6);
		return style;
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}
}

// Draws the dashed connector chain between consecutive world-map nodes. The clickable
// node markers are Button children layered on top (positioned by WorldMapPanel), so this
// control only paints the background path — same pattern as MinimapView / FormationDiscControl.
public partial class WorldMapCanvas : Control
{
	private const float DashLength = 10.0f;
	private const float GapLength = 8.0f;

	public IReadOnlyList<(Vector2 A, Vector2 B, bool Lit)> Connectors { get; set; } = System.Array.Empty<(Vector2, Vector2, bool)>();
	public IReadOnlyList<(Vector2 P, bool Lit)> NodePoints { get; set; } = System.Array.Empty<(Vector2, bool)>();

	public override void _Draw()
	{
		// Explored routes glow; unexplored ones stay dim gray so the map visibly
		// lights up as the player discovers each map.
		var litColor = new Color(0.70f, 0.86f, 1.0f, 0.95f);
		var dimColor = new Color(0.34f, 0.38f, 0.45f, 0.55f);
		foreach ((Vector2 a, Vector2 b, bool lit) in Connectors)
		{
			DrawDashedSegment(a, b, lit ? litColor : dimColor);
		}

		// Station discs under each node: brighter for explored stops.
		foreach ((Vector2 point, bool lit) in NodePoints)
		{
			DrawCircle(point, lit ? 7.0f : 5.0f, lit
				? new Color(0.55f, 0.72f, 0.95f, 0.85f)
				: new Color(0.20f, 0.24f, 0.30f, 0.55f));
		}
	}

	private void DrawDashedSegment(Vector2 from, Vector2 to, Color color)
	{
		Vector2 delta = to - from;
		float length = delta.Length();
		if (length <= 0.01f)
		{
			return;
		}

		Vector2 direction = delta / length;
		float traveled = 0.0f;
		while (traveled < length)
		{
			float dashEnd = Mathf.Min(traveled + DashLength, length);
			DrawLine(from + direction * traveled, from + direction * dashEnd, color, 2.0f, true);
			traveled = dashEnd + GapLength;
		}
	}
}
