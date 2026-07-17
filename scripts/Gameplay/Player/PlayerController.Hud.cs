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

	public void PostSystemMessage(string message, Color color)
	{
		if (_systemLogPanel == null)
		{
			return;
		}

		_systemLogPanel.AddMessage(message, color);
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

	private void UpdateMouseModeForPanels()
	{
		Input.MouseMode = _pauseMenuPanel.Visible || _partyPanel.Visible || _inventoryPanel.Visible || _formationPanel.Visible || _merchantShopPanel.Visible || _mercenaryShopPanel.Visible || _settingsPanel.Visible || (_npcQuestDialog != null && _npcQuestDialog.Visible) || (_mapTravelDialog != null && _mapTravelDialog.Visible)
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
