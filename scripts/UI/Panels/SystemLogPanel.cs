using Godot;
using System;
using System.Collections.Generic;

public enum GameMessageChannel
{
	All,
	System,
	Combat,
	Loot,
	Party,
}

public partial class SystemLogPanel : PanelContainer
{
	private const int MaxStoredMessages = 160;
	private const float DefaultWidth = 370.0f;
	private const float DefaultHeight = 180.0f;
	private const float MinimumWidth = 290.0f;
	private const float MinimumHeight = 145.0f;
	private const ulong ResizeHoldDelayMsec = 180;

	private sealed record MessageEntry(string Text, Color Color, GameMessageChannel Channel, string Timestamp);

	private readonly List<MessageEntry> _messages = new();
	private readonly Dictionary<GameMessageChannel, Button> _channelButtons = new();
	private readonly Dictionary<GameMessageChannel, int> _unreadCounts = new();
	private VBoxContainer _rows = null!;
	private ScrollContainer _scroll = null!;
	private Label _titleLabel = null!;
	private Label _emptyLabel = null!;
	private Button _resizeHandle = null!;
	private GameMessageChannel _selectedChannel = GameMessageChannel.All;
	private int _scrollToBottomPendingFrames;
	// True when messages arrived while the panel was hidden; the row list is
	// then rebuilt lazily when the panel is next shown (see _Notification).
	private bool _messagesDirty;
	private bool _resizePressPending;
	private bool _isResizing;
	private ulong _resizePressStartedAtMsec;
	private Vector2 _resizeStartMousePosition;
	private Vector2 _resizeStartPanelSize;

	public GameMessageChannel SelectedChannel => _selectedChannel;
	public int StoredMessageCount => _messages.Count;
	public Vector2 WindowSize => Size;

	public override void _Ready()
	{
		foreach (GameMessageChannel channel in Enum.GetValues<GameMessageChannel>())
		{
			_unreadCounts[channel] = 0;
		}

		BuildPanel();
		LocaleText.LanguageChanged += OnLanguageChanged;
		RefreshMessages(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Process(double delta)
	{
		if (_scrollToBottomPendingFrames <= 0)
		{
			return;
		}

		_scrollToBottomPendingFrames--;
		if (_scrollToBottomPendingFrames == 0)
		{
			ScrollToBottom();
		}
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!_resizePressPending && !_isResizing)
		{
			return;
		}

		if (inputEvent is InputEventMouseMotion)
		{
			if (_resizePressPending
				&& Time.GetTicksMsec() - _resizePressStartedAtMsec >= ResizeHoldDelayMsec)
			{
				_resizePressPending = false;
				_isResizing = true;
				_resizeStartMousePosition = GetViewport().GetMousePosition();
				_resizeStartPanelSize = Size;
			}

			if (!_isResizing)
			{
				return;
			}

			ResizeFromMouse(GetViewport().GetMousePosition());
			GetViewport().SetInputAsHandled();
			return;
		}

		if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
		{
			bool wasResizing = _isResizing;
			_resizePressPending = false;
			_isResizing = false;
			if (wasResizing)
			{
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public void AddMessage(string message, Color color, GameMessageChannel channel = GameMessageChannel.System)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}

		GameMessageChannel safeChannel = channel == GameMessageChannel.All ? GameMessageChannel.System : channel;
		_messages.Add(new MessageEntry(message.Trim(), color, safeChannel, DateTime.Now.ToString("HH:mm")));
		while (_messages.Count > MaxStoredMessages)
		{
			_messages.RemoveAt(0);
		}

		if (_selectedChannel != GameMessageChannel.All && _selectedChannel != safeChannel)
		{
			_unreadCounts[safeChannel] = _unreadCounts.GetValueOrDefault(safeChannel) + 1;
		}

		RefreshChannelButtons();
		if (_selectedChannel == GameMessageChannel.All || _selectedChannel == safeChannel)
		{
			// Rebuilding the row list is only worth it while visible. When the
			// log is closed (the common case during combat/looting), defer it so
			// bursts of kill/loot messages don't rebuild the whole list each time.
			if (IsVisibleInTree())
			{
				RefreshMessages(true);
			}
			else
			{
				_messagesDirty = true;
			}
		}
	}

	public override void _Notification(int what)
	{
		if (what == (int)NotificationVisibilityChanged && IsVisibleInTree() && _messagesDirty)
		{
			RefreshMessages(true);
		}
	}

	public void SelectChannel(GameMessageChannel channel)
	{
		if (!_channelButtons.ContainsKey(channel))
		{
			return;
		}

		_selectedChannel = channel;
		if (channel == GameMessageChannel.All)
		{
			foreach (GameMessageChannel messageChannel in Enum.GetValues<GameMessageChannel>())
			{
				_unreadCounts[messageChannel] = 0;
			}
		}
		else
		{
			_unreadCounts[channel] = 0;
		}

		RefreshChannelButtons();
		RefreshMessages(true);
	}

	public int GetUnreadCount(GameMessageChannel channel)
	{
		return _unreadCounts.GetValueOrDefault(channel);
	}

	public void SetWindowSize(Vector2 requestedSize)
	{
		Vector2 viewportSize = GetViewportRect().Size;
		float maximumWidth = Mathf.Max(MinimumWidth, viewportSize.X * 0.72f);
		float maximumHeight = Mathf.Max(MinimumHeight, viewportSize.Y * 0.62f);
		float width = Mathf.Clamp(requestedSize.X, MinimumWidth, maximumWidth);
		float height = Mathf.Clamp(requestedSize.Y, MinimumHeight, maximumHeight);

		// Keep the lower-left corner fixed so changing the chat size never pushes
		// controls away from their familiar HUD position.
		OffsetRight = OffsetLeft + width;
		OffsetTop = OffsetBottom - height;
		_scrollToBottomPendingFrames = 2;
	}

	private void BuildPanel()
	{
		Name = "SystemLogPanel";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.0f;
		AnchorRight = 0.0f;
		AnchorTop = 1.0f;
		AnchorBottom = 1.0f;
		OffsetLeft = 20.0f;
		OffsetBottom = -24.0f;
		OffsetRight = OffsetLeft + DefaultWidth;
		OffsetTop = OffsetBottom - DefaultHeight;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.018f, 0.024f, 0.032f, 0.82f),
			BorderColor = new Color(0.34f, 0.43f, 0.54f, 0.64f),
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.55f),
			ShadowSize = 5,
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_top", 7);
		margin.AddThemeConstantOverride("margin_bottom", 7);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 4);
		margin.AddChild(root);

		_titleLabel = MakeLabel(string.Empty, 14, new Color(0.88f, 0.94f, 1.0f));
		_titleLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.86f));
		_titleLabel.AddThemeConstantOverride("outline_size", 4);
		root.AddChild(_titleLabel);

		var tabs = new HBoxContainer();
		tabs.AddThemeConstantOverride("separation", 4);
		root.AddChild(tabs);
		foreach (GameMessageChannel channel in Enum.GetValues<GameMessageChannel>())
		{
			var button = new Button
			{
				ToggleMode = true,
				FocusMode = FocusModeEnum.None,
				CustomMinimumSize = new Vector2(channel == GameMessageChannel.All ? 42.0f : 52.0f, 27.0f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			button.AddThemeFontSizeOverride("font_size", 12);
			GameMessageChannel capturedChannel = channel;
			button.Pressed += () => SelectChannel(capturedChannel);
			tabs.AddChild(button);
			_channelButtons[channel] = button;
		}

		var separator = new HSeparator();
		separator.AddThemeConstantOverride("separation", 2);
		root.AddChild(separator);

		_scroll = new ScrollContainer
		{
			Name = "ChannelMessageScroll",
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
		};
		root.AddChild(_scroll);

		_rows = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_rows.AddThemeConstantOverride("separation", 3);
		_scroll.AddChild(_rows);

		_emptyLabel = MakeLabel(string.Empty, 13, new Color(0.48f, 0.55f, 0.63f));
		_emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_emptyLabel.CustomMinimumSize = new Vector2(0.0f, 80.0f);
		_emptyLabel.VerticalAlignment = VerticalAlignment.Center;
		_rows.AddChild(_emptyLabel);

		_resizeHandle = new Button
		{
			Name = "ChannelResizeHandle",
			Text = string.Empty,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.Bdiagsize,
			TooltipText = LocaleText.T("channel.resize_hint"),
			AnchorLeft = 1.0f,
			AnchorRight = 1.0f,
			OffsetLeft = -28.0f,
			OffsetRight = -3.0f,
			OffsetTop = 2.0f,
			OffsetBottom = 27.0f,
		};
		_resizeHandle.GuiInput += OnResizeHandleGuiInput;
		// PanelContainer stretches every direct Control child across the entire
		// panel. A plain overlay keeps the invisible resize hitbox confined to
		// its intended top-right rectangle instead of covering the channel tabs.
		var resizeOverlay = new Control
		{
			Name = "ChannelResizeOverlay",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(resizeOverlay);
		resizeOverlay.AddChild(_resizeHandle);
		RefreshChannelButtons();
	}

	private void OnResizeHandleGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton)
		{
			return;
		}

		if (mouseButton.Pressed)
		{
			_resizePressPending = true;
			_isResizing = false;
			_resizePressStartedAtMsec = Time.GetTicksMsec();
		}
		else
		{
			_resizePressPending = false;
			_isResizing = false;
		}
		AcceptEvent();
	}

	private void ResizeFromMouse(Vector2 mousePosition)
	{
		Vector2 dragDelta = mousePosition - _resizeStartMousePosition;
		SetWindowSize(new Vector2(
			_resizeStartPanelSize.X + dragDelta.X,
			_resizeStartPanelSize.Y - dragDelta.Y));
	}

	private void RefreshMessages(bool scrollToBottom)
	{
		_messagesDirty = false;
		foreach (Node child in _rows.GetChildren())
		{
			_rows.RemoveChild(child);
			child.QueueFree();
		}

		int visibleCount = 0;
		foreach (MessageEntry entry in _messages)
		{
			if (_selectedChannel != GameMessageChannel.All && entry.Channel != _selectedChannel)
			{
				continue;
			}

			var label = MakeLabel(
				$"[{entry.Timestamp}]  [{GetChannelName(entry.Channel)}]  {entry.Text}",
				14,
				entry.Color);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(0.0f, 22.0f);
			label.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.86f));
			label.AddThemeConstantOverride("outline_size", 3);
			_rows.AddChild(label);
			visibleCount++;
		}

		if (visibleCount == 0)
		{
			_emptyLabel = MakeLabel(LocaleText.T("channel.empty"), 13, new Color(0.48f, 0.55f, 0.63f));
			_emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			_emptyLabel.CustomMinimumSize = new Vector2(0.0f, 80.0f);
			_emptyLabel.VerticalAlignment = VerticalAlignment.Center;
			_rows.AddChild(_emptyLabel);
		}

		if (scrollToBottom)
		{
			// Containers update their minimum size after the current frame. Waiting
			// two frames makes the scrollbar's maximum reliable even when a burst of
			// loot or combat messages arrives together.
			_scrollToBottomPendingFrames = 2;
		}
	}

	private void ScrollToBottom()
	{
		if (_scroll == null || !IsInstanceValid(_scroll))
		{
			return;
		}

		_scroll.ScrollVertical = Mathf.CeilToInt((float)_scroll.GetVScrollBar().MaxValue);
	}

	private void RefreshChannelButtons()
	{
		foreach (KeyValuePair<GameMessageChannel, Button> pair in _channelButtons)
		{
			GameMessageChannel channel = pair.Key;
			Button button = pair.Value;
			int unread = channel == GameMessageChannel.All ? GetTotalUnreadCount() : _unreadCounts.GetValueOrDefault(channel);
			button.Text = unread > 0 ? $"{GetChannelName(channel)}  {unread}" : GetChannelName(channel);
			button.ButtonPressed = channel == _selectedChannel;
			Color accent = GetChannelColor(channel);
			button.AddThemeColorOverride("font_color", channel == _selectedChannel ? accent : new Color(0.68f, 0.74f, 0.82f));
			button.AddThemeColorOverride("font_hover_color", accent);
			button.AddThemeColorOverride("font_pressed_color", accent);
		}

		if (_titleLabel != null)
		{
			_titleLabel.Text = LocaleText.F("channel.title", GetChannelName(_selectedChannel));
		}
	}

	private int GetTotalUnreadCount()
	{
		int total = 0;
		foreach (KeyValuePair<GameMessageChannel, int> pair in _unreadCounts)
		{
			if (pair.Key != GameMessageChannel.All)
			{
				total += pair.Value;
			}
		}
		return total;
	}

	private static string GetChannelName(GameMessageChannel channel)
	{
		return LocaleText.T(channel switch
		{
			GameMessageChannel.System => "channel.system",
			GameMessageChannel.Combat => "channel.combat",
			GameMessageChannel.Loot => "channel.loot",
			GameMessageChannel.Party => "channel.party",
			_ => "channel.all",
		});
	}

	private static Color GetChannelColor(GameMessageChannel channel)
	{
		return channel switch
		{
			GameMessageChannel.Combat => new Color(1.0f, 0.42f, 0.30f),
			GameMessageChannel.Loot => new Color(1.0f, 0.82f, 0.28f),
			GameMessageChannel.Party => new Color(0.42f, 1.0f, 0.72f),
			GameMessageChannel.System => new Color(0.46f, 0.78f, 1.0f),
			_ => new Color(0.90f, 0.94f, 1.0f),
		};
	}

	private void OnLanguageChanged()
	{
		_resizeHandle.TooltipText = LocaleText.T("channel.resize_hint");
		RefreshChannelButtons();
		RefreshMessages(false);
	}

	private static Label MakeLabel(string text, int fontSize, Color color)
	{
		var label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}
}
