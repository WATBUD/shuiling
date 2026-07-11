using Godot;
using System.Collections.Generic;

public partial class SystemLogPanel : PanelContainer
{
	private const int MaxMessages = 7;

	private readonly Queue<Label> _messageLabels = new();
	private VBoxContainer _rows = null!;

	public override void _Ready()
	{
		BuildPanel();
		Visible = false;
	}

	public void AddMessage(string message, Color color)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}

		Visible = true;
		var label = new Label
		{
			Text = message,
			AutowrapMode = TextServer.AutowrapMode.Off,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			CustomMinimumSize = new Vector2(0.0f, 22.0f),
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
		label.AddThemeConstantOverride("outline_size", 4);
		_rows.AddChild(label);
		_messageLabels.Enqueue(label);

		while (_messageLabels.Count > MaxMessages)
		{
			Label oldLabel = _messageLabels.Dequeue();
			oldLabel.QueueFree();
		}

		FadeAndRemove(label);
	}

	private void BuildPanel()
	{
		Name = "SystemLogPanel";
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = 0.0f;
		AnchorRight = 0.0f;
		AnchorTop = 1.0f;
		AnchorBottom = 1.0f;
		OffsetLeft = 22.0f;
		OffsetRight = 440.0f;
		OffsetTop = -210.0f;
		OffsetBottom = -34.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.030f, 0.036f, 0.52f),
			BorderColor = new Color(0.40f, 0.48f, 0.56f, 0.34f),
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

		_rows = new VBoxContainer();
		_rows.AddThemeConstantOverride("separation", 2);
		margin.AddChild(_rows);
	}

	private void FadeAndRemove(Label label)
	{
		var tween = CreateTween();
		tween.TweenInterval(5.0f);
		tween.TweenProperty(label, "modulate:a", 0.0f, 1.25f);
		tween.Finished += () =>
		{
			if (IsInstanceValid(label))
			{
				RemoveTrackedLabel(label);
				label.QueueFree();
			}

			if (_rows.GetChildCount() <= 0)
			{
				Visible = false;
			}
		};
	}

	private void RemoveTrackedLabel(Label label)
	{
		int count = _messageLabels.Count;
		for (int index = 0; index < count; index++)
		{
			Label current = _messageLabels.Dequeue();
			if (current != label && IsInstanceValid(current))
			{
				_messageLabels.Enqueue(current);
			}
		}
	}
}
