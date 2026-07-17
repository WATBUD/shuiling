using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	private void CreatePartyPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "PartyLayer",
			Layer = 30,
		};

		AddChild(layer);
		_partyPanel = new PartyPanel();
		layer.AddChild(_partyPanel);
		_partyPanel.Bind(this);
	}

	private void CreateInventoryPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "InventoryLayer",
			Layer = 34,
		};

		AddChild(layer);
		_inventoryPanel = new InventoryPanel();
		layer.AddChild(_inventoryPanel);
		_inventoryPanel.Bind(this);
		_inventoryPanel.CloseRequested = () => SetInventoryPanelVisible(false);
	}

	private void CreateFormationPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "FormationLayer",
			Layer = 36,
		};

		AddChild(layer);
		_formationPanel = new FormationPanel();
		layer.AddChild(_formationPanel);
		_formationPanel.Bind(this);
		_formationPanel.CloseRequested = () => SetFormationPanelVisible(false);
	}

	private void CreateMerchantShopPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "MerchantShopLayer",
			Layer = 39,
		};

		AddChild(layer);
		_merchantShopPanel = new MerchantShopPanel();
		layer.AddChild(_merchantShopPanel);
		_merchantShopPanel.Bind(this);
		_merchantShopPanel.CloseRequested = () => SetMerchantShopPanelVisible(false);
	}

	private void CreateMercenaryShopPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "MercenaryShopLayer",
			Layer = 40,
		};

		AddChild(layer);
		_mercenaryShopPanel = new MercenaryShopPanel();
		layer.AddChild(_mercenaryShopPanel);
		_mercenaryShopPanel.Bind(this);
		_mercenaryShopPanel.CloseRequested = () => SetMercenaryShopPanelVisible(false);
	}

	private void CreateSettingsPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "SettingsLayer",
			Layer = 45,
		};

		AddChild(layer);
		_settingsPanel = new SettingsPanel();
		layer.AddChild(_settingsPanel);
		_settingsPanel.Bind(this);
		_settingsPanel.CloseRequested = () => SetSettingsPanelVisible(false);
	}

	private void CreatePauseMenuPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "PauseMenuLayer",
			Layer = 48,
		};

		AddChild(layer);
		_pauseMenuPanel = new PanelContainer
		{
			Name = "PauseMenuPanel",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop,
			CustomMinimumSize = new Vector2(440.0f, 360.0f),
		};
		_pauseMenuPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_pauseMenuPanel.OffsetLeft = -220.0f;
		_pauseMenuPanel.OffsetRight = 220.0f;
		_pauseMenuPanel.OffsetTop = -180.0f;
		_pauseMenuPanel.OffsetBottom = 180.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.042f, 0.055f, 0.97f),
			BorderColor = new Color(0.72f, 0.64f, 0.42f, 0.95f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		_pauseMenuPanel.AddThemeStyleboxOverride("panel", style);
		layer.AddChild(_pauseMenuPanel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 22);
		margin.AddThemeConstantOverride("margin_right", 22);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_bottom", 20);
		_pauseMenuPanel.AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		var title = MakeHudLabel(LocaleText.T("pause.title"), 26, new Color(1.0f, 0.92f, 0.68f));
		title.HorizontalAlignment = HorizontalAlignment.Center;
		root.AddChild(title);

		var hint = MakeHudLabel(LocaleText.T("pause.load_hint"), 14, new Color(0.74f, 0.84f, 0.94f));
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		root.AddChild(hint);

		root.AddChild(CreatePauseButton("pause.resume", () => SetPauseMenuVisible(false)));
		root.AddChild(CreatePauseButton("pause.save", SaveCurrentGame));
		root.AddChild(CreatePauseButton("pause.settings", () =>
		{
			SetPauseMenuVisible(false);
			SetSettingsPanelVisible(true);
		}));
		root.AddChild(CreatePauseButton("pause.main_menu", ReturnToMainMenu));
	}

	private Button CreatePauseButton(string textKey, System.Action onPressed)
	{
		var button = new Button
		{
			Text = LocaleText.T(textKey),
			CustomMinimumSize = new Vector2(0.0f, 46.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		button.Pressed += onPressed;
		return button;
	}

	private void SetPartyPanelVisible(bool visible)
	{
		_partyPanel.SetPanelVisible(visible);
		if (visible)
		{
			SetPauseMenuVisible(false, false);
			_settingsPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetInventoryPanelVisible(bool visible)
	{
		_inventoryPanel.SetPanelVisible(visible);
		if (visible)
		{
			SetPauseMenuVisible(false, false);
			_partyPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetFormationPanelVisible(bool visible)
	{
		_formationPanel.SetPanelVisible(visible);
		if (visible)
		{
			SetPauseMenuVisible(false, false);
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetSettingsPanelVisible(bool visible)
	{
		_settingsPanel.SetPanelVisible(visible);
		if (visible)
		{
			SetPauseMenuVisible(false, false);
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetMerchantShopPanelVisible(bool visible)
	{
		_merchantShopPanel.SetPanelVisible(visible);
		if (visible)
		{
			SetPauseMenuVisible(false, false);
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetMercenaryShopPanelVisible(bool visible)
	{
		_mercenaryShopPanel.SetPanelVisible(visible);
		if (visible)
		{
			SetPauseMenuVisible(false, false);
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		UpdateMouseModeForPanels();
	}

	private void SetPauseMenuVisible(bool visible, bool updateMouseMode = true)
	{
		_pauseMenuPanel.Visible = visible;
		if (visible)
		{
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
			_formationPanel.SetPanelVisible(false);
			_merchantShopPanel.SetPanelVisible(false);
			_mercenaryShopPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
			CloseNpcQuestDialog();
			CloseMapTravelDialog();
		}

		if (updateMouseMode)
		{
			UpdateMouseModeForPanels();
		}
	}

	private void ReturnToMainMenu()
	{
		SaveCurrentGame();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		GetTree().ChangeSceneToFile("res://main_menu.tscn");
	}

}
