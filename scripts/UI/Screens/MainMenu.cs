using Godot;

public partial class MainMenu : Control
{
	private Label _saveInfoLabel = null!;
	private Button _loadButton = null!;

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
			OffsetTop = -170.0f,
			OffsetBottom = 170.0f,
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

		Button newGameButton = MakeMenuButton(LocaleText.T("main_menu.new_game"));
		newGameButton.Pressed += StartNewGame;
		root.AddChild(newGameButton);

		_loadButton = MakeMenuButton(LocaleText.T("main_menu.load_game"));
		_loadButton.Pressed += LoadGame;
		root.AddChild(_loadButton);

		Button quitButton = MakeMenuButton(LocaleText.T("main_menu.quit"));
		quitButton.Pressed += () => GetTree().Quit();
		root.AddChild(quitButton);

		RefreshSaveInfo();
	}

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
		GameLaunchOptions.StartNewGame();
		GetTree().ChangeSceneToFile("res://node_3d.tscn");
	}

	private void LoadGame()
	{
		if (!SaveGameManager.HasSave())
		{
			RefreshSaveInfo();
			return;
		}

		GameLaunchOptions.LoadSavedGame();
		GetTree().ChangeSceneToFile("res://node_3d.tscn");
	}
}
