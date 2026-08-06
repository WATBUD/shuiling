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
	private float _detailsRefreshRemaining;
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
	private bool _contextIsPlayer;
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

		_detailsRefreshRemaining -= (float)delta;
		if (_detailsRefreshRemaining > 0.0f)
		{
			return;
		}

		_detailsRefreshRemaining = PerformanceConfig.PartyDetailsRefreshIntervalSeconds;
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
		if (!visible)
		{
			_companionInfoCard?.DiscardPendingAttributeAllocation();
		}
		if (visible)
		{
			_detailsRefreshRemaining = PerformanceConfig.PartyDetailsRefreshIntervalSeconds;
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

		_titleLabel.Text = LocaleText.F("party.title", _player.ActiveParty.Count, _player.ActivePartyLimit, _player.AvailableCompanionCount);
		if (_player.NextPartySlotLevel > 0)
		{
			_titleLabel.Text += "  " + LocaleText.F("party.next_slot", _player.NextPartySlotLevel);
		}
		AddMemberButton(FormatPlayerListText(), _selected == _player, () => SelectMember(_player), null);

		AddHeader(LocaleText.T("party.active"));
		int activeIndex = 1;
		foreach (SimpleActor actor in GetActiveCompanions())
		{
			AddMemberButton(FormatActorListText(activeIndex, actor), _selected == actor, () => SelectMember(actor), actor);
			activeIndex++;
		}

		List<SimpleActor> inactiveCompanions = GetStoredCompanions();
		if (inactiveCompanions.Count > 0)
		{
			AddHeader(LocaleText.T("party.inactive"));
			int inactiveIndex = 1;
			foreach (SimpleActor actor in inactiveCompanions)
			{
				AddMemberButton(FormatActorListText(inactiveIndex, actor), _selected == actor, () => SelectMember(actor), actor);
				inactiveIndex++;
			}
		}

		// 不再顯示「收藏」清單；改為列出已死亡的夥伴（供水池復活辨識）。
		List<SimpleActor> deadCompanions = GetDeadCompanions();
		if (deadCompanions.Count > 0)
		{
			AddHeader(LocaleText.T("party.dead"));
			int deadIndex = 1;
			foreach (SimpleActor actor in deadCompanions)
			{
				AddMemberButton(FormatActorListText(deadIndex, actor), _selected == actor, () => SelectMember(actor), actor);
				deadIndex++;
			}
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
		// The roster labels share the exact same left baseline as the panel title.
		// The detail panel keeps its inset, but the left roster must not add another
		// section margin on top of the root margin.
		listPanel.AddThemeConstantOverride("margin_left", 0);
		listPanel.CustomMinimumSize = new Vector2(300.0f, 0.0f);
		content.AddChild(listPanel);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		listPanel.AddChild(scroll);

		_memberList = new VBoxContainer();
		_memberList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
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

		string rebirthTag = _player.PlayerRebirthCount > 0 ? $" ✦x{_player.PlayerRebirthCount}" : string.Empty;
		return $"[0]: {_player.LocalizedPlayerName}{rebirthTag} {_player.CurrentHealth}/{_player.EffectiveMaxHealth}";
	}

	private static string FormatActorListText(int index, SimpleActor actor)
	{
		if (actor.IsDefeated)
		{
			// Recovery location remains gameplay state, but the roster presents one
			// unified status whether the companion is still in the field or retrieved.
			string state = LocaleText.T("party.state.dead");
			return $"[{index}]: {actor.LocalizedDisplayName} · {state}";
		}

		return $"[{index}]: {actor.LocalizedDisplayName} {actor.CurrentHealth}/{actor.EffectiveMaxHealth}";
	}

	private void AddMemberButton(string text, bool selected, System.Action onPressed, SimpleActor? actor)
	{
		var button = new Button
		{
			Text = text,
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0.0f, 38.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeStyleboxOverride("normal", MakeRosterButtonStyle(selected
			? new Color(0.13f, 0.17f, 0.21f, 0.82f)
			: Colors.Transparent));
		button.AddThemeStyleboxOverride("hover", MakeRosterButtonStyle(new Color(0.17f, 0.22f, 0.27f, 0.90f)));
		button.AddThemeStyleboxOverride("pressed", MakeRosterButtonStyle(new Color(0.21f, 0.27f, 0.33f, 0.94f)));
		button.AddThemeStyleboxOverride("focus", MakeRosterButtonStyle(Colors.Transparent));
		button.AddThemeFontSizeOverride("font_size", 14);
		button.AddThemeColorOverride("font_color", selected ? new Color(1.0f, 0.94f, 0.68f) : new Color(0.9f, 0.94f, 0.98f));
		button.Pressed += onPressed;
		button.GuiInput += inputEvent => OnMemberButtonGuiInput(button, inputEvent, actor);
		_memberList.AddChild(button);
	}

	private static StyleBoxFlat MakeRosterButtonStyle(Color background)
	{
		var style = new StyleBoxFlat { BgColor = background };
		style.SetContentMargin(Side.Left, 0.0f);
		style.SetContentMargin(Side.Right, 8.0f);
		style.SetContentMargin(Side.Top, 5.0f);
		style.SetContentMargin(Side.Bottom, 5.0f);
		style.SetCornerRadiusAll(4);
		return style;
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

	private void OnMemberButtonGuiInput(Button sourceButton, InputEvent inputEvent, SimpleActor? actor)
	{
		if (_player == null || (actor != null && !IsInstanceValid(actor)))
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { Pressed: true } mouseButton)
		{
			return;
		}

		// Do not rebuild the roster inline: it queues sourceButton for deletion while
		// its GuiInput signal is still executing (and can close the popup). Right-click
		// defers via the popup; double-click defers the toggle with CallDeferred.
		if (mouseButton.ButtonIndex == MouseButton.Right)
		{
			_selected = actor != null ? actor : _player;
			UpdateDetails();
			sourceButton.AcceptEvent();
			if (actor == null)
			{
				ShowPlayerContextMenu(GetViewport().GetMousePosition());
			}
			else
			{
				ShowMemberContextMenu(actor, GetViewport().GetMousePosition());
			}
			return;
		}

		// Double-click toggles between active and inactive; both remain in the party roster.
		if (actor != null && mouseButton is { ButtonIndex: MouseButton.Left, DoubleClick: true })
		{
			_selected = actor;
			sourceButton.AcceptEvent();
			CallDeferred(nameof(ToggleDeployment), actor);
		}
	}

	// Toggle a companion between active and inactive party states. Deploying
	// into a full active party is refused with a tip message rather than silently
	// bumping someone (use right-click "替換出戰" for a deliberate replacement).
	private void ToggleDeployment(SimpleActor actor)
	{
		if (_player == null || actor == null || !IsInstanceValid(actor))
		{
			return;
		}

		if (_player.IsInActiveParty(actor))
		{
			_player.StoreCompanion(actor);
		}
		else if (actor.IsDefeated || actor.IsAwaitingRecovery)
		{
			_player.PostSystemMessage(LocaleText.T("party.tip.cannot_deploy"), new Color(1.0f, 0.78f, 0.55f));
			return;
		}
		else if (_player.ActiveParty.Count >= _player.ActivePartyLimit)
		{
			_player.PostSystemMessage(
				_player.NextPartySlotLevel > 0
					? LocaleText.F("party.tip.full_growth", _player.ActivePartyLimit, _player.NextPartySlotLevel)
					: LocaleText.F("party.tip.full", _player.ActivePartyLimit),
				new Color(1.0f, 0.78f, 0.55f));
			return;
		}
		else
		{
			_player.DeployCompanion(actor, false);
		}

		RefreshParty();
		UpdateDetails();
	}

	private void ShowMemberContextMenu(SimpleActor actor, Vector2 screenPosition)
	{
		if (_player == null)
		{
			return;
		}

		_contextActor = actor;
		_contextIsPlayer = false;
		_memberContextMenu.Clear();

		if (_player.IsInActiveParty(actor))
		{
			_memberContextMenu.AddItem(LocaleText.T("button.set_inactive"), 1);
			bool mounted = _player.IsMountedCompanion(actor);
			_memberContextMenu.AddItem(mounted ? "解除騎乘" : LocaleText.F("button.ride", PlayerController.MountAffinityRequirement), 6);
			// 騎乘需要親密度達 50 以上（已在騎乘者可隨時解除）。
			_memberContextMenu.SetItemDisabled(_memberContextMenu.GetItemIndex(6), actor.IsDefeated || (!mounted && actor.Affinity < PlayerController.MountAffinityRequirement));
		}
		else
		{
			string deployText = _player.ActiveParty.Count >= _player.ActivePartyLimit
				? LocaleText.T("button.replace_deploy")
				: LocaleText.T("button.set_active");
			_memberContextMenu.AddItem(deployText, 2);
			_memberContextMenu.SetItemDisabled(_memberContextMenu.GetItemIndex(2), actor.IsDefeated || actor.IsAwaitingRecovery);
		}

		_memberContextMenu.AddSeparator();
		string rebirthText = actor.RebirthCount > 0
			? LocaleText.F("button.rebirth_count", actor.RebirthCount)
			: LocaleText.T("button.rebirth");
		_memberContextMenu.AddItem(rebirthText, 7);
		_memberContextMenu.SetItemDisabled(_memberContextMenu.GetItemIndex(7), !_player.CanRebirthActor(actor));
		_memberContextMenu.Position = new Vector2I(Mathf.RoundToInt(screenPosition.X), Mathf.RoundToInt(screenPosition.Y));
		_memberContextMenu.ResetSize();
		_memberContextMenu.Popup();
	}

	private void ShowPlayerContextMenu(Vector2 screenPosition)
	{
		if (_player == null)
		{
			return;
		}

		_contextActor = null;
		_contextIsPlayer = true;
		_memberContextMenu.Clear();
		string rebirthText = _player.PlayerRebirthCount > 0
			? LocaleText.F("button.rebirth_count", _player.PlayerRebirthCount)
			: LocaleText.T("button.rebirth");
		_memberContextMenu.AddItem(rebirthText, 8);
		_memberContextMenu.SetItemDisabled(_memberContextMenu.GetItemIndex(8), !_player.CanPlayerRebirth);
		_memberContextMenu.Position = new Vector2I(Mathf.RoundToInt(screenPosition.X), Mathf.RoundToInt(screenPosition.Y));
		_memberContextMenu.ResetSize();
		_memberContextMenu.Popup();
	}

	private void OnMemberContextMenuIdPressed(long id)
	{
		if (_player == null)
		{
			return;
		}

		if (_contextIsPlayer)
		{
			if (id == 8)
			{
				_player.TryPlayerRebirth();
			}
			RefreshParty();
			UpdateDetails();
			return;
		}

		if (_contextActor == null || !IsInstanceValid(_contextActor))
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
			case 6:
				_player.ToggleMountCompanion(_contextActor);
				break;
			case 7:
				_player.TryRebirthActor(_contextActor);
				break;
		}

		RefreshParty();
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

		return _selected is SimpleActor actor && IsInstanceValid(actor) && IsCaptured(actor) && !actor.IsAwaitingRecovery;
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
			if (IsInstanceValid(actor)
				&& actor.IsCaptured
				&& !actor.IsDefeated
				&& !actor.IsAwaitingRecovery)
			{
				companions.Add(actor);
			}
		}

		return companions;
	}

	// 已死亡（含倒在野外待回收）的夥伴，顯示在 U 面板的「已死亡」區。
	private List<SimpleActor> GetDeadCompanions()
	{
		var companions = new List<SimpleActor>();
		if (_player == null)
		{
			return companions;
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			// A companion still lying in the field is intentionally absent from
			// the roster. It appears here only after the owner retrieves it.
			if (IsInstanceValid(actor)
				&& actor.IsCaptured
				&& actor.IsDefeated
				&& !actor.IsAwaitingRecovery
				&& !actor.IsInWarehouseCollection)
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
			// Roster sections must be mutually exclusive. A recovered corpse has
			// IsAwaitingRecovery=false but remains defeated until revived; without
			// the IsDefeated check it appeared under both Inactive and Dead.
			if (IsInstanceValid(actor)
				&& actor.IsCaptured
				&& !actor.IsDefeated
				&& !actor.IsAwaitingRecovery
				&& !actor.IsInWarehouseCollection
				&& !_player.IsInActiveParty(actor))
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
}
