using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	private void CreateNpcQuestDialog()
	{
		var layer = new CanvasLayer
		{
			Name = "NpcQuestDialogLayer",
			Layer = 42,
		};
		AddChild(layer);

		_npcQuestDialog = new PanelContainer
		{
			Name = "NpcQuestDialog",
			MouseFilter = Control.MouseFilterEnum.Stop,
			Visible = false,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -260.0f,
			OffsetRight = 260.0f,
			OffsetTop = -150.0f,
			OffsetBottom = 150.0f,
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.042f, 0.052f, 0.96f),
			BorderColor = new Color(0.58f, 0.70f, 0.78f, 0.95f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		_npcQuestDialog.AddThemeStyleboxOverride("panel", style);
		layer.AddChild(_npcQuestDialog);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		_npcQuestDialog.AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		_npcQuestTitleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_npcQuestTitleLabel.AddThemeFontSizeOverride("font_size", 24);
		_npcQuestTitleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.78f));
		root.AddChild(_npcQuestTitleLabel);

		_npcQuestBodyLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_npcQuestBodyLabel.AddThemeFontSizeOverride("font_size", 18);
		_npcQuestBodyLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.96f, 1.0f));
		root.AddChild(_npcQuestBodyLabel);

		_npcQuestRewardLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_npcQuestRewardLabel.AddThemeFontSizeOverride("font_size", 16);
		_npcQuestRewardLabel.AddThemeColorOverride("font_color", new Color(0.70f, 1.0f, 0.76f));
		root.AddChild(_npcQuestRewardLabel);

		var buttons = new HBoxContainer();
		buttons.AddThemeConstantOverride("separation", 12);
		root.AddChild(buttons);

		_npcQuestAcceptButton = MakeQuestDialogButton("quest.button.accept");
		_npcQuestAcceptButton.Pressed += AcceptNpcQuestDialog;
		buttons.AddChild(_npcQuestAcceptButton);

		_npcQuestDeclineButton = MakeQuestDialogButton("quest.button.decline");
		_npcQuestDeclineButton.Pressed += DeclineNpcQuestDialog;
		buttons.AddChild(_npcQuestDeclineButton);
	}

	private static Button MakeQuestDialogButton(string textKey)
	{
		var button = new Button
		{
			Text = LocaleText.T(textKey),
			CustomMinimumSize = new Vector2(130.0f, 40.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 17);
		return button;
	}

	private void ShowNpcQuestDialog(SimpleActor actor)
	{
		if (!CanInteractWithRecruitNpc(actor) || _npcQuestDialog == null)
		{
			return;
		}

		_pendingQuestNpc = actor;
		_npcQuestDialogIsNotice = false;
		string questItemId = GetNpcQuestItemId(actor);
		int affinityReward = GetNpcQuestAffinityReward(questItemId, NpcRecruitQuestItemCount);
		_npcQuestTitleLabel.Text = LocaleText.F("quest.dialog.title", actor.LocalizedDisplayName);
		_npcQuestBodyLabel.Text = LocaleText.F("quest.dialog.body", actor.LocalizedDisplayName, NpcRecruitQuestItemCount, GetInventoryItemDisplayName(questItemId));
		_npcQuestRewardLabel.Text = LocaleText.F("quest.dialog.reward", affinityReward, NpcRecruitAffinityRequirement);
		_npcQuestRewardLabel.Visible = true;
		_npcQuestAcceptButton.Text = LocaleText.T("quest.button.accept");
		_npcQuestDeclineButton.Text = LocaleText.T("quest.button.decline");
		_npcQuestDeclineButton.Visible = true;
		_npcQuestDialog.Visible = true;
		_interactionPromptLabel.Visible = false;
		UpdateMouseModeForPanels();
	}

	private void AcceptNpcQuestDialog()
	{
		if (_npcQuestDialogIsNotice)
		{
			CloseNpcQuestDialog();
			return;
		}

		SimpleActor? actor = _pendingQuestNpc;
		if (actor == null || !CanInteractWithRecruitNpc(actor))
		{
			CloseNpcQuestDialog();
			return;
		}

		_acceptedNpcQuests.Add(actor);
		string questItemId = GetNpcQuestItemId(actor);
		PostSystemMessage(LocaleText.F("system.npc.task_posted", actor.LocalizedDisplayName, NpcRecruitQuestItemCount, GetInventoryItemDisplayName(questItemId)), new Color(0.82f, 0.92f, 1.0f), GameMessageChannel.Party);
		CloseNpcQuestDialog();
	}

	private void DeclineNpcQuestDialog()
	{
		SimpleActor? actor = _pendingQuestNpc;
		if (actor != null && IsInstanceValid(actor))
		{
			PostSystemMessage(LocaleText.F("system.npc.task_declined", actor.LocalizedDisplayName), new Color(0.82f, 0.86f, 0.92f), GameMessageChannel.Party);
		}

		CloseNpcQuestDialog();
	}

	private void CloseNpcQuestDialog()
	{
		_pendingQuestNpc = null;
		_npcQuestDialogIsNotice = false;
		if (_npcQuestDialog != null)
		{
			_npcQuestDialog.Visible = false;
		}

		if (_npcQuestRewardLabel != null)
		{
			_npcQuestRewardLabel.Visible = true;
		}

		if (_npcQuestDeclineButton != null)
		{
			_npcQuestDeclineButton.Visible = true;
		}

		UpdateMouseModeForPanels();
	}

	private void CreateMapTravelDialog()
	{
		var layer = new CanvasLayer { Name = "MapTravelDialogLayer" };
		AddChild(layer);

		_mapTravelDialog = new PanelContainer
		{
			Name = "MapTravelDialog",
			Visible = false,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -330.0f,
			OffsetRight = 330.0f,
			OffsetTop = -270.0f,
			OffsetBottom = 270.0f,
		};
		_mapTravelDialog.AddThemeStyleboxOverride("panel", MakeDialogStyle(new Color(0.05f, 0.07f, 0.09f, 0.94f), new Color(0.35f, 0.82f, 1.0f, 0.72f)));
		layer.AddChild(_mapTravelDialog);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		_mapTravelDialog.AddChild(margin);

		var root = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 10);
		margin.AddChild(root);

		var title = new Label
		{
			Text = LocaleText.T("map.travel.title"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.78f));
		root.AddChild(title);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(624.0f, 430.0f),
		};
		root.AddChild(scroll);

		_mapTravelButtonList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_mapTravelButtonList.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_mapTravelButtonList);

		var cancelButton = MakeQuestDialogButton("dialog.button.cancel");
		cancelButton.Pressed += CloseMapTravelDialog;
		root.AddChild(cancelButton);
	}

	// Toggle the world map guide (M key). Reuses the map travel dialog so the
	// overview and quick-travel share one screen.
	private void ToggleWorldMapGuide()
	{
		if (_mapTravelDialog != null && _mapTravelDialog.Visible)
		{
			CloseMapTravelDialog();
			return;
		}

		if (GetParent() is World world)
		{
			ShowMapTravelDialog(world);
		}
	}

	private void ShowMapTravelDialog(World world)
	{
		ClearChildren(_mapTravelButtonList);
		// Per biome, show all tiers 1..10 with their monster level range. Tiers
		// the player can't enter yet (not progression-unlocked, or player level
		// below the tier's requirement) are shown with a lock badge and disabled.
		foreach ((string id, string label) in world.GetWildMapList())
		{
			_mapTravelButtonList.AddChild(BuildMapTierSection(world, id, label));
		}

		_mapTravelDialog.Visible = true;
		_interactionPromptLabel.Visible = false;
		UpdateMouseModeForPanels();
	}

	private Control BuildMapTierSection(World world, string mapId, string mapLabel)
	{
		var section = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		section.AddThemeConstantOverride("separation", 4);

		bool isHere = world.ActiveMapId == mapId;
		var header = new Label
		{
			Text = isHere ? LocaleText.F("map.guide.here", mapLabel) : mapLabel,
		};
		header.AddThemeFontSizeOverride("font_size", 18);
		header.AddThemeColorOverride("font_color", isHere ? new Color(0.4f, 1.0f, 0.6f) : new Color(0.72f, 0.92f, 1.0f));
		section.AddChild(header);

		// Boss status for this map (alive / respawn countdown).
		foreach (World.BossStatusSnapshot boss in world.GetBossStatusSnapshots())
		{
			if (boss.MapId != mapId)
			{
				continue;
			}

			var bossLabel = new Label
			{
				Text = boss.IsAlive
					? LocaleText.F("map.guide.boss_alive", boss.BossName)
					: LocaleText.F("map.guide.boss_respawn", boss.BossName, boss.RespawnSeconds),
			};
			bossLabel.AddThemeFontSizeOverride("font_size", 12);
			bossLabel.AddThemeColorOverride("font_color", boss.IsAlive ? new Color(1.0f, 0.72f, 0.34f) : new Color(0.6f, 0.66f, 0.72f));
			section.AddChild(bossLabel);
			break;
		}

		var grid = new GridContainer { Columns = 5 };
		grid.AddThemeConstantOverride("h_separation", 6);
		grid.AddThemeConstantOverride("v_separation", 6);
		section.AddChild(grid);

		foreach (World.TierMenuEntry entry in world.GetTierMenu(mapId, Level))
		{
			grid.AddChild(MakeTierButton(world, mapId, entry));
		}

		return section;
	}

	private Control MakeTierButton(World world, string mapId, World.TierMenuEntry entry)
	{
		var button = new Button
		{
			Text = LocaleText.F("map.tier.button", entry.Tier, entry.LevelMin, entry.LevelMax),
			CustomMinimumSize = new Vector2(116.0f, 46.0f),
			ClipText = true,
			Disabled = !entry.Available,
		};
		button.AddThemeFontSizeOverride("font_size", 13);

		if (entry.IsSelected && entry.Available)
		{
			button.AddThemeColorOverride("font_color", new Color(0.4f, 1.0f, 0.6f));
		}

		if (!entry.Available)
		{
			// Locked only because the previous tier's boss hasn't been beaten.
			button.Modulate = new Color(0.62f, 0.64f, 0.68f);
			button.TooltipText = LocaleText.T("map.tier.locked_boss");
			button.AddChild(MakeLockBadge());
		}
		else
		{
			button.Pressed += () =>
			{
				CloseMapTravelDialog();
				world.RequestMapTravel(mapId, entry.Tier);
			};
		}

		return button;
	}

	// Small padlock drawn from panels so it renders regardless of the font's
	// glyph coverage. Anchored to the top-right corner of a tier button.
	private static Control MakeLockBadge()
	{
		var badge = new Control
		{
			CustomMinimumSize = new Vector2(16.0f, 18.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 1.0f,
			AnchorRight = 1.0f,
			OffsetLeft = -19.0f,
			OffsetRight = -3.0f,
			OffsetTop = 3.0f,
			OffsetBottom = 21.0f,
		};

		var shackleStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
		shackleStyle.SetBorderWidthAll(2);
		shackleStyle.BorderWidthBottom = 0;
		shackleStyle.BorderColor = new Color(0.95f, 0.82f, 0.35f);
		shackleStyle.CornerRadiusTopLeft = 5;
		shackleStyle.CornerRadiusTopRight = 5;
		var shackle = new Panel { OffsetLeft = 3, OffsetRight = 13, OffsetTop = 0, OffsetBottom = 9 };
		shackle.AddThemeStyleboxOverride("panel", shackleStyle);
		badge.AddChild(shackle);

		var bodyStyle = new StyleBoxFlat { BgColor = new Color(0.95f, 0.82f, 0.35f) };
		bodyStyle.SetCornerRadiusAll(2);
		var body = new Panel { OffsetLeft = 1, OffsetRight = 15, OffsetTop = 7, OffsetBottom = 18 };
		body.AddThemeStyleboxOverride("panel", bodyStyle);
		badge.AddChild(body);

		var keyholeStyle = new StyleBoxFlat { BgColor = new Color(0.15f, 0.12f, 0.05f) };
		keyholeStyle.SetCornerRadiusAll(2);
		var keyhole = new Panel { OffsetLeft = 6, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 15 };
		keyhole.AddThemeStyleboxOverride("panel", keyholeStyle);
		badge.AddChild(keyhole);

		return badge;
	}

	private void CloseMapTravelDialog()
	{
		if (_mapTravelDialog != null)
		{
			_mapTravelDialog.Visible = false;
		}

		UpdateMouseModeForPanels();
	}

	private static StyleBoxFlat MakeDialogStyle(Color backgroundColor, Color borderColor)
	{
		var style = new StyleBoxFlat
		{
			BgColor = backgroundColor,
			BorderColor = borderColor,
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		return style;
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
