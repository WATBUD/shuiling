using Godot;
using System.Collections.Generic;

// Background music (背景音樂). Main city and Emerald Hunting Grove use distinct
// generated themes so their musical identities never depend on the same asset.
// Other maps play CC0 tracks from wild/<mapId>/ and fall back to wild/.
// Map changes use a two-player crossfade instead of abruptly cutting tracks.
public partial class MusicPlayer : Node
{
	private const string MusicRoot = "res://assets/audio/music/";
	private const string CityDir = MusicRoot + "city/";
	private const string WildDir = MusicRoot + "wild/";

	private readonly List<string> _cityTracks = new();
	private readonly List<string> _wildTracks = new(); // shared fallback (files in wild/)
	private readonly Dictionary<string, List<string>> _mapTracks = new(); // wild/<mapId>/
	private readonly RandomNumberGenerator _rng = new();

	private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[2];
	private AudioStreamWav _cityTheme = null!;
	private AudioStreamWav _forestTheme = null!;
	private Tween? _crossfadeTween;
	private int _activePlayerIndex = -1;
	private string _context = string.Empty;
	private List<string> _currentList = new();
	public string CurrentContext => _context;

	public override void _Ready()
	{
		AudioSettings.Initialize();
		_rng.Randomize();
		for (int index = 0; index < _players.Length; index++)
		{
			int capturedIndex = index;
			_players[index] = new AudioStreamPlayer
			{
				Name = $"BgmPlayer{index + 1}",
				VolumeDb = -40.0f,
				Autoplay = false,
				Bus = AudioSettings.MusicBus,
			};
			AddChild(_players[index]);
			_players[index].Finished += () => OnTrackFinished(capturedIndex);
		}

		_cityTracks.AddRange(ScanTracks(CityDir));
		_wildTracks.AddRange(ScanTracks(WildDir));
		ScanMapFolders();
		_cityTheme = CreateCityTheme();
		_forestTheme = CreateForestTheme();
	}

	// Called on every map change. City → city playlist; each wild map → its own
	// wild/<mapId>/ playlist (else the shared wild/ fallback).
	public void PlayForMap(string mapId)
	{
		string key;
		List<string> list;
		AudioStream? dedicatedStream = null;
		if (mapId == "city")
		{
			key = "city";
			list = new List<string>();
			dedicatedStream = _cityTheme;
		}
		else if (mapId == "wild_forest")
		{
			key = "wild_forest";
			list = new List<string>();
			dedicatedStream = _forestTheme;
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

		if (key == _context && _activePlayerIndex >= 0 && _players[_activePlayerIndex].Playing)
		{
			return;
		}

		_context = key;
		_currentList = list;
		if (dedicatedStream != null)
		{
			SwitchToStream(dedicatedStream);
		}
		else
		{
			PlayRandom();
		}
	}

	private void PlayRandom()
	{
		if (_currentList.Count == 0)
		{
			StopWithFade();
			return;
		}

		string path = _currentList[_rng.RandiRange(0, _currentList.Count - 1)];
		var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
		if (stream == null)
		{
			return;
		}

		SetStreamLoop(stream, _currentList.Count == 1);
		SwitchToStream(stream);
	}

	private void OnTrackFinished(int playerIndex)
	{
		if (playerIndex == _activePlayerIndex && _currentList.Count > 1)
		{
			PlayRandom();
		}
	}

	private void SwitchToStream(AudioStream stream)
	{
		int nextIndex = _activePlayerIndex < 0 ? 0 : 1 - _activePlayerIndex;
		AudioStreamPlayer next = _players[nextIndex];
		AudioStreamPlayer? previous = _activePlayerIndex >= 0 ? _players[_activePlayerIndex] : null;
		_crossfadeTween?.Kill();
		next.Stop();
		next.Stream = stream;
		next.VolumeDb = -40.0f;
		next.Play();
		_activePlayerIndex = nextIndex;

		_crossfadeTween = CreateTween();
		_crossfadeTween.SetParallel(true);
		_crossfadeTween.TweenProperty(next, "volume_db", -8.0f, 1.15f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
		if (previous != null && previous.Playing)
		{
			_crossfadeTween.TweenProperty(previous, "volume_db", -40.0f, 1.15f)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.In);
			_crossfadeTween.SetParallel(false);
			_crossfadeTween.TweenCallback(Callable.From(() =>
			{
				if (previous != next)
				{
					previous.Stop();
				}
			}));
		}
	}

	private void StopWithFade()
	{
		if (_activePlayerIndex < 0)
		{
			return;
		}
		AudioStreamPlayer active = _players[_activePlayerIndex];
		_crossfadeTween?.Kill();
		_crossfadeTween = CreateTween();
		_crossfadeTween.TweenProperty(active, "volume_db", -40.0f, 0.7f);
		_crossfadeTween.TweenCallback(Callable.From(active.Stop));
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

	private static AudioStreamWav CreateCityTheme()
	{
		const int mixRate = 22050;
		const float bpm = 124.0f;
		const int beatCount = 16;
		float beatDuration = 60.0f / bpm;
		float duration = beatDuration * beatCount;
		int frameCount = Mathf.RoundToInt(duration * mixRate);
		byte[] data = new byte[frameCount * 4];
		int[] melody =
		{
			72, 76, 79, 76, 74, 77, 81, 79,
			76, 79, 84, 81, 77, 76, 74, 79,
			72, 76, 79, 84, 83, 81, 79, 76,
			77, 81, 84, 81, 79, 77, 76, 72,
		};
		int[][] chords =
		{
			new[] { 60, 64, 67, 72 },
			new[] { 55, 59, 62, 67 },
			new[] { 57, 60, 64, 69 },
			new[] { 53, 57, 60, 65 },
		};
		var noise = new RandomNumberGenerator { Seed = 0xC17_2026 };

		for (int frame = 0; frame < frameCount; frame++)
		{
			float time = frame / (float)mixRate;
			float beatPosition = time / beatDuration;
			int beat = Mathf.FloorToInt(beatPosition) % beatCount;
			float beatPhase = beatPosition - Mathf.Floor(beatPosition);
			float eighthPosition = beatPosition * 2.0f;
			int eighth = Mathf.FloorToInt(eighthPosition) % melody.Length;
			float eighthPhase = eighthPosition - Mathf.Floor(eighthPosition);
			int chordIndex = (beat / 4) % chords.Length;
			int[] chord = chords[chordIndex];

			float melodyEnvelope = FastPluckEnvelope(eighthPhase, 4.2f);
			float melodyFrequency = MidiFrequency(melody[eighth]);
			float melodyPhase = Mathf.Tau * melodyFrequency * time;
			float bell = (Mathf.Sin(melodyPhase) + 0.34f * Mathf.Sin(melodyPhase * 2.0f) + 0.12f * Mathf.Sin(melodyPhase * 3.01f))
				* melodyEnvelope * 0.105f;

			int arpNote = chord[(eighth + chordIndex) % chord.Length] + (eighth % 4 == 3 ? 12 : 0);
			float arpPhase = Mathf.Tau * MidiFrequency(arpNote) * time;
			float marimba = (Mathf.Sin(arpPhase) + 0.22f * Mathf.Sin(arpPhase * 2.0f))
				* FastPluckEnvelope(eighthPhase, 6.5f) * 0.072f;

			float bassFrequency = MidiFrequency(chord[0] - 24);
			float bassPhase = Mathf.Tau * bassFrequency * time;
			float bassEnvelope = Mathf.Min(beatPhase * 14.0f, 1.0f) * Mathf.Exp(-beatPhase * 1.4f);
			float bass = (Mathf.Sin(bassPhase) + 0.18f * Mathf.Sin(bassPhase * 2.0f)) * bassEnvelope * 0.14f;

			float kick = 0.0f;
			if (beat % 2 == 0)
			{
				float kickEnvelope = Mathf.Exp(-beatPhase * 15.0f);
				float kickFrequency = 48.0f + 62.0f * Mathf.Exp(-beatPhase * 20.0f);
				kick = Mathf.Sin(Mathf.Tau * kickFrequency * time) * kickEnvelope * 0.21f;
			}
			float clap = 0.0f;
			if (beat % 4 is 1 or 3)
			{
				clap = noise.RandfRange(-1.0f, 1.0f) * Mathf.Exp(-beatPhase * 26.0f) * 0.055f;
			}
			float hat = noise.RandfRange(-1.0f, 1.0f) * Mathf.Exp(-eighthPhase * 34.0f) * 0.025f;
			float master = LoopEdgeEnvelope(time, duration);
			float left = (bell * 0.90f + marimba * 1.08f + bass + kick + clap * 0.86f + hat) * master;
			float right = (bell * 1.08f + marimba * 0.90f + bass + kick + clap + hat * 0.86f) * master;
			WritePcm16(data, frame * 4, Mathf.Clamp(left, -0.92f, 0.92f));
			WritePcm16(data, frame * 4 + 2, Mathf.Clamp(right, -0.92f, 0.92f));
		}

		return CreateLoopingWav(data, mixRate, frameCount);
	}

	private static AudioStreamWav CreateForestTheme()
	{
		const int mixRate = 22050;
		const float bpm = 92.0f;
		const int beatCount = 16;
		float beatDuration = 60.0f / bpm;
		float duration = beatDuration * beatCount;
		int frameCount = Mathf.RoundToInt(duration * mixRate);
		byte[] data = new byte[frameCount * 4];
		int[] fluteMelody = { 62, 65, 67, 69, 72, 69, 67, 65, 62, 67, 69, 74, 72, 69, 67, 65 };
		int[] roots = { 50, 48, 53, 55 };
		var noise = new RandomNumberGenerator { Seed = 0xF075_2026 };
		float filteredWind = 0.0f;

		for (int frame = 0; frame < frameCount; frame++)
		{
			float time = frame / (float)mixRate;
			float beatPosition = time / beatDuration;
			int beat = Mathf.FloorToInt(beatPosition) % beatCount;
			float beatPhase = beatPosition - Mathf.Floor(beatPosition);
			int phrase = (beat / 4) % roots.Length;

			float fluteFrequency = MidiFrequency(fluteMelody[beat]);
			float flutePhase = Mathf.Tau * fluteFrequency * time
				+ 0.014f * Mathf.Sin(Mathf.Tau * 5.1f * time);
			float fluteEnvelope = Mathf.Sin(Mathf.Clamp(beatPhase * 1.25f, 0.0f, 1.0f) * Mathf.Pi)
				* Mathf.Exp(-beatPhase * 0.72f);
			float flute = (Mathf.Sin(flutePhase) + 0.16f * Mathf.Sin(flutePhase * 2.0f))
				* fluteEnvelope * 0.082f;

			float rootFrequency = MidiFrequency(roots[phrase] - 12);
			float dronePhase = Mathf.Tau * rootFrequency * time;
			float breathing = 0.68f + 0.20f * Mathf.Sin(Mathf.Tau * time / (beatDuration * 4.0f));
			float drone = (Mathf.Sin(dronePhase) + 0.30f * Mathf.Sin(dronePhase * 1.5f))
				* breathing * 0.075f;

			float pluckFrequency = MidiFrequency(fluteMelody[(beat + 5) % fluteMelody.Length] - 12);
			float pluckPhase = Mathf.Tau * pluckFrequency * time;
			float woodPluck = Mathf.Sin(pluckPhase) * FastPluckEnvelope(beatPhase, 5.8f) * 0.052f;

			float woodClick = 0.0f;
			if (beat % 2 == 0)
			{
				float clickTone = Mathf.Sin(Mathf.Tau * 185.0f * time) + noise.RandfRange(-0.45f, 0.45f);
				woodClick = clickTone * Mathf.Exp(-beatPhase * 30.0f) * 0.032f;
			}

			float rawWind = noise.RandfRange(-1.0f, 1.0f);
			filteredWind = Mathf.Lerp(filteredWind, rawWind, 0.0045f);
			float wind = filteredWind * (0.48f + 0.24f * Mathf.Sin(Mathf.Tau * 0.09f * time)) * 0.035f;
			float master = LoopEdgeEnvelope(time, duration);
			float pan = Mathf.Sin(Mathf.Tau * time / (beatDuration * 8.0f)) * 0.12f;
			float left = (flute * (0.94f - pan) + drone + woodPluck * 1.08f + woodClick + wind) * master;
			float right = (flute * (0.94f + pan) + drone + woodPluck * 0.88f + woodClick * 0.86f + wind) * master;
			WritePcm16(data, frame * 4, Mathf.Clamp(left, -0.88f, 0.88f));
			WritePcm16(data, frame * 4 + 2, Mathf.Clamp(right, -0.88f, 0.88f));
		}

		return CreateLoopingWav(data, mixRate, frameCount);
	}

	private static AudioStreamWav CreateLoopingWav(byte[] data, int mixRate, int frameCount)
	{
		return new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = mixRate,
			Stereo = true,
			LoopMode = AudioStreamWav.LoopModeEnum.Forward,
			LoopBegin = 0,
			LoopEnd = frameCount,
			Data = data,
		};
	}

	private static float FastPluckEnvelope(float phase, float decay)
	{
		return Mathf.Min(phase * 22.0f, 1.0f) * Mathf.Exp(-phase * decay);
	}

	private static float LoopEdgeEnvelope(float time, float duration)
	{
		const float edgeSeconds = 0.018f;
		return Mathf.Clamp(Mathf.Min(time / edgeSeconds, (duration - time) / edgeSeconds), 0.0f, 1.0f);
	}

	private static float MidiFrequency(int midiNote)
	{
		return 440.0f * Mathf.Pow(2.0f, (midiNote - 69) / 12.0f);
	}

	private static void WritePcm16(byte[] data, int offset, float sample)
	{
		short value = (short)Mathf.RoundToInt(Mathf.Clamp(sample, -1.0f, 1.0f) * short.MaxValue);
		data[offset] = (byte)(value & 0xff);
		data[offset + 1] = (byte)((value >> 8) & 0xff);
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
