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
		PostSystemMessage(LocaleText.F("system.npc.task_posted", actor.LocalizedDisplayName, NpcRecruitQuestItemCount, GetInventoryItemDisplayName(questItemId)), new Color(0.82f, 0.92f, 1.0f));
		CloseNpcQuestDialog();
	}

	private void DeclineNpcQuestDialog()
	{
		SimpleActor? actor = _pendingQuestNpc;
		if (actor != null && IsInstanceValid(actor))
		{
			PostSystemMessage(LocaleText.F("system.npc.task_declined", actor.LocalizedDisplayName), new Color(0.82f, 0.86f, 0.92f));
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
			OffsetLeft = -210.0f,
			OffsetRight = 210.0f,
			OffsetTop = -150.0f,
			OffsetBottom = 150.0f,
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

		_mapTravelButtonList = new VBoxContainer();
		_mapTravelButtonList.AddThemeConstantOverride("separation", 8);
		root.AddChild(_mapTravelButtonList);

		var cancelButton = MakeQuestDialogButton("dialog.button.cancel");
		cancelButton.Pressed += CloseMapTravelDialog;
		root.AddChild(cancelButton);
	}

	private void ShowMapTravelDialog(World world)
	{
		ClearChildren(_mapTravelButtonList);
		foreach ((string id, string label) in world.GetWildMapTravelOptions())
		{
			var button = new Button
			{
				Text = label,
				CustomMinimumSize = new Vector2(0.0f, 42.0f),
			};
			button.Pressed += () =>
			{
				CloseMapTravelDialog();
				world.RequestMapTravel(id);
			};
			_mapTravelButtonList.AddChild(button);
		}

		_mapTravelDialog.Visible = true;
		_interactionPromptLabel.Visible = false;
		UpdateMouseModeForPanels();
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
