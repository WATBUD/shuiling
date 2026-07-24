using Godot;
using System.Collections.Generic;

// Free party (自由組隊): lists players currently in the main city and lets you
// invite them, plus shows your current party members. Multiplayer-only.
public partial class PartyInvitePanel : PanelContainer
{
	private Label _titleLabel = null!;
	private Label _membersLabel = null!;
	private Label _emptyLabel = null!;
	private VBoxContainer _cityList = null!;
	private Button _leaveButton = null!;

	public System.Action? CloseRequested { get; set; }

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += RefreshAll;
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.PartyChanged += RefreshAll;
		}

		SetPanelVisible(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshAll;
		if (NetworkManager.Instance != null)
		{
			NetworkManager.Instance.PartyChanged -= RefreshAll;
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshAll();
		}
	}

	private void BuildPanel()
	{
		Name = "PartyInvitePanel";
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -320.0f;
		OffsetRight = 320.0f;
		OffsetTop = -260.0f;
		OffsetBottom = 260.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.09f, 0.96f),
			BorderColor = new Color(0.5f, 0.82f, 1.0f, 0.78f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		AddChild(margin);

		var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 8);
		margin.AddChild(root);

		_titleLabel = new Label { Text = LocaleText.T("party.mp.title"), HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 24);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.94f, 1.0f));
		root.AddChild(_titleLabel);

		_membersLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_membersLabel.AddThemeFontSizeOverride("font_size", 14);
		_membersLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.68f));
		root.AddChild(_membersLabel);

		var hint = new Label { Text = LocaleText.T("party.mp.hint"), HorizontalAlignment = HorizontalAlignment.Center };
		hint.AddThemeFontSizeOverride("font_size", 13);
		hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.92f));
		root.AddChild(hint);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(590.0f, 320.0f),
		};
		root.AddChild(scroll);

		_cityList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_cityList.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_cityList);

		_emptyLabel = new Label { Text = LocaleText.T("party.mp.no_players"), HorizontalAlignment = HorizontalAlignment.Center };
		_emptyLabel.AddThemeFontSizeOverride("font_size", 14);
		_emptyLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.80f));
		root.AddChild(_emptyLabel);

		_leaveButton = new Button { Text = LocaleText.T("party.mp.leave"), CustomMinimumSize = new Vector2(0.0f, 38.0f), Visible = false };
		_leaveButton.Pressed += () => NetworkManager.Instance?.LeaveParty();
		root.AddChild(_leaveButton);

		var closeButton = new Button { Text = LocaleText.T("dialog.button.close"), CustomMinimumSize = new Vector2(0.0f, 40.0f) };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	public void RefreshAll()
	{
		if (_cityList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("party.mp.title");

		NetworkManager? net = NetworkManager.Instance;
		IReadOnlyList<string> members = net?.LocalPartyNames ?? new List<string>();
		if (members.Count > 0)
		{
			// Leader is first; mark it with a star.
			var decorated = new List<string>();
			for (int i = 0; i < members.Count; i++)
			{
				decorated.Add(i == 0 ? "★" + members[i] : members[i]);
			}

			_membersLabel.Text = LocaleText.F("party.mp.members", string.Join("、", decorated));
		}
		else
		{
			_membersLabel.Text = LocaleText.T("party.mp.no_party");
		}

		_leaveButton.Visible = net != null && net.LocalInParty;

		ClearChildren(_cityList);

		if (net == null || !net.IsOnline)
		{
			_emptyLabel.Text = LocaleText.T("party.mp.offline");
			_emptyLabel.Visible = true;
			return;
		}

		// Non-leader members can't invite until they leave their party.
		bool canInvite = net.CanInviteToParty;
		int shown = 0;
		foreach (NetworkManager.ConnectedPlayer player in net.GetConnectedPlayers())
		{
			if (player.IsLocal || player.MapId != "city")
			{
				continue;
			}

			_cityList.AddChild(BuildInviteRow(player, canInvite));
			shown++;
		}

		if (shown == 0)
		{
			_emptyLabel.Text = LocaleText.T("party.mp.no_players");
			_emptyLabel.Visible = true;
		}
		else if (!canInvite)
		{
			_emptyLabel.Text = LocaleText.T("party.mp.leader_only");
			_emptyLabel.Visible = true;
		}
		else
		{
			_emptyLabel.Visible = false;
		}
	}

	private Control BuildInviteRow(NetworkManager.ConnectedPlayer player, bool canInvite)
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var rowStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.09f, 0.12f, 0.16f, 0.95f),
			BorderColor = new Color(0.4f, 0.6f, 0.82f, 0.5f),
		};
		rowStyle.SetBorderWidthAll(1);
		rowStyle.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", rowStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		row.AddChild(margin);

		var line = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		line.AddThemeConstantOverride("separation", 8);
		margin.AddChild(line);

		var name = new Label { Text = player.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
		name.AddThemeFontSizeOverride("font_size", 16);
		name.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1.0f));
		line.AddChild(name);

		long targetPeer = player.PeerId;
		var invite = new Button
		{
			Text = LocaleText.T("party.mp.invite"),
			CustomMinimumSize = new Vector2(120.0f, 36.0f),
			Disabled = !canInvite,
			TooltipText = canInvite ? string.Empty : LocaleText.T("party.mp.leader_only"),
		};
		invite.Pressed += () => NetworkManager.Instance?.InvitePlayerToParty(targetPeer);
		line.AddChild(invite);

		return row;
	}

	private static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}
}
