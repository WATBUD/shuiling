using Godot;
using System.Collections.Generic;

public partial class CompanionInfoCard : PanelContainer
{
	private Label _title = null!;
	private ProgressBar _experienceBar = null!;
	private Label _experience = null!;
	private Label _stats = null!;
	private Label _meta = null!;
	private Label _mode = null!;
	private Label _mood = null!;
	private Label _ability = null!;
	private Label _traitsTitle = null!;
	private Label _equipmentTitle = null!;
	private Label _skillGemsTitle = null!;
	private HFlowContainer _traitFlow = null!;
	private HFlowContainer _equipmentFlow = null!;
	private HFlowContainer _skillGemFlow = null!;
	private FloatingTooltip _tooltip = null!;
	private SimpleActor? _actor;
	private PlayerController? _player;
	private string _detailSignature = string.Empty;

	public override void _Ready()
	{
		var style = new StyleBoxFlat { BgColor = new Color(0.055f, 0.065f, 0.075f, 0.88f), BorderColor = new Color(0.34f, 0.46f, 0.58f, 0.90f) };
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);
		var rows = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
		rows.AddThemeConstantOverride("separation", 3);
		margin.AddChild(rows);

		var header = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		header.AddThemeConstantOverride("separation", 8);
		rows.AddChild(header);
		_title = MakeLabel(14, new Color(1.0f, 0.92f, 0.58f));
		_title.AutowrapMode = TextServer.AutowrapMode.Off;
		_title.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		header.AddChild(_title);
		_experience = MakeLabel(14, new Color(1.0f, 0.92f, 0.58f));
		_experience.AutowrapMode = TextServer.AutowrapMode.Off;
		_experience.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		header.AddChild(_experience);
		_experienceBar = new ProgressBar { MinValue = 0.0, MaxValue = 1.0, ShowPercentage = false, CustomMinimumSize = new Vector2(0.0f, 14.0f), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		rows.AddChild(_experienceBar);

		var columns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		columns.AddThemeConstantOverride("separation", 12);
		rows.AddChild(columns);
		_stats = MakeLabel(12, new Color(0.80f, 0.87f, 0.93f));
		_stats.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(_stats);
		var metaRows = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		metaRows.AddThemeConstantOverride("separation", 0);
		columns.AddChild(metaRows);
		_mode = AddInteractiveLabel(metaRows, new Color(0.88f, 0.95f, 0.72f));
		_mode.GuiInput += OnModeGuiInput;
		_mood = MakeLabel(12, new Color(1.0f, 0.78f, 0.82f));
		metaRows.AddChild(_mood);
		_meta = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f));
		metaRows.AddChild(_meta);
		_ability = AddInteractiveLabel(metaRows);
		_traitsTitle = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f), "build.traits");
		metaRows.AddChild(_traitsTitle);
		_traitFlow = AddFlow(metaRows);
		_equipmentTitle = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f), "build.equipment");
		metaRows.AddChild(_equipmentTitle);
		_equipmentFlow = AddFlow(metaRows);
		_skillGemsTitle = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f), "build.skill_gems");
		metaRows.AddChild(_skillGemsTitle);
		_skillGemFlow = AddFlow(metaRows);

		_tooltip = new FloatingTooltip
		{
			Name = "CompanionDetailTooltip",
			TopLevel = true,
			ZIndex = 100,
			MaxWidth = 720.0f,
			MaxWidthRatio = 0.70f,
			MaxHeightRatio = 0.72f,
		};
		AddChild(_tooltip);
		BindTooltip(_ability, BuildAbilityTooltip);
		SetActor(_actor);
	}

	public override void _Process(double delta)
	{
		if (_tooltip != null && _tooltip.Visible)
		{
			_tooltip.PositionNearMouse(this);
		}
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (_tooltip == null || !_tooltip.Visible || inputEvent is not InputEventMouseButton { Pressed: true } mouseButton)
		{
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			_tooltip.ScrollDetail(-48);
			GetViewport().SetInputAsHandled();
		}
		else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			_tooltip.ScrollDetail(48);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnModeGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			CycleMode();
			AcceptEvent();
		}
	}

	public void SetActor(SimpleActor? actor)
	{
		_player = null;
		_actor = actor != null && IsInstanceValid(actor) ? actor : null;
		if (_title == null) return;
		if (_actor == null)
		{
			_title.Text = LocaleText.T("inventory.companion_info");
			_experienceBar.Value = 0.0;
			_experience.Text = LocaleText.T("inventory.no_companions");
			_stats.Text = _meta.Text = _mood.Text = _ability.Text = string.Empty;
			ClearFlow(_traitFlow);
			ClearFlow(_equipmentFlow);
			ClearFlow(_skillGemFlow);
			_detailSignature = string.Empty;
			_mode.Visible = false;
			_mood.Visible = false;
			_tooltip?.HideTooltip();
			return;
		}

		BuildStats stats = _actor.CurrentBuildStats;
		SetPetSectionsVisible(true);
		_traitsTitle.Text = LocaleText.T("build.traits");
		_equipmentTitle.Text = LocaleText.T("build.equipment");
		_skillGemsTitle.Text = LocaleText.T("build.skill_gems");
		_experienceBar.Visible = true;
		_title.Text = $"{_actor.LocalizedDisplayName} - {LocaleText.F("inventory.info_header", _actor.Level)}";
		_experienceBar.MaxValue = Mathf.Max(_actor.ExperienceToNextLevel, 1);
		_experienceBar.Value = Mathf.Clamp(_actor.Experience, 0, _actor.ExperienceToNextLevel);
		_experience.Text = $"{LocaleText.T("stat.experience")} {_actor.Experience}/{_actor.ExperienceToNextLevel}";
		_stats.Text = string.Join("\n",
			$"HP {(_actor.IsDefeated ? 0 : _actor.CurrentHealth)} / {_actor.EffectiveMaxHealth}",
			$"{LocaleText.T("stat.attack")} {LocaleText.F("build.effective_stat", _actor.EffectiveAttack, _actor.Attack)}",
			$"{LocaleText.T("stat.defense")} {LocaleText.F("build.effective_stat", _actor.EffectiveDefense, _actor.Defense)}",
			$"{LocaleText.T("stat.speed")} {_actor.EffectiveMoveSpeed:0.0}",
			$"{LocaleText.T("tooltip.attack_range")} {_actor.EffectiveAttackRange:0.0}",
			$"{LocaleText.T("tooltip.detection_radius")} {_actor.EffectiveDetectionRadius:0.0}",
			$"{LocaleText.T("tooltip.crit_chance")} {stats.CritChance * 100.0f:0.#}%",
			$"{LocaleText.T("stat.growth")} {_actor.GrowthName}",
			$"{LocaleText.T("stat.state")} {_actor.StateName}");
		_meta.Text = string.Join("\n",
			$"{_actor.TypeName} / {_actor.CombatRangeName}",
			$"{LocaleText.T("stat.role")} {_actor.CombatRoleName}",
			$"{LocaleText.T("stat.affinity")} {_actor.Affinity} / 100",
			$"{LocaleText.T("build.element")} {_actor.BuildElementName}");
		_mood.Text = $"{LocaleText.T("stat.mood")}：{_actor.MoodName}";
		_mood.Visible = true;
		_ability.Text = $"{LocaleText.T("stat.ability")} {_actor.LocalizedSpecialAbility} {LocaleText.T("actor.level_prefix")}{_actor.AbilityRank}";
		string detailSignature = BuildDetailSignature(_actor);
		if (_detailSignature != detailSignature)
		{
			_detailSignature = detailSignature;
			RebuildDetailTerms();
		}
		_mode.Text = $"{LocaleText.T("build.slot.attack_mode")}: {_actor.AttackModeName}";
		_mode.Visible = true;
	}

	public void SetPlayer(PlayerController? player)
	{
		_actor = null;
		_player = player != null && IsInstanceValid(player) ? player : null;
		if (_title == null) return;
		if (_player == null)
		{
			SetActor(null);
			return;
		}

		_title.Text = $"{_player.LocalizedPlayerName} - {LocaleText.F("inventory.info_header", _player.Level)}";
		_experience.Text = $"{LocaleText.T("stat.experience")} {_player.Experience}/{_player.ExperienceToNextLevel}";
		_experienceBar.MaxValue = Mathf.Max(_player.ExperienceToNextLevel, 1);
		_experienceBar.Value = Mathf.Clamp(_player.Experience, 0, _player.ExperienceToNextLevel);
		_experienceBar.Visible = true;
		_stats.Text = string.Join("\n",
			$"HP {_player.CurrentHealth} / {_player.MaxHealth}",
			$"{LocaleText.T("stat.attack")} {_player.Attack}",
			$"{LocaleText.T("stat.defense")} {_player.Defense}",
			$"{LocaleText.T("stat.speed")} {_player.WalkSpeed:0.0}",
			$"{LocaleText.T("tooltip.attack_range")} {_player.AttackRange:0.0}",
			$"{LocaleText.T("tooltip.detection_radius")} {_player.DetectionRadius:0.0}",
			$"{LocaleText.T("tooltip.crit_chance")} {_player.CritChance * 100.0f:0.#}%",
			$"{LocaleText.T("stat.state")} {LocaleText.T("party.playable")}");
		_meta.Text = string.Join("\n",
			$"Player / {LocaleText.T("combat.range.melee")}",
			$"{LocaleText.T("stat.role")} {LocaleText.T("role.dps")} / {LocaleText.T("personality.brave")}",
			$"{LocaleText.T("build.element")} {LocaleText.T("element.physical")}",
			LocaleText.F("inventory.gold", _player.Gold),
			LocaleText.F("party.title", _player.ActiveParty.Count, _player.ActivePartyLimit, _player.CapturedCollection.Count));
		_ability.Text = string.Empty;
		_mode.Visible = false;
		_mood.Visible = false;
		SetPetSectionsVisible(true);
		_ability.Visible = false;
		_traitsTitle.Text = LocaleText.T("build.traits");
		_equipmentTitle.Text = LocaleText.T("build.equipment");
		_skillGemsTitle.Text = LocaleText.T("stat.ability");
		string playerSignature = $"player|{_player.Level}|{_player.Attack}|{_player.Defense}|{_player.WalkSpeed}|{_player.SprintSpeed}|{_player.CaptureNetCapacity}|{_player.CaptureNetRechargeSeconds}";
		if (_detailSignature != playerSignature)
		{
			_detailSignature = playerSignature;
			RebuildPlayerTerms();
		}
	}

	private void RebuildPlayerTerms()
	{
		ClearFlow(_traitFlow);
		ClearFlow(_equipmentFlow);
		ClearFlow(_skillGemFlow);
		if (_player == null) return;

		AddTerm(_traitFlow, LocaleText.T("player.trait.runner"), () =>
			(LocaleText.T("player.trait.runner"), $"{LocaleText.T("stat.speed")} {_player.WalkSpeed:0.0} -> {_player.SprintSpeed:0.0}"));
		AddTerm(_traitFlow, LocaleText.T("player.trait.resilience"), () =>
			(LocaleText.T("player.trait.resilience"), $"HP {_player.MaxHealth}\n{LocaleText.T("stat.defense")} {_player.Defense}"));
		AddTerm(_equipmentFlow, LocaleText.T("player.equipment.sword"), () =>
			(LocaleText.T("player.equipment.sword"), $"{LocaleText.T("stat.attack")} {_player.Attack}\n{LocaleText.T("tooltip.attack_range")} {_player.AttackRange:0.0}"));
		AddTerm(_equipmentFlow, LocaleText.T("player.equipment.shield"), () =>
			(LocaleText.T("player.equipment.shield"), $"{LocaleText.T("stat.defense")} {_player.Defense}\nHP {_player.MaxHealth}"));
		AddTerm(_skillGemFlow, LocaleText.T("player.skill.capture"), () =>
			(LocaleText.T("player.skill.capture"), $"Capacity {_player.CaptureNetCapacity}\nRecharge {_player.CaptureNetRechargeSeconds:0.0}s"));
		AddTerm(_skillGemFlow, LocaleText.T("player.skill.sprint"), () =>
			(LocaleText.T("player.skill.sprint"), $"{LocaleText.T("stat.speed")} {_player.WalkSpeed:0.0} -> {_player.SprintSpeed:0.0}\nJump {_player.JumpVelocity:0.0}"));
	}

	private void SetPetSectionsVisible(bool visible)
	{
		_ability.Visible = visible;
		_traitsTitle.Visible = visible;
		_traitFlow.Visible = visible;
		_equipmentTitle.Visible = visible;
		_equipmentFlow.Visible = visible;
		_skillGemsTitle.Visible = visible;
		_skillGemFlow.Visible = visible;
	}

	private void CycleMode()
	{
		if (_actor == null || !IsInstanceValid(_actor)) return;
		_actor.CycleAttackMode();
		SetActor(_actor);
	}

	private void RebuildDetailTerms()
	{
		ClearFlow(_traitFlow);
		ClearFlow(_equipmentFlow);
		ClearFlow(_skillGemFlow);
		if (_actor == null) return;

		foreach (string traitKey in _actor.TraitKeys)
		{
			string capturedKey = traitKey;
			AddTerm(_traitFlow, LocaleText.T(capturedKey), () => BuildSingleTraitTooltip(capturedKey));
		}

		CompanionBuildLoadout loadout = _actor.BuildLoadout;
		var equipmentItems = new (string Id, string Slot)[]
		{
			(loadout.HelmetId, LocaleText.T("build.slot.helmet")),
			(loadout.WeaponId, LocaleText.T("build.slot.weapon")),
			(loadout.ArmorId, LocaleText.T("build.slot.armor")),
			(loadout.AccessoryId, LocaleText.T("build.slot.accessory")),
			(loadout.AttributeGemId, LocaleText.T("build.slot.attribute")),
		};
		foreach ((string id, string slot) in equipmentItems)
		{
			string capturedId = id;
			string capturedSlot = slot;
			string name = BuildCatalog.GetItemKind(id) == InventoryItemKind.AttributeGem
				? LocaleText.T(BuildCatalog.GetAttributeGem(id).NameKey)
				: LocaleText.T(BuildCatalog.GetEquipment(id).NameKey);
			AddTerm(_equipmentFlow, name, () => (name, InventoryPanel.BuildItemTooltipBody(capturedId, capturedSlot)));
		}

		int unlockedCores = BuildCatalog.GetUnlockedSupportCoreCount(_actor.Level);
		for (int index = 0; index < loadout.SkillGemIds.Length && index < unlockedCores; index++)
		{
			string id = loadout.GetSkillGemId(index);
			if (id == "gem.skill.none")
			{
				continue;
			}

			string slot = LocaleText.F("build.slot.support_core", index + 1);
			SkillGemDefinition gem = BuildCatalog.GetSkillGem(id);
			string name = $"{LocaleText.T(gem.NameKey)} Lv.{loadout.GetSkillGemLevel(index)}";
			AddTerm(_skillGemFlow, name, () => (name, InventoryPanel.BuildItemTooltipBody(id, slot)));
		}
	}

	private static string BuildDetailSignature(SimpleActor actor)
	{
		CompanionBuildLoadout loadout = actor.BuildLoadout;
		return string.Join("|",
			actor.GetInstanceId(),
			actor.SpecialAbility,
			actor.AbilityRank,
			string.Join(",", actor.TraitKeys),
			loadout.HelmetId,
			loadout.WeaponId,
			loadout.ArmorId,
			loadout.AccessoryId,
			loadout.AttributeGemId,
			string.Join(",", loadout.SkillGemIds),
			string.Join(",", loadout.SkillGemLevels));
	}

	private (string, string) BuildSingleTraitTooltip(string traitKey)
	{
		if (_actor == null) return (string.Empty, string.Empty);
		CompanionIdentity identity = BuildCatalog.GetIdentity(_actor);
		string value = traitKey switch
		{
			"identity.passive.move_speed" => $"{LocaleText.T("stat.speed")} +{(identity.MoveSpeedMultiplier - 1.0f) * 100.0f:0.#}%\n{_actor.MoveSpeed:0.0} -> {_actor.MoveSpeed * identity.MoveSpeedMultiplier:0.0}",
			"identity.passive.crit_rate" => $"{LocaleText.T("tooltip.crit_chance")} +{identity.CritChanceBonus * 100.0f:0.#}%\n{LocaleText.T("tooltip.attack_cooldown")} {(1.0f - identity.AttackCooldownMultiplier) * 100.0f:0.#}%",
			"identity.passive.water_damage" or "identity.passive.fire_damage" => $"{LocaleText.T($"element.{identity.ElementAffinityId}")} +{(identity.ElementAffinityDamageMultiplier - 1.0f) * 100.0f:0.#}%\n{LocaleText.T("stat.attack")} x{identity.AttackMultiplier:0.00}",
			"identity.passive.water_aoe" or "identity.passive.attack_range" => $"{LocaleText.T("tooltip.attack_range")} +{identity.AttackRangeBonus:0.0}\n{_actor.AttackRange:0.0} -> {_actor.AttackRange + identity.AttackRangeBonus:0.0}",
			"identity.passive.vitality" => BuildHealthTraitText(identity),
			"identity.passive.power_strike" => $"{LocaleText.T("stat.attack")} +{identity.AttackBonus}\n{_actor.Attack} -> {_actor.Attack + identity.AttackBonus}",
			"identity.passive.thick_hide" => $"HP +{(identity.MaxHealthMultiplier - 1.0f) * 100.0f:0.#}%\n{LocaleText.T("stat.defense")} +{(identity.DefenseMultiplier - 1.0f) * 100.0f:0.#}%",
			"identity.passive.poison_mastery" => $"{LocaleText.T("stat.attack")} +{identity.AttackBonus}\n{LocaleText.T("element.poison")} +{(identity.ElementAffinityDamageMultiplier - 1.0f) * 100.0f:0.#}%",
			"identity.passive.agility" => $"{LocaleText.T("stat.speed")} +{(identity.MoveSpeedMultiplier - 1.0f) * 100.0f:0.#}%\n{_actor.MoveSpeed:0.0} -> {_actor.MoveSpeed * identity.MoveSpeedMultiplier:0.0}",
			"identity.passive.guard_oath" => $"HP +{identity.MaxHealthBonus}\n{LocaleText.T("stat.defense")} +{identity.DefenseBonus}",
			"identity.passive.adaptable" => $"HP +{identity.MaxHealthBonus}\n{LocaleText.T("stat.attack")} +{identity.AttackBonus}\n{LocaleText.T("stat.defense")} +{identity.DefenseBonus}\n{LocaleText.T("stat.speed")} +{(identity.MoveSpeedMultiplier - 1.0f) * 100.0f:0.#}%",
			_ => LocaleText.T(traitKey),
		};
		return (LocaleText.T(traitKey), value);
	}

	private static string BuildHealthTraitText(CompanionIdentity identity)
	{
		var lines = new List<string>();
		if (identity.MaxHealthBonus != 0) lines.Add($"HP +{identity.MaxHealthBonus}");
		if (!Mathf.IsEqualApprox(identity.MaxHealthMultiplier, 1.0f)) lines.Add($"HP +{(identity.MaxHealthMultiplier - 1.0f) * 100.0f:0.#}%");
		return string.Join("\n", lines);
	}

	private void AddTerm(HFlowContainer flow, string text, System.Func<(string Title, string Body)> factory)
	{
		Label label = MakeLabel(12, new Color(0.88f, 0.95f, 0.72f));
		label.Text = $"[{text}]";
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.MouseFilter = MouseFilterEnum.Stop;
		label.MouseDefaultCursorShape = CursorShape.PointingHand;
		flow.AddChild(label);
		BindTooltip(label, factory);
	}

	private static HFlowContainer AddFlow(VBoxContainer parent)
	{
		var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		flow.AddThemeConstantOverride("h_separation", 5);
		flow.AddThemeConstantOverride("v_separation", 1);
		parent.AddChild(flow);
		return flow;
	}

	private static void ClearFlow(HFlowContainer flow)
	{
		foreach (Node child in flow.GetChildren())
		{
			flow.RemoveChild(child);
			child.QueueFree();
		}
	}

	private Label AddInteractiveLabel(VBoxContainer parent, Color? color = null)
	{
		Label label = MakeLabel(12, color ?? new Color(0.82f, 0.92f, 0.76f));
		label.MouseFilter = MouseFilterEnum.Stop;
		label.MouseDefaultCursorShape = CursorShape.PointingHand;
		parent.AddChild(label);
		return label;
	}

	private void BindTooltip(Control control, System.Func<(string Title, string Body)> factory)
	{
		control.MouseEntered += () =>
		{
			bool hasActor = _actor != null && IsInstanceValid(_actor);
			bool hasPlayer = _player != null && IsInstanceValid(_player);
			if (!hasActor && !hasPlayer) return;
			(string title, string body) = factory();
			_tooltip.ShowTooltip(title, body, this);
		};
		control.MouseExited += () => _tooltip.HideTooltip();
	}

	private (string, string) BuildAbilityTooltip()
	{
		if (_actor == null) return (string.Empty, string.Empty);
		BuildStats stats = _actor.CurrentBuildStats;
		ProjectileBehaviorProfile behavior = stats.Behavior;
		var lines = new List<string>
		{
			$"{_actor.LocalizedSpecialAbility} {LocaleText.T("actor.level_prefix")}{_actor.AbilityRank}",
			$"{LocaleText.T("stat.attack")} {_actor.EffectiveAttack}",
			$"{LocaleText.T("tooltip.attack_range")} {_actor.EffectiveAttackRange:0.0}",
			$"{LocaleText.T("tooltip.attack_cooldown")} {_actor.EffectiveAttackCooldown:0.00}s",
		};
		if (stats.HasHealSkill) lines.Add(LocaleText.T("tooltip.enable_heal"));
		if (stats.HasShieldSkill) lines.Add(LocaleText.T("tooltip.enable_shield"));
		if (behavior.ExtraProjectiles > 0) lines.Add($"Multi +{behavior.ExtraProjectiles}");
		if (behavior.SplitCount > 0) lines.Add($"Split {behavior.SplitCount}");
		if (behavior.ChainBounces > 0) lines.Add($"Chain {behavior.ChainBounces}");
		if (behavior.PierceCount > 0) lines.Add($"Pierce {behavior.PierceCount}");
		if (behavior.ExplosionRadius > 0.0f) lines.Add($"Explosion {behavior.ExplosionRadius:0.0}m");
		return (LocaleText.T("stat.ability"), string.Join("\n", lines));
	}

	private (string, string) BuildTraitsTooltip()
	{
		if (_actor == null) return (string.Empty, string.Empty);
		BuildStats stats = _actor.CurrentBuildStats;
		return (LocaleText.T("build.traits"), string.Join("\n",
			_actor.TraitSummary,
			$"HP {_actor.MaxHealth} -> {_actor.EffectiveMaxHealth}",
			$"{LocaleText.T("stat.attack")} {_actor.Attack} -> {_actor.EffectiveAttack}",
			$"{LocaleText.T("stat.defense")} {_actor.Defense} -> {_actor.EffectiveDefense}",
			$"{LocaleText.T("stat.speed")} {_actor.MoveSpeed:0.0} -> {_actor.EffectiveMoveSpeed:0.0}",
			$"{LocaleText.T("tooltip.crit_chance")} {stats.CritChance * 100.0f:0.#}%",
			$"{LocaleText.T("tooltip.life_steal")} {stats.LifeStealPercent * 100.0f:0.#}%",
			$"{LocaleText.T("tooltip.control_chance")} {stats.ControlChance * 100.0f:0.#}%"));
	}

	private (string, string) BuildEquipmentTooltip()
	{
		if (_actor == null) return (string.Empty, string.Empty);
		CompanionBuildLoadout loadout = _actor.BuildLoadout;
		var items = new (string Id, string Slot)[]
		{
			(loadout.HelmetId, LocaleText.T("build.slot.helmet")),
			(loadout.WeaponId, LocaleText.T("build.slot.weapon")),
			(loadout.ArmorId, LocaleText.T("build.slot.armor")),
			(loadout.AccessoryId, LocaleText.T("build.slot.accessory")),
			(loadout.AttributeGemId, LocaleText.T("build.slot.attribute")),
		};
		var blocks = new List<string>();
		foreach ((string id, string slot) in items)
		{
			string name = BuildCatalog.GetItemKind(id) == InventoryItemKind.AttributeGem ? LocaleText.T(BuildCatalog.GetAttributeGem(id).NameKey) : LocaleText.T(BuildCatalog.GetEquipment(id).NameKey);
			blocks.Add($"[{slot}] {name}\n{InventoryPanel.BuildItemTooltipBody(id, slot)}");
		}
		return (LocaleText.T("build.equipment"), string.Join("\n\n", blocks));
	}

	private (string, string) BuildSkillGemTooltip()
	{
		if (_actor == null) return (string.Empty, string.Empty);
		CompanionBuildLoadout loadout = _actor.BuildLoadout;
		var blocks = new List<string>();
		int unlockedCores = BuildCatalog.GetUnlockedSupportCoreCount(_actor.Level);
		for (int index = 0; index < loadout.SkillGemIds.Length && index < unlockedCores; index++)
		{
			string id = loadout.GetSkillGemId(index);
			if (id == "gem.skill.none")
			{
				continue;
			}

			SkillGemDefinition gem = BuildCatalog.GetSkillGem(id);
			string slot = LocaleText.F("build.slot.support_core", index + 1);
			blocks.Add($"{LocaleText.T(gem.NameKey)} Lv.{loadout.GetSkillGemLevel(index)}\n{InventoryPanel.BuildItemTooltipBody(id, slot)}");
		}
		return (LocaleText.T("build.skill_gems"), string.Join("\n\n", blocks));
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label { VerticalAlignment = VerticalAlignment.Top, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static Label MakeLabel(int fontSize, Color color, string textKey)
	{
		Label label = MakeLabel(fontSize, color);
		label.Text = LocaleText.T(textKey);
		return label;
	}
}
