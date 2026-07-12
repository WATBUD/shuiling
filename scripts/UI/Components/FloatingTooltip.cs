using Godot;

public partial class FloatingTooltip : PanelContainer
{
	private readonly Label _titleLabel;
	private readonly Label _bodyLabel;
	private readonly ScrollContainer _bodyScroll;

	public float MaxWidthRatio { get; set; } = 0.34f;
	public float MaxHeightRatio { get; set; } = 0.50f;
	public float MaxWidth { get; set; } = 320.0f;
	public float MinWidth { get; set; } = 120.0f;
	public float MinBodyHeight { get; set; } = 0.0f;
	public Vector2 MouseOffset { get; set; } = new(18.0f, 18.0f);
	public float EdgePadding { get; set; } = 10.0f;

	public FloatingTooltip()
	{
		Visible = false;
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 80;

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
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		root.AddChild(_bodyScroll);

		_bodyLabel = MakeLabel(13, new Color(0.86f, 0.91f, 0.96f));
		_bodyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_bodyScroll.AddChild(_bodyLabel);
	}

	public void ShowTooltip(string title, string body, Control owner)
	{
		_titleLabel.Text = title;
		_bodyLabel.Text = body;
		ApplyContentSize(owner);
		Visible = true;
		ResetSize();
		PositionNearMouse(owner);
	}

	public void HideTooltip()
	{
		Visible = false;
	}

	public void PositionNearMouse(Control owner)
	{
		if (!Visible)
		{
			return;
		}

		ApplyContentSize(owner);
		Vector2 localMouse = owner.GetGlobalMousePosition() - owner.GlobalPosition;
		Vector2 tooltipSize = Size;
		if (tooltipSize.LengthSquared() <= 1.0f)
		{
			tooltipSize = GetCombinedMinimumSize();
		}

		Vector2 position = localMouse + MouseOffset;
		Vector2 bounds = owner.Size;
		if (position.X + tooltipSize.X > bounds.X - EdgePadding)
		{
			position.X = localMouse.X - tooltipSize.X - MouseOffset.X;
		}

		if (position.Y + tooltipSize.Y > bounds.Y - EdgePadding)
		{
			position.Y = localMouse.Y - tooltipSize.Y - MouseOffset.Y;
		}

		position.X = Mathf.Clamp(position.X, EdgePadding, Mathf.Max(EdgePadding, bounds.X - tooltipSize.X - EdgePadding));
		position.Y = Mathf.Clamp(position.Y, EdgePadding, Mathf.Max(EdgePadding, bounds.Y - tooltipSize.Y - EdgePadding));
		Position = position;
	}

	private void ApplyContentSize(Control owner)
	{
		float maxContentWidth = Mathf.Min(MaxWidth, Mathf.Max(MinWidth, owner.Size.X * MaxWidthRatio));
		float titleWidth = _titleLabel.GetCombinedMinimumSize().X;
		float bodyWidth = _bodyLabel.GetCombinedMinimumSize().X;
		float contentWidth = Mathf.Clamp(Mathf.Max(titleWidth, bodyWidth), MinWidth, maxContentWidth);

		_titleLabel.CustomMinimumSize = new Vector2(contentWidth, 0.0f);
		_bodyLabel.CustomMinimumSize = new Vector2(contentWidth, 0.0f);
		float maxPanelHeight = Mathf.Max(160.0f, owner.Size.Y * MaxHeightRatio);
		float maxBodyHeight = Mathf.Max(64.0f, maxPanelHeight - 74.0f);
		float bodyHeight = Mathf.Clamp(_bodyLabel.GetCombinedMinimumSize().Y + 8.0f, MinBodyHeight, maxBodyHeight);
		_bodyScroll.CustomMinimumSize = new Vector2(contentWidth, bodyHeight);
		_bodyScroll.Size = new Vector2(contentWidth, bodyHeight);
		CustomMinimumSize = Vector2.Zero;
		Size = new Vector2(GetCombinedMinimumSize().X, Mathf.Min(GetCombinedMinimumSize().Y, maxPanelHeight));
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
