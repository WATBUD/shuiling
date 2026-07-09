using Godot;
using System.Collections.Generic;

public partial class SettingsPanel : PanelContainer
{
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
	private HSlider _mouseSensitivitySlider = null!;
	private Label _mouseSensitivityValueLabel = null!;

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
		if (_mouseSensitivitySlider != null)
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
		nav.AddChild(MakeNavLabel("ui.video"));
		nav.AddChild(MakeNavLabel("ui.controls"));
		nav.AddChild(MakeNavLabel("ui.shortcuts"));

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		content.AddChild(scroll);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 16);
		scroll.AddChild(rows);

		BuildLanguageSection(rows);
		BuildDisplaySection(rows);
		BuildControlSection(rows);
		BuildShortcutSection(rows);
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
		var sensitivityRow = new HBoxContainer();
		sensitivityRow.AddThemeConstantOverride("separation", 10);

		_mouseSensitivitySlider = new HSlider
		{
			MinValue = 0.08,
			MaxValue = 0.40,
			Step = 0.01,
			CustomMinimumSize = new Vector2(260.0f, 0.0f),
		};
		_mouseSensitivitySlider.ValueChanged += OnMouseSensitivityChanged;
		sensitivityRow.AddChild(_mouseSensitivitySlider);

		_mouseSensitivityValueLabel = MakeLabel("100%", 15, new Color(0.96f, 0.98f, 1.0f));
		_mouseSensitivityValueLabel.CustomMinimumSize = new Vector2(70.0f, 0.0f);
		sensitivityRow.AddChild(_mouseSensitivityValueLabel);
		AddSettingRow(section, "ui.mouse_sensitivity", sensitivityRow);
	}

	private void BuildShortcutSection(VBoxContainer rows)
	{
		var section = MakeSection(rows, "ui.shortcuts");
		AddShortcutRow(section, "shortcut.move", "W / A / S / D");
		AddShortcutRow(section, "shortcut.jump", "Space");
		AddShortcutRow(section, "shortcut.sprint", "Shift");
		AddShortcutRow(section, "shortcut.capture_net", "R");
		AddShortcutRow(section, "shortcut.party", "P");
		AddShortcutRow(section, "shortcut.inventory", "I");
		AddShortcutRow(section, "shortcut.settings", "Esc");
		AddShortcutRow(section, "shortcut.mouse_look", LocaleText.T("shortcut.mouse_move"));
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
		SyncLanguage();
		SyncResolution();
		SyncWindowMode();

		if (_player != null)
		{
			double value = Mathf.Clamp(_player.MouseSensitivity / 0.0022f * 0.20f, 0.08f, 0.40f);
			_mouseSensitivitySlider.Value = value;
			UpdateMouseSensitivityLabel(value);
		}
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

	private void OnMouseSensitivityChanged(double value)
	{
		if (_player != null)
		{
			_player.MouseSensitivity = (float)(0.0022f * (value / 0.20));
		}

		UpdateMouseSensitivityLabel(value);
	}

	private void OnLanguageSelected(long index)
	{
		if (index >= 0 && index < LocaleText.LanguageCodes.Length)
		{
			LocaleText.SetLanguage(LocaleText.LanguageCodes[(int)index]);
		}
	}

	private void UpdateMouseSensitivityLabel(double value)
	{
		_mouseSensitivityValueLabel.Text = $"{Mathf.RoundToInt((float)(value / 0.20 * 100.0))}%";
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
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 14);
		section.AddChild(row);

		var label = MakeLabel(string.IsNullOrEmpty(labelKey) ? string.Empty : LocaleText.T(labelKey), 15, new Color(0.68f, 0.76f, 0.84f));
		label.CustomMinimumSize = new Vector2(130.0f, 0.0f);
		row.AddChild(label);

		control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(control);
	}

	private void AddShortcutRow(VBoxContainer section, string actionKey, string key)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 14);
		section.AddChild(row);

		var actionLabel = MakeLabel(LocaleText.T(actionKey), 15, new Color(0.80f, 0.86f, 0.92f));
		actionLabel.CustomMinimumSize = new Vector2(160.0f, 0.0f);
		row.AddChild(actionLabel);

		var keyLabel = MakeLabel(key, 15, new Color(1.0f, 1.0f, 1.0f));
		keyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(keyLabel);
	}

	private static Label MakeNavLabel(string textKey)
	{
		var label = MakeLabel(LocaleText.T(textKey), 16, new Color(0.88f, 0.94f, 1.0f));
		label.CustomMinimumSize = new Vector2(0.0f, 34.0f);
		return label;
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
}
