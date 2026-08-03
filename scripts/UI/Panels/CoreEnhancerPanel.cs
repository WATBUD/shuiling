using Godot;
using System.Collections.Generic;
using System.Linq;

// 核心強化師面板：兩個分頁。分頁 0 用屬性球＋金幣強化背包內未裝備的技能核心星數；
// 分頁 1 多選收藏中的寵物並分解成屬性球。資料與變動邏輯位於 PlayerController.CoreEnhance.cs。
public partial class CoreEnhancerPanel : PanelContainer
{
	private PlayerController? _player;
	private Label _titleLabel = null!;
	private HBoxContainer _tabBar = null!;
	private Button _tabEnhanceButton = null!;
	private Button _tabDismantleButton = null!;
	private VBoxContainer _list = null!;
	private Button _dismantleButton = null!;
	private FloatingTooltip _tooltip = null!;
	private int _tab;
	private readonly HashSet<SimpleActor> _selectedPets = new();

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

	public override void _Process(double delta)
	{
		if (_tooltip != null && _tooltip.Visible)
		{
			_tooltip.PositionNearMouse(this);
		}
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		if (_list != null)
		{
			RefreshAll();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (!visible)
		{
			_tooltip?.HideTooltip();
		}
		if (visible)
		{
			RefreshAll();
		}
	}

	public void RefreshAll()
	{
		if (_list == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("core_enhancer.title");
		_tabEnhanceButton.Text = LocaleText.T("core_enhancer.tab_enhance");
		_tabDismantleButton.Text = LocaleText.T("core_enhancer.tab_dismantle");
		_tabEnhanceButton.ButtonPressed = _tab == 0;
		_tabDismantleButton.ButtonPressed = _tab == 1;
		_tooltip?.HideTooltip();
		ClearChildren(_list);

		if (_player == null)
		{
			_dismantleButton.Visible = false;
			return;
		}

		if (_tab == 0)
		{
			_dismantleButton.Visible = false;
			RefreshEnhanceTab();
		}
		else
		{
			RefreshDismantleTab();
		}
	}

	private void RefreshEnhanceTab()
	{
		if (_player == null)
		{
			return;
		}

		List<string> ids = _player.GetEnhanceableCoreIds();
		if (ids.Count == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("core_enhancer.empty_cores");
			_list.AddChild(empty);
			return;
		}

		foreach (string id in ids)
		{
			AddEnhanceRow(id);
		}
	}

	private void AddEnhanceRow(string itemId)
	{
		if (_player == null)
		{
			return;
		}

		PlayerController.CoreEnhanceQuote quote = _player.GetCoreEnhanceQuote(itemId);

		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = new Color(0.32f, 0.38f, 0.45f, 0.72f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);
		_list.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		row.AddChild(margin);

		var content = new HBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		content.AddChild(info);

		string coreName = LocaleText.T(BuildCatalog.GetItemNameKey(itemId)) + BuildCatalog.GetStarSuffix(itemId);
		string elementId = BuildCatalog.GetSkillGem(itemId).DamageElementId;
		if (string.IsNullOrEmpty(elementId))
		{
			elementId = "physical";
		}
		string elementName = LocaleText.T($"element.{elementId}");

		var nameLabel = MakeLabel(17, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.Text = $"{coreName}  ·  {elementName}";
		info.AddChild(nameLabel);

		var detailLabel = MakeLabel(14, new Color(0.80f, 0.88f, 0.94f));
		if (quote.IsMax)
		{
			detailLabel.Text = LocaleText.T("core_enhancer.max_row");
		}
		else
		{
			string cost = LocaleText.F(
				"core_enhancer.cost",
				MonsterLootCatalog.GetCoreOrbDisplayName(quote.OrbId),
				quote.OrbCount,
				quote.OrbHave,
				quote.Gold);
			detailLabel.Text = $"{cost}  →  ★{quote.TargetStars}";
		}
		info.AddChild(detailLabel);

		string capturedId = itemId;
		var enhanceButton = new Button
		{
			Text = LocaleText.T("core_enhancer.enhance"),
			CustomMinimumSize = new Vector2(140.0f, 48.0f),
			Disabled = !_player.CanAffordCoreEnhance(quote),
		};
		enhanceButton.Pressed += () =>
		{
			if (_player != null)
			{
				_player.TryEnhanceCore(capturedId);
				RefreshAll();
			}
		};
		content.AddChild(enhanceButton);
	}

	private void RefreshDismantleTab()
	{
		if (_player == null)
		{
			return;
		}

		PruneInvalidSelection();

		int shown = 0;
		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (!IsInstanceValid(actor) || !actor.IsCaptured || actor.IsDefeated)
			{
				continue;
			}

			AddDismantleRow(actor);
			shown++;
		}

		if (shown == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("core_enhancer.empty_pets");
			_list.AddChild(empty);
		}

		_dismantleButton.Visible = true;
		_dismantleButton.Text = LocaleText.T("core_enhancer.dismantle");
		_dismantleButton.Disabled = _selectedPets.Count == 0;
	}

	private void AddDismantleRow(SimpleActor actor)
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = new Color(0.32f, 0.38f, 0.45f, 0.72f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);
		_list.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		row.AddChild(margin);

		var content = new HBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		var checkBox = new CheckBox
		{
			ButtonPressed = _selectedPets.Contains(actor),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		SimpleActor capturedActor = actor;
		checkBox.Toggled += pressed =>
		{
			if (pressed)
			{
				_selectedPets.Add(capturedActor);
			}
			else
			{
				_selectedPets.Remove(capturedActor);
			}
			_dismantleButton.Disabled = _selectedPets.Count == 0;
		};
		content.AddChild(checkBox);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		content.AddChild(info);

		var nameLabel = MakeLabel(17, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.Text = $"{actor.LocalizedDisplayName}  Lv.{actor.Level}";
		info.AddChild(nameLabel);

		string element = BuildCatalog.GetIdentity(actor).ElementAffinityId;
		int tier = CoreEnhanceConfig.TierForLevel(actor.Level);
		string orbName = MonsterLootCatalog.GetCoreOrbDisplayName(MonsterLootCatalog.GetCoreOrbId(element, tier));

		var yieldLabel = MakeLabel(14, new Color(0.80f, 0.88f, 0.94f));
		yieldLabel.Text = orbName;
		info.AddChild(yieldLabel);
	}

	private void PruneInvalidSelection()
	{
		var stale = new List<SimpleActor>();
		foreach (SimpleActor actor in _selectedPets)
		{
			if (!IsInstanceValid(actor) || !actor.IsCaptured || actor.IsDefeated
				|| _player == null || !_player.CapturedCollection.Contains(actor))
			{
				stale.Add(actor);
			}
		}

		foreach (SimpleActor actor in stale)
		{
			_selectedPets.Remove(actor);
		}
	}

	private void BuildPanel()
	{
		Name = "CoreEnhancerPanel";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.12f;
		AnchorTop = 0.10f;
		AnchorRight = 0.88f;
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

		_titleLabel = MakeLabel(24, new Color(0.82f, 0.90f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		root.AddChild(_titleLabel);

		_tabBar = new HBoxContainer();
		_tabBar.AddThemeConstantOverride("separation", 8);
		root.AddChild(_tabBar);

		_tabEnhanceButton = CreateTabButton("core_enhancer.tab_enhance");
		_tabEnhanceButton.Pressed += () =>
		{
			_tab = 0;
			RefreshAll();
		};
		_tabBar.AddChild(_tabEnhanceButton);

		_tabDismantleButton = CreateTabButton("core_enhancer.tab_dismantle");
		_tabDismantleButton.Pressed += () =>
		{
			_tab = 1;
			RefreshAll();
		};
		_tabBar.AddChild(_tabDismantleButton);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(scroll);

		_list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_list);

		_dismantleButton = new Button
		{
			Text = LocaleText.T("core_enhancer.dismantle"),
			CustomMinimumSize = new Vector2(0.0f, 46.0f),
			Visible = false,
		};
		_dismantleButton.Pressed += () =>
		{
			if (_player != null)
			{
				_player.DismantleCompanions(new List<SimpleActor>(_selectedPets));
				_selectedPets.Clear();
				RefreshAll();
			}
		};
		root.AddChild(_dismantleButton);

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);

		_tooltip = new FloatingTooltip
		{
			Name = "CoreEnhancerTooltip",
			MaxWidth = 460.0f,
			MinWidth = 240.0f,
			MaxWidthRatio = 0.55f,
			MaxHeightRatio = 0.58f,
			MinBodyHeight = 64.0f,
			ZIndex = 100,
		};
		AddChild(_tooltip);
	}

	private static Button CreateTabButton(string textKey)
	{
		var button = new Button
		{
			Text = LocaleText.T(textKey),
			ToggleMode = true,
			CustomMinimumSize = new Vector2(120.0f, 32.0f),
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		ApplyTagButtonStyle(button);
		return button;
	}

	private static void ApplyTagButtonStyle(Button button)
	{
		button.AddThemeFontSizeOverride("font_size", 14);
		button.AddThemeStyleboxOverride("normal", MakeTagStyle(new Color(0.075f, 0.086f, 0.105f, 0.92f), new Color(0.24f, 0.30f, 0.38f, 0.78f)));
		button.AddThemeStyleboxOverride("hover", MakeTagStyle(new Color(0.11f, 0.13f, 0.16f, 0.96f), new Color(0.42f, 0.55f, 0.66f, 0.90f)));
		button.AddThemeStyleboxOverride("pressed", MakeTagStyle(new Color(0.18f, 0.27f, 0.33f, 0.98f), new Color(0.70f, 0.90f, 1.0f, 0.98f)));
		button.AddThemeStyleboxOverride("hover_pressed", MakeTagStyle(new Color(0.20f, 0.31f, 0.38f, 1.0f), new Color(0.78f, 0.94f, 1.0f, 1.0f)));
		button.AddThemeColorOverride("font_color", new Color(0.78f, 0.86f, 0.92f));
		button.AddThemeColorOverride("font_pressed_color", new Color(1.0f, 0.96f, 0.78f));
		button.AddThemeColorOverride("font_hover_color", new Color(0.92f, 0.98f, 1.0f));
	}

	private static StyleBoxFlat MakeTagStyle(Color background, Color border)
	{
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = 10.0f;
		style.ContentMarginRight = 10.0f;
		style.ContentMarginTop = 4.0f;
		style.ContentMarginBottom = 4.0f;
		return style;
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}
}
