using Godot;
using System.Collections.Generic;

// Free party (自由組隊). Two areas: the top lists current teammates, the bottom
// lists other players in the main city you can invite. When an invite is
// accepted the player moves from the bottom area up into the teammate area.
public partial class PartyInvitePanel : PanelContainer
{
	private Label _titleLabel = null!;
	private Label _teamHeader = null!;
	private Label _teamEmpty = null!;
	private VBoxContainer _teamList = null!;
	private Label _inviteHeader = null!;
	private Label _inviteEmpty = null!;
	private VBoxContainer _inviteList = null!;
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
		OffsetTop = -280.0f;
		OffsetBottom = 280.0f;

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

		var hint = new Label { Text = LocaleText.T("party.mp.hint"), HorizontalAlignment = HorizontalAlignment.Center };
		hint.AddThemeFontSizeOverride("font_size", 13);
		hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.92f));
		root.AddChild(hint);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(590.0f, 360.0f),
		};
		root.AddChild(scroll);

		var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		content.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(content);

		// --- Teammate area (top) ---
		_teamHeader = MakeHeader("party.mp.team_header", new Color(1.0f, 0.92f, 0.68f));
		content.AddChild(_teamHeader);
		_teamList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_teamList.AddThemeConstantOverride("separation", 5);
		content.AddChild(_teamList);
		_teamEmpty = MakeNote("party.mp.no_party");
		content.AddChild(_teamEmpty);

		content.AddChild(new HSeparator());

		// --- Invitable area (bottom) ---
		_inviteHeader = MakeHeader("party.mp.invite_header", new Color(0.72f, 0.88f, 1.0f));
		content.AddChild(_inviteHeader);
		_inviteList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_inviteList.AddThemeConstantOverride("separation", 5);
		content.AddChild(_inviteList);
		_inviteEmpty = MakeNote("party.mp.no_players");
		content.AddChild(_inviteEmpty);

		_leaveButton = new Button { Text = LocaleText.T("party.mp.leave"), CustomMinimumSize = new Vector2(0.0f, 38.0f), Visible = false };
		_leaveButton.Pressed += () => NetworkManager.Instance?.LeaveParty();
		root.AddChild(_leaveButton);

		var closeButton = new Button { Text = LocaleText.T("dialog.button.close"), CustomMinimumSize = new Vector2(0.0f, 40.0f) };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	public void RefreshAll()
	{
		if (_teamList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("party.mp.title");
		_teamHeader.Text = LocaleText.T("party.mp.team_header");
		_inviteHeader.Text = LocaleText.T("party.mp.invite_header");
		ClearChildren(_teamList);
		ClearChildren(_inviteList);

		NetworkManager? net = NetworkManager.Instance;
		if (net == null || !net.IsOnline)
		{
			_teamEmpty.Text = LocaleText.T("party.mp.offline");
			_teamEmpty.Visible = true;
			_inviteEmpty.Visible = false;
			_leaveButton.Visible = false;
			return;
		}

		// Teammate area.
		IReadOnlyList<string> memberNames = net.LocalPartyNames;
		IReadOnlyList<long> memberPeers = net.LocalPartyPeers;
		long selfPeer = net.LocalPeerId;
		for (int i = 0; i < memberNames.Count; i++)
		{
			bool isLeader = i == 0; // leader is broadcast first
			bool isSelf = i < memberPeers.Count && memberPeers[i] == selfPeer;
			_teamList.AddChild(BuildTeamRow(memberNames[i], isLeader, isSelf));
		}

		_teamEmpty.Text = LocaleText.T("party.mp.no_party");
		_teamEmpty.Visible = memberNames.Count == 0;
		_leaveButton.Visible = net.LocalInParty;

		// Invitable area: city players who aren't already teammates.
		bool canInvite = net.CanInviteToParty;
		var partyPeers = new HashSet<long>(memberPeers);
		int invitable = 0;
		foreach (NetworkManager.ConnectedPlayer player in net.GetConnectedPlayers())
		{
			if (player.IsLocal || player.MapId != "city" || partyPeers.Contains(player.PeerId))
			{
				continue;
			}

			_inviteList.AddChild(BuildInviteRow(player, canInvite));
			invitable++;
		}

		if (invitable == 0)
		{
			_inviteEmpty.Text = LocaleText.T("party.mp.no_players");
			_inviteEmpty.Visible = true;
		}
		else if (!canInvite)
		{
			_inviteEmpty.Text = LocaleText.T("party.mp.leader_only");
			_inviteEmpty.Visible = true;
		}
		else
		{
			_inviteEmpty.Visible = false;
		}
	}

	private Control BuildTeamRow(string name, bool isLeader, bool isSelf)
	{
		var row = MakeRowPanel(new Color(0.10f, 0.16f, 0.12f, 0.95f), new Color(0.5f, 0.85f, 0.6f, 0.6f), out HBoxContainer line);

		string label = isLeader ? "★" + name : name;
		if (isSelf)
		{
			label += " " + LocaleText.T("party.mp.you");
		}

		var nameLabel = new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
		nameLabel.AddThemeFontSizeOverride("font_size", 16);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.86f, 1.0f, 0.9f));
		line.AddChild(nameLabel);

		var tag = new Label { Text = LocaleText.T(isLeader ? "party.mp.leader_tag" : "party.mp.teammate"), VerticalAlignment = VerticalAlignment.Center };
		tag.AddThemeFontSizeOverride("font_size", 13);
		tag.AddThemeColorOverride("font_color", new Color(0.7f, 1.0f, 0.78f));
		line.AddChild(tag);

		return row;
	}

	private Control BuildInviteRow(NetworkManager.ConnectedPlayer player, bool canInvite)
	{
		var row = MakeRowPanel(new Color(0.09f, 0.12f, 0.16f, 0.95f), new Color(0.4f, 0.6f, 0.82f, 0.5f), out HBoxContainer line);

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

	private static PanelContainer MakeRowPanel(Color bg, Color border, out HBoxContainer line)
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var rowStyle = new StyleBoxFlat { BgColor = bg, BorderColor = border };
		rowStyle.SetBorderWidthAll(1);
		rowStyle.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", rowStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		row.AddChild(margin);

		line = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		line.AddThemeConstantOverride("separation", 8);
		margin.AddChild(line);
		return row;
	}

	private static Label MakeHeader(string key, Color color)
	{
		var label = new Label { Text = LocaleText.T(key) };
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static Label MakeNote(string key)
	{
		var label = new Label { Text = LocaleText.T(key), HorizontalAlignment = HorizontalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.80f));
		return label;
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
