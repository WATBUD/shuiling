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
	private AudioStreamPlayer _sfxPlayer = null!;
	private ColorRect _flash = null!;
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
			AddResultRow(id, animate);
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

	private void AddResultRow(string itemId, bool animate)
	{
		int rarity = RewardTier(itemId);
		Color rarityColor = RarityColor(rarity);
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		// Hover shows the item's full stats so the player needn't open the bag.
		string hoverId = itemId;
		row.MouseEntered += () => ShowResultInfo(hoverId);
		row.MouseExited += () => _tooltip?.HideTooltip();
		if (animate)
		{
			// Start hidden; the reveal tween fades/shines it in.
			row.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		}

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = new Color(rarityColor.R, rarityColor.G, rarityColor.B, rarity >= 5 ? 0.95f : 0.6f),
		};
		style.SetBorderWidthAll(rarity >= 7 ? 2 : 1);
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

		var rarityLabel = MakeLabel(17, rarityColor);
		rarityLabel.Text = $"★{rarity}";
		content.AddChild(rarityLabel);

		var nameLabel = MakeLabel(17, rarity >= 7 ? rarityColor : new Color(0.96f, 0.98f, 1.0f));
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

		// Draw odds for this tier, right-aligned, so players see how rare the pull was.
		var rateLabel = MakeLabel(14, new Color(0.72f, 0.80f, 0.88f));
		rateLabel.Text = LocaleText.F("gacha.win_rate", GachaConfig.TierProbability(rarity) * 100.0f);
		rateLabel.HorizontalAlignment = HorizontalAlignment.Right;
		content.AddChild(rateLabel);
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
		_drawOneButton.Pressed += () => OnDraw(1);
		buttonBar.AddChild(_drawOneButton);

		_drawTenButton = new Button
		{
			Text = LocaleText.T("gacha.draw_ten"),
			CustomMinimumSize = new Vector2(0.0f, 48.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_drawTenButton.Pressed += () => OnDraw(10);
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
	}

	private void OnDraw(int count)
	{
		if (_player == null)
		{
			return;
		}

		List<string> results = _player.DrawGacha(count);
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
