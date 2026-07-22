using Godot;

public partial class MainMenu : Control
{
	private Label _saveInfoLabel = null!;
	private Button _loadButton = null!;

	// Multiplayer dialogs (host-authoritative co-op, see NetworkManager.cs).
	private PanelContainer? _hostDialog;
	private PanelContainer? _joinDialog;
	private LineEdit _hostPortEdit = null!;
	private LineEdit _joinAddressEdit = null!;
	private LineEdit _joinPortEdit = null!;
	private Label _hostStatusLabel = null!;
	private Label _joinStatusLabel = null!;
	private Button _hostNewButton = null!;
	private Button _hostLoadButton = null!;
	private Button _hostTestButton = null!;
	private Label _hostDiagLabel = null!;
	private Button _joinConfirmButton = null!;

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
			OffsetLeft = 0.0f,
			OffsetRight = 0.0f,
			OffsetTop = 0.0f,
			OffsetBottom = 0.0f,
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
			OffsetTop = -235.0f,
			OffsetBottom = 235.0f,
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

		_saveInfoLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_saveInfoLabel.AddThemeFontSizeOverride("font_size", 16);
		_saveInfoLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.88f, 1.0f));
		root.AddChild(_saveInfoLabel);

		Button newGameButton = MakeMenuButton(LocaleText.T("main_menu.singleplayer"));
		newGameButton.Pressed += StartNewGame;
		root.AddChild(newGameButton);

		_loadButton = MakeMenuButton(LocaleText.T("main_menu.load_game"));
		_loadButton.Pressed += LoadGame;
		root.AddChild(_loadButton);

		Button hostButton = MakeMenuButton(LocaleText.T("main_menu.host_server"));
		hostButton.Pressed += ShowHostDialog;
		root.AddChild(hostButton);

		Button joinButton = MakeMenuButton(LocaleText.T("main_menu.join_server"));
		joinButton.Pressed += ShowJoinDialog;
		root.AddChild(joinButton);

		Button quitButton = MakeMenuButton(LocaleText.T("main_menu.quit"));
		quitButton.Pressed += () => GetTree().Quit();
		root.AddChild(quitButton);

		BuildHostDialog();
		BuildJoinDialog();
		RefreshSaveInfo();
	}

	// ---------------------------------------------------------------- multiplayer dialogs

	private PanelContainer MakeDialogPanel(string titleKey, out VBoxContainer content)
	{
		var panel = new PanelContainer
		{
			Visible = false,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -230.0f,
			OffsetRight = 230.0f,
			OffsetTop = -235.0f,
			OffsetBottom = 235.0f,
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

		content.AddChild(MakeFieldLabel("net.dialog.port"));
		_hostPortEdit = new LineEdit { Text = NetworkManager.DefaultPort.ToString(), CustomMinimumSize = new Vector2(0.0f, 38.0f) };
		content.AddChild(_hostPortEdit);

		var playerCapLabel = new Label
		{
			Text = LocaleText.F("net.dialog.max_players", NetworkManager.MaxPlayers),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		playerCapLabel.AddThemeFontSizeOverride("font_size", 13);
		playerCapLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.82f));
		content.AddChild(playerCapLabel);

		_hostStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_hostStatusLabel.AddThemeFontSizeOverride("font_size", 14);
		_hostStatusLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.72f, 0.68f));
		content.AddChild(_hostStatusLabel);

		_hostTestButton = MakeMenuButton(LocaleText.T("net.diag.button"));
		_hostTestButton.Pressed += RunNetworkTest;
		content.AddChild(_hostTestButton);

		_hostDiagLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_hostDiagLabel.AddThemeFontSizeOverride("font_size", 13);
		_hostDiagLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.9f, 1.0f));
		content.AddChild(_hostDiagLabel);

		_hostNewButton = MakeMenuButton(LocaleText.T("net.dialog.host_new"));
		_hostNewButton.Pressed += () => StartHosting(false);
		content.AddChild(_hostNewButton);

		_hostLoadButton = MakeMenuButton(LocaleText.T("net.dialog.host_load"));
		_hostLoadButton.Pressed += () => StartHosting(true);
		content.AddChild(_hostLoadButton);

		Button cancel = MakeMenuButton(LocaleText.T("dialog.button.cancel"));
		cancel.Pressed += () => _hostDialog!.Visible = false;
		content.AddChild(cancel);
	}

	private void BuildJoinDialog()
	{
		_joinDialog = MakeDialogPanel("net.dialog.join_title", out VBoxContainer content);

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

	private void ShowHostDialog()
	{
		if (_joinDialog != null)
		{
			_joinDialog.Visible = false;
		}
		if (_hostDialog != null)
		{
			_hostStatusLabel.Text = string.Empty;
			_hostDiagLabel.Text = string.Empty;
			_hostTestButton.Disabled = false;
			_hostLoadButton.Disabled = !SaveGameManager.HasSave();
			_hostDialog.Visible = true;
		}
	}

	private void ShowJoinDialog()
	{
		if (_hostDialog != null)
		{
			_hostDialog.Visible = false;
		}
		if (_joinDialog != null)
		{
			_joinStatusLabel.Text = string.Empty;
			_joinConfirmButton.Disabled = false;
			_joinDialog.Visible = true;
		}
	}

	private static bool TryParsePort(string text, out int port)
	{
		return int.TryParse(text.Trim(), out port) && port >= 1024 && port <= 65535;
	}

	// Runs the listen-server reachability diagnostic. The quick bind test is on
	// the main thread; the blocking UPnP discovery + firewall calls run on a
	// worker and marshal the results back with CallDeferred.
	private void RunNetworkTest()
	{
		if (!TryParsePort(_hostPortEdit.Text, out int port))
		{
			_hostDiagLabel.Text = NetworkDiagnostics.Marker(NetworkDiagnostics.Level.Fail) + LocaleText.T("net.error.invalid_port");
			return;
		}

		_hostTestButton.Disabled = true;
		_hostDiagLabel.Text = LocaleText.T("net.diag.running");

		NetworkDiagnostics.Line bindLine = NetworkDiagnostics.TestPortBind(port);
		System.Threading.Tasks.Task.Run(() =>
		{
			var lines = new System.Collections.Generic.List<NetworkDiagnostics.Line> { bindLine };
			lines.AddRange(NetworkDiagnostics.RunNat(port));
			lines.Add(NetworkDiagnostics.EnsureFirewallRule(port));
			Callable.From(() => ShowDiagnostics(lines)).CallDeferred();
		});
	}

	private void ShowDiagnostics(System.Collections.Generic.List<NetworkDiagnostics.Line> lines)
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

		builder.AppendLine();
		builder.Append(anyFail
			? LocaleText.T("net.diag.result_blocked")
			: anyWarn
				? LocaleText.T("net.diag.result_maybe")
				: LocaleText.T("net.diag.result_reachable"));

		_hostDiagLabel.Text = builder.ToString();
		_hostDiagLabel.AddThemeColorOverride("font_color", anyFail
			? new Color(1.0f, 0.6f, 0.55f)
			: anyWarn
				? new Color(1.0f, 0.85f, 0.5f)
				: new Color(0.6f, 1.0f, 0.7f));
		_hostTestButton.Disabled = false;
	}

	private void StartHosting(bool loadSave)
	{
		if (NetworkManager.Instance == null)
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

		if (loadSave && SaveGameManager.HasSave())
		{
			GameLaunchOptions.LoadSavedGame();
		}
		else
		{
			GameLaunchOptions.StartNewGame();
		}
		GetTree().ChangeSceneToFile("res://node_3d.tscn");
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

		_joinConfirmButton.Disabled = true;
		_joinStatusLabel.Text = LocaleText.T("net.status.connecting");
	}

	private void CancelJoining()
	{
		NetworkManager.Instance?.ResetSession();
		if (_joinDialog != null)
		{
			_joinDialog.Visible = false;
		}
	}

	private void OnJoinWelcomed()
	{
		// Seed received from the host — enter the shared world. The local save
		// (if any) still provides this player's own character/progress.
		if (SaveGameManager.HasSave())
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
		if (_joinDialog != null && _joinDialog.Visible)
		{
			_joinStatusLabel.Text = reason;
			_joinConfirmButton.Disabled = false;
		}
	}

	// ---------------------------------------------------------------- original flows

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

		Button newGameButton = MakeMenuButton("New Game");
		newGameButton.Pressed += StartNewGame;
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

	private void RefreshSaveInfo()
	{
		bool hasSave = SaveGameManager.HasSave();
		_loadButton.Disabled = !hasSave;
		_saveInfoLabel.Text = hasSave
			? LocaleText.F("main_menu.save_found", SaveGameManager.GetSavePath())
			: LocaleText.T("main_menu.no_save");
	}

	private void StartNewGame()
	{
		// New game goes through character creation (model + name) first.
		NetworkManager.Instance?.ResetSession();
		GetTree().ChangeSceneToFile("res://character_select.tscn");
	}

	private void LoadGame()
	{
		if (!SaveGameManager.HasSave())
		{
			RefreshSaveInfo();
			return;
		}

		NetworkManager.Instance?.ResetSession();
		GameLaunchOptions.LoadSavedGame();
		GetTree().ChangeSceneToFile("res://node_3d.tscn");
	}
}
