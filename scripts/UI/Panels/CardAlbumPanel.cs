using Godot;
using System.Collections.Generic;

// Card album (卡冊) — the monster-card collection. Shows every card owned (one
// per model) and the resulting team stat bonus.
public partial class CardAlbumPanel : PanelContainer
{
	private PlayerController? _player;
	private Label _titleLabel = null!;
	private Label _summaryLabel = null!;
	private Label _emptyLabel = null!;
	private GridContainer _grid = null!;

	public System.Action? CloseRequested { get; set; }

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += RefreshAll;
		SetPanelVisible(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshAll;
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		RefreshAll();
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshAll();
		}
	}

	private void BuildPanel()
	{
		Name = "CardAlbumPanel";
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -420.0f;
		OffsetRight = 420.0f;
		OffsetTop = -300.0f;
		OffsetBottom = 300.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.09f, 0.96f),
			BorderColor = new Color(0.68f, 0.82f, 1.0f, 0.78f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		AddChild(margin);

		var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 8);
		margin.AddChild(root);

		_titleLabel = new Label { Text = LocaleText.T("card.title"), HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 24);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.94f, 1.0f));
		root.AddChild(_titleLabel);

		_summaryLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_summaryLabel.AddThemeFontSizeOverride("font_size", 15);
		_summaryLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.68f));
		root.AddChild(_summaryLabel);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(800.0f, 420.0f),
		};
		root.AddChild(scroll);

		_grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_grid.AddThemeConstantOverride("h_separation", 8);
		_grid.AddThemeConstantOverride("v_separation", 8);
		scroll.AddChild(_grid);

		_emptyLabel = new Label { Text = LocaleText.T("card.empty"), HorizontalAlignment = HorizontalAlignment.Center };
		_emptyLabel.AddThemeFontSizeOverride("font_size", 15);
		_emptyLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.80f));
		root.AddChild(_emptyLabel);

		var closeButton = new Button { Text = LocaleText.T("dialog.button.close"), CustomMinimumSize = new Vector2(0.0f, 40.0f) };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	public void RefreshAll()
	{
		if (_grid == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("card.title");

		ClearChildren(_grid);
		if (_player == null)
		{
			return;
		}

		int count = _player.OwnedCardCount;
		int bonusPercent = Mathf.RoundToInt((_player.CardCollectionMultiplier - 1.0f) * 100.0f);
		_summaryLabel.Text = LocaleText.F("card.summary", count, bonusPercent);
		_emptyLabel.Visible = count == 0;

		foreach (string key in _player.GetOwnedCardKeys())
		{
			_grid.AddChild(BuildCardCell(key));
		}
	}

	private Control BuildCardCell(string cardKey)
	{
		var cell = new PanelContainer { CustomMinimumSize = new Vector2(148.0f, 96.0f) };
		var cellStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.14f, 0.22f, 0.96f),
			BorderColor = new Color(1.0f, 0.84f, 0.42f, 0.85f),
		};
		cellStyle.SetBorderWidthAll(2);
		cellStyle.SetCornerRadiusAll(6);
		cell.AddThemeStyleboxOverride("panel", cellStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		cell.AddChild(margin);

		var box = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 4);
		margin.AddChild(box);

		var tag = new Label { Text = LocaleText.T("card.tag"), HorizontalAlignment = HorizontalAlignment.Center };
		tag.AddThemeFontSizeOverride("font_size", 11);
		tag.AddThemeColorOverride("font_color", new Color(0.72f, 0.82f, 0.94f));
		box.AddChild(tag);

		var name = new Label
		{
			Text = ExternalModelLibrary.LocalizedCardName(cardKey),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		name.AddThemeFontSizeOverride("font_size", 16);
		name.AddThemeColorOverride("font_color", new Color(1.0f, 0.96f, 0.86f));
		box.AddChild(name);

		return cell;
	}

	private static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}
}
