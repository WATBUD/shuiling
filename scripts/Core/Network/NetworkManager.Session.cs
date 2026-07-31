using Godot;

public partial class NetworkManager : Node
{
	// Returns an empty string on success, otherwise an error description.
	public string CreateServer(int port)
	{
		if (IsOnline)
		{
			ResetSession();
		}

		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateServer(port, MaxPlayers - 1);
		if (error != Error.Ok)
		{
			return error.ToString();
		}

		Multiplayer.MultiplayerPeer = peer;
		Mode = NetMode.Host;
		// Best-effort UPnP auto port-forward so most players can host without
		// manual router setup (off-thread; failures fall back to manual/relay).
		int forwardPort = port;
		System.Threading.Tasks.Task.Run(() => NetworkDiagnostics.TryOpenPort(forwardPort));
		WorldSeed = (int)(GD.Randi() % int.MaxValue);
		if (WorldSeed == 0)
		{
			WorldSeed = 1;
		}
		_playerNames[1] = LocalPlayerName;
		return string.Empty;
	}

	// Host-only: use an existing world's saved seed so a hosted world matches its
	// single-player layout. Call after CreateServer, before entering the world.
	public void OverrideWorldSeed(int seed)
	{
		if (IsHost && seed != 0)
		{
			WorldSeed = seed;
		}
	}

	public string JoinServer(string address, int port)
	{
		if (IsOnline)
		{
			ResetSession();
		}

		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateClient(address, port);
		if (error != Error.Ok)
		{
			return error.ToString();
		}

		Multiplayer.MultiplayerPeer = peer;
		Mode = NetMode.Client;
		return string.Empty;
	}

	public void ResetSession()
	{
		if (Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer)
		{
			Multiplayer.MultiplayerPeer.Close();
		}
		Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		Mode = NetMode.Offline;
		WorldSeed = 0;
		_playerNames.Clear();
		_playerModels.Clear();
		_leaderOf.Clear();
		SetLocalPartyMirror(System.Array.Empty<long>(), System.Array.Empty<string>(), -1);
		ClearPlayerPuppets();
	}

	private void OnPeerConnected(long peerId)
	{
		// Everyone introduces themselves to the newcomer directly (name + model).
		RpcId(peerId, MethodName.ReceivePlayerName, LocalPlayerName);
		RpcId(peerId, MethodName.ReceivePlayerModel, LocalPlayerModelPath);
	}

	private void OnPeerDisconnected(long peerId)
	{
		string name = GetPlayerName(peerId);
		_playerNames.Remove(peerId);
		_playerModels.Remove(peerId);
		if (_playerPuppets.TryGetValue(peerId, out RemotePlayerPuppet? puppet))
		{
			if (IsInstanceValid(puppet))
			{
				puppet.QueueFree();
			}
			_playerPuppets.Remove(peerId);
		}
		RemoveCompanionPuppetsFor(peerId);
		HandlePartyDisconnect(peerId);
		PostWorldMessage(LocaleText.F("system.net.player_left", name), new Color(1.0f, 0.72f, 0.5f));
	}

	private void OnConnectedToServer()
	{
		RpcId(1, MethodName.ServerReceiveHello, LocalPlayerName);
	}

	private void OnConnectionFailed()
	{
		ResetSession();
		JoinFailed?.Invoke(LocaleText.T("net.error.connect_failed"));
	}

	private void OnServerDisconnected()
	{
		bool wasInWorld = ActiveWorld != null && IsInstanceValid(ActiveWorld);
		ResetSession();
		if (wasInWorld)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			if (ServerConnectionLost != null)
			{
				// In-world UI shows a notice, then returns to the menu on confirm.
				ServerConnectionLost.Invoke();
			}
			else
			{
				CallDeferred(MethodName.ReturnClientToMainMenu);
			}
		}
		else
		{
			JoinFailed?.Invoke(LocaleText.T("net.error.connect_failed"));
		}
	}

	private void ReturnClientToMainMenu()
	{
		if (IsInstanceValid(this) && GetTree() != null)
		{
			GetTree().ChangeSceneToFile("res://main_menu.tscn");
		}
	}
}
