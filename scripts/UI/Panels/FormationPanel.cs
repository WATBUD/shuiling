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
	private readonly List<FormationSlotButton> _slotButtons = new();
	private int _selectedSlot = -1;
	private int _contextSlot = -1;
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
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", MakeStyle(new Color(0.10f, 0.13f, 0.16f, 0.96f), new Color(0.95f, 0.82f, 0.42f, 0.95f), 2));

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		panel.AddChild(margin);

		var label = MakeLabel(14, new Color(1.0f, 0.96f, 0.76f));
		label.Text = actor.LocalizedDisplayName;
		margin.AddChild(label);
		return panel;
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
			if (IsInstanceValid(actor) && actor.IsCaptured)
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

		int slotIndex = _player.GetFormationSlot(actor);
		string slotText = slotIndex >= 0
			? LocaleText.F("formation.slot_value", slotIndex + 1)
			: LocaleText.T("formation.unassigned");

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
		chip.AddThemeColorOverride("font_color", slotIndex >= 0 ? new Color(1.0f, 0.94f, 0.62f) : new Color(0.92f, 0.96f, 1.0f));
		chip.Pressed += () =>
		{
			if (_player == null)
			{
				return;
			}

			int actorSlot = _player.GetFormationSlot(actor);
			if (actorSlot >= 0)
			{
				SelectSlot(actorSlot);
			}
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
		_panelHeight = Mathf.Clamp(viewport.Y * 0.68f, Mathf.Min(420.0f, viewport.Y - 96.0f), viewport.Y - 96.0f);
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
	public FormationPanel? OwnerPanel { get; set; }
	public int SlotIndex { get; set; }
	public bool IsPlayerSlot { get; set; }
	public SimpleActor? Actor { get; private set; }

	public void SetActor(SimpleActor? actor, bool selected)
	{
		Actor = actor;
		Text = GetSlotText();
		AddThemeFontSizeOverride("font_size", IsPlayerSlot ? 11 : 10);
		AddThemeColorOverride("font_color", IsPlayerSlot ? new Color(0.35f, 1.0f, 0.72f) : actor != null ? new Color(1.0f, 0.94f, 0.62f) : new Color(0.62f, 0.70f, 0.76f));
		AddThemeStyleboxOverride("normal", MakeSlotStyle(selected));
		AddThemeStyleboxOverride("hover", MakeSlotStyle(true));
		AddThemeStyleboxOverride("pressed", MakeSlotStyle(true));
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (Actor == null || OwnerPanel == null || IsPlayerSlot)
		{
			return default;
		}

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

		return Actor != null ? Actor.LocalizedDisplayName : LocaleText.T("formation.empty_cell");
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

		SetDragPreview(OwnerPanel.MakeDragPreview(Actor));
		return OwnerPanel.MakeDragPayload(Actor, -1);
	}
}

public partial class FormationDragPayload : RefCounted
{
	public SimpleActor? Actor { get; set; }
	public int SourceSlot { get; set; } = -1;
}
