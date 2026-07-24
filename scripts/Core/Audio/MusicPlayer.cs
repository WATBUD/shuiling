using Godot;
using System.Collections.Generic;

// Background music (背景音樂). Plays CC0 tracks dropped into
// res://assets/audio/music/. City uses city/; each wild map uses its own
// wild/<mapId>/ folder (e.g. wild/wild_forest/), falling back to any files
// placed directly in wild/ when a map has no dedicated track. Multiple tracks
// rotate randomly; a single track loops.
public partial class MusicPlayer : Node
{
	private const string MusicRoot = "res://assets/audio/music/";
	private const string CityDir = MusicRoot + "city/";
	private const string WildDir = MusicRoot + "wild/";

	private readonly List<string> _cityTracks = new();
	private readonly List<string> _wildTracks = new(); // shared fallback (files in wild/)
	private readonly Dictionary<string, List<string>> _mapTracks = new(); // wild/<mapId>/
	private readonly RandomNumberGenerator _rng = new();

	private AudioStreamPlayer _player = null!;
	private string _context = string.Empty;
	private List<string> _currentList = new();

	public override void _Ready()
	{
		AudioSettings.Initialize();
		_rng.Randomize();
		_player = new AudioStreamPlayer { Name = "BgmPlayer", VolumeDb = -8.0f, Autoplay = false, Bus = AudioSettings.MusicBus };
		AddChild(_player);
		_player.Finished += OnTrackFinished;

		_cityTracks.AddRange(ScanTracks(CityDir));
		_wildTracks.AddRange(ScanTracks(WildDir));
		ScanMapFolders();
	}

	// Called on every map change. City → city playlist; each wild map → its own
	// wild/<mapId>/ playlist (else the shared wild/ fallback).
	public void PlayForMap(string mapId)
	{
		string key;
		List<string> list;
		if (mapId == "city")
		{
			key = "city";
			list = _cityTracks;
		}
		else if (_mapTracks.TryGetValue(mapId, out List<string>? mapList) && mapList.Count > 0)
		{
			key = mapId;
			list = mapList;
		}
		else
		{
			key = "wild";
			list = _wildTracks;
		}

		if (key == _context && _player.Playing)
		{
			return;
		}

		_context = key;
		_currentList = list;
		PlayRandom();
	}

	private void PlayRandom()
	{
		if (_currentList.Count == 0)
		{
			_player.Stop();
			return;
		}

		string path = _currentList[_rng.RandiRange(0, _currentList.Count - 1)];
		var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
		if (stream == null)
		{
			return;
		}

		SetStreamLoop(stream, _currentList.Count == 1);
		_player.Stream = stream;
		_player.Play();
	}

	private void OnTrackFinished()
	{
		PlayRandom();
	}

	// Discover wild/<mapId>/ subfolders and index their tracks by folder name.
	private void ScanMapFolders()
	{
		using DirAccess dir = DirAccess.Open(WildDir);
		if (dir == null)
		{
			return;
		}

		dir.ListDirBegin();
		for (string name = dir.GetNext(); !string.IsNullOrEmpty(name); name = dir.GetNext())
		{
			if (!dir.CurrentIsDir() || name is "." or "..")
			{
				continue;
			}

			List<string> tracks = ScanTracks(WildDir + name + "/");
			if (tracks.Count > 0)
			{
				_mapTracks[name] = tracks;
			}
		}

		dir.ListDirEnd();
	}

	public static void SetStreamLoop(AudioStream stream, bool loop)
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

	public static List<string> ScanTracks(string directory)
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
