using Godot;
using System.Collections.Generic;

public partial class MerchantShopPanel : PanelContainer
{
	private PlayerController? _player;
	private PlayerController.MerchantShopKind _shopKind;
	private VBoxContainer _buyList = null!;
	private VBoxContainer _sellList = null!;
	private Label _titleLabel = null!;
	private Label _goldLabel = null!;

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
		ClearChildren(_buyList);
		ClearChildren(_sellList);

		if (_player == null)
		{
			return;
		}

		foreach (PlayerController.ShopTradeEntry entry in _player.GetShopBuyEntries(_shopKind))
		{
			AddTradeRow(_buyList, entry, true);
		}

		foreach (PlayerController.ShopTradeEntry entry in _player.GetShopSellEntries(_shopKind))
		{
			AddTradeRow(_sellList, entry, false);
		}

		if (_sellList.GetChildCount() == 0)
		{
			var empty = MakeLabel(14, new Color(0.70f, 0.76f, 0.82f));
			empty.Text = LocaleText.T("shop.sell.empty");
			_sellList.AddChild(empty);
		}
	}

	private void BuildPanel()
	{
		Name = "MerchantShopPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -455.0f;
		OffsetRight = 455.0f;
		OffsetTop = -315.0f;
		OffsetBottom = 315.0f;
		CustomMinimumSize = new Vector2(910.0f, 630.0f);

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

		var columns = new HBoxContainer();
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.AddThemeConstantOverride("separation", 14);
		root.AddChild(columns);

		_buyList = CreateTradeColumn(columns, "shop.buy");
		_sellList = CreateTradeColumn(columns, "shop.sell");

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	private VBoxContainer CreateTradeColumn(HBoxContainer parent, string titleKey)
	{
		var section = new VBoxContainer
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
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		section.AddChild(scroll);

		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(list);
		return list;
	}

	private void AddTradeRow(VBoxContainer parent, PlayerController.ShopTradeEntry entry, bool isBuy)
	{
		var row = new PanelContainer();
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.075f, 0.085f, 0.10f, 0.94f),
			BorderColor = new Color(0.28f, 0.34f, 0.42f, 0.70f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);
		parent.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		row.AddChild(margin);

		var line = new HBoxContainer();
		line.AddThemeConstantOverride("separation", 10);
		margin.AddChild(line);

		var info = new VBoxContainer();
		info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		info.AddThemeConstantOverride("separation", 4);
		line.AddChild(info);

		var name = MakeLabel(16, new Color(0.96f, 0.98f, 1.0f));
		name.Text = entry.DisplayName;
		info.AddChild(name);

		var detail = MakeLabel(13, new Color(0.72f, 0.80f, 0.88f));
		detail.Text = entry.Detail;
		detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		info.AddChild(detail);

		var action = new Button
		{
			Text = isBuy ? LocaleText.F("shop.button.buy", entry.Price) : LocaleText.F("shop.button.sell", entry.Price),
			CustomMinimumSize = new Vector2(105.0f, 48.0f),
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
