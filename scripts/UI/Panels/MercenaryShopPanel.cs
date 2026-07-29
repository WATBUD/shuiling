using Godot;
using System.Collections.Generic;

// 夥伴招募所面板：上方分頁切換「傭兵 / 夥伴」，兩個清單皆為每 3 小時累積 1 隻 1 等的候選。
public partial class MercenaryShopPanel : PanelContainer
{
	private PlayerController? _player;
	private VBoxContainer _offerList = null!;
	private Label _goldLabel = null!;
	private Label _titleLabel = null!;
	private Label _hintLabel = null!;
	private Button _mercTabButton = null!;
	private Button _companionTabButton = null!;
	private int _selectedTab; // 0 = 傭兵, 1 = 夥伴

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

	private void SelectTab(int tab)
	{
		_selectedTab = tab;
		RefreshAll();
	}

	public void RefreshAll()
	{
		if (_offerList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("recruit.title");
		_goldLabel.Text = LocaleText.F("inventory.gold", _player?.Gold ?? 0);
		_mercTabButton.Text = LocaleText.T("recruit.tab.mercenary");
		_companionTabButton.Text = LocaleText.T("recruit.tab.companion");
		_mercTabButton.Disabled = _selectedTab == 0;
		_companionTabButton.Disabled = _selectedTab == 1;
		_hintLabel.Text = LocaleText.T("recruit.hint");
		ClearChildren(_offerList);

		if (_player == null)
		{
			return;
		}

		IReadOnlyList<PlayerController.ContractCompanionOffer> offers = _selectedTab == 0
			? _player.ContractCompanionOffers
			: _player.CompanionRecruitOffers;

		if (offers.Count == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("recruit.empty");
			_offerList.AddChild(empty);
			return;
		}

		foreach (PlayerController.ContractCompanionOffer offer in offers)
		{
			AddOfferRow(offer);
		}
	}

	private void BuildPanel()
	{
		Name = "MercenaryShopPanel";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.10f;
		AnchorTop = 0.10f;
		AnchorRight = 0.90f;
		AnchorBottom = 0.90f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.97f),
			BorderColor = new Color(0.64f, 0.86f, 0.72f, 0.96f),
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

		_titleLabel = MakeLabel(24, new Color(0.82f, 1.0f, 0.90f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_goldLabel = MakeLabel(18, new Color(1.0f, 0.84f, 0.34f));
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_goldLabel.CustomMinimumSize = new Vector2(160.0f, 0.0f);
		header.AddChild(_goldLabel);

		// 上方分頁籤：傭兵 / 夥伴
		var tabRow = new HBoxContainer();
		tabRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(tabRow);

		_mercTabButton = new Button { CustomMinimumSize = new Vector2(150.0f, 40.0f) };
		_mercTabButton.Pressed += () => SelectTab(0);
		tabRow.AddChild(_mercTabButton);

		_companionTabButton = new Button { CustomMinimumSize = new Vector2(150.0f, 40.0f) };
		_companionTabButton.Pressed += () => SelectTab(1);
		tabRow.AddChild(_companionTabButton);

		_hintLabel = MakeLabel(15, new Color(0.72f, 0.82f, 0.88f));
		_hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_hintLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		root.AddChild(_hintLabel);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(scroll);

		_offerList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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

		bool isCompanion = offer.Category == "companion";
		var tagLabel = MakeLabel(14, isCompanion ? new Color(0.64f, 1.0f, 0.82f) : new Color(1.0f, 0.86f, 0.46f));
		tagLabel.Text = LocaleText.T(isCompanion ? "recruit.tag.companion" : "recruit.tag.mercenary");
		tagLabel.CustomMinimumSize = new Vector2(64.0f, 0.0f);
		tagLabel.VerticalAlignment = VerticalAlignment.Center;
		content.AddChild(tagLabel);

		SimpleActor previewActor = CreateMercenaryOfferPreview(offer);
		var infoCard = new CompanionInfoCard
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		infoCard.SetActor(previewActor);
		content.AddChild(infoCard);
		row.TreeExiting += () =>
		{
			if (IsInstanceValid(previewActor))
			{
				previewActor.Free();
			}
		};

		var hireButton = new Button
		{
			Text = LocaleText.F("recruit.button.recruit", offer.Cost),
			CustomMinimumSize = new Vector2(150.0f, 54.0f),
			Disabled = _player == null || _player.Gold < offer.Cost,
		};
		hireButton.Pressed += () =>
		{
			if (_player == null)
			{
				return;
			}

			bool ok = isCompanion ? _player.TryRecruitCompanion(offer) : _player.TryHireContractCompanion(offer);
			if (ok)
			{
				RefreshAll();
			}
		};
		content.AddChild(hireButton);
	}

	private static SimpleActor CreateMercenaryOfferPreview(PlayerController.ContractCompanionOffer offer)
	{
		var actor = new SimpleActor
		{
			ActorKind = "npc",
			MoveSpeed = 6.5f,
		};
		actor.ConfigureStats(offer.NameKey, offer.Level, offer.MaxHealth, offer.Attack, offer.Defense, offer.Level * 8, 0);
		actor.ConfigureGrowth("ability.none", Mathf.Max(offer.Level / 2, 1));
		actor.ConfigureCombatProfile(offer.CombatRole, "personality.brave", "ability.none", 5);
		actor.ClearBuildLoadout();
		return actor;
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
