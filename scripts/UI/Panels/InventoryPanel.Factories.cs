using Godot;

public partial class InventoryPanel : PanelContainer
{
	private VBoxContainer MakeSection(string title, Vector2 minSize)
	{
		var section = new VBoxContainer
		{
			CustomMinimumSize = minSize,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		section.AddThemeConstantOverride("separation", 10);

		var label = MakeLabel(17, new Color(0.86f, 0.92f, 0.98f));
		label.Text = title;
		section.AddChild(label);
		return section;
	}

	private static PanelContainer MakeInfoPanel(Vector2 minSize)
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = minSize,
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.018f, 0.024f, 0.032f, 0.78f),
			BorderColor = new Color(0.22f, 0.30f, 0.38f, 0.8f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	private static VBoxContainer MakeScrollableList(VBoxContainer section)
	{
		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		section.AddChild(scroll);

		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(list);
		return list;
	}

	private static void ClearChildren(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label
		{
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static Button MakeButton(string text)
	{
		var button = new Button { Text = text };
		ApplyButtonStyle(button);
		return button;
	}

	private static void ApplyButtonStyle(Button button)
	{
		button.AddThemeFontSizeOverride("font_size", 13);
	}
}
