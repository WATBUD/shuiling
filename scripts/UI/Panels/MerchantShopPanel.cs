using Godot;
using System.Collections.Generic;

public partial class MerchantShopPanel : PanelContainer
{
	private PlayerController? _player;
	private PlayerController.MerchantShopKind _shopKind;
	private VBoxContainer _buySection = null!;
	private VBoxContainer _sellSection = null!;
	private VBoxContainer _buyList = null!;
	private VBoxContainer _sellList = null!;
	private Label _titleLabel = null!;
	private Label _goldLabel = null!;
	private Label _noticeLabel = null!;
	private PanelContainer _tooltipPanel = null!;
	private Label _tooltipTitleLabel = null!;
	private Label _tooltipBodyLabel = null!;
	private ScrollContainer _tooltipBodyScroll = null!;

	public System.Action? CloseRequested { get; set; }

	public override void _Process(double delta)
	{
		if (_tooltipPanel != null && _tooltipPanel.Visible)
		{
			PositionTooltip();
		}
	}

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
		RefreshAll();
	}

	public void Open(PlayerController.MerchantShopKind shopKind)
	{
		_shopKind = shopKind;
		SetPanelVisible(true);
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
		if (_buyList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T(_shopKind switch
		{
			PlayerController.MerchantShopKind.Blacksmith => "shop.blacksmith.trade",
			PlayerController.MerchantShopKind.PetShop => "shop.pet.trade",
			_ => "shop.item.trade",
		});
		_goldLabel.Text = LocaleText.F("inventory.gold", _player?.Gold ?? 0);
		_noticeLabel.Visible = _shopKind == PlayerController.MerchantShopKind.PetShop;
		_noticeLabel.Text = _noticeLabel.Visible ? LocaleText.T("shop.pet.notice") : string.Empty;
		_buySection.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_sellSection.Visible = false;
		ClearChildren(_buyList);
		ClearChildren(_sellList);
		HideTradeTooltip();

		if (_player == null)
		{
			return;
		}

		foreach (PlayerController.ShopTradeEntry entry in _player.GetShopBuyEntries(_shopKind))
		{
			AddTradeRow(_buyList, entry, true);
		}

	}

	private void BuildPanel()
	{
		Name = "MerchantShopPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -560.0f;
		OffsetRight = 560.0f;
		OffsetTop = -340.0f;
		OffsetBottom = 340.0f;
		CustomMinimumSize = new Vector2(1120.0f, 680.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.042f, 0.048f, 0.058f, 0.97f),
			BorderColor = new Color(0.55f, 0.68f, 0.78f, 0.95f),
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

		_titleLabel = MakeLabel(24, new Color(1.0f, 0.94f, 0.78f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_goldLabel = MakeLabel(18, new Color(1.0f, 0.84f, 0.34f));
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_goldLabel.CustomMinimumSize = new Vector2(170.0f, 0.0f);
		header.AddChild(_goldLabel);

		_noticeLabel = MakeLabel(15, new Color(0.76f, 0.88f, 1.0f));
		_noticeLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_noticeLabel.CustomMinimumSize = new Vector2(0.0f, 34.0f);
		root.AddChild(_noticeLabel);

		var columns = new HBoxContainer();
		columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.AddThemeConstantOverride("separation", 14);
		root.AddChild(columns);

		_buyList = CreateTradeColumn(columns, "shop.buy", out _buySection);
		_sellList = CreateTradeColumn(columns, "shop.sell", out _sellSection);

		BuildTooltipPanel();

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	private VBoxContainer CreateTradeColumn(HBoxContainer parent, string titleKey, out VBoxContainer section)
	{
		section = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		section.AddThemeConstantOverride("separation", 8);
		parent.AddChild(section);

		var title = MakeLabel(18, new Color(0.82f, 0.92f, 1.0f));
		title.Text = LocaleText.T(titleKey);
		section.AddChild(title);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		section.AddChild(scroll);

		var list = new VBoxContainer();
		list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(list);
		return list;
	}

	private void AddTradeRow(VBoxContainer parent, PlayerController.ShopTradeEntry entry, bool isBuy)
	{
		var row = new PanelContainer();
		bool isPetBuyRow = _shopKind == PlayerController.MerchantShopKind.PetShop && isBuy;
		row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.CustomMinimumSize = new Vector2(0.0f, 64.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.075f, 0.085f, 0.10f, 0.94f),
			BorderColor = new Color(0.28f, 0.34f, 0.42f, 0.70f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);
		row.MouseFilter = MouseFilterEnum.Stop;
		row.MouseEntered += () => ShowTradeTooltip(entry);
		row.MouseExited += HideTradeTooltip;
		parent.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", isPetBuyRow ? 14 : 10);
		margin.AddThemeConstantOverride("margin_right", isPetBuyRow ? 14 : 10);
		margin.AddThemeConstantOverride("margin_top", isPetBuyRow ? 12 : 8);
		margin.AddThemeConstantOverride("margin_bottom", isPetBuyRow ? 12 : 8);
		row.AddChild(margin);

		var line = new HBoxContainer();
		line.AddThemeConstantOverride("separation", isPetBuyRow ? 16 : 10);
		margin.AddChild(line);

		var info = new VBoxContainer();
		info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		info.AddThemeConstantOverride("separation", 4);
		line.AddChild(info);

		var name = MakeLabel(isPetBuyRow ? 18 : 16, new Color(0.96f, 0.98f, 1.0f));
		name.Text = entry.DisplayName;
		name.VerticalAlignment = VerticalAlignment.Center;
		name.SizeFlagsVertical = SizeFlags.ExpandFill;
		info.AddChild(name);

		var action = new Button
		{
			Text = isBuy ? LocaleText.F("shop.button.buy", entry.Price) : LocaleText.F("shop.button.sell", entry.Price),
			CustomMinimumSize = new Vector2(isPetBuyRow ? 132.0f : 118.0f, 48.0f),
			Disabled = _player == null || (isBuy && _player.Gold < entry.Price),
		};
		action.Pressed += () =>
		{
			if (_player == null)
			{
				return;
			}

			bool changed = isBuy
				? _player.TryBuyShopItem(_shopKind, entry.ItemId, entry.Price)
				: _player.TrySellShopItem(_shopKind, entry.ItemId, entry.Price);
			if (changed)
			{
				RefreshAll();
			}
		};
		line.AddChild(action);
	}

	private void BuildTooltipPanel()
	{
		_tooltipPanel = new PanelContainer
		{
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(420.0f, 120.0f),
		};

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.040f, 0.050f, 0.98f),
			BorderColor = new Color(0.78f, 0.68f, 0.42f, 0.96f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		_tooltipPanel.AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		_tooltipPanel.AddChild(margin);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 6);
		margin.AddChild(rows);

		_tooltipTitleLabel = MakeLabel(16, new Color(1.0f, 0.91f, 0.55f));
		_tooltipTitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		rows.AddChild(_tooltipTitleLabel);

		_tooltipBodyScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		rows.AddChild(_tooltipBodyScroll);

		_tooltipBodyLabel = MakeLabel(13, new Color(0.86f, 0.91f, 0.96f));
		_tooltipBodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_tooltipBodyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_tooltipBodyScroll.AddChild(_tooltipBodyLabel);

		AddChild(_tooltipPanel);
	}

	private void ShowTradeTooltip(PlayerController.ShopTradeEntry entry)
	{
		if (string.IsNullOrWhiteSpace(entry.Detail))
		{
			return;
		}

		_tooltipTitleLabel.Text = entry.DisplayName;
		_tooltipBodyLabel.Text = entry.Detail;
		ApplyTooltipHeightLimit();
		_tooltipPanel.Visible = true;
		PositionTooltip();
	}

	private void HideTradeTooltip()
	{
		if (_tooltipPanel != null)
		{
			_tooltipPanel.Visible = false;
		}
	}

	private void PositionTooltip()
	{
		ApplyTooltipHeightLimit();
		Vector2 panelSize = _tooltipPanel.Size;
		if (panelSize.X <= 1.0f || panelSize.Y <= 1.0f)
		{
			panelSize = _tooltipPanel.GetCombinedMinimumSize();
		}

		Vector2 available = Size;
		if (available.X <= 1.0f || available.Y <= 1.0f)
		{
			available = CustomMinimumSize;
		}

		Vector2 desired = new(
			(available.X - panelSize.X) * 0.5f,
			(available.Y - panelSize.Y) * 0.5f
		);
		desired.X = Mathf.Clamp(desired.X, 18.0f, Mathf.Max(18.0f, available.X - panelSize.X - 18.0f));
		desired.Y = Mathf.Clamp(desired.Y, 18.0f, Mathf.Max(18.0f, available.Y - panelSize.Y - 18.0f));

		_tooltipPanel.Position = desired;
	}

	private void ApplyTooltipHeightLimit()
	{
		Vector2 available = Size;
		if (available.X <= 1.0f || available.Y <= 1.0f)
		{
			available = CustomMinimumSize;
		}

		float maxPanelHeight = Mathf.Max(180.0f, available.Y * 0.50f);
		float maxBodyHeight = Mathf.Max(80.0f, maxPanelHeight - 74.0f);
		float desiredBodyHeight = Mathf.Clamp(_tooltipBodyLabel.GetCombinedMinimumSize().Y + 8.0f, 56.0f, maxBodyHeight);
		_tooltipBodyScroll.CustomMinimumSize = new Vector2(0.0f, desiredBodyHeight);
		_tooltipBodyScroll.Size = new Vector2(_tooltipBodyScroll.Size.X, desiredBodyHeight);

		_tooltipPanel.CustomMinimumSize = new Vector2(420.0f, 0.0f);
		_tooltipPanel.Size = new Vector2(420.0f, Mathf.Min(_tooltipPanel.GetCombinedMinimumSize().Y, maxPanelHeight));
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
