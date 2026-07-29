using Godot;

// One persistent player shared by every screen before entering the game world.
// Keeping it under SceneTree.Root prevents menu -> character-select transitions
// from restarting or layering the same song.
public static class PreGameMusic
{
	public const string TrackPath = "res://assets/audio/music/menu/pre_game_theme.mp3";
	private const string PlayerName = "PreGameMusic";
	private const float StartOffsetSeconds = 4.0f;
	private static AudioStreamPlayer? _pendingPlayer;

	public static void Start(Node context)
	{
		if (context == null || !GodotObject.IsInstanceValid(context))
		{
			return;
		}

		AudioSettings.Initialize();
		Node root = context.GetTree().Root;
		AudioStreamPlayer? player = root.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
		if (player != null)
		{
			if (!player.Playing)
			{
				player.Play(StartOffsetSeconds);
			}
			return;
		}
		if (_pendingPlayer != null && GodotObject.IsInstanceValid(_pendingPlayer))
		{
			return;
		}

		if (!ResourceLoader.Exists(TrackPath))
		{
			GD.PushWarning($"Pre-game music not found: {TrackPath}");
			return;
		}

		AudioStream? stream = GD.Load<AudioStream>(TrackPath);
		if (stream == null)
		{
			GD.PushWarning($"Unable to load pre-game music: {TrackPath}");
			return;
		}

		MusicPlayer.SetStreamLoop(stream, true);
		if (stream is AudioStreamMP3 mp3)
		{
			mp3.LoopOffset = StartOffsetSeconds;
		}
		player = new AudioStreamPlayer
		{
			Name = PlayerName,
			Bus = AudioSettings.MusicBus,
			VolumeDb = -8.0f,
			Stream = stream,
		};
		_pendingPlayer = player;
		player.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(player))
			{
				player.Play(StartOffsetSeconds);
			}
		};
		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(root) || !GodotObject.IsInstanceValid(player))
			{
				_pendingPlayer = null;
				return;
			}

			AudioStreamPlayer? existing = root.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
			if (existing != null)
			{
				player.Free();
				_pendingPlayer = null;
				return;
			}

			root.AddChild(player);
			_pendingPlayer = null;
			player.Play(StartOffsetSeconds);
		}).CallDeferred();
	}

	public static void Stop(Node context)
	{
		if (context == null || !GodotObject.IsInstanceValid(context))
		{
			return;
		}

		AudioStreamPlayer? player = context.GetTree().Root.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
		if (player == null)
		{
			if (_pendingPlayer != null && GodotObject.IsInstanceValid(_pendingPlayer))
			{
				_pendingPlayer.Free();
				_pendingPlayer = null;
			}
			return;
		}

		player.Stop();
		player.QueueFree();
	}
}
