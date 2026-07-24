using Godot;

// Shown to a client when the host closes the room / the connection drops. The
// player acknowledges, then returns to the main menu (instead of a silent bounce).
public partial class DisconnectNotice : PanelContainer
{
	public override void _Ready()
	{
		BuildPanel();
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.ServerConnectionLost += OnConnectionLost;
		}

		Visible = false;
	}

	public override void _ExitTree()
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.ServerConnectionLost -= OnConnectionLost;
		}
	}

	private void BuildPanel()
	{
		Name = "DisconnectNotice";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -230.0f;
		OffsetRight = 230.0f;
		OffsetTop = -100.0f;
		OffsetBottom = 100.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.09f, 0.06f, 0.07f, 0.98f),
			BorderColor = new Color(1.0f, 0.6f, 0.5f, 0.9f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 14);
		margin.AddChild(root);

		var title = new Label { Text = LocaleText.T("net.disconnected.title"), HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 20);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.8f, 0.72f));
		root.AddChild(title);

		var body = new Label { Text = LocaleText.T("net.disconnected.body"), HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		body.AddThemeFontSizeOverride("font_size", 15);
		body.AddThemeColorOverride("font_color", new Color(0.9f, 0.92f, 0.96f));
		root.AddChild(body);

		var ok = new Button { Text = LocaleText.T("net.disconnected.ok"), CustomMinimumSize = new Vector2(0.0f, 42.0f) };
		ok.Pressed += ReturnToMenu;
		root.AddChild(ok);
	}

	private void OnConnectionLost()
	{
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void ReturnToMenu()
	{
		Visible = false;
		GetTree().ChangeSceneToFile("res://main_menu.tscn");
	}
}
