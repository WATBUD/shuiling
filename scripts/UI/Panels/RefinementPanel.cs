using Godot;
using System.Collections.Generic;

// 精煉 NPC 面板：列出「背包內未裝備的裝備」，花費金幣＋對應階級強化水晶把星等 +1（最高 10★）。
public partial class RefinementPanel : PanelContainer
{
	private PlayerController? _player;
	private VBoxContainer _itemList = null!;
	private Label _titleLabel = null!;
	private Label _goldLabel = null!;
	private Label _hintLabel = null!;

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
		if (_itemList != null)
		{
			RefreshAll();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshAll();
		}
	}

	public void RefreshAll()
	{
		if (_itemList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("refine.title");
		_hintLabel.Text = LocaleText.T("refine.hint");
		_goldLabel.Text = LocaleText.F("inventory.gold", _player?.Gold ?? 0);
		ClearChildren(_itemList);

		if (_player == null)
		{
			return;
		}

		List<string> ids = _player.GetRefinableBagEquipmentIds();
		if (ids.Count == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("refine.empty");
			_itemList.AddChild(empty);
			return;
		}

		foreach (string id in ids)
		{
			AddItemRow(id);
		}
	}

	private void BuildPanel()
	{
		Name = "RefinementPanel";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.12f;
		AnchorTop = 0.10f;
		AnchorRight = 0.88f;
		AnchorBottom = 0.90f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.97f),
			BorderColor = new Color(0.58f, 0.72f, 0.95f, 0.96f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		root.AddChild(header);

		_titleLabel = MakeLabel(24, new Color(0.82f, 0.90f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_goldLabel = MakeLabel(18, new Color(1.0f, 0.84f, 0.34f));
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_goldLabel.CustomMinimumSize = new Vector2(160.0f, 0.0f);
		header.AddChild(_goldLabel);

		_hintLabel = MakeLabel(15, new Color(0.72f, 0.82f, 0.88f));
		_hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(_hintLabel);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(scroll);

		_itemList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_itemList.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_itemList);

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	private void AddItemRow(string itemId)
	{
		if (_player == null)
		{
			return;
		}

		PlayerController.RefinementQuote quote = _player.GetRefinementQuote(itemId);
		int owned = _player.GetInventoryCount(itemId);

		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = new Color(0.32f, 0.38f, 0.45f, 0.72f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);
		_itemList.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		row.AddChild(margin);

		var content = new HBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		content.AddChild(info);

		string baseName = LocaleText.T(BuildCatalog.GetItemNameKey(itemId));
		var nameLabel = MakeLabel(17, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.Text = $"{baseName}{BuildCatalog.GetStarSuffix(itemId)}  ×{owned}";
		info.AddChild(nameLabel);

		var detailLabel = MakeLabel(14, new Color(0.80f, 0.88f, 0.94f));
		if (quote.CanRefine)
		{
			int ownedCrystals = _player.GetInventoryCount(quote.CrystalId);
			string crystalName = LocaleText.T(MonsterLootCatalog.GetNameKey(quote.CrystalId));
			detailLabel.Text = LocaleText.F(
				"refine.row.detail",
				quote.CurrentStars,
				quote.TargetStars,
				quote.SuccessPercent,
				quote.Gold,
				crystalName,
				quote.CrystalCount,
				ownedCrystals);
		}
		else
		{
			detailLabel.Text = LocaleText.T("refine.row.max");
		}

		info.AddChild(detailLabel);

		var refineButton = new Button
		{
			Text = LocaleText.T("refine.button"),
			CustomMinimumSize = new Vector2(140.0f, 48.0f),
			Disabled = !quote.CanRefine || owned <= 0,
		};
		string capturedId = itemId;
		refineButton.Pressed += () =>
		{
			if (_player != null)
			{
				_player.TryRefineBagEquipment(capturedId);
				RefreshAll();
			}
		};
		content.AddChild(refineButton);
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
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
