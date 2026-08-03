using Godot;
using System.Collections.Generic;

// 扭蛋抽獎面板：顯示每抽花費與金幣，兩顆按鈕（抽 1／抽 10），
// 下方列出最近一次抽到的獎勵。抽獎邏輯位於 PlayerController.Gacha.cs。
public partial class GachaPanel : PanelContainer
{
	private PlayerController? _player;
	private Label _titleLabel = null!;
	private Label _goldLabel = null!;
	private Label _costLabel = null!;
	private Button _drawOneButton = null!;
	private Button _drawTenButton = null!;
	private VBoxContainer _list = null!;
	private FloatingTooltip _tooltip = null!;
	private readonly List<string> _lastResults = new();

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

		_titleLabel.Text = LocaleText.T("gacha.title");
		_tooltip?.HideTooltip();
		ClearChildren(_list);

		if (_player == null)
		{
			_costLabel.Text = string.Empty;
			_goldLabel.Text = string.Empty;
			_drawOneButton.Disabled = true;
			_drawTenButton.Disabled = true;
			return;
		}

		_costLabel.Text = LocaleText.F("gacha.cost", _player.GachaDrawCost);
		_goldLabel.Text = $"{_player.Gold}";
		_drawOneButton.Text = LocaleText.T("gacha.draw_one");
		_drawTenButton.Text = LocaleText.T("gacha.draw_ten");
		bool canDraw = _player.Gold >= _player.GachaDrawCost;
		_drawOneButton.Disabled = !canDraw;
		_drawTenButton.Disabled = !canDraw;

		if (_lastResults.Count == 0)
		{
			var hint = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			hint.Text = LocaleText.T("gacha.pull_hint");
			_list.AddChild(hint);
			return;
		}

		foreach (string id in _lastResults)
		{
			AddResultRow(id);
		}
	}

	private void AddResultRow(string itemId)
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

		int tier = Mathf.Max(
			BuildCatalog.GetEquipmentStars(itemId),
			Mathf.Max(BuildCatalog.GetSkillCoreStars(itemId), MonsterLootCatalog.GetEnhanceCrystalTier(itemId)));

		var rarityLabel = MakeLabel(17, new Color(1.0f, 0.86f, 0.42f));
		rarityLabel.Text = $"★{tier}";
		content.AddChild(rarityLabel);

		var nameLabel = MakeLabel(17, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		int crystalTier = MonsterLootCatalog.GetEnhanceCrystalTier(itemId);
		if (crystalTier > 0)
		{
			nameLabel.Text = LocaleText.T(MonsterLootCatalog.GetNameKey(itemId));
		}
		else
		{
			nameLabel.Text = LocaleText.T(BuildCatalog.GetItemNameKey(itemId)) + BuildCatalog.GetStarSuffix(itemId);
		}
		content.AddChild(nameLabel);
	}

	private void BuildPanel()
	{
		Name = "GachaPanel";
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

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		root.AddChild(header);

		_titleLabel = MakeLabel(24, new Color(0.82f, 0.90f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_goldLabel = MakeLabel(20, new Color(1.0f, 0.92f, 0.62f));
		_goldLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		header.AddChild(_goldLabel);

		_costLabel = MakeLabel(16, new Color(0.80f, 0.88f, 0.94f));
		root.AddChild(_costLabel);

		var buttonBar = new HBoxContainer();
		buttonBar.AddThemeConstantOverride("separation", 8);
		root.AddChild(buttonBar);

		_drawOneButton = new Button
		{
			Text = LocaleText.T("gacha.draw_one"),
			CustomMinimumSize = new Vector2(0.0f, 48.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_drawOneButton.Pressed += () =>
		{
			if (_player != null)
			{
				_lastResults.Clear();
				_lastResults.AddRange(_player.DrawGacha(1));
				RefreshAll();
			}
		};
		buttonBar.AddChild(_drawOneButton);

		_drawTenButton = new Button
		{
			Text = LocaleText.T("gacha.draw_ten"),
			CustomMinimumSize = new Vector2(0.0f, 48.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_drawTenButton.Pressed += () =>
		{
			if (_player != null)
			{
				_lastResults.Clear();
				_lastResults.AddRange(_player.DrawGacha(10));
				RefreshAll();
			}
		};
		buttonBar.AddChild(_drawTenButton);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(scroll);

		_list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_list);

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);

		_tooltip = new FloatingTooltip
		{
			Name = "GachaTooltip",
			MaxWidth = 460.0f,
			MinWidth = 240.0f,
			MaxWidthRatio = 0.55f,
			MaxHeightRatio = 0.58f,
			MinBodyHeight = 64.0f,
			ZIndex = 100,
		};
		AddChild(_tooltip);
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
