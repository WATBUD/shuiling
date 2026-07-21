using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	private void CreateCaptureAmmoHud()
	{
		var layer = new CanvasLayer
		{
			Name = "CaptureAmmoLayer",
			Layer = 24,
		};
		AddChild(layer);

		_captureAmmoPanel = new PanelContainer
		{
			Name = "CaptureAmmoHud",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 1.0f,
			AnchorRight = 1.0f,
			AnchorTop = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = -224.0f,
			OffsetRight = -28.0f,
			OffsetTop = -112.0f,
			OffsetBottom = -28.0f,
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.041f, 0.050f, 0.78f),
			BorderColor = new Color(0.62f, 0.72f, 0.82f, 0.58f),
		};
		panelStyle.SetBorderWidthAll(1);
		panelStyle.SetCornerRadiusAll(4);
		_captureAmmoPanel.AddThemeStyleboxOverride("panel", panelStyle);
		layer.AddChild(_captureAmmoPanel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		_captureAmmoPanel.AddChild(margin);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 4);
		margin.AddChild(rows);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		rows.AddChild(titleRow);

		var netLabel = MakeHudLabel(LocaleText.T("hud.net"), 13, new Color(0.68f, 0.80f, 0.90f));
		titleRow.AddChild(netLabel);

		_captureAmmoCaptionLabel = MakeHudLabel(LocaleText.T("hud.capture_net_key"), 13, new Color(0.86f, 0.92f, 0.96f));
		_captureAmmoCaptionLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_captureAmmoCaptionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(_captureAmmoCaptionLabel);

		_captureAmmoCountLabel = MakeHudLabel($"6 / {CaptureNetCapacity}", 31, new Color(1.0f, 1.0f, 1.0f));
		_captureAmmoCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
		rows.AddChild(_captureAmmoCountLabel);

		_captureAmmoRechargeBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 7.0f),
		};
		rows.AddChild(_captureAmmoRechargeBar);
		UpdateCaptureAmmoHud();
	}

	private void CreateDamageFlashHud()
	{
		var layer = new CanvasLayer
		{
			Name = "DamageFlashLayer",
			Layer = 80,
		};
		AddChild(layer);

		_damageFlashOverlay = new ColorRect
		{
			Name = "DamageFlashOverlay",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			AnchorTop = 0.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 0.0f,
			OffsetRight = 0.0f,
			OffsetTop = 0.0f,
			OffsetBottom = 0.0f,
			Color = new Color(1.0f, 0.06f, 0.02f, 0.0f),
			Visible = false,
		};
		layer.AddChild(_damageFlashOverlay);
	}

	private void CreateInteractionPromptHud()
	{
		var layer = new CanvasLayer
		{
			Name = "InteractionPromptLayer",
			Layer = 26,
		};
		AddChild(layer);

		_interactionPromptLabel = new Label
		{
			Name = "InteractionPrompt",
			Text = string.Empty,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.78f,
			AnchorBottom = 0.78f,
			OffsetLeft = -240.0f,
			OffsetRight = 240.0f,
			OffsetTop = -22.0f,
			OffsetBottom = 22.0f,
			Visible = false,
		};
		_interactionPromptLabel.AddThemeFontSizeOverride("font_size", 18);
		_interactionPromptLabel.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.88f));
		_interactionPromptLabel.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.03f, 0.025f, 0.94f));
		_interactionPromptLabel.AddThemeConstantOverride("outline_size", 6);
		layer.AddChild(_interactionPromptLabel);
	}

	private void CreateSystemLogPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "SystemLogLayer",
			Layer = 28,
		};
		AddChild(layer);

		_systemLogPanel = new SystemLogPanel();
		layer.AddChild(_systemLogPanel);
	}

	public void PostSystemMessage(string message, Color color, GameMessageChannel channel = GameMessageChannel.System)
	{
		if (_systemLogPanel == null)
		{
			return;
		}

		_systemLogPanel.AddMessage(message, color, channel);
	}

	private void CreateBossHud()
	{
		var layer = new CanvasLayer
		{
			Name = "BossHudLayer",
			// Gameplay HUD stays below inventory/party/settings layers so the
			// overview never covers controls while the player adjusts its options.
			Layer = 29,
		};
		AddChild(layer);

		_bossWorldStatusPanel = new PanelContainer
		{
			Name = "BossWorldStatusPanel",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -360.0f,
			OffsetRight = 360.0f,
			OffsetTop = 10.0f,
			OffsetBottom = 88.0f,
			Visible = false,
		};
		var worldStatusStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.030f, 0.040f, 0.92f),
			BorderColor = new Color(0.72f, 0.56f, 0.22f, 0.82f),
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.64f),
			ShadowSize = 6,
		};
		worldStatusStyle.SetBorderWidthAll(1);
		worldStatusStyle.SetCornerRadiusAll(6);
		_bossWorldStatusPanel.AddThemeStyleboxOverride("panel", worldStatusStyle);
		layer.AddChild(_bossWorldStatusPanel);

		var statusMargin = new MarginContainer();
		statusMargin.AddThemeConstantOverride("margin_left", 10);
		statusMargin.AddThemeConstantOverride("margin_right", 10);
		statusMargin.AddThemeConstantOverride("margin_top", 6);
		statusMargin.AddThemeConstantOverride("margin_bottom", 7);
		_bossWorldStatusPanel.AddChild(statusMargin);
		var statusRows = new VBoxContainer();
		statusRows.AddThemeConstantOverride("separation", 4);
		statusMargin.AddChild(statusRows);
		_bossWorldStatusTitleLabel = MakeHudLabel(LocaleText.T("boss.overview.title"), 15, new Color(1.0f, 0.84f, 0.46f));
		_bossWorldStatusTitleLabel.Name = "BossWorldStatusTitle";
		_bossWorldStatusTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		statusRows.AddChild(_bossWorldStatusTitleLabel);
		_bossWorldStatusEntryLabel = MakeHudLabel(string.Empty, 17, new Color(1.0f, 0.78f, 0.28f));
		_bossWorldStatusEntryLabel.Name = "BossWorldStatusEntry";
		_bossWorldStatusEntryLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_bossWorldStatusEntryLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		statusRows.AddChild(_bossWorldStatusEntryLabel);

		_bossHudPanel = new PanelContainer
		{
			Name = "BossHealthHud",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -330.0f,
			OffsetRight = 330.0f,
			OffsetTop = 110.0f,
			OffsetBottom = 184.0f,
			Visible = false,
		};
		var healthPanelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.018f, 0.022f, 0.90f),
			BorderColor = new Color(0.92f, 0.65f, 0.18f, 0.86f),
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.72f),
			ShadowSize = 8,
		};
		healthPanelStyle.SetBorderWidthAll(2);
		healthPanelStyle.SetCornerRadiusAll(6);
		_bossHudPanel.AddThemeStyleboxOverride("panel", healthPanelStyle);
		layer.AddChild(_bossHudPanel);

		var healthMargin = new MarginContainer();
		healthMargin.AddThemeConstantOverride("margin_left", 16);
		healthMargin.AddThemeConstantOverride("margin_right", 16);
		healthMargin.AddThemeConstantOverride("margin_top", 7);
		healthMargin.AddThemeConstantOverride("margin_bottom", 8);
		_bossHudPanel.AddChild(healthMargin);

		var healthRows = new VBoxContainer();
		healthRows.AddThemeConstantOverride("separation", 4);
		healthMargin.AddChild(healthRows);

		var bossTitleRow = new HBoxContainer();
		bossTitleRow.AddThemeConstantOverride("separation", 12);
		healthRows.AddChild(bossTitleRow);
		_bossHudNameLabel = MakeHudLabel(string.Empty, 19, new Color(1.0f, 0.78f, 0.30f));
		_bossHudNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_bossHudNameLabel.AddThemeColorOverride("font_outline_color", new Color(0.03f, 0.01f, 0.01f, 0.96f));
		_bossHudNameLabel.AddThemeConstantOverride("outline_size", 5);
		bossTitleRow.AddChild(_bossHudNameLabel);
		_bossHudHealthLabel = MakeHudLabel(string.Empty, 15, new Color(1.0f, 0.92f, 0.82f));
		_bossHudHealthLabel.HorizontalAlignment = HorizontalAlignment.Right;
		bossTitleRow.AddChild(_bossHudHealthLabel);

		_bossHudHealthBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 18.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		var barBackground = new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.075f, 0.075f, 0.96f),
			BorderColor = new Color(0.48f, 0.32f, 0.18f, 0.92f),
		};
		barBackground.SetBorderWidthAll(1);
		barBackground.SetCornerRadiusAll(4);
		var barFill = new StyleBoxFlat
		{
			BgColor = new Color(0.86f, 0.11f, 0.07f, 0.98f),
			BorderColor = new Color(1.0f, 0.50f, 0.12f, 0.95f),
		};
		barFill.SetBorderWidthAll(1);
		barFill.SetCornerRadiusAll(4);
		_bossHudHealthBar.AddThemeStyleboxOverride("background", barBackground);
		_bossHudHealthBar.AddThemeStyleboxOverride("fill", barFill);
		healthRows.AddChild(_bossHudHealthBar);

		_bossAnnouncementPanel = new PanelContainer
		{
			Name = "BossAnnouncement",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -365.0f,
			OffsetRight = 365.0f,
			OffsetTop = 194.0f,
			OffsetBottom = 304.0f,
			PivotOffset = new Vector2(365.0f, 55.0f),
			Visible = false,
		};
		var announcementStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.018f, 0.025f, 0.91f),
			BorderColor = new Color(1.0f, 0.60f, 0.12f, 0.94f),
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.78f),
			ShadowSize = 12,
		};
		announcementStyle.SetBorderWidthAll(2);
		announcementStyle.SetCornerRadiusAll(7);
		_bossAnnouncementPanel.AddThemeStyleboxOverride("panel", announcementStyle);
		layer.AddChild(_bossAnnouncementPanel);

		var announcementMargin = new MarginContainer();
		announcementMargin.AddThemeConstantOverride("margin_left", 24);
		announcementMargin.AddThemeConstantOverride("margin_right", 24);
		announcementMargin.AddThemeConstantOverride("margin_top", 12);
		announcementMargin.AddThemeConstantOverride("margin_bottom", 12);
		_bossAnnouncementPanel.AddChild(announcementMargin);
		var announcementRows = new VBoxContainer();
		announcementRows.AddThemeConstantOverride("separation", 4);
		announcementMargin.AddChild(announcementRows);

		_bossAnnouncementTitleLabel = MakeHudLabel(string.Empty, 27, new Color(1.0f, 0.70f, 0.18f));
		_bossAnnouncementTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_bossAnnouncementTitleLabel.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.01f, 0.01f, 0.98f));
		_bossAnnouncementTitleLabel.AddThemeConstantOverride("outline_size", 7);
		announcementRows.AddChild(_bossAnnouncementTitleLabel);
		_bossAnnouncementBodyLabel = MakeHudLabel(string.Empty, 17, new Color(1.0f, 0.92f, 0.80f));
		_bossAnnouncementBodyLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_bossAnnouncementBodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		announcementRows.AddChild(_bossAnnouncementBodyLabel);
	}

	public void RefreshBossWorldStatus(bool showOverview)
	{
		if (_bossWorldStatusPanel == null || GetParent() is not World world)
		{
			return;
		}

		IReadOnlyList<World.BossStatusSnapshot> snapshots = world.GetBossStatusSnapshots();
		var livingBosses = new List<World.BossStatusSnapshot>(snapshots.Count);
		foreach (World.BossStatusSnapshot snapshot in snapshots)
		{
			if (snapshot.IsAlive)
			{
				livingBosses.Add(snapshot);
			}
		}

		var signatureParts = new List<string>(livingBosses.Count + 1) { LocaleText.CurrentLanguage };
		foreach (World.BossStatusSnapshot snapshot in livingBosses)
		{
			signatureParts.Add(snapshot.MapId);
		}
		string signature = string.Join("|", signatureParts);
		if (!showOverview && signature == _bossWorldStatusSignature)
		{
			return;
		}

		_bossWorldStatusSignature = signature;
		StartBossWorldStatusSequence(livingBosses);
	}

	private void StartBossWorldStatusSequence(IReadOnlyList<World.BossStatusSnapshot> livingBosses)
	{
		_bossWorldStatusTween?.Kill();
		if (!_bossAnnouncementsEnabled || livingBosses.Count == 0)
		{
			_bossWorldStatusPanel.Visible = false;
			return;
		}

		_bossWorldStatusPanel.Visible = true;
		_bossWorldStatusPanel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_bossWorldStatusTween = CreateTween();
		for (int index = 0; index < livingBosses.Count; index++)
		{
			World.BossStatusSnapshot snapshot = livingBosses[index];
			int sequenceNumber = index + 1;
			_bossWorldStatusTween.TweenCallback(Callable.From(() =>
			{
				_bossWorldStatusTitleLabel.Text = LocaleText.F("boss.overview.sequence", sequenceNumber, livingBosses.Count);
				_bossWorldStatusEntryLabel.Text = LocaleText.F("boss.overview.entry", snapshot.MapName, snapshot.BossName);
				_bossWorldStatusPanel.Visible = true;
			}));
			_bossWorldStatusTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
			_bossWorldStatusTween.TweenProperty(
				_bossWorldStatusPanel,
				"modulate",
				new Color(1.0f, 1.0f, 1.0f, _bossAnnouncementOpacity),
				0.35f);
			_bossWorldStatusTween.TweenInterval(4.15f);
			_bossWorldStatusTween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
			_bossWorldStatusTween.TweenProperty(
				_bossWorldStatusPanel,
				"modulate",
				new Color(1.0f, 1.0f, 1.0f, 0.0f),
				0.50f);
		}
		_bossWorldStatusTween.TweenCallback(Callable.From(() => _bossWorldStatusPanel.Visible = false));
	}

	public void SetBossAnnouncementsEnabled(bool enabled)
	{
		_bossAnnouncementsEnabled = enabled;
		if (!enabled)
		{
			_bossWorldStatusTween?.Kill();
			if (_bossWorldStatusPanel != null)
			{
				_bossWorldStatusPanel.Visible = false;
			}
		}
		if (!enabled && _bossAnnouncementPanel != null)
		{
			_bossAnnouncementTween?.Kill();
			_bossAnnouncementPanel.Visible = false;
		}
		if (enabled)
		{
			RefreshBossWorldStatus(true);
		}
	}

	public void SetBossAnnouncementOpacity(float opacity)
	{
		_bossAnnouncementOpacity = Mathf.Clamp(opacity, 0.20f, 1.0f);
		if (_bossWorldStatusPanel != null)
		{
			_bossWorldStatusPanel.Modulate = new Color(1.0f, 1.0f, 1.0f, _bossAnnouncementOpacity);
		}
		if (_bossAnnouncementPanel != null && _bossAnnouncementPanel.Visible)
		{
			_bossAnnouncementPanel.Modulate = new Color(1.0f, 1.0f, 1.0f, _bossAnnouncementOpacity);
		}
	}

	private void UpdateBossWorldStatusHud(float step)
	{
		_bossWorldStatusRefreshRemaining = Mathf.Max(_bossWorldStatusRefreshRemaining - step, 0.0f);
		if (_bossWorldStatusRefreshRemaining > 0.0f)
		{
			return;
		}

		_bossWorldStatusRefreshRemaining = 0.50f;
		RefreshBossWorldStatus(false);
	}

	public void SetActiveBoss(SimpleActor? boss)
	{
		SimpleActor? validBoss = boss != null && IsInstanceValid(boss) && !boss.IsDefeated ? boss : null;
		if (_activeBoss != validBoss)
		{
			_bossHudCombatVisibleRemaining = 0.0f;
		}
		_activeBoss = validBoss;
		UpdateBossHud(0.0f);
	}

	public void NotifyBossCombat(SimpleActor boss)
	{
		if (!IsInstanceValid(boss) || !boss.IsBoss || boss.IsDefeated || !boss.Visible)
		{
			return;
		}

		_activeBoss = boss;
		_bossHudCombatVisibleRemaining = 8.0f;
		UpdateBossHud(0.0f);
	}

	public void ShowBossAppeared(SimpleActor boss, string mapName)
	{
		if (boss.Visible)
		{
			SetActiveBoss(boss);
		}
		RefreshBossWorldStatus(false);
		PostSystemMessage(
			LocaleText.F("boss.announcement.location", boss.LocalizedDisplayName, mapName),
			new Color(1.0f, 0.76f, 0.24f),
			GameMessageChannel.System);
		ShowBossMessage(
			LocaleText.T("boss.announcement.appeared"),
			LocaleText.F("boss.announcement.location", boss.LocalizedDisplayName, mapName),
			new Color(1.0f, 0.66f, 0.14f));
	}

	public void ShowBossEnraged(SimpleActor boss)
	{
		SetActiveBoss(boss);
		PostSystemMessage(
			LocaleText.F("boss.announcement.enraged_body", boss.LocalizedDisplayName),
			new Color(1.0f, 0.30f, 0.14f),
			GameMessageChannel.Combat);
		ShowBossMessage(
			LocaleText.T("boss.announcement.enraged"),
			LocaleText.F("boss.announcement.enraged_body", boss.LocalizedDisplayName),
			new Color(1.0f, 0.20f, 0.08f));
	}

	public void ShowBossDefeated(SimpleActor boss)
	{
		if (_activeBoss == boss)
		{
			SetActiveBoss(null);
		}
		RefreshBossWorldStatus(false);
		PostSystemMessage(
			LocaleText.F("boss.announcement.defeated_body", boss.LocalizedDisplayName),
			new Color(1.0f, 0.82f, 0.28f),
			GameMessageChannel.Combat);
		ShowBossMessage(
			LocaleText.T("boss.announcement.defeated"),
			LocaleText.F("boss.announcement.defeated_body", boss.LocalizedDisplayName),
			new Color(1.0f, 0.84f, 0.30f));
	}

	private void ShowBossMessage(string title, string body, Color accentColor)
	{
		if (_bossAnnouncementPanel == null || !_bossAnnouncementsEnabled)
		{
			return;
		}

		_bossAnnouncementTween?.Kill();
		_bossAnnouncementTitleLabel.Text = title;
		_bossAnnouncementTitleLabel.AddThemeColorOverride("font_color", accentColor);
		_bossAnnouncementBodyLabel.Text = body;
		_bossAnnouncementPanel.Visible = true;
		_bossAnnouncementPanel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_bossAnnouncementPanel.Scale = new Vector2(0.94f, 0.94f);
		_bossAnnouncementTween = CreateTween();
		_bossAnnouncementTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		_bossAnnouncementTween.TweenProperty(_bossAnnouncementPanel, "modulate", new Color(1.0f, 1.0f, 1.0f, _bossAnnouncementOpacity), 0.22f);
		_bossAnnouncementTween.Parallel().TweenProperty(_bossAnnouncementPanel, "scale", Vector2.One, 0.22f);
		_bossAnnouncementTween.TweenInterval(3.2f);
		_bossAnnouncementTween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		_bossAnnouncementTween.TweenProperty(_bossAnnouncementPanel, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), 0.48f);
		_bossAnnouncementTween.TweenCallback(Callable.From(() => _bossAnnouncementPanel.Visible = false));
	}

	private void UpdateBossHud(float step)
	{
		if (_bossHudPanel == null)
		{
			return;
		}

		if (_activeBoss == null || !IsInstanceValid(_activeBoss) || _activeBoss.IsDefeated || !_activeBoss.Visible)
		{
			_activeBoss = null;
			_bossHudCombatVisibleRemaining = 0.0f;
			_bossHudPanel.Visible = false;
			return;
		}

		_bossHudCombatVisibleRemaining = Mathf.Max(_bossHudCombatVisibleRemaining - Mathf.Max(step, 0.0f), 0.0f);
		if (_bossHudCombatVisibleRemaining <= 0.0f)
		{
			_bossHudPanel.Visible = false;
			return;
		}

		_bossHudPanel.Visible = true;
		_bossHudNameLabel.Text = LocaleText.F("boss.hud.name", _activeBoss.Level, _activeBoss.LocalizedDisplayName);
		_bossHudNameLabel.AddThemeColorOverride(
			"font_color",
			_activeBoss.IsBossEnraged ? new Color(1.0f, 0.24f, 0.10f) : new Color(1.0f, 0.78f, 0.30f));
		int maxHealth = Mathf.Max(_activeBoss.EffectiveMaxHealth, 1);
		int currentHealth = Mathf.Clamp(_activeBoss.CurrentHealth, 0, maxHealth);
		_bossHudHealthLabel.Text = LocaleText.F("boss.hud.health", currentHealth, maxHealth);
		_bossHudHealthBar.Value = currentHealth / (double)maxHealth * 100.0;
	}

	private void UpdateCaptureAmmoHud()
	{
		if (_captureAmmoCountLabel == null || _captureAmmoRechargeBar == null)
		{
			return;
		}

		int capacity = Mathf.Max(CaptureNetCapacity, 1);
		float rechargeSeconds = Mathf.Max(CaptureNetRechargeSeconds, 0.05f);
		_captureAmmoCountLabel.Text = $"{_captureNetCharges} / {capacity}";

		if (_captureNetCharges <= 0)
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.34f, 0.28f));
		}
		else if (_captureNetCharges <= 2)
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.38f));
		}
		else
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f));
		}

		bool full = _captureNetCharges >= capacity;
		float rechargeProgress = full
			? 100.0f
			: Mathf.Clamp((1.0f - _captureNetRechargeRemaining / rechargeSeconds) * 100.0f, 0.0f, 100.0f);
		_captureAmmoRechargeBar.Value = rechargeProgress;
		_captureAmmoCaptionLabel.Text = full
			? LocaleText.T("hud.capture_net_key")
			: LocaleText.F("hud.recharge_seconds", Mathf.CeilToInt(_captureNetRechargeRemaining));
	}

	private static Label MakeHudLabel(string text, int fontSize, Color color)
	{
		var label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private void CreateTargetInfoPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "TargetInfoLayer",
			Layer = 20,
		};

		AddChild(layer);
		_targetInfoPanel = new TargetInfoPanel();
		layer.AddChild(_targetInfoPanel);
	}

	private void CreateMinimapPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "MinimapLayer",
			Layer = 22,
		};

		AddChild(layer);
		_minimapPanel = new MinimapPanel();
		layer.AddChild(_minimapPanel);
		_minimapPanel.Bind(this);
	}

	public void RefreshMinimap()
	{
		if (_minimapPanel != null && IsInstanceValid(_minimapPanel))
		{
			_minimapPanel.RefreshMapInfo();
		}
	}

	private void UpdateMouseModeForPanels()
	{
		Input.MouseMode = _pauseMenuPanel.Visible || _partyPanel.Visible || _inventoryPanel.Visible || _formationPanel.Visible || _merchantShopPanel.Visible || _mercenaryShopPanel.Visible || _warehousePanel.Visible || _settingsPanel.Visible || (_npcQuestDialog != null && _npcQuestDialog.Visible) || (_mapTravelDialog != null && _mapTravelDialog.Visible)
			? Input.MouseModeEnum.Visible
			: _cameraMode == CameraViewMode.GodView
				? Input.MouseModeEnum.Visible
				: Input.MouseModeEnum.Captured;
	}

	private void UpdateTargetInfoPanel()
	{
		bool foundActor = _cameraMode == CameraViewMode.GodView
			? TryRaycastActor(GetViewport().GetMousePosition(), out SimpleActor actor)
			: TryRaycastActor(out actor);
		if (foundActor)
		{
			_targetInfoPanel.ShowActor(actor);
			return;
		}

		_targetInfoPanel.HideActor();
	}

}
