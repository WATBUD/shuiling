using Godot;
using System.Collections.Generic;

// Top-right HUD list of everyone in the multiplayer session and the map/tier
// each player is currently in. Hidden entirely in single-player. Polls the
// NetworkManager a couple of times a second.
public partial class MultiplayerPartyHud : PanelContainer
{
	private const float RefreshInterval = 0.5f;

	private VBoxContainer _rows = null!;
	private Label _title = null!;
	private float _refreshRemaining;

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += Refresh;
		Visible = false;
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= Refresh;
	}

	public override void _Process(double delta)
	{
		_refreshRemaining -= (float)delta;
		if (_refreshRemaining > 0.0f)
		{
			return;
		}

		_refreshRemaining = RefreshInterval;
		Refresh();
	}

	private void BuildPanel()
	{
		Name = "MultiplayerPartyHud";
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = 1.0f;
		AnchorRight = 1.0f;
		AnchorTop = 0.0f;
		AnchorBottom = 0.0f;
		OffsetLeft = -290.0f;
		OffsetRight = -18.0f;
		OffsetTop = 18.0f;
		OffsetBottom = 18.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.045f, 0.06f, 0.78f),
			BorderColor = new Color(0.5f, 0.72f, 0.95f, 0.6f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 4);
		margin.AddChild(root);

		_title = new Label { Text = LocaleText.T("net.party.title") };
		_title.AddThemeFontSizeOverride("font_size", 14);
		_title.AddThemeColorOverride("font_color", new Color(0.72f, 0.88f, 1.0f));
		root.AddChild(_title);

		_rows = new VBoxContainer();
		_rows.AddThemeConstantOverride("separation", 2);
		root.AddChild(_rows);
	}

	private void Refresh()
	{
		if (_rows == null)
		{
			return;
		}

		NetworkManager? net = NetworkManager.Instance;
		if (net == null || !net.IsOnline)
		{
			Visible = false;
			return;
		}

		List<NetworkManager.ConnectedPlayer> players = net.GetConnectedPlayers();
		Visible = players.Count > 0;
		_title.Text = LocaleText.F("net.party.title_count", players.Count);

		ClearRows();
		World? world = net.ActiveWorld;
		foreach (NetworkManager.ConnectedPlayer player in players)
		{
			string mapName = world != null && IsInstanceValid(world) ? world.GetMapDisplayName(player.MapId) : player.MapId;
			string location = player.MapId == "city" ? mapName : LocaleText.F("net.party.location_tier", mapName, player.Tier);

			var row = new Label
			{
				Text = LocaleText.F("net.party.row", player.Name, location),
			};
			row.AddThemeFontSizeOverride("font_size", 13);
			row.AddThemeColorOverride("font_color", player.IsLocal ? new Color(1.0f, 0.94f, 0.66f) : new Color(0.86f, 0.92f, 1.0f));
			_rows.AddChild(row);
		}
	}

	private void ClearRows()
	{
		foreach (Node child in _rows.GetChildren())
		{
			_rows.RemoveChild(child);
			child.QueueFree();
		}
	}
}
