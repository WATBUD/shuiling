using Godot;
using System.Collections.Generic;

public partial class InventoryPanel : PanelContainer
{
	private enum EquipTarget
	{
		Helmet,
		Weapon,
		Armor,
		Accessory,
		AttributeGem,
		SkillGem1,
		SkillGem2,
		SkillGem3,
		AiGem,
	}

	private PlayerController? _player;
	private SimpleActor? _selectedActor;
	private EquipTarget _selectedTarget = EquipTarget.Weapon;
	private VBoxContainer _companionList = null!;
	private VBoxContainer _itemList = null!;
	private Label _titleLabel = null!;
	private Label _selectedActorLabel = null!;
	private Label _selectedSlotLabel = null!;
	private Label _buildSummaryLabel = null!;
	private Button _helmetButton = null!;
	private Button _weaponButton = null!;
	private Button _armorButton = null!;
	private Button _accessoryButton = null!;
	private Button _attributeButton = null!;
	private Button _skill1Button = null!;
	private Button _skill2Button = null!;
	private Button _skill3Button = null!;
	private Button _aiButton = null!;

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
		if (_companionList != null)
		{
			SelectDefaultActor();
			RefreshAll();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			SelectDefaultActor();
			RefreshAll();
		}
	}

	public void SelectActor(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !actor.IsCaptured)
		{
			return;
		}

		_selectedActor = actor;
		RefreshAll();
	}

	public void RefreshAll()
	{
		if (_player == null || _companionList == null)
		{
			return;
		}

		RefreshCompanionList();
		RefreshSlotButtons();
		RefreshItemList();
		RefreshDetails();
	}

	private void BuildPanel()
	{
		Name = "InventoryPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -560.0f;
		OffsetRight = 560.0f;
		OffsetTop = -340.0f;
		OffsetBottom = 340.0f;
		CustomMinimumSize = new Vector2(1120.0f, 680.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.034f, 0.040f, 0.050f, 0.96f),
			BorderColor = new Color(0.40f, 0.52f, 0.64f, 0.92f),
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

		_titleLabel = MakeLabel(26, new Color(1.0f, 1.0f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		var closeButton = MakeButton(LocaleText.T("ui.close"));
		closeButton.CustomMinimumSize = new Vector2(96.0f, 36.0f);
		closeButton.Pressed += OnClosePressed;
		header.AddChild(closeButton);

		var content = new HBoxContainer();
		content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		content.AddThemeConstantOverride("separation", 14);
		root.AddChild(content);

		var companionSection = MakeSection(LocaleText.T("inventory.companions"), new Vector2(260.0f, 0.0f));
		content.AddChild(companionSection);
		_companionList = MakeScrollableList(companionSection);

		var equipmentSection = MakeSection(LocaleText.T("inventory.equipment_slots"), new Vector2(420.0f, 0.0f));
		content.AddChild(equipmentSection);

		_selectedActorLabel = MakeLabel(22, new Color(1.0f, 0.96f, 0.76f));
		equipmentSection.AddChild(_selectedActorLabel);

		_buildSummaryLabel = MakeLabel(14, new Color(0.74f, 0.83f, 0.90f));
		equipmentSection.AddChild(_buildSummaryLabel);

		var slotGrid = new GridContainer { Columns = 2 };
		slotGrid.AddThemeConstantOverride("h_separation", 8);
		slotGrid.AddThemeConstantOverride("v_separation", 8);
		slotGrid.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		equipmentSection.AddChild(slotGrid);

		_helmetButton = AddSlotButton(slotGrid, EquipTarget.Helmet);
		_weaponButton = AddSlotButton(slotGrid, EquipTarget.Weapon);
		_armorButton = AddSlotButton(slotGrid, EquipTarget.Armor);
		_accessoryButton = AddSlotButton(slotGrid, EquipTarget.Accessory);
		_attributeButton = AddSlotButton(slotGrid, EquipTarget.AttributeGem);
		_aiButton = AddSlotButton(slotGrid, EquipTarget.AiGem);
		_skill1Button = AddSlotButton(slotGrid, EquipTarget.SkillGem1);
		_skill2Button = AddSlotButton(slotGrid, EquipTarget.SkillGem2);
		_skill3Button = AddSlotButton(slotGrid, EquipTarget.SkillGem3);

		_selectedSlotLabel = MakeLabel(14, new Color(0.98f, 0.98f, 0.98f));
		equipmentSection.AddChild(_selectedSlotLabel);

		var itemSection = MakeSection(LocaleText.T("inventory.items"), new Vector2(390.0f, 0.0f));
		content.AddChild(itemSection);
		_itemList = MakeScrollableList(itemSection);
		RefreshText();
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

	private Button AddSlotButton(GridContainer parent, EquipTarget target)
	{
		var button = MakeButton(string.Empty);
		button.CustomMinimumSize = new Vector2(0.0f, 54.0f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		button.Pressed += () => SelectTarget(target);
		parent.AddChild(button);
		return button;
	}

	private void SelectDefaultActor()
	{
		if (_player == null)
		{
			return;
		}

		if (_selectedActor != null && IsInstanceValid(_selectedActor) && _selectedActor.IsCaptured)
		{
			return;
		}

		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				_selectedActor = actor;
				return;
			}
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				_selectedActor = actor;
				return;
			}
		}
	}

	private void SelectTarget(EquipTarget target)
	{
		_selectedTarget = target;
		RefreshSlotButtons();
		RefreshItemList();
		RefreshDetails();
	}

	private void RefreshCompanionList()
	{
		ClearChildren(_companionList);
		if (_player == null)
		{
			return;
		}

		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				AddCompanionButton(actor, LocaleText.T("party.active"));
			}
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !_player.IsInActiveParty(actor))
			{
				AddCompanionButton(actor, LocaleText.T("party.collection"));
			}
		}

		if (_companionList.GetChildCount() == 0)
		{
			var empty = MakeLabel(14, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("inventory.no_companions");
			_companionList.AddChild(empty);
		}
	}

	private void AddCompanionButton(SimpleActor actor, string groupLabel)
	{
		var button = MakeButton($"{groupLabel}  {actor.LocalizedDisplayName}");
		button.Alignment = HorizontalAlignment.Left;
		button.CustomMinimumSize = new Vector2(0.0f, 42.0f);
		button.AddThemeColorOverride("font_color", actor == _selectedActor ? new Color(1.0f, 0.94f, 0.62f) : new Color(0.92f, 0.96f, 1.0f));
		button.Pressed += () => SelectActor(actor);
		_companionList.AddChild(button);
	}

	private void RefreshSlotButtons()
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			SetSlotsDisabled(true);
			return;
		}

		SetSlotsDisabled(false);
		CompanionBuildLoadout loadout = _selectedActor.BuildLoadout;
		SetSlotButton(_helmetButton, EquipTarget.Helmet, BuildCatalog.GetEquipment(loadout.HelmetId).NameKey);
		SetSlotButton(_weaponButton, EquipTarget.Weapon, BuildCatalog.GetEquipment(loadout.WeaponId).NameKey);
		SetSlotButton(_armorButton, EquipTarget.Armor, BuildCatalog.GetEquipment(loadout.ArmorId).NameKey);
		SetSlotButton(_accessoryButton, EquipTarget.Accessory, BuildCatalog.GetEquipment(loadout.AccessoryId).NameKey);
		SetSlotButton(_attributeButton, EquipTarget.AttributeGem, BuildCatalog.GetAttributeGem(loadout.AttributeGemId).NameKey);
		SetSlotButton(_aiButton, EquipTarget.AiGem, BuildCatalog.GetAiGem(loadout.AiGemId).NameKey);
		SetSlotButton(_skill1Button, EquipTarget.SkillGem1, BuildCatalog.GetSkillGem(loadout.SkillGemIds[0]).NameKey);
		SetSlotButton(_skill2Button, EquipTarget.SkillGem2, BuildCatalog.GetSkillGem(loadout.SkillGemIds[1]).NameKey);
		SetSlotButton(_skill3Button, EquipTarget.SkillGem3, BuildCatalog.GetSkillGem(loadout.SkillGemIds[2]).NameKey);
	}

	private void SetSlotButton(Button button, EquipTarget target, string itemNameKey)
	{
		button.Text = $"{GetTargetName(target)}\n{LocaleText.T(itemNameKey)}";
		button.AddThemeColorOverride("font_color", target == _selectedTarget ? new Color(1.0f, 0.92f, 0.50f) : new Color(0.92f, 0.96f, 1.0f));
	}

	private void SetSlotsDisabled(bool disabled)
	{
		foreach (Button button in new[] { _helmetButton, _weaponButton, _armorButton, _accessoryButton, _attributeButton, _skill1Button, _skill2Button, _skill3Button, _aiButton })
		{
			button.Disabled = disabled;
			if (disabled)
			{
				button.Text = "-";
			}
		}
	}

	private void RefreshItemList()
	{
		ClearChildren(_itemList);
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			AddItemListMessage("inventory.no_companions");
			return;
		}

		List<string> itemIds = GetCompatibleInventoryItems();
		int added = 0;
		foreach (string itemId in itemIds)
		{
			if (!_player.HasInventoryItem(itemId))
			{
				continue;
			}

			AddItemButton(itemId);
			added++;
		}

		if (added == 0)
		{
			AddItemListMessage("inventory.no_items");
		}
	}

	private List<string> GetCompatibleInventoryItems()
	{
		var ids = new List<string>();
		switch (_selectedTarget)
		{
			case EquipTarget.Helmet:
				foreach (EquipmentDefinition item in BuildCatalog.GetEquipmentDefinitions(EquipmentSlot.Helmet))
				{
					ids.Add(item.Id);
				}
				break;
			case EquipTarget.Weapon:
				foreach (EquipmentDefinition item in BuildCatalog.GetEquipmentDefinitions(EquipmentSlot.Weapon))
				{
					ids.Add(item.Id);
				}
				break;
			case EquipTarget.Armor:
				foreach (EquipmentDefinition item in BuildCatalog.GetEquipmentDefinitions(EquipmentSlot.Armor))
				{
					ids.Add(item.Id);
				}
				break;
			case EquipTarget.Accessory:
				foreach (EquipmentDefinition item in BuildCatalog.GetEquipmentDefinitions(EquipmentSlot.Accessory))
				{
					ids.Add(item.Id);
				}
				break;
			case EquipTarget.AttributeGem:
				foreach (AttributeGemDefinition item in BuildCatalog.GetAttributeGemDefinitions())
				{
					ids.Add(item.Id);
				}
				break;
			case EquipTarget.AiGem:
				foreach (AiGemDefinition item in BuildCatalog.GetAiGemDefinitions())
				{
					ids.Add(item.Id);
				}
				break;
			default:
				foreach (SkillGemDefinition item in BuildCatalog.GetSkillGemDefinitions())
				{
					ids.Add(item.Id);
				}
				break;
		}

		return ids;
	}

	private void AddItemButton(string itemId)
	{
		int count = _player?.GetInventoryCount(itemId) ?? 0;
		string countText = BuildCatalog.IsFreeItem(itemId) ? string.Empty : $"  x{count}";
		var button = MakeButton($"{LocaleText.T(BuildCatalog.GetItemNameKey(itemId))}{countText}");
		button.Alignment = HorizontalAlignment.Left;
		button.CustomMinimumSize = new Vector2(0.0f, 42.0f);
		button.Pressed += () => EquipItem(itemId);
		_itemList.AddChild(button);
	}

	private void EquipItem(string itemId)
	{
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor) || !_player.HasInventoryItem(itemId))
		{
			return;
		}

		switch (_selectedTarget)
		{
			case EquipTarget.Helmet:
				_selectedActor.EquipBuildEquipment(EquipmentSlot.Helmet, itemId);
				break;
			case EquipTarget.Weapon:
				_selectedActor.EquipBuildEquipment(EquipmentSlot.Weapon, itemId);
				break;
			case EquipTarget.Armor:
				_selectedActor.EquipBuildEquipment(EquipmentSlot.Armor, itemId);
				break;
			case EquipTarget.Accessory:
				_selectedActor.EquipBuildEquipment(EquipmentSlot.Accessory, itemId);
				break;
			case EquipTarget.AttributeGem:
				_selectedActor.EquipAttributeGem(itemId);
				break;
			case EquipTarget.AiGem:
				_selectedActor.EquipAiGem(itemId);
				break;
			case EquipTarget.SkillGem1:
				_selectedActor.EquipSkillGem(0, itemId);
				break;
			case EquipTarget.SkillGem2:
				_selectedActor.EquipSkillGem(1, itemId);
				break;
			case EquipTarget.SkillGem3:
				_selectedActor.EquipSkillGem(2, itemId);
				break;
		}

		RefreshAll();
	}

	private void RefreshDetails()
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			_selectedActorLabel.Text = LocaleText.T("inventory.no_companions");
			_selectedSlotLabel.Text = string.Empty;
			_buildSummaryLabel.Text = string.Empty;
			return;
		}

		_selectedActorLabel.Text = _selectedActor.LocalizedDisplayName;
		_buildSummaryLabel.Text = LocaleText.F(
			"inventory.build_summary",
			_selectedActor.CurrentBuildStats.BuildPower,
			_selectedActor.EffectiveAttack,
			_selectedActor.EffectiveDefense,
			_selectedActor.BuildElementName
		);
		_selectedSlotLabel.Text = LocaleText.F("inventory.selected_slot", GetTargetName(_selectedTarget));
	}

	private string GetTargetName(EquipTarget target)
	{
		return target switch
		{
			EquipTarget.Helmet => LocaleText.T("build.slot.helmet"),
			EquipTarget.Weapon => LocaleText.T("build.slot.weapon"),
			EquipTarget.Armor => LocaleText.T("build.slot.armor"),
			EquipTarget.Accessory => LocaleText.T("build.slot.accessory"),
			EquipTarget.AttributeGem => LocaleText.T("build.slot.attribute"),
			EquipTarget.SkillGem1 => LocaleText.T("build.slot.skill1"),
			EquipTarget.SkillGem2 => LocaleText.T("build.slot.skill2"),
			EquipTarget.SkillGem3 => LocaleText.T("build.slot.skill3"),
			_ => LocaleText.T("build.slot.ai"),
		};
	}

	private void AddItemListMessage(string key)
	{
		var label = MakeLabel(14, new Color(0.72f, 0.78f, 0.84f));
		label.Text = LocaleText.T(key);
		_itemList.AddChild(label);
	}

	private void RefreshText()
	{
		if (_titleLabel == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("inventory.title");
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
		button.AddThemeFontSizeOverride("font_size", 13);
		return button;
	}
}
