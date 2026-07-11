using Godot;
using System.Collections.Generic;

public partial class MercenaryShopPanel : PanelContainer
{
	private PlayerController? _player;
	private VBoxContainer _offerList = null!;
	private Label _goldLabel = null!;
	private Label _titleLabel = null!;

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
		if (_offerList != null)
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
		if (_offerList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("mercenary.shop.title");
		_goldLabel.Text = LocaleText.F("inventory.gold", _player?.Gold ?? 0);
		ClearChildren(_offerList);

		if (_player == null)
		{
			return;
		}

		foreach (PlayerController.ContractCompanionOffer offer in _player.ContractCompanionOffers)
		{
			AddOfferRow(offer);
		}
	}

	private void BuildPanel()
	{
		Name = "MercenaryShopPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -380.0f;
		OffsetRight = 380.0f;
		OffsetTop = -285.0f;
		OffsetBottom = 285.0f;
		CustomMinimumSize = new Vector2(760.0f, 570.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.97f),
			BorderColor = new Color(0.72f, 0.58f, 0.34f, 0.96f),
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

		_titleLabel = MakeLabel(24, new Color(1.0f, 0.92f, 0.72f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_goldLabel = MakeLabel(18, new Color(1.0f, 0.84f, 0.34f));
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_goldLabel.CustomMinimumSize = new Vector2(160.0f, 0.0f);
		header.AddChild(_goldLabel);

		var hint = MakeLabel(15, new Color(0.72f, 0.82f, 0.88f));
		hint.Text = LocaleText.T("mercenary.shop.hint");
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(hint);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(scroll);

		_offerList = new VBoxContainer();
		_offerList.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_offerList);

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	private void AddOfferRow(PlayerController.ContractCompanionOffer offer)
	{
		var row = new PanelContainer();
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = new Color(0.32f, 0.38f, 0.45f, 0.72f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);
		_offerList.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		row.AddChild(margin);

		var content = new HBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		var info = new VBoxContainer();
		info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		info.AddThemeConstantOverride("separation", 5);
		content.AddChild(info);

		var name = MakeLabel(19, new Color(0.96f, 0.98f, 1.0f));
		name.Text = LocaleText.F("mercenary.offer.name", LocaleText.T(offer.NameKey), offer.Level, LocaleText.T(offer.RoleNameKey));
		info.AddChild(name);

		var summary = MakeLabel(14, new Color(0.72f, 0.82f, 0.90f));
		summary.Text = LocaleText.T(offer.SummaryKey);
		summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		info.AddChild(summary);

		var stats = MakeLabel(14, new Color(0.82f, 0.92f, 0.78f));
		stats.Text = LocaleText.F("mercenary.offer.stats", offer.Attack, offer.Defense, offer.MaxHealth);
		info.AddChild(stats);

		var hireButton = new Button
		{
			Text = LocaleText.F("mercenary.button.hire", offer.Cost),
			CustomMinimumSize = new Vector2(150.0f, 54.0f),
			Disabled = _player == null || _player.Gold < offer.Cost,
		};
		hireButton.Pressed += () =>
		{
			if (_player != null && _player.TryHireContractCompanion(offer))
			{
				RefreshAll();
			}
		};
		content.AddChild(hireButton);
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
