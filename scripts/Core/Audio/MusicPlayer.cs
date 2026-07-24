using Godot;
using System.Collections.Generic;

// Background music (背景音樂). Plays CC0 tracks dropped into
// res://assets/audio/music/city/ and .../wild/. Switches automatically between
// the city playlist and the wild playlist as the player changes maps. Multiple
// tracks rotate randomly; a single track loops.
public partial class MusicPlayer : Node
{
	private const string CityDir = "res://assets/audio/music/city/";
	private const string WildDir = "res://assets/audio/music/wild/";

	private readonly List<string> _cityTracks = new();
	private readonly List<string> _wildTracks = new();
	private readonly RandomNumberGenerator _rng = new();

	private AudioStreamPlayer _player = null!;
	private string _context = string.Empty; // "city" | "wild"

	public override void _Ready()
	{
		_rng.Randomize();
		_player = new AudioStreamPlayer { Name = "BgmPlayer", VolumeDb = -8.0f, Autoplay = false };
		AddChild(_player);
		_player.Finished += OnTrackFinished;

		_cityTracks.AddRange(ScanTracks(CityDir));
		_wildTracks.AddRange(ScanTracks(WildDir));
	}

	// Called on every map change. City maps use the city playlist; every other
	// map uses the wild playlist.
	public void PlayForMap(string mapId)
	{
		string context = mapId == "city" ? "city" : "wild";
		if (context == _context && _player.Playing)
		{
			return;
		}

		_context = context;
		PlayRandomFromContext();
	}

	private void PlayRandomFromContext()
	{
		List<string> list = _context == "city" ? _cityTracks : _wildTracks;
		if (list.Count == 0)
		{
			_player.Stop();
			return;
		}

		string path = list[_rng.RandiRange(0, list.Count - 1)];
		var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
		if (stream == null)
		{
			return;
		}

		// A lone track loops seamlessly; playlists advance on Finished instead.
		SetStreamLoop(stream, list.Count == 1);
		_player.Stream = stream;
		_player.Play();
	}

	private void OnTrackFinished()
	{
		// Only advances when the current stream isn't looping (playlist of 2+).
		PlayRandomFromContext();
	}

	private static void SetStreamLoop(AudioStream stream, bool loop)
	{
		switch (stream)
		{
			case AudioStreamOggVorbis ogg:
				ogg.Loop = loop;
				break;
			case AudioStreamMP3 mp3:
				mp3.Loop = loop;
				break;
			case AudioStreamWav wav:
				wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
				break;
		}
	}

	private static List<string> ScanTracks(string directory)
	{
		var tracks = new List<string>();
		using DirAccess dir = DirAccess.Open(directory);
		if (dir == null)
		{
			return tracks;
		}

		dir.ListDirBegin();
		for (string name = dir.GetNext(); !string.IsNullOrEmpty(name); name = dir.GetNext())
		{
			if (dir.CurrentIsDir())
			{
				continue;
			}

			// In exported builds Godot appends nothing; in the editor audio files
			// sit next to a ".import" companion we must skip.
			string lower = name.ToLowerInvariant();
			if (lower.EndsWith(".ogg") || lower.EndsWith(".mp3") || lower.EndsWith(".wav"))
			{
				tracks.Add(directory + name);
			}
		}

		dir.ListDirEnd();
		return tracks;
	}
}
