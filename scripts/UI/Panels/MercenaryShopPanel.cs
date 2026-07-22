using Godot;
using System.Collections.Generic;

public partial class MercenaryShopPanel : PanelContainer
{
	private PlayerController? _player;
	private VBoxContainer _offerList = null!;
	private Label _goldLabel = null!;
	private Label _titleLabel = null!;
	private Label _refreshLabel = null!;
	private Button _refreshButton = null!;
	private float _refreshUiRemaining;

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
		if (!Visible)
		{
			return;
		}

		_refreshUiRemaining -= (float)delta;
		if (_refreshUiRemaining <= 0.0f)
		{
			_refreshUiRemaining = 1.0f;
			RefreshAll();
		}
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
		_refreshLabel.Text = _player?.GetMercenaryRefreshCountdownText() ?? LocaleText.F("mercenary.refresh.countdown", 0, 0, 0);
		_refreshButton.Text = LocaleText.F("mercenary.button.refresh", _player?.MercenaryManualRefreshCost ?? 50);
		_refreshButton.Disabled = _player == null || _player.Gold < _player.MercenaryManualRefreshCost;
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
		// Match the pet shop window size (a large centred 80% panel).
		AnchorLeft = 0.10f;
		AnchorTop = 0.10f;
		AnchorRight = 0.90f;
		AnchorBottom = 0.90f;
		OffsetLeft = 0.0f;
		OffsetTop = 0.0f;
		OffsetRight = 0.0f;
		OffsetBottom = 0.0f;
		CustomMinimumSize = Vector2.Zero;

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

		var refreshRow = new HBoxContainer();
		refreshRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(refreshRow);

		_refreshLabel = MakeLabel(15, new Color(0.82f, 0.92f, 1.0f));
		_refreshLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		refreshRow.AddChild(_refreshLabel);

		_refreshButton = new Button
		{
			CustomMinimumSize = new Vector2(150.0f, 38.0f),
		};
		_refreshButton.Pressed += () =>
		{
			if (_player != null && _player.TryRefreshMercenaryOffersManually())
			{
				RefreshAll();
			}
		};
		refreshRow.AddChild(_refreshButton);

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

		// Same rich info card as the pet shop: a preview companion fed into a
		// CompanionInfoCard, freed when the row leaves the tree.
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

	// Build a throwaway companion that mirrors the mercenary offer, so the
	// CompanionInfoCard shows the exact same rich layout as the pet shop.
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
