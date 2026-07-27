using Godot;
using System.Collections.Generic;

public partial class FormationPanel : PanelContainer
{
	private const float MaxDiscSize = 430.0f;
	private const float MinDiscSize = 220.0f;
	private static readonly int[] RingSlotOrder =
	{
		7, 11, 13, 17,
		6, 8, 16, 18,
		2, 10, 14, 22,
		1, 3, 5, 9, 15, 19, 21, 23,
		0, 4, 20, 24,
	};

	private PlayerController? _player;
	private Control _formationGrid = null!;
	private HFlowContainer _rosterList = null!;
	private Label _titleLabel = null!;
	private Label _countLabel = null!;
	private Label _selectedLabel = null!;
	private PopupMenu _slotContextMenu = null!;
	private FloatingTooltip _orbTooltip = null!;
	private readonly List<FormationSlotButton> _slotButtons = new();
	private int _selectedSlot = -1;
	private int _contextSlot = -1;
	private SimpleActor? _hoveredOrbActor;
	private float _discSize = MaxDiscSize;
	private float _slotCellSize = 58.0f;
	private float _playerSlotCellSize = 72.0f;
	private float _panelWidth = 1040.0f;
	private float _panelHeight = 620.0f;
	private Vector2 _lastViewportSize = Vector2.Zero;

	public System.Action? CloseRequested { get; set; }

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += OnLanguageChanged;
		SetPanelVisible(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Process(double delta)
	{
		if (_orbTooltip != null && _orbTooltip.Visible)
		{
			_orbTooltip.PositionNearMouse(this);
		}
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		if (_formationGrid != null)
		{
			RefreshAll();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RebuildForViewportIfNeeded();
			RefreshAll();
		}
	}

	public void RefreshAll()
	{
		if (_player == null || _formationGrid == null)
		{
			return;
		}

		RefreshText();
		RefreshGrid();
		RefreshRoster();
	}

	internal FormationDragPayload MakeDragPayload(SimpleActor actor, int sourceSlot)
	{
		return new FormationDragPayload
		{
			Actor = actor,
			SourceSlot = sourceSlot,
		};
	}

	internal Control MakeDragPreview(SimpleActor actor)
	{
		var tooltip = new FloatingTooltip
		{
			MaxWidthRatio = 0.34f,
			MaxWidth = 320.0f,
			MinWidth = 120.0f,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		tooltip.SetContent(actor.LocalizedDisplayName, BuildOrbTooltipBody(actor), this);
		tooltip.Position = Vector2.Zero;
		return tooltip;
	}

	internal bool CanDropDataOnSlot(int slotIndex, Variant data)
	{
		if (!TryGetPayload(data, out FormationDragPayload? payload) || payload is not { Actor: SimpleActor actor })
		{
			return false;
		}

		return IsInstanceValid(actor) && _player?.CanAssignCompanionToFormation(actor, slotIndex) == true;
	}

	internal void DropDataOnSlot(int slotIndex, Variant data)
	{
		if (!TryGetPayload(data, out FormationDragPayload? payload) || payload is not { Actor: SimpleActor actor } || _player == null)
		{
			return;
		}

		if (_player.AssignCompanionToFormation(actor, slotIndex))
		{
			_selectedSlot = slotIndex;
			RefreshAll();
		}
	}

	internal void SelectSlot(int slotIndex)
	{
		_selectedSlot = slotIndex;
		RefreshGrid();
	}

	internal void ClearSlot(int slotIndex)
	{
		if (_player == null)
		{
			return;
		}

		if (_player.ClearFormationSlot(slotIndex))
		{
			_selectedSlot = -1;
			RefreshAll();
		}
	}

	internal void ShowSlotContextMenu(int slotIndex, Vector2 globalPosition)
	{
		if (_player == null || slotIndex == _player.FormationPlayerSlotIndex || _player.GetFormationActor(slotIndex) == null)
		{
			return;
		}

		_contextSlot = slotIndex;
		_selectedSlot = slotIndex;
		RefreshGrid();
		_slotContextMenu.Clear();
		_slotContextMenu.AddItem(LocaleText.T("formation.clear_slot"), 1);
		_slotContextMenu.Position = new Vector2I(Mathf.RoundToInt(globalPosition.X), Mathf.RoundToInt(globalPosition.Y));
		_slotContextMenu.Popup();
	}

	internal void ShowOrbTooltip(SimpleActor actor)
	{
		if (!IsInstanceValid(actor))
		{
			return;
		}

		_hoveredOrbActor = actor;
		_orbTooltip.ShowTooltip(actor.LocalizedDisplayName, BuildOrbTooltipBody(actor), this);
	}

	internal void HideOrbTooltip(SimpleActor actor)
	{
		if (_hoveredOrbActor != actor)
		{
			return;
		}

		_hoveredOrbActor = null;
		if (_orbTooltip != null)
		{
			_orbTooltip.HideTooltip();
		}
	}

	private void BuildPanel()
	{
		Name = "FormationPanel";
		MouseFilter = MouseFilterEnum.Stop;
		UpdateResponsiveDiscMetrics();
		SetAnchorsPreset(LayoutPreset.Center);
		GrowHorizontal = GrowDirection.Both;
		GrowVertical = GrowDirection.Both;
		OffsetLeft = _panelWidth * -0.5f;
		OffsetRight = _panelWidth * 0.5f;
		OffsetTop = _panelHeight * -0.5f;
		OffsetBottom = _panelHeight * 0.5f;
		CustomMinimumSize = Vector2.Zero;
		AddThemeStyleboxOverride("panel", MakeStyle(new Color(0.035f, 0.041f, 0.050f, 0.96f), new Color(0.44f, 0.56f, 0.68f, 0.92f), 2));

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

		_titleLabel = MakeLabel(26, new Color(1.0f, 1.0f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		var closeButton = MakeButton(LocaleText.T("ui.close"));
		closeButton.CustomMinimumSize = new Vector2(96.0f, 36.0f);
		closeButton.Pressed += OnClosePressed;
		header.AddChild(closeButton);

		_countLabel = MakeLabel(14, new Color(0.74f, 0.83f, 0.90f));
		root.AddChild(_countLabel);

		var content = new HBoxContainer();
		content.SizeFlagsVertical = SizeFlags.ExpandFill;
		content.AddThemeConstantOverride("separation", 16);
		root.AddChild(content);

		var gridSection = MakeSection(string.Empty, new Vector2(_discSize + 28.0f, 0.0f));
		content.AddChild(gridSection);

		_formationGrid = new FormationDiscControl
		{
			DiscSize = _discSize,
			CustomMinimumSize = new Vector2(_discSize, _discSize),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		gridSection.AddChild(_formationGrid);

		_slotButtons.Clear();
		for (int index = 0; index < 25; index++)
		{
			int slotIndex = index;
			var slot = new FormationSlotButton
			{
				OwnerPanel = this,
				SlotIndex = slotIndex,
				IsPlayerSlot = slotIndex == 12,
			};
			float size = slot.IsPlayerSlot ? _playerSlotCellSize : _slotCellSize;
			slot.CustomMinimumSize = new Vector2(size, size);
			slot.Size = new Vector2(size, size);
			slot.Position = GetDiscSlotPosition(slotIndex, size);
			slot.Pressed += () => SelectSlot(slotIndex);
			slot.MouseEntered += slot.ShowOrbTooltip;
			slot.MouseExited += slot.HideOrbTooltip;
			_formationGrid.AddChild(slot);
			_slotButtons.Add(slot);
		}

		_selectedLabel = MakeLabel(15, new Color(0.94f, 0.97f, 1.0f));
		_selectedLabel.CustomMinimumSize = new Vector2(0.0f, 52.0f);
		_selectedLabel.Visible = false;
		gridSection.AddChild(_selectedLabel);

		_slotContextMenu = new PopupMenu
		{
			Name = "FormationSlotContextMenu",
		};
		_slotContextMenu.IdPressed += OnSlotContextMenuPressed;
		AddChild(_slotContextMenu);

		CreateOrbTooltip();

		var rosterSection = MakeSection(LocaleText.T("formation.roster"), new Vector2(380.0f, 0.0f));
		content.AddChild(rosterSection);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		rosterSection.AddChild(scroll);

		_rosterList = new HFlowContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_rosterList.AddThemeConstantOverride("h_separation", 8);
		_rosterList.AddThemeConstantOverride("v_separation", 8);
		scroll.AddChild(_rosterList);
	}

	private void RefreshText()
	{
		if (_player == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("formation.title");
		_countLabel.Text = LocaleText.F("formation.count", _player.ActiveParty.Count, _player.ActivePartyLimit, _player.FormationAssignedCount);
	}

	private void RefreshGrid()
	{
		if (_player == null)
		{
			return;
		}

		for (int index = 0; index < _slotButtons.Count; index++)
		{
			FormationSlotButton slot = _slotButtons[index];
			SimpleActor? actor = _player.GetFormationActor(index);
			slot.SetActor(actor, index == _selectedSlot);
		}

		RefreshSelectedDetails();
	}

	private void RefreshSelectedDetails()
	{
		if (_player == null || _selectedSlot < 0)
		{
			_selectedLabel.Visible = false;
			return;
		}

		if (_selectedSlot == _player.FormationPlayerSlotIndex)
		{
			_selectedLabel.Visible = false;
			return;
		}

		SimpleActor? actor = _player.GetFormationActor(_selectedSlot);
		if (actor == null)
		{
			_selectedLabel.Visible = false;
			return;
		}

		_selectedLabel.Visible = true;
		_selectedLabel.Text = LocaleText.F("formation.selected_actor", actor.LocalizedDisplayName, actor.CombatRoleName, actor.EffectiveAttack, actor.EffectiveDefense);
		if (!string.IsNullOrEmpty(actor.FormationBonusSummary))
		{
			_selectedLabel.Text += $"\n{LocaleText.F("formation.bonus.active", actor.FormationBonusSummary)}";
		}
	}

	private void RefreshRoster()
	{
		ClearChildren(_rosterList);
		if (_player == null)
		{
			return;
		}

		int added = 0;
		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && _player.GetFormationSlot(actor) < 0)
			{
				AddRosterChip(actor);
				added++;
			}
		}

		if (added == 0)
		{
			var empty = MakeLabel(14, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("formation.no_active_companions");
			empty.AutowrapMode = TextServer.AutowrapMode.Off;
			empty.HorizontalAlignment = HorizontalAlignment.Left;
			empty.CustomMinimumSize = new Vector2(260.0f, 28.0f);
			_rosterList.AddChild(empty);
		}
	}

	private void AddRosterChip(SimpleActor actor)
	{
		if (_player == null)
		{
			return;
		}

		string slotText = LocaleText.T("formation.unassigned");

		var chip = new FormationActorChip
		{
			OwnerPanel = this,
			Actor = actor,
			Text = $"{actor.LocalizedDisplayName}\n{slotText}",
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(116.0f, 54.0f),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		chip.AddThemeFontSizeOverride("font_size", 12);
		chip.AddThemeColorOverride("font_color", new Color(0.92f, 0.96f, 1.0f));
		chip.Pressed += () =>
		{
			_selectedSlot = -1;
			RefreshGrid();
		};
		_rosterList.AddChild(chip);
	}

	private void OnSlotContextMenuPressed(long id)
	{
		if (id == 1 && _contextSlot >= 0)
		{
			ClearSlot(_contextSlot);
			_contextSlot = -1;
		}
	}

	private void CreateOrbTooltip()
	{
		_orbTooltip = new FloatingTooltip
		{
			Name = "FormationOrbTooltip",
			MaxWidthRatio = 0.34f,
			MaxWidth = 320.0f,
			MinWidth = 120.0f,
		};
		AddChild(_orbTooltip);
	}

	private static string BuildOrbTooltipBody(SimpleActor actor)
	{
		return string.Join("\n", new[]
		{
			$"{LocaleText.T("actor.level_prefix")}{actor.Level} / {actor.CombatRoleName}",
			LocaleText.F("stat.health_value", actor.CurrentHealth, actor.EffectiveMaxHealth),
			$"{LocaleText.T("stat.attack")} {actor.EffectiveAttack}",
			$"{LocaleText.T("stat.defense")} {actor.EffectiveDefense}",
		});
	}

	private void OnClosePressed()
	{
		if (CloseRequested != null)
		{
			CloseRequested();
			return;
		}

		SetPanelVisible(false);
	}

	private void OnLanguageChanged()
	{
		bool wasVisible = Visible;
		RebuildPanel(wasVisible);
	}

	private void RebuildForViewportIfNeeded()
	{
		Vector2 viewportSize = GetViewportRect().Size;
		if (_lastViewportSize == viewportSize)
		{
			return;
		}

		RebuildPanel(true);
	}

	private void RebuildPanel(bool visible)
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		BuildPanel();
		Visible = visible;
		RefreshAll();
	}

	private bool TryGetPayload(Variant data, out FormationDragPayload? payload)
	{
		payload = data.AsGodotObject() as FormationDragPayload;
		return payload?.Actor != null && IsInstanceValid(payload.Actor);
	}

	private void UpdateResponsiveDiscMetrics()
	{
		Vector2 viewport = GetViewportRect().Size;
		_lastViewportSize = viewport;
		_panelWidth = Mathf.Clamp(viewport.X * 0.78f, Mathf.Min(760.0f, viewport.X - 80.0f), 1040.0f);
		float verticalSafeMargin = Mathf.Max(48.0f, viewport.Y * 0.10f);
		_panelHeight = Mathf.Clamp(viewport.Y * 0.80f, Mathf.Min(460.0f, viewport.Y - verticalSafeMargin * 2.0f), viewport.Y - verticalSafeMargin * 2.0f);
		float availableHeight = _panelHeight - 168.0f;
		float availableWidth = _panelWidth * 0.50f;
		_discSize = Mathf.Clamp(Mathf.Min(availableHeight, availableWidth), MinDiscSize, MaxDiscSize);
		_slotCellSize = Mathf.Clamp(_discSize * 0.092f, 24.0f, 40.0f);
		_playerSlotCellSize = Mathf.Clamp(_discSize * 0.120f, 30.0f, 52.0f);
	}

	private Vector2 GetDiscSlotPosition(int slotIndex, float slotSize)
	{
		Vector2 center = new(_discSize * 0.5f, _discSize * 0.5f);
		if (slotIndex == 12)
		{
			return center - new Vector2(slotSize, slotSize) * 0.5f;
		}

		int orderIndex = System.Array.IndexOf(RingSlotOrder, slotIndex);
		if (orderIndex < 0)
		{
			orderIndex = Mathf.Max(slotIndex - (slotIndex > 12 ? 1 : 0), 0);
		}

		int ring = Mathf.Clamp(orderIndex / 8, 0, 2);
		int ringSlot = orderIndex % 8;
		float outerRadius = _discSize * 0.44f;
		float radius = ring switch
		{
			0 => outerRadius * 0.38f,
			1 => outerRadius * 0.69f,
			_ => outerRadius,
		};
		float angle = -Mathf.Pi * 0.5f + ringSlot * (Mathf.Pi * 2.0f / 8.0f);
		Vector2 offset = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
		return center + offset - new Vector2(slotSize, slotSize) * 0.5f;
	}

	private VBoxContainer MakeSection(string title, Vector2 minSize)
	{
		var section = new VBoxContainer
		{
			CustomMinimumSize = minSize,
		};
		section.AddThemeConstantOverride("separation", 10);

		if (!string.IsNullOrWhiteSpace(title))
		{
			var label = MakeLabel(17, new Color(0.86f, 0.92f, 0.98f));
			label.Text = title;
			section.AddChild(label);
		}
		return section;
	}

	private static Button MakeButton(string text)
	{
		var button = new Button
		{
			Text = text,
		};
		button.AddThemeFontSizeOverride("font_size", 14);
		return button;
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		return label;
	}

	private static StyleBoxFlat MakeStyle(Color background, Color border, int borderWidth)
	{
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
		};
		style.SetBorderWidthAll(borderWidth);
		style.SetCornerRadiusAll(6);
		return style;
	}

	private static void ClearChildren(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}
}

public partial class FormationDiscControl : Control
{
	public float DiscSize { get; set; } = 430.0f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Pass;
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 center = Size * 0.5f;
		float outerRadius = DiscSize * 0.44f;
		float centerRadius = DiscSize * 0.036f;
		DrawCircle(center, DiscSize * 0.455f, new Color(0.045f, 0.055f, 0.066f, 0.76f));
		DrawArc(center, outerRadius * 0.38f, 0.0f, Mathf.Tau, 96, new Color(0.34f, 0.62f, 0.78f, 0.34f), 2.6f);
		DrawArc(center, outerRadius * 0.69f, 0.0f, Mathf.Tau, 96, new Color(0.72f, 0.58f, 0.30f, 0.32f), 2.6f);
		DrawArc(center, outerRadius, 0.0f, Mathf.Tau, 128, new Color(0.78f, 0.84f, 0.92f, 0.24f), 2.8f);
		DrawCircle(center, centerRadius, new Color(0.08f, 0.17f, 0.14f, 0.46f));
		DrawArc(center, centerRadius, 0.0f, Mathf.Tau, 64, new Color(0.42f, 1.0f, 0.74f, 0.36f), 1.2f);
	}
}

public partial class FormationSlotButton : Button
{
	private static readonly Dictionary<string, Texture2D> OrbIconCache = new();

	public FormationPanel? OwnerPanel { get; set; }
	public int SlotIndex { get; set; }
	public bool IsPlayerSlot { get; set; }
	public SimpleActor? Actor { get; private set; }

	public void SetActor(SimpleActor? actor, bool selected)
	{
		Actor = actor;
		Text = GetSlotText();
		Icon = GetSlotIcon();
		ExpandIcon = true;
		IconAlignment = HorizontalAlignment.Center;
		AddThemeFontSizeOverride("font_size", IsPlayerSlot ? 11 : 10);
		AddThemeColorOverride("font_color", IsPlayerSlot ? new Color(0.35f, 1.0f, 0.72f) : new Color(0.62f, 0.70f, 0.76f));
		AddThemeStyleboxOverride("normal", MakeSlotStyle(selected));
		AddThemeStyleboxOverride("hover", MakeSlotStyle(true));
		AddThemeStyleboxOverride("pressed", MakeSlotStyle(true));
	}

	public void ShowOrbTooltip()
	{
		if (Actor != null && OwnerPanel != null && !IsPlayerSlot)
		{
			OwnerPanel.ShowOrbTooltip(Actor);
		}
	}

	public void HideOrbTooltip()
	{
		if (Actor != null && OwnerPanel != null)
		{
			OwnerPanel.HideOrbTooltip(Actor);
		}
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (Actor == null || OwnerPanel == null || IsPlayerSlot)
		{
			return default;
		}

		OwnerPanel.HideOrbTooltip(Actor);
		SetDragPreview(OwnerPanel.MakeDragPreview(Actor));
		return OwnerPanel.MakeDragPayload(Actor, SlotIndex);
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return OwnerPanel?.CanDropDataOnSlot(SlotIndex, data) == true;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		OwnerPanel?.DropDataOnSlot(SlotIndex, data);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && !IsPlayerSlot)
		{
			OwnerPanel?.ShowSlotContextMenu(SlotIndex, GetGlobalMousePosition());
			AcceptEvent();
		}
	}

	private string GetSlotText()
	{
		if (IsPlayerSlot)
		{
			return LocaleText.T("formation.player_cell");
		}

		return Actor != null ? string.Empty : LocaleText.T("formation.empty_cell");
	}

	private Texture2D? GetSlotIcon()
	{
		if (Actor == null)
		{
			return null;
		}

		// 定位不再影響外觀；同一種族共用同一張可愛圖示。
		string raceId = BuildCatalog.GetRaceId(Actor);
		if (OrbIconCache.TryGetValue(raceId, out Texture2D? cached))
		{
			return cached;
		}

		Texture2D icon = CreateRaceOrbIcon(raceId);
		OrbIconCache[raceId] = icon;
		return icon;
	}

	// 每個種族一張可愛的小圓臉圖示（同種族共用）。舊的「大眼＋嘴巴」臉已移除，
	// 改成柔和的球身＋種族專屬配件（耳朵／角／光點）＋一對亮亮的小眼睛。
	private static Texture2D CreateRaceOrbIcon(string raceId)
	{
		const int size = 64;
		const float center = (size - 1) * 0.5f;
		const float radius = 27.0f;
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(new Color(0.0f, 0.0f, 0.0f, 0.0f));

		Color baseColor = GetRaceOrbColor(raceId);
		Color rimColor = baseColor.Lerp(new Color(1.0f, 1.0f, 1.0f, 1.0f), 0.35f);

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = x - center;
				float dy = y - center;
				float distance = Mathf.Sqrt(dx * dx + dy * dy);
				if (distance > radius)
				{
					continue;
				}

				float t = Mathf.Clamp(distance / radius, 0.0f, 1.0f);
				Color color = baseColor.Lerp(baseColor.Darkened(0.45f), t * 0.5f);
				if (distance > radius - 3.5f)
				{
					color = color.Lerp(rimColor, 0.6f);
				}

				float shine = Mathf.Clamp(1.0f - new Vector2(dx + 9.0f, dy + 11.0f).Length() / 20.0f, 0.0f, 1.0f);
				color = color.Lerp(new Color(1.0f, 1.0f, 1.0f, 1.0f), shine * 0.4f);
				image.SetPixel(x, y, color);
			}
		}

		// 沒有臉，改在球身上疊一個代表該種族的深色剪影圖案（人形／野獸／龍／魔／精靈）。
		DrawRaceEmblem(image, raceId);
		return ImageTexture.CreateFromImage(image);
	}

	private static Color GetRaceOrbColor(string raceId) => raceId switch
	{
		"race.human" => new Color(0.53f, 0.72f, 1.0f, 1.0f),    // 柔藍
		"race.beast" => new Color(0.95f, 0.63f, 0.34f, 1.0f),   // 暖橙
		"race.dragon" => new Color(0.44f, 0.82f, 0.50f, 1.0f),  // 嫩綠
		"race.demon" => new Color(0.72f, 0.46f, 0.95f, 1.0f),   // 夢紫
		"race.spirit" => new Color(0.42f, 0.88f, 0.92f, 1.0f),  // 水青
		_ => new Color(0.66f, 0.72f, 0.80f, 1.0f),
	};

	// 每個種族一個代表性剪影：人形、野獸腳印、飛龍、惡魔、精靈星光。
	// 全部用深色，並裁切在球身內，維持乾淨的圓形外觀。
	private static readonly Color EmblemColor = new Color(0.15f, 0.15f, 0.21f, 1.0f);

	private static void DrawRaceEmblem(Image image, string raceId)
	{
		Color c = EmblemColor;
		switch (raceId)
		{
			case "race.human": // 人形：頭＋肩身
				EmblemCircle(image, new Vector2(32.0f, 20.0f), 6.5f, c);
				EmblemTriangle(image, new Vector2(24.0f, 33.0f), new Vector2(40.0f, 33.0f), new Vector2(45.0f, 52.0f), c);
				EmblemTriangle(image, new Vector2(24.0f, 33.0f), new Vector2(45.0f, 52.0f), new Vector2(19.0f, 52.0f), c);
				break;
			case "race.beast": // 野獸腳印：大肉墊＋四趾
				EmblemCircle(image, new Vector2(32.0f, 41.0f), 9.0f, c);
				EmblemCircle(image, new Vector2(21.0f, 31.0f), 4.0f, c);
				EmblemCircle(image, new Vector2(28.5f, 25.0f), 4.2f, c);
				EmblemCircle(image, new Vector2(35.5f, 25.0f), 4.2f, c);
				EmblemCircle(image, new Vector2(43.0f, 31.0f), 4.0f, c);
				break;
			case "race.dragon": // 飛龍：雙翼＋帶角的小頭
				EmblemTriangle(image, new Vector2(26.0f, 32.0f), new Vector2(8.0f, 26.0f), new Vector2(17.0f, 43.0f), c);
				EmblemTriangle(image, new Vector2(38.0f, 32.0f), new Vector2(56.0f, 26.0f), new Vector2(47.0f, 43.0f), c);
				EmblemCircle(image, new Vector2(32.0f, 35.0f), 6.0f, c);
				EmblemCircle(image, new Vector2(32.0f, 24.0f), 4.5f, c);
				EmblemTriangle(image, new Vector2(29.0f, 21.0f), new Vector2(27.0f, 12.0f), new Vector2(33.0f, 19.0f), c);
				EmblemTriangle(image, new Vector2(35.0f, 21.0f), new Vector2(37.0f, 12.0f), new Vector2(31.0f, 19.0f), c);
				break;
			case "race.demon": // 惡魔：帶角的頭＋尖下巴
				EmblemCircle(image, new Vector2(32.0f, 31.0f), 9.0f, c);
				EmblemTriangle(image, new Vector2(25.0f, 24.0f), new Vector2(19.0f, 10.0f), new Vector2(30.0f, 22.0f), c);
				EmblemTriangle(image, new Vector2(39.0f, 24.0f), new Vector2(45.0f, 10.0f), new Vector2(34.0f, 22.0f), c);
				EmblemTriangle(image, new Vector2(27.0f, 39.0f), new Vector2(37.0f, 39.0f), new Vector2(32.0f, 51.0f), c);
				break;
			case "race.spirit": // 精靈：四角星光
				EmblemTriangle(image, new Vector2(32.0f, 8.0f), new Vector2(29.0f, 32.0f), new Vector2(35.0f, 32.0f), c);
				EmblemTriangle(image, new Vector2(32.0f, 56.0f), new Vector2(29.0f, 32.0f), new Vector2(35.0f, 32.0f), c);
				EmblemTriangle(image, new Vector2(8.0f, 32.0f), new Vector2(32.0f, 29.0f), new Vector2(32.0f, 35.0f), c);
				EmblemTriangle(image, new Vector2(56.0f, 32.0f), new Vector2(32.0f, 29.0f), new Vector2(32.0f, 35.0f), c);
				EmblemCircle(image, new Vector2(32.0f, 32.0f), 5.0f, c);
				break;
		}
	}

	// 只在球身（alpha > 0）上作畫，剪影自然被裁進圓球裡，不會有碎塊飄在球外。
	private static void EmblemCircle(Image image, Vector2 center, float radius, Color color)
	{
		int minX = Mathf.Max(Mathf.FloorToInt(center.X - radius), 0);
		int maxX = Mathf.Min(Mathf.CeilToInt(center.X + radius), image.GetWidth() - 1);
		int minY = Mathf.Max(Mathf.FloorToInt(center.Y - radius), 0);
		int maxY = Mathf.Min(Mathf.CeilToInt(center.Y + radius), image.GetHeight() - 1);
		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				if (new Vector2(x - center.X, y - center.Y).Length() <= radius && image.GetPixel(x, y).A > 0.0f)
				{
					image.SetPixel(x, y, color);
				}
			}
		}
	}

	private static void EmblemTriangle(Image image, Vector2 a, Vector2 b, Vector2 c, Color color)
	{
		int minX = Mathf.Max(Mathf.FloorToInt(Mathf.Min(a.X, Mathf.Min(b.X, c.X))), 0);
		int maxX = Mathf.Min(Mathf.CeilToInt(Mathf.Max(a.X, Mathf.Max(b.X, c.X))), image.GetWidth() - 1);
		int minY = Mathf.Max(Mathf.FloorToInt(Mathf.Min(a.Y, Mathf.Min(b.Y, c.Y))), 0);
		int maxY = Mathf.Min(Mathf.CeilToInt(Mathf.Max(a.Y, Mathf.Max(b.Y, c.Y))), image.GetHeight() - 1);
		float area = EdgeFunction(a, b, c);
		if (Mathf.IsZeroApprox(area))
		{
			return;
		}

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				var p = new Vector2(x + 0.5f, y + 0.5f);
				float w0 = EdgeFunction(b, c, p);
				float w1 = EdgeFunction(c, a, p);
				float w2 = EdgeFunction(a, b, p);
				bool inside = area > 0.0f
					? w0 >= 0.0f && w1 >= 0.0f && w2 >= 0.0f
					: w0 <= 0.0f && w1 <= 0.0f && w2 <= 0.0f;
				if (inside && image.GetPixel(x, y).A > 0.0f)
				{
					image.SetPixel(x, y, color);
				}
			}
		}
	}

	private static float EdgeFunction(Vector2 a, Vector2 b, Vector2 c)
	{
		return (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
	}

	private StyleBoxFlat MakeSlotStyle(bool highlighted)
	{
		Color background = IsPlayerSlot
			? new Color(0.07f, 0.18f, 0.14f, 0.96f)
			: Actor != null ? new Color(0.16f, 0.13f, 0.08f, 0.96f) : new Color(0.064f, 0.074f, 0.086f, 0.96f);
		Color border = highlighted
			? new Color(1.0f, 0.86f, 0.38f, 0.98f)
			: IsPlayerSlot ? new Color(0.30f, 0.88f, 0.62f, 0.88f) : new Color(0.28f, 0.36f, 0.44f, 0.86f);
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
		};
		style.SetBorderWidthAll(highlighted ? 2 : 1);
		style.SetCornerRadiusAll(99);
		return style;
	}
}

public partial class FormationActorChip : Button
{
	public FormationPanel? OwnerPanel { get; set; }
	public SimpleActor? Actor { get; set; }

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (Actor == null || OwnerPanel == null || !IsInstanceValid(Actor))
		{
			return default;
		}

		OwnerPanel.HideOrbTooltip(Actor);
		SetDragPreview(OwnerPanel.MakeDragPreview(Actor));
		return OwnerPanel.MakeDragPayload(Actor, -1);
	}
}

public partial class FormationDragPayload : RefCounted
{
	public SimpleActor? Actor { get; set; }
	public int SourceSlot { get; set; } = -1;
}
