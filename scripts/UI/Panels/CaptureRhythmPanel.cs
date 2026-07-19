using Godot;
using System;
using System.Collections.Generic;

public partial class CaptureRhythmPanel : Control
{
	private enum CaptureDirection
	{
		Up,
		Down,
		Left,
		Right,
	}

	private const float ReadyDelaySeconds = 0.72f;
	private const float ResultDisplaySeconds = 0.62f;
	private const float SuccessDisplaySeconds = 1.48f;
	private const float SadFailureDisplaySeconds = 1.72f;
	private const int MaximumMistakes = 3;

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<CaptureDirection> _sequence = new();
	private readonly List<PanelContainer> _commandPanels = new();
	private SimpleActor? _target;
	private Label _titleLabel = null!;
	private Label _subtitleLabel = null!;
	private Label _statusLabel = null!;
	private Label _progressLabel = null!;
	private Label _mistakeLabel = null!;
	private Label _timeLabel = null!;
	private Label _hintLabel = null!;
	private GridContainer _commandGrid = null!;
	private ProgressBar _timeBar = null!;
	private AudioStreamPlayer _tonePlayer = null!;
	private AudioStreamPlayer _battleMusicPlayer = null!;
	private int _currentIndex;
	private int _mistakes;
	private float _timeLimit;
	private float _timeRemaining;
	private float _readyRemaining;
	private float _resultRemaining;
	private bool _acceptingInput;
	private bool? _pendingResult;
	private bool _treeWasPaused;
	private Input.MouseModeEnum _previousMouseMode;

	public event Action<SimpleActor>? ChallengeSucceeded;
	public event Action<SimpleActor>? ChallengeFailed;

	public bool IsChallengeActive => Visible && _target != null;
	public int CommandCount => _sequence.Count;
	public int CurrentProgress => _currentIndex;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_rng.Seed = Time.GetTicksUsec() ^ (ulong)GetInstanceId();
		BuildPanel();
		LocaleText.LanguageChanged += RefreshLocalizedText;
		Visible = false;
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshLocalizedText;
		_battleMusicPlayer?.Stop();
		if (IsChallengeActive)
		{
			GetTree().Paused = _treeWasPaused;
			Input.MouseMode = _previousMouseMode;
		}
	}

	public override void _Process(double delta)
	{
		if (!IsChallengeActive)
		{
			return;
		}

		float step = (float)delta;
		if (_pendingResult.HasValue)
		{
			_resultRemaining -= step;
			if (_resultRemaining <= 0.0f)
			{
				CompleteChallenge(_pendingResult.Value);
			}
			return;
		}

		if (!_acceptingInput)
		{
			_readyRemaining -= step;
			if (_readyRemaining <= 0.0f)
			{
				_acceptingInput = true;
				_statusLabel.Text = LocaleText.T("capture.rhythm.start");
				_statusLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.90f, 0.38f));
			}
			return;
		}

		_timeRemaining = Mathf.Max(_timeRemaining - step, 0.0f);
		RefreshTimeDisplay();
		if (_timeRemaining <= 0.0f)
		{
			BeginResult(false, "capture.rhythm.timeout");
		}
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!IsChallengeActive || inputEvent is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		if (keyEvent.Keycode == Key.Escape)
		{
			BeginResult(false, "capture.rhythm.cancelled");
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!TryGetDirection(keyEvent, out CaptureDirection direction))
		{
			return;
		}

		GetViewport().SetInputAsHandled();
		if (_acceptingInput && !_pendingResult.HasValue)
		{
			HandleDirection(direction);
		}
	}

	public bool Begin(SimpleActor actor)
	{
		if (IsChallengeActive || !IsInstanceValid(actor) || !actor.CanBeCaptured)
		{
			return false;
		}

		_target = actor;
		_currentIndex = 0;
		_mistakes = 0;
		_pendingResult = null;
		_acceptingInput = false;
		_readyRemaining = ReadyDelaySeconds;
		int commandCount = Mathf.Clamp(4 + Mathf.Max(actor.Level - 1, 0) / 2, 4, 18);
		_timeLimit = Mathf.Clamp(4.5f + commandCount * 0.55f, 6.5f, 12.5f);
		_timeRemaining = _timeLimit;
		GenerateSequence(commandCount);
		BuildCommandTiles();
		RefreshLocalizedText();
		RefreshProgressDisplay();
		RefreshTimeDisplay();
		_statusLabel.Text = LocaleText.T("capture.rhythm.ready");
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.88f, 1.0f));
		_treeWasPaused = GetTree().Paused;
		_previousMouseMode = Input.MouseMode;
		Visible = true;
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		StartCaptureBattleMusic();
		return true;
	}

	private void HandleDirection(CaptureDirection direction)
	{
		if (_currentIndex >= _sequence.Count)
		{
			return;
		}

		if (direction == _sequence[_currentIndex])
		{
			PlayTone(DirectionFrequency(direction), 0.065f, 0.22f);
			_currentIndex++;
			_statusLabel.Text = LocaleText.T("capture.rhythm.correct");
			_statusLabel.AddThemeColorOverride("font_color", new Color(0.46f, 1.0f, 0.66f));
			RefreshCommandTiles();
			RefreshProgressDisplay();
			if (_currentIndex >= _sequence.Count)
			{
				BeginResult(true, "capture.rhythm.success");
			}
			return;
		}

		_mistakes++;
		PlayTone(155.0f, 0.13f, 0.26f);
		int wrongIndex = _currentIndex;
		_currentIndex = 0;
		_statusLabel.Text = LocaleText.T("capture.rhythm.wrong_reset");
		_statusLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.34f, 0.28f));
		FlashTileWrong(wrongIndex);
		RefreshProgressDisplay();
		if (_mistakes >= MaximumMistakes)
		{
			BeginResult(false, "capture.rhythm.failed", true);
		}
	}

	private void BeginResult(bool success, string statusKey, bool playSadFailure = false)
	{
		if (_pendingResult.HasValue)
		{
			return;
		}

		_acceptingInput = false;
		_pendingResult = success;
		_resultRemaining = playSadFailure
			? SadFailureDisplaySeconds
			: success ? SuccessDisplaySeconds : ResultDisplaySeconds;
		_statusLabel.Text = LocaleText.T(statusKey);
		_statusLabel.AddThemeColorOverride("font_color", success
			? new Color(0.44f, 1.0f, 0.64f)
			: new Color(1.0f, 0.34f, 0.28f));
		if (success)
		{
			PlayCaptureSuccessJingle();
		}
		else if (playSadFailure)
		{
			PlaySadFailureJingle();
		}
		else
		{
			PlayTone(success ? 1046.5f : 120.0f, success ? 0.22f : 0.20f, success ? 0.28f : 0.24f);
		}
		StopCaptureBattleMusic();
	}

	private void CompleteChallenge(bool success)
	{
		SimpleActor? completedTarget = _target;
		_target = null;
		_pendingResult = null;
		_acceptingInput = false;
		Visible = false;
		GetTree().Paused = _treeWasPaused;
		Input.MouseMode = _previousMouseMode;
		if (completedTarget == null || !IsInstanceValid(completedTarget))
		{
			return;
		}

		if (success)
		{
			ChallengeSucceeded?.Invoke(completedTarget);
		}
		else
		{
			ChallengeFailed?.Invoke(completedTarget);
		}
	}

	private void GenerateSequence(int count)
	{
		_sequence.Clear();
		for (int index = 0; index < count; index++)
		{
			CaptureDirection direction = (CaptureDirection)_rng.RandiRange(0, 3);
			if (index >= 2 && direction == _sequence[index - 1] && direction == _sequence[index - 2])
			{
				direction = (CaptureDirection)(((int)direction + _rng.RandiRange(1, 3)) % 4);
			}
			_sequence.Add(direction);
		}
	}

	private void BuildCommandTiles()
	{
		foreach (Node child in _commandGrid.GetChildren())
		{
			_commandGrid.RemoveChild(child);
			child.QueueFree();
		}
		_commandPanels.Clear();
		_commandGrid.Columns = Mathf.Min(_sequence.Count, 9);

		for (int index = 0; index < _sequence.Count; index++)
		{
			var tile = new PanelContainer
			{
				CustomMinimumSize = new Vector2(58.0f, 58.0f),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			var arrow = new Label
			{
				Text = DirectionGlyph(_sequence[index]),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			arrow.AddThemeFontSizeOverride("font_size", 31);
			arrow.AddThemeColorOverride("font_color", new Color(0.88f, 0.93f, 0.98f));
			tile.AddChild(arrow);
			_commandGrid.AddChild(tile);
			_commandPanels.Add(tile);
		}
		RefreshCommandTiles();
	}

	private void RefreshCommandTiles()
	{
		for (int index = 0; index < _commandPanels.Count; index++)
		{
			Color background = index < _currentIndex
				? new Color(0.06f, 0.34f, 0.20f, 0.96f)
				: index == _currentIndex
					? new Color(0.42f, 0.28f, 0.045f, 0.98f)
					: new Color(0.075f, 0.09f, 0.12f, 0.94f);
			Color border = index < _currentIndex
				? new Color(0.32f, 1.0f, 0.60f, 0.94f)
				: index == _currentIndex
					? new Color(1.0f, 0.78f, 0.20f, 1.0f)
					: new Color(0.34f, 0.43f, 0.54f, 0.76f);
			_commandPanels[index].AddThemeStyleboxOverride("panel", MakeTileStyle(background, border));
		}
	}

	private void FlashTileWrong(int tileIndex)
	{
		if (tileIndex < 0 || tileIndex >= _commandPanels.Count)
		{
			return;
		}

		PanelContainer tile = _commandPanels[tileIndex];
		tile.AddThemeStyleboxOverride("panel", MakeTileStyle(new Color(0.52f, 0.055f, 0.04f, 0.98f), new Color(1.0f, 0.20f, 0.14f)));
		Tween tween = CreateTween();
		tween.SetPauseMode(Tween.TweenPauseMode.Process);
		tween.TweenInterval(0.16f);
		tween.TweenCallback(Callable.From(RefreshCommandTiles));
	}

	private void RefreshProgressDisplay()
	{
		_progressLabel.Text = LocaleText.F("capture.rhythm.progress", _currentIndex, _sequence.Count);
		_mistakeLabel.Text = LocaleText.F("capture.rhythm.mistakes", _mistakes, MaximumMistakes);
	}

	private void RefreshTimeDisplay()
	{
		_timeBar.MaxValue = Mathf.Max(_timeLimit, 0.1f);
		_timeBar.Value = _timeRemaining;
		_timeLabel.Text = LocaleText.F("capture.rhythm.time", _timeRemaining.ToString("0.0"));
	}

	private void RefreshLocalizedText()
	{
		_titleLabel.Text = LocaleText.T("capture.rhythm.title");
		_hintLabel.Text = LocaleText.T("capture.rhythm.hint");
		if (_target != null && IsInstanceValid(_target))
		{
			_subtitleLabel.Text = LocaleText.F("capture.rhythm.subtitle", _target.LocalizedDisplayName, _target.Level, _sequence.Count);
		}
		RefreshProgressDisplay();
		RefreshTimeDisplay();
	}

	private void BuildPanel()
	{
		Name = "CaptureRhythmPanel";
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;

		var dim = new ColorRect
		{
			Color = new Color(0.0f, 0.0f, 0.0f, 0.62f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var panel = new PanelContainer
		{
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -350.0f,
			OffsetRight = 350.0f,
			OffsetTop = -220.0f,
			OffsetBottom = 220.0f,
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.035f, 0.055f, 0.98f),
			BorderColor = new Color(0.30f, 0.78f, 1.0f, 0.96f),
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.72f),
			ShadowSize = 14,
		};
		panelStyle.SetBorderWidthAll(2);
		panelStyle.SetCornerRadiusAll(12);
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_bottom", 20);
		panel.AddChild(margin);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 10);
		margin.AddChild(rows);

		_titleLabel = MakeLabel(28, new Color(0.72f, 0.92f, 1.0f), HorizontalAlignment.Center);
		rows.AddChild(_titleLabel);
		_subtitleLabel = MakeLabel(16, new Color(0.82f, 0.88f, 0.94f), HorizontalAlignment.Center);
		rows.AddChild(_subtitleLabel);

		var commandCenter = new CenterContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		rows.AddChild(commandCenter);
		_commandGrid = new GridContainer
		{
			Name = "CaptureCommandGrid",
		};
		_commandGrid.AddThemeConstantOverride("h_separation", 8);
		_commandGrid.AddThemeConstantOverride("v_separation", 8);
		commandCenter.AddChild(_commandGrid);

		var infoRow = new HBoxContainer();
		infoRow.AddThemeConstantOverride("separation", 12);
		rows.AddChild(infoRow);
		_progressLabel = MakeLabel(14, new Color(0.62f, 0.86f, 1.0f), HorizontalAlignment.Left);
		_progressLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		infoRow.AddChild(_progressLabel);
		_mistakeLabel = MakeLabel(14, new Color(1.0f, 0.62f, 0.50f), HorizontalAlignment.Right);
		infoRow.AddChild(_mistakeLabel);

		_timeBar = new ProgressBar
		{
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 10.0f),
		};
		rows.AddChild(_timeBar);
		_timeLabel = MakeLabel(13, new Color(0.72f, 0.80f, 0.88f), HorizontalAlignment.Center);
		rows.AddChild(_timeLabel);
		_statusLabel = MakeLabel(19, new Color(1.0f, 0.90f, 0.38f), HorizontalAlignment.Center);
		_statusLabel.CustomMinimumSize = new Vector2(0.0f, 30.0f);
		rows.AddChild(_statusLabel);
		_hintLabel = MakeLabel(13, new Color(0.58f, 0.66f, 0.74f), HorizontalAlignment.Center);
		_hintLabel.Text = LocaleText.T("capture.rhythm.hint");
		rows.AddChild(_hintLabel);

		_tonePlayer = new AudioStreamPlayer
		{
			Name = "CaptureInputSound",
			ProcessMode = ProcessModeEnum.Always,
		};
		AddChild(_tonePlayer);

		_battleMusicPlayer = new AudioStreamPlayer
		{
			Name = "CaptureBattleMusic",
			ProcessMode = ProcessModeEnum.Always,
			Stream = CreateCaptureBattleMusic(),
			VolumeDb = -40.0f,
		};
		AddChild(_battleMusicPlayer);
	}

	private void StartCaptureBattleMusic()
	{
		_battleMusicPlayer.Stop();
		_battleMusicPlayer.VolumeDb = -32.0f;
		_battleMusicPlayer.Play();
		Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		fade.TweenProperty(_battleMusicPlayer, "volume_db", -7.5f, 0.42f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
	}

	private void StopCaptureBattleMusic()
	{
		if (!_battleMusicPlayer.Playing)
		{
			return;
		}

		Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		fade.TweenProperty(_battleMusicPlayer, "volume_db", -36.0f, 0.48f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		fade.TweenCallback(Callable.From(_battleMusicPlayer.Stop));
	}

	private static AudioStreamWav CreateCaptureBattleMusic()
	{
		const int mixRate = 22050;
		const float bpm = 140.0f;
		const int beatCount = 8;
		float beatDuration = 60.0f / bpm;
		float duration = beatDuration * beatCount;
		int frameCount = Mathf.RoundToInt(mixRate * duration);
		byte[] data = new byte[frameCount * 4];
		float[] bassNotes = { 55.0f, 55.0f, 65.41f, 49.0f, 55.0f, 73.42f, 65.41f, 49.0f };
		float[] arpeggio = { 220.0f, 261.63f, 329.63f, 392.0f, 233.08f, 293.66f, 349.23f, 440.0f };
		var random = new RandomNumberGenerator { Seed = 0xC4A7_2026 };

		for (int frame = 0; frame < frameCount; frame++)
		{
			float time = frame / (float)mixRate;
			float beatPosition = time / beatDuration;
			int beat = Mathf.FloorToInt(beatPosition) % beatCount;
			float beatPhase = beatPosition - Mathf.Floor(beatPosition);
			float eighthPosition = beatPosition * 2.0f;
			int eighth = Mathf.FloorToInt(eighthPosition);
			float eighthPhase = eighthPosition - Mathf.Floor(eighthPosition);

			float kickEnvelope = Mathf.Exp(-beatPhase * 13.0f);
			float kickFrequency = 48.0f + 72.0f * Mathf.Exp(-beatPhase * 18.0f);
			float kick = Mathf.Sin(Mathf.Tau * kickFrequency * time) * kickEnvelope * 0.48f;

			float bassEnvelope = Mathf.Min(beatPhase * 18.0f, 1.0f) * Mathf.Exp(-beatPhase * 1.8f);
			float bassPhase = Mathf.Tau * bassNotes[beat] * time;
			float bass = (Mathf.Sin(bassPhase) + 0.30f * Mathf.Sin(bassPhase * 2.0f)) * bassEnvelope * 0.20f;

			float arpEnvelope = Mathf.Sin(Mathf.Clamp(eighthPhase * 1.35f, 0.0f, 1.0f) * Mathf.Pi) * Mathf.Exp(-eighthPhase * 1.3f);
			float arpFrequency = arpeggio[eighth % arpeggio.Length] * (eighth % 4 == 3 ? 2.0f : 1.0f);
			float arpPhase = Mathf.Tau * arpFrequency * time;
			float arp = (Mathf.Sin(arpPhase) + 0.22f * Mathf.Sin(arpPhase * 2.01f)) * arpEnvelope * 0.105f;

			float hatEnvelope = Mathf.Exp(-eighthPhase * 30.0f);
			float hat = random.RandfRange(-1.0f, 1.0f) * hatEnvelope * (eighth % 2 == 1 ? 0.085f : 0.045f);
			float tension = Mathf.Sin(Mathf.Tau * 110.0f * time + 0.7f * Mathf.Sin(Mathf.Tau * 0.5f * time)) * 0.035f;
			float left = Mathf.Clamp(kick + bass + arp * 0.88f + hat * 0.72f + tension, -0.92f, 0.92f);
			float right = Mathf.Clamp(kick + bass + arp * 1.08f + hat + tension, -0.92f, 0.92f);
			WritePcm16(data, frame * 4, left);
			WritePcm16(data, frame * 4 + 2, right);
		}

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

	private static void WritePcm16(byte[] data, int offset, float sample)
	{
		short value = (short)Mathf.RoundToInt(Mathf.Clamp(sample, -1.0f, 1.0f) * short.MaxValue);
		data[offset] = (byte)(value & 0xff);
		data[offset + 1] = (byte)((value >> 8) & 0xff);
	}

	private void PlayTone(float frequency, float duration, float volume)
	{
		var generator = new AudioStreamGenerator
		{
			MixRate = 22050.0f,
			BufferLength = Mathf.Max(duration + 0.04f, 0.10f),
		};
		_tonePlayer.Stop();
		_tonePlayer.Stream = generator;
		_tonePlayer.Play();
		if (_tonePlayer.GetStreamPlayback() is not AudioStreamGeneratorPlayback playback)
		{
			return;
		}

		int sampleCount = Mathf.RoundToInt(generator.MixRate * duration);
		for (int index = 0; index < sampleCount; index++)
		{
			float progress = index / (float)Mathf.Max(sampleCount - 1, 1);
			float envelope = Mathf.Sin(progress * Mathf.Pi);
			float sample = Mathf.Sin(Mathf.Tau * frequency * index / generator.MixRate) * volume * envelope;
			playback.PushFrame(new Vector2(sample, sample));
		}
	}

	private void PlaySadFailureJingle()
	{
		const int mixRate = 22050;
		const float duration = 1.58f;
		int sampleCount = Mathf.RoundToInt(mixRate * duration);
		byte[] data = new byte[sampleCount * 4];
		float[] starts = { 0.0f, 0.43f, 0.86f };
		float[] lengths = { 0.34f, 0.34f, 0.70f };
		float[] pitches = { 392.0f, 349.23f, 293.66f };

		for (int index = 0; index < sampleCount; index++)
		{
			float time = index / (float)mixRate;
			float sample = 0.0f;
			for (int note = 0; note < starts.Length; note++)
			{
				float localTime = time - starts[note];
				if (localTime < 0.0f || localTime >= lengths[note])
				{
					continue;
				}

				float progress = localTime / lengths[note];
				float envelope = Mathf.Sin(progress * Mathf.Pi) * Mathf.Exp(-progress * (note == 2 ? 0.45f : 0.9f));
				float pitchDrop = Mathf.Lerp(1.0f, note == 2 ? 0.68f : 0.82f, progress);
				float vibrato = 1.0f + 0.018f * Mathf.Sin(Mathf.Tau * 5.2f * localTime);
				float phase = Mathf.Tau * pitches[note] * pitchDrop * vibrato * localTime;
				sample += (Mathf.Sin(phase) + 0.24f * Mathf.Sin(phase * 2.0f)) * envelope * 0.24f;
			}

			float left = Mathf.Clamp(sample * 0.94f, -0.9f, 0.9f);
			float right = Mathf.Clamp(sample * 1.06f, -0.9f, 0.9f);
			WritePcm16(data, index * 4, left);
			WritePcm16(data, index * 4 + 2, right);
		}

		_tonePlayer.Stop();
		_tonePlayer.Stream = new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = mixRate,
			Stereo = true,
			Data = data,
		};
		_tonePlayer.Play();
	}

	private void PlayCaptureSuccessJingle()
	{
		const int mixRate = 22050;
		const float duration = 1.34f;
		int sampleCount = Mathf.RoundToInt(mixRate * duration);
		byte[] data = new byte[sampleCount * 4];
		float[] starts = { 0.0f, 0.18f, 0.36f, 0.55f, 0.76f };
		float[] lengths = { 0.25f, 0.25f, 0.27f, 0.30f, 0.56f };
		float[] pitches = { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f };

		for (int index = 0; index < sampleCount; index++)
		{
			float time = index / (float)mixRate;
			float left = 0.0f;
			float right = 0.0f;
			for (int note = 0; note < starts.Length; note++)
			{
				float localTime = time - starts[note];
				if (localTime < 0.0f || localTime >= lengths[note])
				{
					continue;
				}

				float progress = localTime / lengths[note];
				float attack = Mathf.Min(progress * 18.0f, 1.0f);
				float envelope = attack * Mathf.Exp(-progress * (note == starts.Length - 1 ? 1.2f : 2.6f));
				float shimmer = 1.0f + 0.006f * Mathf.Sin(Mathf.Tau * 6.4f * localTime);
				float phase = Mathf.Tau * pitches[note] * shimmer * localTime;
				float tone = (Mathf.Sin(phase) + 0.28f * Mathf.Sin(phase * 2.0f) + 0.10f * Mathf.Sin(phase * 3.0f)) * envelope * 0.17f;
				float pan = note / (float)(starts.Length - 1);
				left += tone * Mathf.Lerp(1.08f, 0.78f, pan);
				right += tone * Mathf.Lerp(0.78f, 1.08f, pan);

				if (note == starts.Length - 1)
				{
					float chordPhase = Mathf.Tau * 659.25f * localTime;
					float chord = Mathf.Sin(chordPhase) * envelope * 0.095f;
					left += chord;
					right += chord;
				}
			}

			WritePcm16(data, index * 4, Mathf.Clamp(left, -0.9f, 0.9f));
			WritePcm16(data, index * 4 + 2, Mathf.Clamp(right, -0.9f, 0.9f));
		}

		_tonePlayer.Stop();
		_tonePlayer.Stream = new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = mixRate,
			Stereo = true,
			Data = data,
		};
		_tonePlayer.Play();
	}

	private static bool TryGetDirection(InputEventKey keyEvent, out CaptureDirection direction)
	{
		Key key = keyEvent.Keycode;
		Key physicalKey = keyEvent.PhysicalKeycode;
		if (key == Key.Up || key == Key.W || physicalKey == Key.W)
		{
			direction = CaptureDirection.Up;
			return true;
		}
		if (key == Key.Down || key == Key.S || physicalKey == Key.S)
		{
			direction = CaptureDirection.Down;
			return true;
		}
		if (key == Key.Left || key == Key.A || physicalKey == Key.A)
		{
			direction = CaptureDirection.Left;
			return true;
		}
		if (key == Key.Right || key == Key.D || physicalKey == Key.D)
		{
			direction = CaptureDirection.Right;
			return true;
		}

		direction = default;
		return false;
	}

	private static float DirectionFrequency(CaptureDirection direction)
	{
		return direction switch
		{
			CaptureDirection.Up => 880.0f,
			CaptureDirection.Down => 587.3f,
			CaptureDirection.Left => 659.3f,
			_ => 784.0f,
		};
	}

	private static string DirectionGlyph(CaptureDirection direction)
	{
		return direction switch
		{
			CaptureDirection.Up => "↑",
			CaptureDirection.Down => "↓",
			CaptureDirection.Left => "←",
			_ => "→",
		};
	}

	private static StyleBoxFlat MakeTileStyle(Color background, Color border)
	{
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		return style;
	}

	private static Label MakeLabel(int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.88f));
		label.AddThemeConstantOverride("outline_size", 3);
		return label;
	}
}
