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
	private Label _ability = null!;
	private Label _traits = null!;
	private Label _equipment = null!;
	private Label _skillGems = null!;
	private FloatingTooltip _tooltip = null!;
	private SimpleActor? _actor;

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
		_meta = MakeLabel(12, new Color(0.74f, 0.88f, 0.80f));
		metaRows.AddChild(_meta);
		_ability = AddInteractiveLabel(metaRows);
		_traits = AddInteractiveLabel(metaRows);
		_equipment = AddInteractiveLabel(metaRows);
		_skillGems = AddInteractiveLabel(metaRows);

		_tooltip = new FloatingTooltip { MaxWidth = 390.0f, MaxWidthRatio = 0.70f, MaxHeightRatio = 0.72f };
		AddChild(_tooltip);
		BindTooltip(_ability, BuildAbilityTooltip);
		BindTooltip(_traits, BuildTraitsTooltip);
		BindTooltip(_equipment, BuildEquipmentTooltip);
		BindTooltip(_skillGems, BuildSkillGemTooltip);
		SetActor(_actor);
	}

	public override void _Process(double delta)
	{
		if (_tooltip != null && _tooltip.Visible) _tooltip.PositionNearMouse(this);
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
		_actor = actor != null && IsInstanceValid(actor) ? actor : null;
		if (_title == null) return;
		if (_actor == null)
		{
			_title.Text = LocaleText.T("inventory.companion_info");
			_experienceBar.Value = 0.0;
			_experience.Text = LocaleText.T("inventory.no_companions");
			_stats.Text = _meta.Text = _ability.Text = _traits.Text = _equipment.Text = _skillGems.Text = string.Empty;
			_mode.Visible = false;
			_tooltip?.HideTooltip();
			return;
		}

		BuildStats stats = _actor.CurrentBuildStats;
		_title.Text = $"{_actor.LocalizedDisplayName} - {LocaleText.F("inventory.info_header", _actor.Level)}";
		_experienceBar.MaxValue = Mathf.Max(_actor.ExperienceToNextLevel, 1);
		_experienceBar.Value = Mathf.Clamp(_actor.Experience, 0, _actor.ExperienceToNextLevel);
		_experience.Text = $"{LocaleText.T("stat.experience")} {_actor.Experience}/{_actor.ExperienceToNextLevel}";
		_stats.Text = string.Join("\n",
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
			$"{LocaleText.T("stat.role")} {_actor.CombatRoleName} / {_actor.LocalizedPersonality}",
			$"{LocaleText.T("stat.affinity")} {_actor.Affinity} / 100",
			$"{LocaleText.T("build.element")} {_actor.BuildElementName} / {_actor.BuildRareComboName}");
		_ability.Text = $"{LocaleText.T("stat.ability")} {_actor.LocalizedSpecialAbility} {LocaleText.T("actor.level_prefix")}{_actor.AbilityRank}";
		_traits.Text = $"{LocaleText.T("build.traits")} {_actor.TraitSummary}";
		_equipment.Text = $"{LocaleText.T("build.equipment")} {LocaleText.F("build.equipment_summary", _actor.BuildEquipmentSummary, stats.EquipmentSocketCount)}";
		_skillGems.Text = $"{LocaleText.T("build.skill_gems")} {_actor.BuildSkillSummary}";
		_mode.Text = $"{LocaleText.T("build.slot.attack_mode")}: {_actor.AttackModeName}";
		_mode.Visible = true;
	}

	private void CycleMode()
	{
		if (_actor == null || !IsInstanceValid(_actor)) return;
		_actor.CycleAttackMode();
		SetActor(_actor);
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
			if (_actor == null || !IsInstanceValid(_actor)) return;
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
			_actor.LocalizedPassiveAbility,
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
		for (int index = 0; index < loadout.SkillGemIds.Length; index++)
		{
			string id = loadout.SkillGemIds[index];
			SkillGemDefinition gem = BuildCatalog.GetSkillGem(id);
			string slot = LocaleText.T($"build.slot.skill{index + 1}");
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
}
