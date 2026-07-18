using Godot;
using System.Collections.Generic;

public partial class PartyPanel : PanelContainer
{
	private PlayerController? _player;
	private VBoxContainer _memberList = null!;
	private Label _titleLabel = null!;
	private Label _levelLabel = null!;
	private Label _attackLabel = null!;
	private Label _defenseLabel = null!;
	private Label _speedLabel = null!;
	private Label _growthLabel = null!;
	private Label _experienceLabel = null!;
	private Label _abilityLabel = null!;
	private Label _combatRoleLabel = null!;
	private Label _personalityLabel = null!;
	private Label _passiveLabel = null!;
	private Label _affinityLabel = null!;
	private Label _stateLabel = null!;
	private Label _elementLabel = null!;
	private Label _equipmentLabel = null!;
	private Label _skillGemsLabel = null!;
	private Label _attackModeLabel = null!;
	private CompanionInfoCard _companionInfoCard = null!;
	private Button _helmetButton = null!;
	private Button _weaponButton = null!;
	private Button _armorButton = null!;
	private Button _accessoryButton = null!;
	private Button _attributeGemButton = null!;
	private Button _skillGem1Button = null!;
	private Button _skillGem2Button = null!;
	private Button _skillGem3Button = null!;
	private Button _attackModeButton = null!;
	private PopupMenu _memberContextMenu = null!;
	private SimpleActor? _contextActor;
	private GodotObject? _selected;

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += OnLanguageChanged;
		SetPanelVisible(false);

		if (_player != null)
		{
			RefreshParty();
			UpdateDetails();
		}
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Process(double delta)
	{
		if (!Visible)
		{
			return;
		}

		UpdateDetails();
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		_selected = player;

		if (_memberList != null)
		{
			RefreshParty();
			UpdateDetails();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshParty();
			UpdateDetails();
		}
	}

	public void RefreshParty()
	{
		if (_player == null || _memberList == null)
		{
			return;
		}

		foreach (Node child in _memberList.GetChildren())
		{
			_memberList.RemoveChild(child);
			child.QueueFree();
		}

		_titleLabel.Text = LocaleText.F("party.title", _player.ActiveParty.Count, _player.ActivePartyLimit, _player.CapturedCollection.Count);
		AddMemberButton(FormatPlayerListText(), _selected == _player, () => SelectMember(_player), null);

		AddHeader(LocaleText.T("party.active"));
		int activeIndex = 1;
		foreach (SimpleActor actor in GetActiveCompanions())
		{
			AddMemberButton(FormatActorListText(activeIndex, actor), _selected == actor, () => SelectMember(actor), actor);
			activeIndex++;
		}

		AddHeader(LocaleText.T("party.collection"));
		int storedIndex = 1;
		foreach (SimpleActor actor in GetStoredCompanions())
		{
			AddMemberButton(FormatActorListText(storedIndex, actor), _selected == actor, () => SelectMember(actor), actor);
			storedIndex++;
		}

		if (!IsSelectedValid())
		{
			SelectMember(_player);
		}

	}

	private void BuildPanel()
	{
		Name = "PartyPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -520.0f;
		OffsetRight = 520.0f;
		OffsetTop = -330.0f;
		OffsetBottom = 330.0f;
		CustomMinimumSize = new Vector2(1040.0f, 660.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.94f),
			BorderColor = new Color(0.34f, 0.46f, 0.58f, 0.95f),
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

		_titleLabel = MakeLabel(22, new Color(0.96f, 0.98f, 1.0f));
		root.AddChild(_titleLabel);

		var content = new HBoxContainer();
		content.SizeFlagsVertical = SizeFlags.ExpandFill;
		content.AddThemeConstantOverride("separation", 14);
		root.AddChild(content);

		var listPanel = MakeSection();
		listPanel.CustomMinimumSize = new Vector2(300.0f, 0.0f);
		content.AddChild(listPanel);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		listPanel.AddChild(scroll);

		_memberList = new VBoxContainer();
		_memberList.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_memberList);

		var detailPanel = MakeSection();
		detailPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		content.AddChild(detailPanel);

		var detailScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		detailPanel.AddChild(detailScroll);

		var detailRows = new VBoxContainer();
		detailRows.AddThemeConstantOverride("separation", 10);
		detailRows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		detailScroll.AddChild(detailRows);

		_companionInfoCard = new CompanionInfoCard
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		detailRows.AddChild(_companionInfoCard);


		_memberContextMenu = new PopupMenu
		{
			Name = "MemberContextMenu",
		};
		_memberContextMenu.IdPressed += OnMemberContextMenuIdPressed;
		AddChild(_memberContextMenu);
	}

	private static MarginContainer MakeSection()
	{
		var section = new MarginContainer();
		section.AddThemeConstantOverride("margin_left", 12);
		section.AddThemeConstantOverride("margin_right", 12);
		section.AddThemeConstantOverride("margin_top", 12);
		section.AddThemeConstantOverride("margin_bottom", 12);
		return section;
	}

	private void AddHeader(string text)
	{
		var label = MakeLabel(13, new Color(0.62f, 0.70f, 0.76f));
		label.Text = text;
		_memberList.AddChild(label);
	}

	private string FormatPlayerListText()
	{
		if (_player == null)
		{
			return "[0]: - 0/0";
		}

		return $"[0]: {_player.LocalizedPlayerName} {_player.CurrentHealth}/{_player.MaxHealth}";
	}

	private static string FormatActorListText(int index, SimpleActor actor)
	{
		return $"[{index}]: {actor.LocalizedDisplayName} {actor.CurrentHealth}/{actor.EffectiveMaxHealth}";
	}

	private void AddMemberButton(string text, bool selected, System.Action onPressed, SimpleActor? actor)
	{
		var button = new Button
		{
			Text = text,
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0.0f, 38.0f),
		};
		button.AddThemeFontSizeOverride("font_size", 14);
		button.AddThemeColorOverride("font_color", selected ? new Color(1.0f, 0.94f, 0.68f) : new Color(0.9f, 0.94f, 0.98f));
		button.Pressed += onPressed;
		button.GuiInput += inputEvent => OnMemberButtonGuiInput(inputEvent, actor);
		_memberList.AddChild(button);
	}

	private static Button AddBuildButton(GridContainer parent, System.Action onPressed)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(0.0f, 36.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 12);
		button.Pressed += onPressed;
		parent.AddChild(button);
		return button;
	}

	private void SelectMember(GodotObject member)
	{
		_selected = member;
		RefreshParty();
		UpdateDetails();
	}

	private void UpdateDetails()
	{
		if (_player == null)
		{
			return;
		}

		if (_selected is SimpleActor actor && IsInstanceValid(actor))
		{
			_companionInfoCard.SetActor(actor);
			return;
		}

		_selected = _player;
		_companionInfoCard.SetPlayer(_player);
	}

	private void UpdateBuildButtons(SimpleActor actor)
	{
		SetBuildButtonsDisabled(false);
		CompanionBuildLoadout loadout = actor.BuildLoadout;
		_helmetButton.Text = BuildButtonText("build.slot.helmet", LocaleText.T(BuildCatalog.GetEquipment(loadout.HelmetId).NameKey));
		_weaponButton.Text = BuildButtonText("build.slot.weapon", LocaleText.T(BuildCatalog.GetEquipment(loadout.WeaponId).NameKey));
		_armorButton.Text = BuildButtonText("build.slot.armor", LocaleText.T(BuildCatalog.GetEquipment(loadout.ArmorId).NameKey));
		_accessoryButton.Text = BuildButtonText("build.slot.accessory", LocaleText.T(BuildCatalog.GetEquipment(loadout.AccessoryId).NameKey));
		_attributeGemButton.Text = BuildButtonText("build.slot.attribute", LocaleText.T(BuildCatalog.GetAttributeGem(loadout.AttributeGemId).NameKey));
		_attackModeButton.Text = BuildButtonText("build.slot.attack_mode", actor.AttackModeName);
		_skillGem1Button.Text = BuildButtonText("build.slot.skill1", LocaleText.T(BuildCatalog.GetSkillGem(loadout.SkillGemIds[0]).NameKey));
		_skillGem2Button.Text = BuildButtonText("build.slot.skill2", LocaleText.T(BuildCatalog.GetSkillGem(loadout.SkillGemIds[1]).NameKey));
		_skillGem3Button.Text = BuildButtonText("build.slot.skill3", LocaleText.T(BuildCatalog.GetSkillGem(loadout.SkillGemIds[2]).NameKey));
		ItemIconLibrary.Apply(_helmetButton, loadout.HelmetId, 32);
		ItemIconLibrary.Apply(_weaponButton, loadout.WeaponId, 32);
		ItemIconLibrary.Apply(_armorButton, loadout.ArmorId, 32);
		ItemIconLibrary.Apply(_accessoryButton, loadout.AccessoryId, 32);
		ItemIconLibrary.Apply(_attributeGemButton, loadout.AttributeGemId, 32);
		ItemIconLibrary.Apply(_skillGem1Button, loadout.SkillGemIds[0], 32);
		ItemIconLibrary.Apply(_skillGem2Button, loadout.SkillGemIds[1], 32);
		ItemIconLibrary.Apply(_skillGem3Button, loadout.SkillGemIds[2], 32);
	}

	private static string BuildButtonText(string slotKey, string value)
	{
		return LocaleText.F("build.button.slot", LocaleText.T(slotKey), value);
	}

	private void SetBuildButtonsDisabled(bool disabled)
	{
		_helmetButton.Disabled = disabled;
		_weaponButton.Disabled = disabled;
		_armorButton.Disabled = disabled;
		_accessoryButton.Disabled = disabled;
		_attributeGemButton.Disabled = disabled;
		_skillGem1Button.Disabled = disabled;
		_skillGem2Button.Disabled = disabled;
		_skillGem3Button.Disabled = disabled;
		_attackModeButton.Disabled = disabled;

		if (disabled)
		{
			foreach (Button button in new[] { _helmetButton, _weaponButton, _armorButton, _accessoryButton, _attributeGemButton, _skillGem1Button, _skillGem2Button, _skillGem3Button })
			{
				button.Icon = null;
			}
			_helmetButton.Text = "-";
			_weaponButton.Text = "-";
			_armorButton.Text = "-";
			_accessoryButton.Text = "-";
			_attributeGemButton.Text = "-";
			_skillGem1Button.Text = "-";
			_skillGem2Button.Text = "-";
			_skillGem3Button.Text = "-";
			_attackModeButton.Text = "-";
		}
	}

	private void OnMemberButtonGuiInput(InputEvent inputEvent, SimpleActor? actor)
	{
		if (actor == null || !IsInstanceValid(actor) || _player == null)
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } mouseButton)
		{
			return;
		}

		SelectMember(actor);
		ShowMemberContextMenu(actor, mouseButton.GlobalPosition);
		AcceptEvent();
	}

	private void ShowMemberContextMenu(SimpleActor actor, Vector2 screenPosition)
	{
		if (_player == null)
		{
			return;
		}

		_contextActor = actor;
		_memberContextMenu.Clear();

		if (_player.IsInActiveParty(actor))
		{
			_memberContextMenu.AddItem(LocaleText.T("button.store"), 1);
			_memberContextMenu.AddItem(_player.IsMountedCompanion(actor) ? "解除騎乘" : "騎乘這位夥伴", 6);
			_memberContextMenu.SetItemDisabled(_memberContextMenu.GetItemIndex(6), actor.IsDefeated);
		}
		else
		{
			string deployText = _player.ActiveParty.Count >= _player.ActivePartyLimit
				? LocaleText.T("button.replace_deploy")
				: LocaleText.T("button.add_deploy");
			_memberContextMenu.AddItem(deployText, 2);
		}

		_memberContextMenu.AddSeparator();
		_memberContextMenu.AddItem(LocaleText.T("button.train"), 3);
		string materialName = string.IsNullOrEmpty(actor.EvolutionMaterialId)
			? string.Empty
			: LocaleText.T(MonsterLootCatalog.GetNameKey(actor.EvolutionMaterialId));
		string evolveText = actor.EvolutionMaterialCount > 0
			? $"{LocaleText.T("button.evolve")} ({materialName} {actor.EvolutionMaterialCount})"
			: LocaleText.T("button.evolve");
		_memberContextMenu.AddItem(evolveText, 4);
		_memberContextMenu.SetItemDisabled(_memberContextMenu.GetItemIndex(4), !_player.CanEvolveActor(actor));
		_memberContextMenu.AddItem(LocaleText.T("button.enhance_ability"), 5);
		_memberContextMenu.Position = new Vector2I(Mathf.RoundToInt(screenPosition.X), Mathf.RoundToInt(screenPosition.Y));
		_memberContextMenu.Popup();
	}

	private void OnMemberContextMenuIdPressed(long id)
	{
		if (_player == null || _contextActor == null || !IsInstanceValid(_contextActor))
		{
			return;
		}

		switch (id)
		{
			case 1:
				_player.StoreCompanion(_contextActor);
				break;
			case 2:
				_player.DeployCompanion(_contextActor, true);
				break;
			case 3:
				_contextActor.GrantTraining(25);
				break;
			case 4:
				_player.TryEvolveActor(_contextActor);
				break;
			case 5:
				_contextActor.EnhanceAbility();
				break;
			case 6:
				_player.ToggleMountCompanion(_contextActor);
				break;
		}

		RefreshParty();
		UpdateDetails();
	}

	private void OnHelmetPressed()
	{
		OpenInventoryForSelectedActor();
	}

	private void OnWeaponPressed()
	{
		OpenInventoryForSelectedActor();
	}

	private void OnArmorPressed()
	{
		OpenInventoryForSelectedActor();
	}

	private void OnAccessoryPressed()
	{
		OpenInventoryForSelectedActor();
	}

	private void OnAttributeGemPressed()
	{
		OpenInventoryForSelectedActor();
	}

	private void OnSkillGemPressed(int slotIndex)
	{
		OpenInventoryForSelectedActor();
	}

	private void OnAttackModePressed()
	{
		if (_selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		actor.CycleAttackMode();
		UpdateDetails();
	}

	private void OpenInventoryForSelectedActor()
	{
		if (_player == null || _selected is not SimpleActor actor || !IsInstanceValid(actor))
		{
			return;
		}

		_player.OpenInventoryForActor(actor);
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
		if (_player != null)
		{
			RefreshParty();
			UpdateDetails();
		}
	}

	private bool IsSelectedValid()
	{
		if (_selected == _player)
		{
			return true;
		}

		return _selected is SimpleActor actor && IsInstanceValid(actor) && IsCaptured(actor);
	}

	private bool IsCaptured(SimpleActor actor)
	{
		return actor.IsCaptured;
	}

	private List<SimpleActor> GetActiveCompanions()
	{
		var companions = new List<SimpleActor>();
		if (_player == null)
		{
			return companions;
		}

		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				companions.Add(actor);
			}
		}

		return companions;
	}

	private List<SimpleActor> GetStoredCompanions()
	{
		var companions = new List<SimpleActor>();
		if (_player == null)
		{
			return companions;
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !_player.IsInActiveParty(actor))
			{
				companions.Add(actor);
			}
		}

		return companions;
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		return label;
	}

	private static Label AddStatRow(VBoxContainer rows, string titleKey)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		rows.AddChild(row);

		var titleLabel = MakeLabel(15, new Color(0.66f, 0.74f, 0.80f));
		titleLabel.Text = LocaleText.T(titleKey);
		titleLabel.CustomMinimumSize = new Vector2(72.0f, 0.0f);
		row.AddChild(titleLabel);

		var valueLabel = MakeLabel(15, new Color(0.96f, 0.98f, 1.0f));
		valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(valueLabel);
		return valueLabel;
	}
}
