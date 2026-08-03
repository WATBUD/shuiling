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
	private Label _merchantLabel = null!;
	private ProgressBar _expBar = null!;
	private OptionButton _tierOption = null!;
	private HSlider _countSlider = null!;
	private Label _countLabel = null!;
	private Button _drawButton = null!;
	private Button _ratesButton = null!;
	private Window _ratesWindow = null!;
	private VBoxContainer _ratesList = null!;
	private GridContainer _list = null!;
	private const float ResultTileWidth = 98.0f;
	private const int ResultTileGap = 8;
	private FloatingTooltip _tooltip = null!;
	private AudioStreamPlayer _sfxPlayer = null!;
	private ColorRect _flash = null!;
	private readonly List<string> _lastResults = new();
	private int _selectedTier = 1;
	private int _lastDrawTier = 1;

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
			_ratesWindow?.Hide();
		}
		if (visible)
		{
			RefreshAll();
		}
	}

	public void RefreshAll()
	{
		RebuildAll(false);
	}

	private void RebuildAll(bool animate)
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
			_merchantLabel.Text = string.Empty;
			_expBar.Visible = false;
			_tierOption.Clear();
			_tierOption.Disabled = true;
			_drawButton.Disabled = true;
			return;
		}

		int unlockedMax = _player.GachaUnlockedMaxTier;
		_selectedTier = Mathf.Clamp(_selectedTier, 1, unlockedMax);
		int cost = _player.GachaDrawCost(_selectedTier);

		_merchantLabel.Text = LocaleText.F("gacha.merchant_level", _player.GachaMerchantLevel, unlockedMax);
		if (_player.GachaMerchantMaxed)
		{
			_expBar.Visible = false;
		}
		else
		{
			_expBar.Visible = true;
			_expBar.MaxValue = Mathf.Max(_player.GachaMerchantExpToNext, 1);
			_expBar.Value = _player.GachaMerchantExp;
		}

		PopulateTierOptions(unlockedMax);

		_goldLabel.Text = $"{_player.Gold}";
		UpdateDrawControls();
		RefreshRatesTable();

		if (_lastResults.Count == 0)
		{
			_list.Columns = 1;
			var hint = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			hint.Text = LocaleText.T("gacha.pull_hint");
			_list.AddChild(hint);
			return;
		}

		if (_list.GetParent() is Control scroll)
		{
			UpdateResultColumns(scroll);
		}

		foreach (string id in _lastResults)
		{
			AddResultTile(id, animate);
		}
	}

	private void ShowResultInfo(string itemId)
	{
		if (_tooltip == null)
		{
			return;
		}

		string title = InventoryPanel.BuildItemTooltipTitle(itemId);
		string body = InventoryPanel.BuildItemTooltipBody(itemId, string.Empty);
		_tooltip.ShowTooltip(title, body, this);
	}

	// One result rendered as a compact thumbnail tile: item icon, star tier, and
	// draw odds. Hovering shows the full stats tooltip (name, #id, values) so a big
	// batch stays browsable without a name on every row.
	private void AddResultTile(string itemId, bool animate)
	{
		int rarity = RewardTier(itemId);
		Color rarityColor = RarityColor(rarity);
		var tile = new PanelContainer { CustomMinimumSize = new Vector2(ResultTileWidth, 120.0f) };
		// Hover shows the item's full stats so the player needn't open the bag.
		string hoverId = itemId;
		tile.MouseEntered += () => ShowResultInfo(hoverId);
		tile.MouseExited += () => _tooltip?.HideTooltip();
		if (animate)
		{
			// Start hidden; the reveal tween fades/shines it in.
			tile.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		}

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = new Color(rarityColor.R, rarityColor.G, rarityColor.B, rarity >= 5 ? 0.95f : 0.6f),
		};
		style.SetBorderWidthAll(rarity >= 7 ? 2 : 1);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(6);
		tile.AddThemeStyleboxOverride("panel", style);
		_list.AddChild(tile);

		var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 2);
		tile.AddChild(box);

		TextureRect icon = ItemIconLibrary.CreateRect(itemId, 58.0f);
		icon.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		box.AddChild(icon);

		var starLabel = MakeLabel(14, rarityColor);
		starLabel.Text = $"★{rarity}";
		starLabel.HorizontalAlignment = HorizontalAlignment.Center;
		starLabel.MouseFilter = MouseFilterEnum.Ignore;
		box.AddChild(starLabel);

		// Draw odds for this tier so players see how rare the pull was.
		var rateLabel = MakeLabel(11, new Color(0.72f, 0.80f, 0.88f));
		rateLabel.Text = LocaleText.F("gacha.win_rate", GachaConfig.TierProbability(rarity, _lastDrawTier) * 100.0f);
		rateLabel.HorizontalAlignment = HorizontalAlignment.Center;
		rateLabel.MouseFilter = MouseFilterEnum.Ignore;
		box.AddChild(rateLabel);
	}

	// Fit as many thumbnail columns as the results viewport allows.
	private void UpdateResultColumns(Control viewport)
	{
		if (_list == null)
		{
			return;
		}

		float available = Mathf.Max(viewport.Size.X, ResultTileWidth);
		_list.Columns = Mathf.Max(1, Mathf.FloorToInt((available + ResultTileGap) / (ResultTileWidth + ResultTileGap)));
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

		// Odds table opens in its own window; its button sits beside the title.
		_ratesButton = new Button
		{
			Text = LocaleText.T("gacha.rates_button"),
			CustomMinimumSize = new Vector2(0.0f, 34.0f),
		};
		_ratesButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_ratesButton.Pressed += OpenRatesWindow;
		header.AddChild(_ratesButton);

		_goldLabel = MakeLabel(20, new Color(1.0f, 0.92f, 0.62f));
		_goldLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		header.AddChild(_goldLabel);

		// Merchant level + EXP progress toward the next draw-cap unlock.
		_merchantLabel = MakeLabel(15, new Color(0.72f, 1.0f, 0.82f));
		_merchantLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(_merchantLabel);

		_expBar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0.0f, 12.0f),
			ShowPercentage = false,
		};
		root.AddChild(_expBar);

		// One row: draw-cap dropdown (left) + a draggable draw-count slider + the
		// chosen count (right). Picking a higher cap raises the reachable tier and
		// the per-draw cost; the slider chooses how many draws to fire at once.
		var controlRow = new HBoxContainer();
		controlRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(controlRow);

		_tierOption = new OptionButton
		{
			CustomMinimumSize = new Vector2(170.0f, 40.0f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		_tierOption.ItemSelected += index => OnTierSelected(index);
		controlRow.AddChild(_tierOption);

		_countSlider = new HSlider
		{
			MinValue = 1,
			MaxValue = 100,
			Step = 1,
			Value = 1,
			CustomMinimumSize = new Vector2(0.0f, 40.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		_countSlider.ValueChanged += _ => UpdateDrawControls();
		controlRow.AddChild(_countSlider);

		_countLabel = MakeLabel(18, new Color(1.0f, 0.94f, 0.72f));
		_countLabel.CustomMinimumSize = new Vector2(64.0f, 0.0f);
		_countLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_countLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		controlRow.AddChild(_countLabel);

		_costLabel = MakeLabel(16, new Color(0.80f, 0.88f, 0.94f));
		root.AddChild(_costLabel);

		_drawButton = new Button
		{
			CustomMinimumSize = new Vector2(0.0f, 48.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_drawButton.Pressed += () => OnDraw((int)_countSlider.Value);
		root.AddChild(_drawButton);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(scroll);

		// Results render as a responsive grid of thumbnail tiles so a big batch
		// stays browsable; columns recompute as the viewport resizes.
		_list = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("h_separation", ResultTileGap);
		_list.AddThemeConstantOverride("v_separation", ResultTileGap);
		scroll.AddChild(_list);
		scroll.Resized += () => UpdateResultColumns(scroll);

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

		_sfxPlayer = new AudioStreamPlayer
		{
			Name = "GachaSfx",
			Bus = AudioSettings.SfxBus,
			ProcessMode = ProcessModeEnum.Always,
		};
		AddChild(_sfxPlayer);

		// Full-panel rarity flash overlay; transparent until a draw reveals.
		_flash = new ColorRect
		{
			Name = "GachaFlash",
			Color = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 90,
		};
		_flash.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_flash);

		// Odds table lives in its own (embedded) window opened from the header.
		_ratesWindow = new Window
		{
			Name = "GachaRatesWindow",
			Title = LocaleText.T("gacha.rates_window"),
			Visible = false,
			Size = new Vector2I(260, 380),
			Unresizable = true,
		};
		_ratesWindow.CloseRequested += () => _ratesWindow.Hide();
		var winMargin = new MarginContainer();
		winMargin.SetAnchorsPreset(LayoutPreset.FullRect);
		winMargin.AddThemeConstantOverride("margin_left", 14);
		winMargin.AddThemeConstantOverride("margin_right", 14);
		winMargin.AddThemeConstantOverride("margin_top", 12);
		winMargin.AddThemeConstantOverride("margin_bottom", 12);
		_ratesWindow.AddChild(winMargin);
		_ratesList = new VBoxContainer();
		_ratesList.AddThemeConstantOverride("separation", 3);
		winMargin.AddChild(_ratesList);
		AddChild(_ratesWindow);
	}

	private void OpenRatesWindow()
	{
		if (_ratesWindow == null)
		{
			return;
		}

		RefreshRatesTable();
		_ratesWindow.PopupCentered(new Vector2I(260, 380));
	}

	// Fills the odds window with each drawable tier (cap..1) and its cascade
	// probability for the currently selected cap, so the odds shown match what a
	// draw will actually do. Cheap enough to refresh whenever the cap changes.
	private void RefreshRatesTable()
	{
		if (_ratesList == null)
		{
			return;
		}

		ClearChildren(_ratesList);
		if (_player == null)
		{
			return;
		}

		int cap = Mathf.Clamp(_selectedTier, 1, _player.GachaUnlockedMaxTier);
		var title = MakeLabel(15, new Color(0.82f, 0.90f, 1.0f));
		title.Text = LocaleText.F("gacha.rates_title", cap);
		_ratesList.AddChild(title);

		for (int tier = cap; tier >= 1; tier--)
		{
			var row = MakeLabel(15, RarityColor(tier));
			row.Text = LocaleText.F("gacha.rates_row", tier, GachaConfig.TierProbability(tier, cap) * 100.0f);
			_ratesList.AddChild(row);
		}
	}

	// Updates the count readout, per-draw cost, and draw button from the slider
	// without rebuilding the results list (called live on every slider tick).
	private void UpdateDrawControls()
	{
		if (_player == null)
		{
			return;
		}

		int count = (int)_countSlider.Value;
		int cost = _player.GachaDrawCost(_selectedTier);
		_countLabel.Text = LocaleText.F("gacha.count", count);
		_costLabel.Text = LocaleText.F("gacha.cost", cost);
		_drawButton.Text = LocaleText.F("gacha.draw", count, cost * count);
		_drawButton.Disabled = _player.Gold < cost;
	}

	// Rebuild the dropdown to list every unlocked cap (1..unlockedMax) with its
	// per-draw cost, then reselect the current tier. Select() does not re-emit
	// ItemSelected, so this is safe to call from RebuildAll without recursing.
	private void PopulateTierOptions(int unlockedMax)
	{
		_tierOption.Disabled = false;
		_tierOption.Clear();
		int selectedIndex = 0;
		for (int tier = 1; tier <= unlockedMax; tier++)
		{
			_tierOption.AddItem(LocaleText.F("gacha.tier_option", tier, GachaConfig.DrawCost(tier)), tier);
			if (tier == _selectedTier)
			{
				selectedIndex = _tierOption.ItemCount - 1;
			}
		}

		_tierOption.Select(selectedIndex);
	}

	private void OnTierSelected(long index)
	{
		if (_player == null || index < 0)
		{
			return;
		}

		_selectedTier = Mathf.Clamp(_tierOption.GetItemId((int)index), 1, _player.GachaUnlockedMaxTier);
		RefreshAll();
	}

	private void OnDraw(int count)
	{
		if (_player == null)
		{
			return;
		}

		_lastDrawTier = Mathf.Clamp(_selectedTier, 1, _player.GachaUnlockedMaxTier);
		List<string> results = _player.DrawGacha(count, _lastDrawTier);
		if (results.Count == 0)
		{
			RefreshAll();
			return;
		}

		_lastResults.Clear();
		_lastResults.AddRange(results);
		int best = 1;
		foreach (string id in results)
		{
			best = Mathf.Max(best, RewardTier(id));
		}

		RebuildAll(true); // rows start transparent
		PlayRoll();
		BeginReveal(best);
	}

	// Staggered shine-in of each result row, then a rarity flash + reveal chime
	// once the suspense roll has played out.
	private void BeginReveal(int bestTier)
	{
		const float rollDelay = 0.45f;
		const float step = 0.07f;
		int index = 0;
		foreach (Node child in _list.GetChildren())
		{
			if (child is not Control row)
			{
				continue;
			}

			Tween tween = CreateTween();
			tween.TweenInterval(rollDelay + index * step);
			tween.TweenProperty(row, "modulate", new Color(1.7f, 1.7f, 1.7f, 1.0f), 0.12f);
			tween.TweenProperty(row, "modulate", Colors.White, 0.20f);
			index++;
		}

		SceneTreeTimer timer = GetTree().CreateTimer(rollDelay);
		timer.Timeout += () =>
		{
			FlashScreen(bestTier);
			PlayReveal(bestTier);
		};
	}

	private void FlashScreen(int tier)
	{
		Color color = RarityColor(tier);
		float peak = tier >= 8 ? 0.7f : (tier >= 5 ? 0.45f : 0.28f);
		float duration = tier >= 8 ? 0.6f : 0.4f;
		_flash.Color = new Color(color.R, color.G, color.B, peak);
		Tween tween = CreateTween();
		tween.TweenProperty(_flash, "color:a", 0.0f, duration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
	}

	private static int RewardTier(string itemId)
	{
		return Mathf.Max(
			BuildCatalog.GetEquipmentStars(itemId),
			Mathf.Max(BuildCatalog.GetSkillCoreStars(itemId), MonsterLootCatalog.GetEnhanceCrystalTier(itemId)));
	}

	private static Color RarityColor(int tier)
	{
		return tier switch
		{
			>= 10 => new Color(1.0f, 0.55f, 0.2f),
			>= 9 => new Color(1.0f, 0.84f, 0.3f),
			>= 7 => new Color(0.78f, 0.5f, 1.0f),
			>= 5 => new Color(0.45f, 0.7f, 1.0f),
			>= 3 => new Color(0.5f, 0.95f, 0.6f),
			_ => new Color(0.85f, 0.88f, 0.94f),
		};
	}

	// Rising tick sequence — the "rolling" suspense before the reveal.
	private void PlayRoll()
	{
		const int mixRate = 22050;
		const float duration = 0.45f;
		int sampleCount = Mathf.RoundToInt(mixRate * duration);
		byte[] data = new byte[sampleCount * 2];
		const int ticks = 9;
		for (int i = 0; i < sampleCount; i++)
		{
			float t = i / (float)mixRate;
			float pos = t / duration;
			int tick = Mathf.Clamp((int)(pos * ticks), 0, ticks - 1);
			float local = pos * ticks - tick;
			float freq = Mathf.Lerp(420.0f, 1150.0f, tick / (float)(ticks - 1));
			float env = Mathf.Exp(-local * 9.0f);
			float sample = Mathf.Sin(Mathf.Tau * freq * t) * env * 0.16f;
			WritePcm16(data, i * 2, Mathf.Clamp(sample, -0.9f, 0.9f));
		}

		PlayWav(data, mixRate);
	}

	// Ascending chime whose length/brightness scales with the best rarity drawn.
	private void PlayReveal(int tier)
	{
		const int mixRate = 22050;
		int notes = Mathf.Clamp(2 + tier / 2, 2, 6);
		float duration = 0.35f + notes * 0.12f;
		int sampleCount = Mathf.RoundToInt(mixRate * duration);
		byte[] data = new byte[sampleCount * 2];
		float baseFreq = Mathf.Lerp(392.0f, 659.0f, Mathf.Clamp(tier / 10.0f, 0.0f, 1.0f));
		float[] ratios = { 1.0f, 1.122f, 1.335f, 1.5f, 1.682f, 2.0f };
		for (int i = 0; i < sampleCount; i++)
		{
			float t = i / (float)mixRate;
			float sample = 0.0f;
			for (int k = 0; k < notes; k++)
			{
				float start = k * 0.10f;
				float length = 0.5f + (k == notes - 1 ? 0.4f : 0.0f);
				float lt = t - start;
				if (lt < 0.0f || lt >= length)
				{
					continue;
				}

				float p = lt / length;
				float env = Mathf.Min(p * 16.0f, 1.0f) * Mathf.Exp(-p * (k == notes - 1 ? 1.4f : 2.8f));
				float phase = Mathf.Tau * baseFreq * ratios[k] * t;
				sample += (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2.0f)) * env * 0.16f;
			}

			if (tier >= 8)
			{
				sample += Mathf.Sin(Mathf.Tau * 1760.0f * t) * Mathf.Exp(-t * 2.5f) * 0.05f * Mathf.Sin(Mathf.Tau * 9.0f * t);
			}

			WritePcm16(data, i * 2, Mathf.Clamp(sample, -0.95f, 0.95f));
		}

		PlayWav(data, mixRate);
	}

	private void PlayWav(byte[] data, int mixRate)
	{
		_sfxPlayer.Stop();
		_sfxPlayer.Stream = new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = mixRate,
			Stereo = false,
			Data = data,
		};
		_sfxPlayer.Play();
	}

	private static void WritePcm16(byte[] data, int offset, float sample)
	{
		short value = (short)Mathf.Clamp(Mathf.RoundToInt(sample * 32767.0f), short.MinValue, short.MaxValue);
		data[offset] = (byte)(value & 0xFF);
		data[offset + 1] = (byte)((value >> 8) & 0xFF);
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
