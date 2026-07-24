using Godot;
using System.Collections.Generic;

public partial class MainMenu : Control
{
	// Multiplayer dialogs (host-authoritative co-op, see NetworkManager.cs).
	private PanelContainer? _worldListDialog;
	private PanelContainer? _newWorldModeDialog;
	private VBoxContainer _worldListContainer = null!;
	private Label _worldListEmptyLabel = null!;
	private PanelContainer? _hostDialog;
	private PanelContainer? _joinDialog;
	private LineEdit _hostPortEdit = null!;
	private LineEdit _joinAddressEdit = null!;
	private LineEdit _joinPortEdit = null!;
	private OptionButton _recentServerOption = null!;
	private System.Collections.Generic.List<NetworkPrefs.ServerEntry> _recentServers = new();
	private bool _awaitingJoin;
	private Label _hostStatusLabel = null!;
	private Label _hostWorldLabel = null!;
	private Label _hostIpLabel = null!;
	private Label _joinStatusLabel = null!;
	private Button _hostStartButton = null!;
	private Label _hostDiagLabel = null!;
	private bool _diagRunning;
	private bool _diagPending;
	private Button _joinConfirmButton = null!;

	private SettingsPanel? _settingsPanel;
	private AudioStreamPlayer? _menuMusic;
	private System.Collections.Generic.List<string> _menuTracks = new();

	// The world chosen from the list to host.
	private string _hostWorldId = string.Empty;
	private int _hostWorldSeed;
	private bool _hostIsNewWorld;

	public override void _Ready()
	{
		try
		{
			BuildMenu();
		}
		catch (System.Exception exception)
		{
			GD.PushError($"Main menu failed to initialize: {exception}");
			BuildFallbackMenu(exception.Message);
		}

		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.JoinWelcomed += OnJoinWelcomed;
			NetworkManager.Instance.JoinFailed += OnJoinFailed;
		}

		// Coming back from character-select's Cancel: reopen only the previous
		// screen (the single/multiplayer window), matching the forward flow where
		// the world list is hidden while that window is shown.
		if (GameLaunchOptions.ReturnToNewWorldMode)
		{
			GameLaunchOptions.ReturnToNewWorldMode = false;
			if (_newWorldModeDialog != null)
			{
				_newWorldModeDialog.Visible = true;
			}
		}

	}

	public override void _ExitTree()
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.JoinWelcomed -= OnJoinWelcomed;
			NetworkManager.Instance.JoinFailed -= OnJoinFailed;
		}
	}

	private void BuildMenu()
	{
		ClearChildren();
		SetAnchorsPreset(LayoutPreset.FullRect);

		var background = new ColorRect
		{
			Color = new Color(0.025f, 0.035f, 0.045f),
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			AnchorTop = 0.0f,
			AnchorBottom = 1.0f,
		};
		AddChild(background);

		var root = new VBoxContainer
		{
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -220.0f,
			OffsetRight = 220.0f,
			OffsetTop = -200.0f,
			OffsetBottom = 200.0f,
		};
		root.AddThemeConstantOverride("separation", 16);
		AddChild(root);

		var title = new Label
		{
			Text = LocaleText.T("main_menu.title"),
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		title.AddThemeFontSizeOverride("font_size", 36);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.76f));
		root.AddChild(title);

		Button playButton = MakeMenuButton(LocaleText.T("main_menu.play"));
		playButton.Pressed += ShowWorldList;
		root.AddChild(playButton);

		Button joinButton = MakeMenuButton(LocaleText.T("main_menu.join_server"));
		joinButton.Pressed += ShowJoinDialog;
		root.AddChild(joinButton);

		Button settingsButton = MakeMenuButton(LocaleText.T("ui.settings"));
		settingsButton.Pressed += ShowSettings;
		root.AddChild(settingsButton);

		Button quitButton = MakeMenuButton(LocaleText.T("main_menu.quit"));
		quitButton.Pressed += () => GetTree().Quit();
		root.AddChild(quitButton);

		BuildWorldListDialog();
		BuildNewWorldModeDialog();
		BuildHostDialog();
		BuildJoinDialog();
		BuildSettingsPanel();
		StartMenuMusic();
	}

	private void BuildSettingsPanel()
	{
		_settingsPanel = new SettingsPanel { Visible = false };
		AddChild(_settingsPanel);
		_settingsPanel.CloseRequested = () => _settingsPanel!.SetPanelVisible(false);
	}

	private void ShowSettings()
	{
		if (_worldListDialog != null) _worldListDialog.Visible = false;
		if (_newWorldModeDialog != null) _newWorldModeDialog.Visible = false;
		if (_hostDialog != null) _hostDialog.Visible = false;
		if (_joinDialog != null) _joinDialog.Visible = false;
		_settingsPanel?.SetPanelVisible(true);
	}

	// Main-menu background music (its own looping player, on the Music bus).
	private void StartMenuMusic()
	{
		AudioSettings.Initialize();
		_menuTracks = MusicPlayer.ScanTracks("res://assets/audio/music/menu/");
		if (_menuTracks.Count == 0)
		{
			return;
		}

		_menuMusic = new AudioStreamPlayer { Name = "MenuMusic", Bus = AudioSettings.MusicBus, VolumeDb = -8.0f };
		AddChild(_menuMusic);
		_menuMusic.Finished += PlayRandomMenuTrack;
		PlayRandomMenuTrack();
	}

	private void PlayRandomMenuTrack()
	{
		if (_menuMusic == null || _menuTracks.Count == 0)
		{
			return;
		}

		string path = _menuTracks[(int)(GD.Randi() % (uint)_menuTracks.Count)];
		var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
		if (stream == null)
		{
			return;
		}

		MusicPlayer.SetStreamLoop(stream, _menuTracks.Count == 1);
		_menuMusic.Stream = stream;
		_menuMusic.Play();
	}

	// Shared "new world" window: pick single-player or multiplayer BEFORE going to
	// character creation.
	private void BuildNewWorldModeDialog()
	{
		_newWorldModeDialog = MakeDialogPanel("world.mode_title", out VBoxContainer content);

		var hint = new Label
		{
			Text = LocaleText.T("world.mode_hint"),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		hint.AddThemeFontSizeOverride("font_size", 14);
		hint.AddThemeColorOverride("font_color", new Color(0.72f, 0.82f, 0.92f));
		content.AddChild(hint);

		Button single = MakeMenuButton(LocaleText.T("world.mode.single_play"));
		single.Pressed += () => ChooseNewWorldMode(false);
		content.AddChild(single);

		Button multi = MakeMenuButton(LocaleText.T("world.mode.multi_play"));
		multi.Pressed += () => ChooseNewWorldMode(true);
		content.AddChild(multi);

		Button cancel = MakeMenuButton(LocaleText.T("dialog.button.cancel"));
		cancel.Pressed += () =>
		{
			_newWorldModeDialog!.Visible = false;
			ShowWorldList();
		};
		content.AddChild(cancel);
	}

	private void ChooseNewWorldMode(bool multiplayer)
	{
		GameLaunchOptions.NewWorldIsMultiplayer = multiplayer;
		if (_newWorldModeDialog != null)
		{
			_newWorldModeDialog.Visible = false;
		}

		if (multiplayer)
		{
			// Set up the server FIRST, then go to character creation.
			ShowHostDialogForNewWorld();
		}
		else
		{
			NetworkManager.Instance?.ResetSession();
			GetTree().ChangeSceneToFile("res://character_select.tscn");
		}
	}

	// ---------------------------------------------------------------- world list

	private void BuildWorldListDialog()
	{
		_worldListDialog = MakeDialogPanel("world.list_title", out VBoxContainer content, 390.0f, 320.0f);

		var hint = new Label
		{
			Text = LocaleText.T("world.list_hint"),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		hint.AddThemeFontSizeOverride("font_size", 13);
		hint.AddThemeColorOverride("font_color", new Color(0.66f, 0.78f, 0.9f));
		content.AddChild(hint);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(560.0f, 330.0f),
		};
		content.AddChild(scroll);

		_worldListContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_worldListContainer.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_worldListContainer);

		_worldListEmptyLabel = new Label
		{
			Text = LocaleText.T("world.list_empty"),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_worldListEmptyLabel.AddThemeFontSizeOverride("font_size", 15);
		_worldListEmptyLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.92f));
		content.AddChild(_worldListEmptyLabel);

		Button newWorld = MakeMenuButton(LocaleText.T("world.new"));
		newWorld.Pressed += NewWorld;
		content.AddChild(newWorld);

		Button back = MakeMenuButton(LocaleText.T("dialog.button.cancel"));
		back.Pressed += () => _worldListDialog!.Visible = false;
		content.AddChild(back);
	}

	private void ShowWorldList()
	{
		if (_hostDialog != null) _hostDialog.Visible = false;
		if (_joinDialog != null) _joinDialog.Visible = false;
		RefreshWorldList();
		if (_worldListDialog != null) _worldListDialog.Visible = true;
	}

	private void RefreshWorldList()
	{
		foreach (Node child in _worldListContainer.GetChildren())
		{
			_worldListContainer.RemoveChild(child);
			child.QueueFree();
		}

		List<SaveGameManager.WorldSaveInfo> worlds = SaveGameManager.ListWorlds();
		_worldListEmptyLabel.Visible = worlds.Count == 0;
		foreach (SaveGameManager.WorldSaveInfo world in worlds)
		{
			_worldListContainer.AddChild(BuildWorldRow(world));
		}
	}

	private Control BuildWorldRow(SaveGameManager.WorldSaveInfo world)
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.09f, 0.12f, 0.16f, 0.95f),
			BorderColor = new Color(0.4f, 0.6f, 0.82f, 0.6f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		row.AddChild(margin);

		var cardContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		cardContent.AddThemeConstantOverride("separation", 10);
		margin.AddChild(cardContent);

		var info = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		info.AddThemeConstantOverride("separation", 4);
		cardContent.AddChild(info);

		var nameLabel = new Label
		{
			Text = world.Name,
			VerticalAlignment = VerticalAlignment.Center,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.78f));
		info.AddChild(nameLabel);

		var autoSave = new CheckButton
		{
			Text = LocaleText.T("world.auto_save"),
			ButtonPressed = world.AutoSaveOnExit,
			TooltipText = LocaleText.T("world.auto_save_select_hint"),
			CustomMinimumSize = new Vector2(156.0f, 38.0f),
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
			Alignment = HorizontalAlignment.Left,
		};
		autoSave.AddThemeFontSizeOverride("font_size", 14);
		var autoSaveStyle = new StyleBoxEmpty
		{
			ContentMarginLeft = 0.0f,
			ContentMarginRight = 0.0f,
			ContentMarginTop = 0.0f,
			ContentMarginBottom = 0.0f,
		};
		autoSave.AddThemeStyleboxOverride("normal", autoSaveStyle);
		autoSave.AddThemeStyleboxOverride("hover", autoSaveStyle);
		autoSave.AddThemeStyleboxOverride("pressed", autoSaveStyle);
		autoSave.AddThemeStyleboxOverride("hover_pressed", autoSaveStyle);
		autoSave.AddThemeStyleboxOverride("focus", autoSaveStyle);
		autoSave.AddThemeStyleboxOverride("disabled", autoSaveStyle);
		bool updatingAutoSave = false;
		autoSave.Toggled += enabled =>
		{
			if (updatingAutoSave)
			{
				return;
			}

			if (SaveGameManager.TrySetWorldAutoSave(world.Id, enabled, out string error))
			{
				world.AutoSaveOnExit = enabled;
				return;
			}

			GD.PushWarning($"Failed to update auto-save for world {world.Id}: {error}");
			updatingAutoSave = true;
			autoSave.SetPressedNoSignal(world.AutoSaveOnExit);
			updatingAutoSave = false;
		};
		string modeText = LocaleText.T(world.LastMode == "multiplayer" ? "world.mode.multiplayer" : "world.mode.single");
		var meta = new Label
		{
			Text = LocaleText.F("world.row_meta", modeText, world.Level, FormatSavedAt(world.SavedAt)),
			VerticalAlignment = VerticalAlignment.Center,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		meta.AddThemeFontSizeOverride("font_size", 13);
		meta.AddThemeColorOverride("font_color", new Color(0.66f, 0.76f, 0.88f));
		info.AddChild(meta);

		var actions = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Begin,
		};
		actions.AddThemeConstantOverride("separation", 8);
		cardContent.AddChild(actions);
		actions.AddChild(autoSave);

		var play = new Button { Text = LocaleText.T("world.play_single"), CustomMinimumSize = new Vector2(150.0f, 38.0f) };
		play.AddThemeFontSizeOverride("font_size", 14);
		play.Pressed += () => PlaySingle(world);
		actions.AddChild(play);

		var host = new Button { Text = LocaleText.T("world.host"), CustomMinimumSize = new Vector2(150.0f, 38.0f) };
		host.AddThemeFontSizeOverride("font_size", 14);
		host.Pressed += () => ShowHostDialog(world);
		actions.AddChild(host);

		var delete = new Button { Text = LocaleText.T("world.delete"), CustomMinimumSize = new Vector2(116.0f, 38.0f) };
		delete.AddThemeFontSizeOverride("font_size", 14);
		delete.Pressed += () =>
		{
			if (delete.HasMeta("armed"))
			{
				SaveGameManager.DeleteWorld(world.Id);
				RefreshWorldList();
			}
			else
			{
				// Two-click guard so a stray click never nukes a world.
				delete.SetMeta("armed", true);
				delete.Text = LocaleText.T("world.delete_confirm");
			}
		};
		actions.AddChild(delete);

		return row;
	}

	private void PlaySingle(SaveGameManager.WorldSaveInfo world)
	{
		NetworkManager.Instance?.ResetSession();
		GameLaunchOptions.LoadWorld(world.Id, world.Seed);
		GetTree().ChangeSceneToFile("res://node_3d.tscn");
	}

	private void NewWorld()
	{
		if (_worldListDialog != null) _worldListDialog.Visible = false;
		if (_newWorldModeDialog != null) _newWorldModeDialog.Visible = true;
	}

	private static string FormatSavedAt(string savedAt)
	{
		if (System.DateTimeOffset.TryParse(savedAt, out System.DateTimeOffset when))
		{
			return when.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
		}

		return savedAt;
	}

	// ---------------------------------------------------------------- dialogs

	private PanelContainer MakeDialogPanel(string titleKey, out VBoxContainer content, float halfWidth = 230.0f, float halfHeight = 235.0f)
	{
		var panel = new PanelContainer
		{
			Visible = false,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -halfWidth,
			OffsetRight = halfWidth,
			OffsetTop = -halfHeight,
			OffsetBottom = halfHeight,
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.09f, 0.97f),
			BorderColor = new Color(0.35f, 0.82f, 1.0f, 0.72f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		panel.AddThemeStyleboxOverride("panel", style);
		AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		panel.AddChild(margin);

		content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);

		var title = new Label
		{
			Text = LocaleText.T(titleKey),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.78f));
		content.AddChild(title);

		return panel;
	}

	private static Label MakeFieldLabel(string key)
	{
		var label = new Label { Text = LocaleText.T(key) };
		label.AddThemeFontSizeOverride("font_size", 15);
		label.AddThemeColorOverride("font_color", new Color(0.78f, 0.88f, 1.0f));
		return label;
	}

	private void BuildHostDialog()
	{
		_hostDialog = MakeDialogPanel("net.dialog.host_title", out VBoxContainer content);

		_hostWorldLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_hostWorldLabel.AddThemeFontSizeOverride("font_size", 15);
		_hostWorldLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.94f, 1.0f));
		content.AddChild(_hostWorldLabel);

		content.AddChild(MakeFieldLabel("net.dialog.port"));
		_hostPortEdit = new LineEdit { Text = NetworkManager.DefaultPort.ToString(), CustomMinimumSize = new Vector2(0.0f, 38.0f) };
		// Re-run the reachability check whenever the port is edited.
		_hostPortEdit.TextChanged += _ => RequestDiagnostic();
		content.AddChild(_hostPortEdit);

		var playerCapLabel = new Label
		{
			Text = LocaleText.F("net.dialog.max_players", NetworkManager.MaxPlayers),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		playerCapLabel.AddThemeFontSizeOverride("font_size", 13);
		playerCapLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.82f));
		content.AddChild(playerCapLabel);

		// Host addresses so friends know what to type in "Join"; the reachability
		// result is shown on the line directly below the public IP.
		_hostIpLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_hostIpLabel.AddThemeFontSizeOverride("font_size", 14);
		_hostIpLabel.AddThemeColorOverride("font_color", new Color(0.72f, 1.0f, 0.82f));
		content.AddChild(_hostIpLabel);

		_hostDiagLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_hostDiagLabel.AddThemeFontSizeOverride("font_size", 13);
		_hostDiagLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.9f, 1.0f));
		content.AddChild(_hostDiagLabel);

		_hostStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_hostStatusLabel.AddThemeFontSizeOverride("font_size", 14);
		_hostStatusLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.72f, 0.68f));
		content.AddChild(_hostStatusLabel);

		_hostStartButton = MakeMenuButton(LocaleText.T("world.host_start"));
		_hostStartButton.Pressed += StartHosting;
		content.AddChild(_hostStartButton);

		Button cancel = MakeMenuButton(LocaleText.T("dialog.button.cancel"));
		cancel.Pressed += () =>
		{
			_hostDialog!.Visible = false;
			if (_hostIsNewWorld && _newWorldModeDialog != null)
			{
				// Back to the single/multiplayer choice for a new world.
				_newWorldModeDialog.Visible = true;
			}
			else
			{
				ShowWorldList();
			}
		};
		content.AddChild(cancel);
	}

	private void BuildJoinDialog()
	{
		_joinDialog = MakeDialogPanel("net.dialog.join_title", out VBoxContainer content);

		content.AddChild(MakeFieldLabel("net.dialog.recent"));
		_recentServerOption = new OptionButton { CustomMinimumSize = new Vector2(0.0f, 36.0f) };
		_recentServerOption.ItemSelected += OnRecentServerSelected;
		content.AddChild(_recentServerOption);

		content.AddChild(MakeFieldLabel("net.dialog.ip"));
		_joinAddressEdit = new LineEdit { Text = "127.0.0.1", CustomMinimumSize = new Vector2(0.0f, 38.0f) };
		content.AddChild(_joinAddressEdit);

		content.AddChild(MakeFieldLabel("net.dialog.port"));
		_joinPortEdit = new LineEdit { Text = NetworkManager.DefaultPort.ToString(), CustomMinimumSize = new Vector2(0.0f, 38.0f) };
		content.AddChild(_joinPortEdit);

		_joinStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_joinStatusLabel.AddThemeFontSizeOverride("font_size", 14);
		_joinStatusLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.72f, 0.68f));
		content.AddChild(_joinStatusLabel);

		_joinConfirmButton = MakeMenuButton(LocaleText.T("net.dialog.join"));
		_joinConfirmButton.Pressed += StartJoining;
		content.AddChild(_joinConfirmButton);

		Button cancel = MakeMenuButton(LocaleText.T("dialog.button.cancel"));
		cancel.Pressed += CancelJoining;
		content.AddChild(cancel);
	}

	private void ShowHostDialog(SaveGameManager.WorldSaveInfo world)
	{
		_hostIsNewWorld = false;
		_hostWorldId = world.Id;
		_hostWorldSeed = world.Seed;
		OpenHostDialog(LocaleText.F("world.host_world", world.Name));
	}

	// New multiplayer world: create the server here, BEFORE character creation.
	private void ShowHostDialogForNewWorld()
	{
		_hostIsNewWorld = true;
		_hostWorldId = string.Empty;
		_hostWorldSeed = 0;
		OpenHostDialog(LocaleText.T("world.host_new_title"));
	}

	private void OpenHostDialog(string titleText)
	{
		if (_worldListDialog != null) _worldListDialog.Visible = false;
		if (_newWorldModeDialog != null) _newWorldModeDialog.Visible = false;
		if (_joinDialog != null) _joinDialog.Visible = false;
		if (_hostDialog != null)
		{
			_hostWorldLabel.Text = titleText;
			_hostStartButton.Text = LocaleText.T(_hostIsNewWorld ? "world.host_create" : "world.host_start");
			_hostStatusLabel.Text = string.Empty;
			_hostDiagLabel.Text = string.Empty;
			ShowHostAddresses();
			_hostDialog.Visible = true;
			// Auto-detect reachability on entry (no manual button).
			RequestDiagnostic();
		}
	}

	// LAN IP is instant; the public/WAN IP needs a (blocking) UPnP query, so fetch
	// it on a worker and fill it in when it arrives.
	private void ShowHostAddresses()
	{
		string lan = NetworkDiagnostics.GetLocalIPv4();
		string lanText = string.IsNullOrEmpty(lan) ? LocaleText.T("net.dialog.ip_unknown") : lan;
		_hostIpLabel.Text = LocaleText.F("net.dialog.lan_ip", lanText) + "\n" + LocaleText.F("net.dialog.wan_ip", LocaleText.T("net.dialog.wan_checking"));

		System.Threading.Tasks.Task.Run(() =>
		{
			string wan = NetworkDiagnostics.QueryExternalIp();
			Callable.From(() =>
			{
				if (_hostIpLabel != null && IsInstanceValid(_hostIpLabel))
				{
					string wanText = string.IsNullOrEmpty(wan) ? LocaleText.T("net.dialog.ip_unknown") : wan;
					_hostIpLabel.Text = LocaleText.F("net.dialog.lan_ip", lanText) + "\n" + LocaleText.F("net.dialog.wan_ip", wanText);
				}
			}).CallDeferred();
		});
	}

	private void ShowJoinDialog()
	{
		if (_worldListDialog != null) _worldListDialog.Visible = false;
		if (_hostDialog != null) _hostDialog.Visible = false;
		if (_joinDialog != null)
		{
			_joinStatusLabel.Text = string.Empty;
			_joinConfirmButton.Disabled = false;
			PopulateRecentServers();
			_joinDialog.Visible = true;
		}
	}

	private void PopulateRecentServers()
	{
		_recentServers = NetworkPrefs.GetRecentServers();
		_recentServerOption.Clear();
		if (_recentServers.Count == 0)
		{
			_recentServerOption.AddItem(LocaleText.T("net.dialog.recent_none"));
			_recentServerOption.Disabled = true;
			return;
		}

		_recentServerOption.Disabled = false;
		foreach (NetworkPrefs.ServerEntry entry in _recentServers)
		{
			_recentServerOption.AddItem($"{entry.Address}:{entry.Port}");
		}

		// Pre-fill with the most recently used server.
		_recentServerOption.Selected = 0;
		_joinAddressEdit.Text = _recentServers[0].Address;
		_joinPortEdit.Text = _recentServers[0].Port.ToString();
	}

	private void OnRecentServerSelected(long index)
	{
		if (index < 0 || index >= _recentServers.Count)
		{
			return;
		}

		_joinAddressEdit.Text = _recentServers[(int)index].Address;
		_joinPortEdit.Text = _recentServers[(int)index].Port.ToString();
	}

	private static bool TryParsePort(string text, out int port)
	{
		return int.TryParse(text.Trim(), out port) && port >= 1024 && port <= 65535;
	}

	// Runs the listen-server reachability diagnostic. The quick bind test is on
	// the main thread; the blocking UPnP discovery + firewall calls run on a
	// worker and marshal the results back with CallDeferred.
	// Auto-detect reachability (runs on host-screen entry and on every port edit).
	// Guarded so overlapping requests coalesce; the latest port is re-checked once
	// the in-flight check finishes.
	private void RequestDiagnostic()
	{
		if (_hostDialog == null || !_hostDialog.Visible)
		{
			return;
		}

		if (!TryParsePort(_hostPortEdit.Text, out int port))
		{
			_hostDiagLabel.Text = NetworkDiagnostics.Marker(NetworkDiagnostics.Level.Fail) + LocaleText.T("net.error.invalid_port");
			return;
		}

		if (_diagRunning)
		{
			_diagPending = true;
			return;
		}

		_diagRunning = true;
		_hostDiagLabel.Text = LocaleText.T("net.diag.running");

		NetworkDiagnostics.Line bindLine = NetworkDiagnostics.TestPortBind(port);
		System.Threading.Tasks.Task.Run(() =>
		{
			var lines = new List<NetworkDiagnostics.Line> { bindLine };
			lines.AddRange(NetworkDiagnostics.RunNat(port));
			lines.Add(NetworkDiagnostics.EnsureFirewallRule(port));
			Callable.From(() => ShowDiagnostics(lines)).CallDeferred();
		});
	}

	private void ShowDiagnostics(List<NetworkDiagnostics.Line> lines)
	{
		var builder = new System.Text.StringBuilder();
		bool anyFail = false;
		bool anyWarn = false;
		foreach (NetworkDiagnostics.Line line in lines)
		{
			builder.Append(NetworkDiagnostics.Marker(line.Level)).AppendLine(line.Message);
			anyFail |= line.Level == NetworkDiagnostics.Level.Fail;
			anyWarn |= line.Level == NetworkDiagnostics.Level.Warn;
		}

		_hostDiagLabel.Text = builder.ToString().TrimEnd();
		_hostDiagLabel.AddThemeColorOverride("font_color", anyFail
			? new Color(1.0f, 0.6f, 0.55f)
			: anyWarn
				? new Color(1.0f, 0.85f, 0.5f)
				: new Color(0.6f, 1.0f, 0.7f));

		// Re-run once more if the port changed while this check was in flight.
		_diagRunning = false;
		if (_diagPending)
		{
			_diagPending = false;
			RequestDiagnostic();
		}
	}

	private void StartHosting()
	{
		if (NetworkManager.Instance == null)
		{
			return;
		}

		if (!_hostIsNewWorld && string.IsNullOrEmpty(_hostWorldId))
		{
			return;
		}

		if (!TryParsePort(_hostPortEdit.Text, out int port))
		{
			_hostStatusLabel.Text = LocaleText.T("net.error.invalid_port");
			return;
		}

		string error = NetworkManager.Instance.CreateServer(port);
		if (error.Length > 0)
		{
			_hostStatusLabel.Text = LocaleText.F("net.error.create_failed", error);
			return;
		}

		if (_hostIsNewWorld)
		{
			// Server is up (its WorldSeed is the new world's seed). Now pick the
			// character; character-select enters the world when done.
			GetTree().ChangeSceneToFile("res://character_select.tscn");
		}
		else
		{
			// Existing world: host with its saved seed + progress, enter directly.
			NetworkManager.Instance.OverrideWorldSeed(_hostWorldSeed);
			GameLaunchOptions.LoadWorld(_hostWorldId, _hostWorldSeed);
			GetTree().ChangeSceneToFile("res://node_3d.tscn");
		}
	}

	private void StartJoining()
	{
		if (NetworkManager.Instance == null)
		{
			return;
		}

		string address = _joinAddressEdit.Text.Trim();
		if (address.Length == 0)
		{
			_joinStatusLabel.Text = LocaleText.T("net.error.invalid_ip");
			return;
		}

		if (!TryParsePort(_joinPortEdit.Text, out int port))
		{
			_joinStatusLabel.Text = LocaleText.T("net.error.invalid_port");
			return;
		}

		string error = NetworkManager.Instance.JoinServer(address, port);
		if (error.Length > 0)
		{
			_joinStatusLabel.Text = LocaleText.F("net.error.create_failed", error);
			return;
		}

		// Remember this server for next time.
		NetworkPrefs.AddRecentServer(address, port);
		_joinConfirmButton.Disabled = true;
		_joinStatusLabel.Text = LocaleText.T("net.status.connecting");

		// Fail gracefully instead of hanging on "connecting" forever.
		_awaitingJoin = true;
		SceneTreeTimer timer = GetTree().CreateTimer(12.0);
		timer.Timeout += OnJoinTimeout;
	}

	private void CancelJoining()
	{
		_awaitingJoin = false;
		NetworkManager.Instance?.ResetSession();
		if (_joinDialog != null)
		{
			_joinDialog.Visible = false;
		}
	}

	private void OnJoinTimeout()
	{
		// The MainMenu may already be freed (join succeeded and changed scene).
		if (!GodotObject.IsInstanceValid(this) || !_awaitingJoin)
		{
			return;
		}

		_awaitingJoin = false;
		NetworkManager.Instance?.ResetSession();
		if (_joinDialog != null && _joinDialog.Visible)
		{
			_joinStatusLabel.Text = LocaleText.T("net.error.timeout");
			_joinConfirmButton.Disabled = false;
		}
	}

	private void OnJoinWelcomed()
	{
		_awaitingJoin = false;
		// Seed received from the host — enter the shared world. A dedicated "guest"
		// slot carries this player's own character/progress across servers.
		GameLaunchOptions.ActiveWorldId = "guest";
		if (SaveGameManager.HasWorld("guest"))
		{
			GameLaunchOptions.LoadSavedGame();
		}
		else
		{
			GameLaunchOptions.StartNewGame();
		}

		GetTree().ChangeSceneToFile("res://node_3d.tscn");
	}

	private void OnJoinFailed(string reason)
	{
		_awaitingJoin = false;
		if (_joinDialog != null && _joinDialog.Visible)
		{
			_joinStatusLabel.Text = reason;
			_joinConfirmButton.Disabled = false;
		}
	}

	// ---------------------------------------------------------------- fallback

	private void BuildFallbackMenu(string errorMessage)
	{
		ClearChildren();
		SetAnchorsPreset(LayoutPreset.FullRect);

		var background = new ColorRect
		{
			Color = new Color(0.035f, 0.020f, 0.025f),
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
		};
		AddChild(background);

		var root = new VBoxContainer
		{
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -260.0f,
			OffsetRight = 260.0f,
			OffsetTop = -150.0f,
			OffsetBottom = 150.0f,
		};
		root.AddThemeConstantOverride("separation", 14);
		AddChild(root);

		var title = new Label
		{
			Text = "Shuiling",
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		title.AddThemeFontSizeOverride("font_size", 36);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.76f));
		root.AddChild(title);

		var message = new Label
		{
			Text = $"Startup UI fallback\n{errorMessage}",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		message.AddThemeFontSizeOverride("font_size", 15);
		message.AddThemeColorOverride("font_color", new Color(1.0f, 0.72f, 0.68f));
		root.AddChild(message);

		Button newGameButton = MakeMenuButton("New World");
		newGameButton.Pressed += NewWorld;
		root.AddChild(newGameButton);

		Button quitButton = MakeMenuButton("Quit");
		quitButton.Pressed += () => GetTree().Quit();
		root.AddChild(quitButton);
	}

	private static Button MakeMenuButton(string text)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(260.0f, 48.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 20);
		return button;
	}

	private void ClearChildren()
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}
	}
}
