using Godot;

// Popup shown when another player invites the local player to a party. Self-
// contained: subscribes to the NetworkManager invite event and responds via it.
public partial class PartyInviteDialog : PanelContainer
{
	private Label _messageLabel = null!;
	private long _inviterPeer = -1;

	public override void _Ready()
	{
		BuildPanel();
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.PartyInviteReceived += OnInviteReceived;
		}

		Visible = false;
	}

	public override void _ExitTree()
	{
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.PartyInviteReceived -= OnInviteReceived;
		}
	}

	private void BuildPanel()
	{
		Name = "PartyInviteDialog";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -220.0f;
		OffsetRight = 220.0f;
		OffsetTop = -90.0f;
		OffsetBottom = 90.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.08f, 0.11f, 0.98f),
			BorderColor = new Color(0.6f, 0.9f, 1.0f, 0.85f),
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
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		_messageLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_messageLabel.AddThemeFontSizeOverride("font_size", 17);
		_messageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.78f));
		root.AddChild(_messageLabel);

		var buttons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		buttons.AddThemeConstantOverride("separation", 12);
		root.AddChild(buttons);

		var accept = new Button { Text = LocaleText.T("party.invite.accept"), CustomMinimumSize = new Vector2(0.0f, 42.0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		accept.Pressed += () => Respond(true);
		buttons.AddChild(accept);

		var decline = new Button { Text = LocaleText.T("party.invite.decline"), CustomMinimumSize = new Vector2(0.0f, 42.0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		decline.Pressed += () => Respond(false);
		buttons.AddChild(decline);
	}

	private void OnInviteReceived(long inviterPeer, string inviterName)
	{
		_inviterPeer = inviterPeer;
		_messageLabel.Text = LocaleText.F("party.invite.prompt", inviterName);
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void Respond(bool accept)
	{
		if (_inviterPeer >= 0)
		{
			NetworkManager.Instance?.RespondToPartyInvite(_inviterPeer, accept);
		}

		_inviterPeer = -1;
		Visible = false;
	}
}
