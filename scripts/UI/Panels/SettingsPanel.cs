using Godot;
using System.Collections.Generic;

public partial class SettingsPanel : PanelContainer
{
	private enum SettingsCategory
	{
		Language,
		Video,
		Controls,
		Shortcuts,
	}

	private readonly List<Vector2I> _resolutions = new()
	{
		new Vector2I(1280, 720),
		new Vector2I(1600, 900),
		new Vector2I(1920, 1080),
		new Vector2I(2560, 1440),
		new Vector2I(3840, 2160),
	};

	private PlayerController? _player;
	private OptionButton _languageOption = null!;
	private OptionButton _resolutionOption = null!;
	private OptionButton _windowModeOption = null!;
	private Button _thirdPersonCameraButton = null!;
	private Button _godViewCameraButton = null!;
	private HSlider _damageTextScaleSlider = null!;
	private Label _damageTextScaleValueLabel = null!;
	private HSlider _nameplateScaleSlider = null!;
	private Label _nameplateScaleValueLabel = null!;
	private CheckButton _bossAnnouncementToggle = null!;
	private HSlider _bossAnnouncementOpacitySlider = null!;
	private Label _bossAnnouncementOpacityValueLabel = null!;
	private VBoxContainer _settingsRows = null!;
	private readonly Dictionary<SettingsCategory, Button> _categoryButtons = new();
	private SettingsCategory _selectedCategory = SettingsCategory.Language;

	public System.Action? CloseRequested { get; set; }

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += OnLanguageChanged;
		SetPanelVisible(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= OnLanguageChanged;
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		if (_thirdPersonCameraButton != null)
		{
			SyncFromPlayer();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			SyncFromPlayer();
		}
	}

	private void BuildPanel()
	{
		Name = "SettingsPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -470.0f;
		OffsetRight = 470.0f;
		OffsetTop = -300.0f;
		OffsetBottom = 300.0f;
		CustomMinimumSize = new Vector2(940.0f, 600.0f);

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.041f, 0.050f, 0.96f),
			BorderColor = new Color(0.36f, 0.48f, 0.60f, 0.88f),
		};
		panelStyle.SetBorderWidthAll(2);
		panelStyle.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", panelStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 14);
		margin.AddChild(root);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		root.AddChild(header);

		var title = MakeLabel(LocaleText.T("ui.settings"), 26, new Color(1.0f, 1.0f, 1.0f));
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(title);

		var closeButton = MakeButton(LocaleText.T("ui.close"));
		closeButton.CustomMinimumSize = new Vector2(96.0f, 36.0f);
		closeButton.Pressed += OnClosePressed;
		header.AddChild(closeButton);

		var content = new HBoxContainer();
		content.SizeFlagsVertical = SizeFlags.ExpandFill;
		content.AddThemeConstantOverride("separation", 16);
		root.AddChild(content);

		var nav = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(150.0f, 0.0f),
		};
		nav.AddThemeConstantOverride("separation", 8);
		content.AddChild(nav);
		AddCategoryButton(nav, SettingsCategory.Language, "ui.language");
		AddCategoryButton(nav, SettingsCategory.Video, "ui.video");
		AddCategoryButton(nav, SettingsCategory.Controls, "ui.controls");
		AddCategoryButton(nav, SettingsCategory.Shortcuts, "ui.shortcuts");

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		content.AddChild(scroll);

		_settingsRows = new VBoxContainer();
		_settingsRows.AddThemeConstantOverride("separation", 16);
		scroll.AddChild(_settingsRows);

		BuildSelectedCategory();
	}

	private void BuildSelectedCategory()
	{
		if (_settingsRows == null)
		{
			return;
		}

		foreach (Node child in _settingsRows.GetChildren())
		{
			_settingsRows.RemoveChild(child);
			child.QueueFree();
		}

		_languageOption = null!;
		_resolutionOption = null!;
		_windowModeOption = null!;
		_thirdPersonCameraButton = null!;
		_godViewCameraButton = null!;
		_damageTextScaleSlider = null!;
		_damageTextScaleValueLabel = null!;
		_nameplateScaleSlider = null!;
		_nameplateScaleValueLabel = null!;
		_bossAnnouncementToggle = null!;
		_bossAnnouncementOpacitySlider = null!;
		_bossAnnouncementOpacityValueLabel = null!;

		switch (_selectedCategory)
		{
			case SettingsCategory.Language:
				BuildLanguageSection(_settingsRows);
				break;
			case SettingsCategory.Video:
				BuildDisplaySection(_settingsRows);
				break;
			case SettingsCategory.Controls:
				BuildControlSection(_settingsRows);
				break;
			case SettingsCategory.Shortcuts:
				BuildShortcutSection(_settingsRows);
				break;
		}

		SyncFromPlayer();
		RefreshCategoryButtons();
	}

	private void BuildLanguageSection(VBoxContainer rows)
	{
		var section = MakeSection(rows, "ui.language");
		_languageOption = new OptionButton { CustomMinimumSize = new Vector2(260.0f, 36.0f) };
		foreach (string languageCode in LocaleText.LanguageCodes)
		{
			_languageOption.AddItem(LocaleText.T($"language.{languageCode}"));
		}
		_languageOption.ItemSelected += OnLanguageSelected;
		AddSettingRow(section, "ui.language", _languageOption);
	}

	private void BuildDisplaySection(VBoxContainer rows)
	{
		var section = MakeSection(rows, "ui.video");
		_resolutionOption = new OptionButton { CustomMinimumSize = new Vector2(260.0f, 36.0f) };
		foreach (Vector2I resolution in _resolutions)
		{
			_resolutionOption.AddItem($"{resolution.X} x {resolution.Y}");
		}
		AddSettingRow(section, "ui.resolution", _resolutionOption);

		_windowModeOption = new OptionButton { CustomMinimumSize = new Vector2(260.0f, 36.0f) };
		_windowModeOption.AddItem(LocaleText.T("ui.windowed"));
		_windowModeOption.AddItem(LocaleText.T("ui.borderless"));
		_windowModeOption.AddItem(LocaleText.T("ui.fullscreen"));
		AddSettingRow(section, "ui.window_mode", _windowModeOption);

		var applyButton = MakeButton(LocaleText.T("ui.apply_video"));
		applyButton.CustomMinimumSize = new Vector2(160.0f, 38.0f);
		applyButton.Pressed += ApplyDisplaySettings;
		AddSettingRow(section, string.Empty, applyButton);
	}

	private void BuildControlSection(VBoxContainer rows)
	{
		var section = MakeSection(rows, "ui.controls");
		var cameraModeButtons = new HBoxContainer();
		cameraModeButtons.AddThemeConstantOverride("separation", 8);

		_thirdPersonCameraButton = MakeCameraModeButton(LocaleText.T("ui.camera_third_person"));
		_thirdPersonCameraButton.Pressed += OnThirdPersonCameraPressed;
		cameraModeButtons.AddChild(_thirdPersonCameraButton);

		_godViewCameraButton = MakeCameraModeButton(LocaleText.T("ui.camera_god_view"));
		_godViewCameraButton.Pressed += OnGodViewCameraPressed;
		cameraModeButtons.AddChild(_godViewCameraButton);

		AddSettingRow(section, "ui.camera_mode", cameraModeButtons);

		var damageTextControl = new HBoxContainer();
		damageTextControl.AddThemeConstantOverride("separation", 12);
		_damageTextScaleSlider = new HSlider
		{
			MinValue = CombatEffect.MinimumDamageTextScale,
			MaxValue = CombatEffect.MaximumDamageTextScale,
			Step = 0.05,
			CustomMinimumSize = new Vector2(230.0f, 36.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_damageTextScaleSlider.ValueChanged += OnDamageTextScaleChanged;
		damageTextControl.AddChild(_damageTextScaleSlider);
		_damageTextScaleValueLabel = MakeLabel(string.Empty, 15, new Color(1.0f, 0.92f, 0.66f));
		_damageTextScaleValueLabel.CustomMinimumSize = new Vector2(64.0f, 36.0f);
		_damageTextScaleValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		damageTextControl.AddChild(_damageTextScaleValueLabel);
		AddSettingRow(section, "ui.damage_text_size", damageTextControl);

		var nameplateControl = new HBoxContainer();
		nameplateControl.AddThemeConstantOverride("separation", 12);
		_nameplateScaleSlider = new HSlider
		{
			MinValue = SimpleActor.MinNameplateScale,
			MaxValue = SimpleActor.MaxNameplateScale,
			Step = 0.25,
			CustomMinimumSize = new Vector2(230.0f, 36.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_nameplateScaleSlider.ValueChanged += OnNameplateScaleChanged;
		nameplateControl.AddChild(_nameplateScaleSlider);
		_nameplateScaleValueLabel = MakeLabel(string.Empty, 15, new Color(1.0f, 0.92f, 0.66f));
		_nameplateScaleValueLabel.CustomMinimumSize = new Vector2(64.0f, 36.0f);
		_nameplateScaleValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		nameplateControl.AddChild(_nameplateScaleValueLabel);
		AddSettingRow(section, "ui.nameplate_size", nameplateControl);

		_bossAnnouncementToggle = new CheckButton
		{
			Text = LocaleText.T("ui.boss_announcement_show"),
			CustomMinimumSize = new Vector2(230.0f, 36.0f),
		};
		_bossAnnouncementToggle.Toggled += OnBossAnnouncementToggled;
		AddSettingRow(section, "ui.boss_announcement", _bossAnnouncementToggle);

		var bossOpacityControl = new HBoxContainer();
		bossOpacityControl.AddThemeConstantOverride("separation", 12);
		_bossAnnouncementOpacitySlider = new HSlider
		{
			MinValue = 0.20,
			MaxValue = 1.0,
			Step = 0.05,
			CustomMinimumSize = new Vector2(230.0f, 36.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_bossAnnouncementOpacitySlider.ValueChanged += OnBossAnnouncementOpacityChanged;
		bossOpacityControl.AddChild(_bossAnnouncementOpacitySlider);
		_bossAnnouncementOpacityValueLabel = MakeLabel(string.Empty, 15, new Color(1.0f, 0.92f, 0.66f));
		_bossAnnouncementOpacityValueLabel.CustomMinimumSize = new Vector2(64.0f, 36.0f);
		_bossAnnouncementOpacityValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		bossOpacityControl.AddChild(_bossAnnouncementOpacityValueLabel);
		AddSettingRow(section, "ui.boss_announcement_opacity", bossOpacityControl);
	}

	private void BuildShortcutSection(VBoxContainer rows)
	{
		var section = MakeSection(rows, "ui.shortcuts");
		AddShortcutRow(section, "shortcut.move", "W / A / S / D");
		AddShortcutRow(section, "shortcut.jump", "Space");
		AddShortcutRow(section, "shortcut.sprint", "Shift");
		AddShortcutRow(section, "shortcut.capture_net", "R");
		AddShortcutTextRow(section, LocaleText.T("shortcut.select_focus"), "Left Mouse");
		AddShortcutTextRow(section, LocaleText.T("shortcut.interact_revive"), "E");
		AddShortcutRow(section, "shortcut.save_game", "F5");
		AddShortcutRow(section, "shortcut.party", "P");
		AddShortcutRow(section, "shortcut.inventory", "I");
		AddShortcutRow(section, "shortcut.formation_panel", "F");
		AddShortcutRow(section, "shortcut.settings", "Esc");
		AddShortcutRow(section, "shortcut.camera", LocaleText.T("shortcut.mouse_look"));
	}

	private void ApplyDisplaySettings()
	{
		Vector2I resolution = _resolutions[Mathf.Clamp(_resolutionOption.Selected, 0, _resolutions.Count - 1)];
		int mode = _windowModeOption.Selected;

		if (mode == 2)
		{
			DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
			return;
		}

		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, mode == 1);
		DisplayServer.WindowSetSize(resolution);
		DisplayServer.WindowSetPosition((DisplayServer.ScreenGetSize() - resolution) / 2);
	}

	private void OnClosePressed()
	{
		if (CloseRequested != null)
		{
			CloseRequested();
			return;
		}

		SetPanelVisible(false);
	}

	private void SyncFromPlayer()
	{
		if (_languageOption != null)
		{
			SyncLanguage();
		}

		if (_resolutionOption != null)
		{
			SyncResolution();
		}

		if (_windowModeOption != null)
		{
			SyncWindowMode();
		}

		if (_thirdPersonCameraButton != null && _godViewCameraButton != null)
		{
			SyncCameraMode();
		}

		if (_damageTextScaleSlider != null)
		{
			SyncDamageTextScale();
		}

		if (_nameplateScaleSlider != null)
		{
			SyncNameplateScale();
		}

		if (_bossAnnouncementToggle != null && _bossAnnouncementOpacitySlider != null)
		{
			SyncBossAnnouncementSettings();
		}
	}

	private void SyncBossAnnouncementSettings()
	{
		bool enabled = _player?.BossAnnouncementsEnabled ?? true;
		float opacity = _player?.BossAnnouncementOpacity ?? 0.90f;
		_bossAnnouncementToggle.SetPressedNoSignal(enabled);
		_bossAnnouncementOpacitySlider.SetValueNoSignal(opacity);
		_bossAnnouncementOpacitySlider.Editable = enabled;
		UpdateBossAnnouncementOpacityLabel(opacity);
	}

	private void OnBossAnnouncementToggled(bool enabled)
	{
		_player?.SetBossAnnouncementsEnabled(enabled);
		if (_bossAnnouncementOpacitySlider != null)
		{
			_bossAnnouncementOpacitySlider.Editable = enabled;
		}
	}

	private void OnBossAnnouncementOpacityChanged(double value)
	{
		float opacity = (float)value;
		_player?.SetBossAnnouncementOpacity(opacity);
		UpdateBossAnnouncementOpacityLabel(opacity);
	}

	private void UpdateBossAnnouncementOpacityLabel(float opacity)
	{
		if (_bossAnnouncementOpacityValueLabel != null)
		{
			_bossAnnouncementOpacityValueLabel.Text = LocaleText.F("ui.opacity_value", Mathf.RoundToInt(opacity * 100.0f));
		}
	}

	private void SyncDamageTextScale()
	{
		float scale = _player?.DamageTextScale ?? CombatEffect.DamageTextScale;
		_damageTextScaleSlider.SetValueNoSignal(scale);
		UpdateDamageTextScaleLabel(scale);
	}

	private void OnDamageTextScaleChanged(double value)
	{
		float scale = (float)value;
		if (_player != null)
		{
			_player.SetDamageTextScale(scale);
		}
		else
		{
			CombatEffect.SetDamageTextScale(scale);
		}
		UpdateDamageTextScaleLabel(scale);
	}

	private void UpdateDamageTextScaleLabel(float scale)
	{
		if (_damageTextScaleValueLabel != null)
		{
			_damageTextScaleValueLabel.Text = LocaleText.F("ui.damage_text_size_value", Mathf.RoundToInt(scale * 100.0f));
		}
	}

	private void SyncNameplateScale()
	{
		float scale = _player?.NameplateScale ?? SimpleActor.NameplateScale;
		_nameplateScaleSlider.SetValueNoSignal(scale);
		UpdateNameplateScaleLabel(scale);
	}

	private void OnNameplateScaleChanged(double value)
	{
		var scale = (float)value;
		if (_player != null)
		{
			_player.SetNameplateScale(scale);
		}
		else
		{
			SimpleActor.SetNameplateScale(scale);
		}

		UpdateNameplateScaleLabel(scale);
	}

	private void UpdateNameplateScaleLabel(float scale)
	{
		if (_nameplateScaleValueLabel != null)
		{
			_nameplateScaleValueLabel.Text = LocaleText.F("ui.damage_text_size_value", Mathf.RoundToInt(scale * 100.0f));
		}
	}

	private void SyncCameraMode()
	{
		bool isGodView = _player?.CameraMode == PlayerController.CameraViewMode.GodView;
		_thirdPersonCameraButton.ButtonPressed = !isGodView;
		_godViewCameraButton.ButtonPressed = isGodView;
	}

	private void SyncLanguage()
	{
		for (int index = 0; index < LocaleText.LanguageCodes.Length; index++)
		{
			if (LocaleText.LanguageCodes[index] == LocaleText.CurrentLanguage)
			{
				_languageOption.Selected = index;
				return;
			}
		}

		_languageOption.Selected = 0;
	}

	private void SyncResolution()
	{
		Vector2I currentSize = DisplayServer.WindowGetSize();
		int selected = 0;
		for (int index = 0; index < _resolutions.Count; index++)
		{
			if (_resolutions[index] == currentSize)
			{
				selected = index;
				break;
			}
		}

		_resolutionOption.Selected = selected;
	}

	private void SyncWindowMode()
	{
		DisplayServer.WindowMode mode = DisplayServer.WindowGetMode();
		bool borderless = DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Borderless);

		if (mode == DisplayServer.WindowMode.ExclusiveFullscreen || mode == DisplayServer.WindowMode.Fullscreen)
		{
			_windowModeOption.Selected = 2;
		}
		else
		{
			_windowModeOption.Selected = borderless ? 1 : 0;
		}
	}

	private void OnLanguageSelected(long index)
	{
		if (index >= 0 && index < LocaleText.LanguageCodes.Length)
		{
			LocaleText.SetLanguage(LocaleText.LanguageCodes[(int)index]);
		}
	}

	private void OnThirdPersonCameraPressed()
	{
		_player?.SetCameraMode(PlayerController.CameraViewMode.ThirdPerson);
		SyncCameraMode();
	}

	private void OnGodViewCameraPressed()
	{
		_player?.SetCameraMode(PlayerController.CameraViewMode.GodView);
		SyncCameraMode();
	}

	private void OnLanguageChanged()
	{
		bool wasVisible = Visible;
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		BuildPanel();
		Visible = wasVisible;
		SyncFromPlayer();
	}

	private void AddCategoryButton(VBoxContainer nav, SettingsCategory category, string textKey)
	{
		var button = MakeButton(LocaleText.T(textKey));
		button.CustomMinimumSize = new Vector2(0.0f, 38.0f);
		button.Alignment = HorizontalAlignment.Left;
		button.ToggleMode = true;
		button.Pressed += () => SelectCategory(category);
		nav.AddChild(button);
		_categoryButtons[category] = button;
	}

	private void SelectCategory(SettingsCategory category)
	{
		if (_selectedCategory == category)
		{
			RefreshCategoryButtons();
			return;
		}

		_selectedCategory = category;
		BuildSelectedCategory();
	}

	private void RefreshCategoryButtons()
	{
		foreach (KeyValuePair<SettingsCategory, Button> pair in _categoryButtons)
		{
			pair.Value.ButtonPressed = pair.Key == _selectedCategory;
		}
	}

	private VBoxContainer MakeSection(VBoxContainer rows, string titleKey)
	{
		var section = new VBoxContainer();
		section.AddThemeConstantOverride("separation", 10);
		rows.AddChild(section);

		var title = MakeLabel(LocaleText.T(titleKey), 20, new Color(1.0f, 0.94f, 0.72f));
		section.AddChild(title);
		return section;
	}

	private void AddSettingRow(VBoxContainer section, string labelKey, Control control)
	{
		var row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0.0f, 38.0f),
		};
		row.AddThemeConstantOverride("separation", 14);
		section.AddChild(row);

		var label = MakeLabel(string.IsNullOrEmpty(labelKey) ? string.Empty : LocaleText.T(labelKey), 15, new Color(0.68f, 0.76f, 0.84f));
		label.CustomMinimumSize = new Vector2(130.0f, 0.0f);
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.ClipText = false;
		row.AddChild(label);

		control.CustomMinimumSize = new Vector2(Mathf.Max(control.CustomMinimumSize.X, 260.0f), Mathf.Max(control.CustomMinimumSize.Y, 36.0f));
		control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(control);
	}

	private void AddShortcutRow(VBoxContainer section, string actionKey, string key)
	{
		AddShortcutTextRow(section, LocaleText.T(actionKey), key);
	}

	private void AddShortcutTextRow(VBoxContainer section, string actionText, string key)
	{
		var row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0.0f, 28.0f),
		};
		row.AddThemeConstantOverride("separation", 14);
		section.AddChild(row);

		var actionLabel = MakeLabel(actionText, 15, new Color(0.80f, 0.86f, 0.92f));
		actionLabel.CustomMinimumSize = new Vector2(230.0f, 0.0f);
		actionLabel.AutowrapMode = TextServer.AutowrapMode.Off;
		actionLabel.ClipText = false;
		row.AddChild(actionLabel);

		var keyLabel = MakeLabel(key, 15, new Color(1.0f, 1.0f, 1.0f));
		keyLabel.CustomMinimumSize = new Vector2(260.0f, 0.0f);
		keyLabel.AutowrapMode = TextServer.AutowrapMode.Off;
		keyLabel.ClipText = false;
		keyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(keyLabel);
	}

	private static Label MakeLabel(string text, int fontSize, Color color)
	{
		var label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static Button MakeButton(string text)
	{
		var button = new Button { Text = text };
		button.AddThemeFontSizeOverride("font_size", 14);
		return button;
	}

	private static Button MakeCameraModeButton(string text)
	{
		var button = MakeButton(text);
		button.ToggleMode = true;
		button.CustomMinimumSize = new Vector2(150.0f, 36.0f);
		button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return button;
	}
}
