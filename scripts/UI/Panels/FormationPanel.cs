using Godot;
using System.Collections.Generic;

public partial class FormationPanel : PanelContainer
{
	private const int SlotCellSize = 82;

	private PlayerController? _player;
	private GridContainer _formationGrid = null!;
	private VBoxContainer _rosterList = null!;
	private Label _titleLabel = null!;
	private Label _countLabel = null!;
	private Label _selectedLabel = null!;
	private Button _clearButton = null!;
	private readonly List<FormationSlotButton> _slotButtons = new();
	private int _selectedSlot = -1;

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

	private void BuildPanel()
	{
		Name = "FormationPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -520.0f;
		OffsetRight = 520.0f;
		OffsetTop = -340.0f;
		OffsetBottom = 340.0f;
		CustomMinimumSize = new Vector2(1040.0f, 680.0f);
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

		var gridSection = MakeSection(LocaleText.T("formation.grid"), new Vector2(560.0f, 0.0f));
		content.AddChild(gridSection);

		_formationGrid = new GridContainer
		{
			Columns = 5,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		_formationGrid.AddThemeConstantOverride("h_separation", 8);
		_formationGrid.AddThemeConstantOverride("v_separation", 8);
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
				CustomMinimumSize = new Vector2(SlotCellSize, SlotCellSize),
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			};
			slot.Pressed += () => SelectSlot(slotIndex);
			_formationGrid.AddChild(slot);
			_slotButtons.Add(slot);
		}

		_selectedLabel = MakeLabel(15, new Color(0.94f, 0.97f, 1.0f));
		_selectedLabel.CustomMinimumSize = new Vector2(0.0f, 52.0f);
		gridSection.AddChild(_selectedLabel);

		_clearButton = MakeButton(LocaleText.T("formation.clear_slot"));
		_clearButton.CustomMinimumSize = new Vector2(160.0f, 36.0f);
		_clearButton.Pressed += OnClearPressed;
		gridSection.AddChild(_clearButton);

		var rosterSection = MakeSection(LocaleText.T("formation.roster"), new Vector2(380.0f, 0.0f));
		content.AddChild(rosterSection);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		rosterSection.AddChild(scroll);

		_rosterList = new VBoxContainer();
		_rosterList.AddThemeConstantOverride("separation", 8);
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
		_clearButton.Text = LocaleText.T("formation.clear_slot");
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
			_selectedLabel.Text = LocaleText.T("formation.select_empty");
			_clearButton.Disabled = true;
			return;
		}

		if (_selectedSlot == _player.FormationPlayerSlotIndex)
		{
			_selectedLabel.Text = LocaleText.F("formation.selected_player", _player.LocalizedPlayerName);
			_clearButton.Disabled = true;
			return;
		}

		SimpleActor? actor = _player.GetFormationActor(_selectedSlot);
		if (actor == null)
		{
			_selectedLabel.Text = LocaleText.F("formation.selected_empty", _selectedSlot + 1);
			_clearButton.Disabled = true;
			return;
		}

		_selectedLabel.Text = LocaleText.F("formation.selected_actor", actor.LocalizedDisplayName, actor.CombatRoleName, actor.EffectiveAttack, actor.EffectiveDefense);
		_clearButton.Disabled = false;
	}

	private void RefreshRoster()
	{
		ClearChildren(_rosterList);
		if (_player == null)
		{
			return;
		}

		int added = 0;
		AddRosterHeader("party.active");
		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				AddRosterChip(actor, "party.active");
				added++;
			}
		}

		AddRosterHeader("party.collection");
		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !_player.IsInActiveParty(actor))
			{
				AddRosterChip(actor, "party.collection");
				added++;
			}
		}

		if (added == 0)
		{
			var empty = MakeLabel(14, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("inventory.no_companions");
			_rosterList.AddChild(empty);
		}
	}

	private void AddRosterHeader(string key)
	{
		var label = MakeLabel(13, new Color(0.62f, 0.70f, 0.76f));
		label.Text = LocaleText.T(key);
		_rosterList.AddChild(label);
	}

	private void AddRosterChip(SimpleActor actor, string groupKey)
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
			Text = $"{actor.LocalizedDisplayName}\n{LocaleText.T(groupKey)} / {slotText}",
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0.0f, 54.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		chip.AddThemeFontSizeOverride("font_size", 13);
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

	private void OnClearPressed()
	{
		if (_selectedSlot >= 0)
		{
			ClearSlot(_selectedSlot);
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
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		BuildPanel();
		Visible = wasVisible;
		RefreshAll();
	}

	private bool TryGetPayload(Variant data, out FormationDragPayload? payload)
	{
		payload = data.AsGodotObject() as FormationDragPayload;
		return payload?.Actor != null && IsInstanceValid(payload.Actor);
	}

	private VBoxContainer MakeSection(string title, Vector2 minSize)
	{
		var section = new VBoxContainer
		{
			CustomMinimumSize = minSize,
		};
		section.AddThemeConstantOverride("separation", 10);

		var label = MakeLabel(17, new Color(0.86f, 0.92f, 0.98f));
		label.Text = title;
		section.AddChild(label);
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
		AddThemeFontSizeOverride("font_size", 12);
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
			OwnerPanel?.ClearSlot(SlotIndex);
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
		style.SetCornerRadiusAll(5);
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
