using Godot;

public partial class FloatingTooltip : PanelContainer
{
	private readonly Label _titleLabel;
	private readonly Label _bodyLabel;
	private readonly ScrollContainer _bodyScroll;

	public float MaxWidthRatio { get; set; } = 0.70f;
	public float MaxHeightRatio { get; set; } = 0.72f;
	public float MaxWidth { get; set; } = 720.0f;
	public float MinWidth { get; set; } = 120.0f;
	public float MinBodyHeight { get; set; } = 0.0f;
	public Vector2 MouseOffset { get; set; } = new(18.0f, 18.0f);
	public float EdgePadding { get; set; } = 10.0f;

	public FloatingTooltip()
	{
		Visible = false;
		// A tooltip is an overlay, not layout content. Keeping it top-level prevents
		// its natural size from changing the minimum size of the card/panel that owns it.
		TopLevel = true;
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 80;
		SetAnchorsPreset(LayoutPreset.TopLeft);
		SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		SizeFlagsVertical = SizeFlags.ShrinkBegin;
		GrowHorizontal = GrowDirection.Begin;
		GrowVertical = GrowDirection.Begin;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.97f),
			BorderColor = new Color(0.78f, 0.66f, 0.36f, 0.94f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 9);
		margin.AddThemeConstantOverride("margin_bottom", 9);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 5);
		margin.AddChild(root);

		_titleLabel = MakeLabel(16, new Color(1.0f, 0.92f, 0.58f));
		root.AddChild(_titleLabel);

		_bodyScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		root.AddChild(_bodyScroll);

		_bodyLabel = MakeLabel(13, new Color(0.86f, 0.91f, 0.96f));
		_bodyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_bodyScroll.AddChild(_bodyLabel);
		SetMousePassthroughRecursive(this);
	}

	public void ShowTooltip(string title, string body, Control owner)
	{
		SetContent(title, body, owner);
		PositionNearMouse(owner);
	}

	public void SetContent(string title, string body, Control owner)
	{
		_titleLabel.Text = title;
		_bodyLabel.Text = body;
		ApplyContentSize(owner);
		Visible = true;
	}

	public void HideTooltip()
	{
		Visible = false;
	}

	public void ScrollDetail(int amount)
	{
		if (!Visible)
		{
			return;
		}

		_bodyScroll.ScrollVertical += amount;
	}

	public void PositionNearMouse(Control owner)
	{
		if (!Visible)
		{
			return;
		}

		ApplyContentSize(owner);
		// Top-level controls use viewport/canvas coordinates directly. Non-top-level
		// controls use coordinates relative to their owner.
		Vector2 coordinateOrigin = TopLevel ? Vector2.Zero : owner.GlobalPosition;
		Vector2 localMouse = owner.GetGlobalMousePosition() - coordinateOrigin;
		Vector2 viewportSize = owner.GetViewportRect().Size;
		Vector2 tooltipSize = Size;
		if (tooltipSize.LengthSquared() <= 1.0f)
		{
			tooltipSize = GetCombinedMinimumSize();
		}

		Vector2 position = localMouse + MouseOffset;
		Vector2 viewportMin = new Vector2(EdgePadding, EdgePadding) - coordinateOrigin;
		Vector2 viewportMax = viewportSize - coordinateOrigin - new Vector2(EdgePadding, EdgePadding);
		if (position.X + tooltipSize.X > viewportMax.X)
		{
			position.X = localMouse.X - tooltipSize.X - MouseOffset.X;
		}

		if (position.Y + tooltipSize.Y > viewportMax.Y)
		{
			position.Y = localMouse.Y - tooltipSize.Y - MouseOffset.Y;
		}

		position.X = Mathf.Clamp(position.X, viewportMin.X, Mathf.Max(viewportMin.X, viewportMax.X - tooltipSize.X));
		position.Y = Mathf.Clamp(position.Y, viewportMin.Y, Mathf.Max(viewportMin.Y, viewportMax.Y - tooltipSize.Y));
		Position = position;
	}

	private static void SetMousePassthroughRecursive(Node node)
	{
		if (node is Control control)
		{
			control.MouseFilter = MouseFilterEnum.Ignore;
		}

		foreach (Node child in node.GetChildren())
		{
			SetMousePassthroughRecursive(child);
		}
	}

	private void ApplyContentSize(Control owner)
	{
		_titleLabel.CustomMinimumSize = Vector2.Zero;
		_bodyLabel.CustomMinimumSize = Vector2.Zero;
		_bodyScroll.CustomMinimumSize = Vector2.Zero;

		Vector2 viewportSize = owner.GetViewportRect().Size;
		float availableViewportWidth = Mathf.Max(MinWidth, viewportSize.X - EdgePadding * 2.0f - 24.0f);
		float maxContentWidth = Mathf.Min(MaxWidth, Mathf.Min(availableViewportWidth, viewportSize.X * MaxWidthRatio));
		float titleWidth = _titleLabel.GetCombinedMinimumSize().X;
		float bodyWidth = _bodyLabel.GetCombinedMinimumSize().X;
		float contentWidth = Mathf.Clamp(Mathf.Max(titleWidth, bodyWidth), MinWidth, maxContentWidth);

		_titleLabel.CustomMinimumSize = new Vector2(contentWidth, 0.0f);
		_bodyLabel.CustomMinimumSize = new Vector2(contentWidth, 0.0f);
		float availableViewportHeight = Mathf.Max(160.0f, viewportSize.Y - EdgePadding * 2.0f);
		float maxPanelHeight = Mathf.Min(availableViewportHeight, Mathf.Max(160.0f, viewportSize.Y * MaxHeightRatio));
		float titleHeight = _titleLabel.GetCombinedMinimumSize().Y;
		float maxBodyHeight = Mathf.Max(48.0f, maxPanelHeight - titleHeight - 23.0f);
		float naturalBodyHeight = _bodyLabel.GetCombinedMinimumSize().Y;
		float bodyHeight = Mathf.Clamp(naturalBodyHeight, MinBodyHeight, maxBodyHeight);
		_bodyScroll.CustomMinimumSize = new Vector2(contentWidth, bodyHeight);
		_bodyScroll.Size = new Vector2(contentWidth, bodyHeight);

		Vector2 panelSize = new(contentWidth + 24.0f, Mathf.Min(titleHeight + bodyHeight + 23.0f, maxPanelHeight));
		CustomMinimumSize = panelSize;
		Size = panelSize;
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}
}
