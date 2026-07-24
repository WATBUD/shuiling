using Godot;
using System.Collections.Generic;

// Character creation screen shown before a new game: a grid of the game's
// character models with live rotating 3D previews, a name field, and a start
// button. The choice is written to GameLaunchOptions and consumed by the player
// on spawn (PlayerController.ApplyNewGameCharacterChoice).
public partial class CharacterSelect : Control
{
	private readonly List<Node3D> _previewPivots = new();
	private readonly List<Button> _cellButtons = new();
	private List<(string Path, string Display)> _models = new();
	private LineEdit _nameEdit = null!;
	private LineEdit _worldNameEdit = null!;
	private CheckBox _autoSaveCheck = null!;
	private Button _startButton = null!;
	private int _selectedIndex;
	private bool _isMultiplayer;
	private float _cellWidth = 190.0f;

	public override void _Ready()
	{
		// Mode was chosen on the "new world" window before this screen.
		_isMultiplayer = GameLaunchOptions.NewWorldIsMultiplayer;
		_models = ExternalModelLibrary.GetAvailableCharacterModels();
		BuildUi();
	}

	private void BuildUi()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);

		var background = new ColorRect
		{
			Color = new Color(0.025f, 0.035f, 0.045f),
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
		};
		AddChild(background);

		// Fill the window with margins so the layout adapts to any height and the
		// bottom buttons are never clipped (the model grid scrolls to absorb slack).
		var root = new VBoxContainer
		{
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 60.0f,
			OffsetTop = 28.0f,
			OffsetRight = -60.0f,
			OffsetBottom = -28.0f,
		};
		root.AddThemeConstantOverride("separation", 12);
		AddChild(root);

		var title = new Label
		{
			Text = LocaleText.T("character.select.title"),
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		title.AddThemeFontSizeOverride("font_size", 22);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.76f));
		root.AddChild(title);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 200.0f),
		};
		root.AddChild(scroll);

		// Always 6 characters per row: size each cell to the window width so the
		// row is filled edge-to-edge with no right-hand gap.
		const int columns = 6;
		const float cellSeparation = 10.0f;
		float available = GetViewportRect().Size.X - 120.0f - 24.0f; // root margins + scrollbar
		_cellWidth = Mathf.Max(Mathf.Floor((available - (columns - 1) * cellSeparation) / columns), 120.0f);

		var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", (int)cellSeparation);
		grid.AddThemeConstantOverride("v_separation", 10);
		scroll.AddChild(grid);

		if (_models.Count == 0)
		{
			var none = new Label { Text = LocaleText.T("character.select.none") };
			none.AddThemeColorOverride("font_color", new Color(1.0f, 0.72f, 0.68f));
			grid.AddChild(none);
		}

		for (int index = 0; index < _models.Count; index++)
		{
			grid.AddChild(BuildModelCell(index));
		}

		var nameRow = new HBoxContainer();
		nameRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(nameRow);

		var nameLabel = new Label { Text = LocaleText.T("character.select.name") };
		nameLabel.AddThemeFontSizeOverride("font_size", 13);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.9f, 1.0f));
		nameRow.AddChild(nameLabel);

		_nameEdit = new LineEdit
		{
			Text = LocaleText.T("player.default_name"),
			CustomMinimumSize = new Vector2(180.0f, 30.0f),
			MaxLength = 24,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_nameEdit.TextChanged += _ => UpdateStartEnabled();
		nameRow.AddChild(_nameEdit);

		var worldRow = new HBoxContainer();
		worldRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(worldRow);

		var worldLabel = new Label { Text = LocaleText.T("world.name_label") };
		worldLabel.AddThemeFontSizeOverride("font_size", 13);
		worldLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.9f, 1.0f));
		worldRow.AddChild(worldLabel);

		_worldNameEdit = new LineEdit
		{
			Text = LocaleText.T("world.default_name"),
			CustomMinimumSize = new Vector2(180.0f, 30.0f),
			MaxLength = 24,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_worldNameEdit.TextChanged += _ => UpdateStartEnabled();
		worldRow.AddChild(_worldNameEdit);

		_autoSaveCheck = new CheckBox
		{
			Text = LocaleText.T("world.auto_save"),
			ButtonPressed = true,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		_autoSaveCheck.AddThemeFontSizeOverride("font_size", 14);
		root.AddChild(_autoSaveCheck);

		var buttons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		buttons.AddThemeConstantOverride("separation", 12);
		root.AddChild(buttons);

		var backButton = MakeButton(LocaleText.T("dialog.button.cancel"));
		backButton.Pressed += () =>
		{
			// Multiplayer: a server was created on the host screen before this step;
			// tear it down when backing out.
			if (_isMultiplayer)
			{
				NetworkManager.Instance?.ResetSession();
			}

			// Return to the previous screen (world list + single/multiplayer choice).
			GameLaunchOptions.ReturnToNewWorldMode = true;
			GetTree().ChangeSceneToFile("res://main_menu.tscn");
		};
		buttons.AddChild(backButton);

		_startButton = MakeButton(LocaleText.T(_isMultiplayer ? "character.select.start_host" : "character.select.start_single"));
		_startButton.Pressed += () => StartGame(_isMultiplayer);
		buttons.AddChild(_startButton);

		SelectModel(0);
		UpdateStartEnabled();
	}

	private Control BuildModelCell(int index)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(_cellWidth, 250.0f),
			Flat = false,
			ToggleMode = true,
		};
		button.Pressed += () => SelectModel(index);
		_cellButtons.Add(button);

		var column = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
		};
		column.AddThemeConstantOverride("separation", 2);
		button.AddChild(column);

		int viewportWidth = Mathf.Max((int)(_cellWidth - 8.0f), 100);
		var viewportContainer = new SubViewportContainer
		{
			Stretch = true,
			CustomMinimumSize = new Vector2(viewportWidth, 216.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		column.AddChild(viewportContainer);

		var viewport = new SubViewport
		{
			Size = new Vector2I(viewportWidth, 216),
			OwnWorld3D = true,
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		viewportContainer.AddChild(viewport);

		// Flattering 3/4 view with a little per-card variation so the row feels
		// alive rather than a set of identical mugshots.
		var pivot = new Node3D { RotationDegrees = new Vector3(0.0f, -30.0f + GD.Randf() * 24.0f, 0.0f) };
		viewport.AddChild(pivot);
		_previewPivots.Add(pivot);

		Node3D? model = ExternalModelLibrary.InstantiatePreviewModel(_models[index].Path);
		if (model != null)
		{
			model.Position = Vector3.Zero;
			pivot.AddChild(model);
			// Animal-type models (cube_pets/animal-*) read slightly oversized next
			// to humanoids, so shrink them to 0.75 of the unified height.
			float extraScale = _models[index].Path.Contains("animal") ? 0.75f : 1.0f;
			// Auto-fit so every model (tiny rats, huge dragons, off-origin
			// meshes) is centered and framed the same in the preview.
			CallDeferred(nameof(FitPreviewModel), model, extraScale);

			// Each character performs its own looping action for variety.
			string[] actions = { "idle", "idle", "walk", "run" };
			CallDeferred(nameof(PlayPreviewAnimation), model, actions[(int)(GD.Randi() % (uint)actions.Length)]);
		}

		var light = new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-42.0f, -34.0f, 0.0f),
			LightEnergy = 1.4f,
		};
		viewport.AddChild(light);

		var camera = new Camera3D { Position = new Vector3(0.0f, 1.0f, 2.5f) };
		viewport.AddChild(camera);
		camera.LookAt(new Vector3(0.0f, 0.95f, 0.0f), Vector3.Up);

		var nameLabel = new Label
		{
			Text = _models[index].Display,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 15);
		column.AddChild(nameLabel);

		return button;
	}

	private void SelectModel(int index)
	{
		_selectedIndex = index;
		for (int i = 0; i < _cellButtons.Count; i++)
		{
			Button button = _cellButtons[i];
			bool selected = i == index;
			button.ButtonPressed = selected;
			button.Modulate = selected ? new Color(1.0f, 1.0f, 1.0f) : new Color(0.7f, 0.72f, 0.76f);
		}
	}

	private void UpdateStartEnabled()
	{
		bool ready = _models.Count > 0 && _nameEdit.Text.Trim().Length > 0;
		_startButton.Disabled = !ready;
	}

	private void StartGame(bool host)
	{
		if (_models.Count == 0)
		{
			return;
		}

		string name = _nameEdit.Text.Trim();
		if (name.Length == 0)
		{
			name = LocaleText.T("player.default_name");
		}

		string worldName = _worldNameEdit.Text.Trim();
		if (worldName.Length == 0)
		{
			worldName = LocaleText.T("world.default_name");
		}

		// Multiplayer: the server is already running (created on the host screen
		// before this step), so use its world seed. Single-player: fresh seed.
		int seed;
		if (host && NetworkManager.Instance is { IsOnline: true } net && net.WorldSeed != 0)
		{
			seed = net.WorldSeed;
		}
		else
		{
			seed = unchecked((int)GD.Randi());
			if (seed == 0)
			{
				seed = 1;
			}
		}

		GameLaunchOptions.NewWorldAutoSave = _autoSaveCheck.ButtonPressed;
		GameLaunchOptions.StartNewWorld(SaveGameManager.NewWorldId(), worldName, seed, _models[_selectedIndex].Path, name);

		if (host)
		{
			// Server already up — just enter the shared world.
			GetTree().ChangeSceneToFile("res://node_3d.tscn");
		}
		else
		{
			NetworkManager.Instance?.ResetSession();
			GetTree().ChangeSceneToFile("res://node_3d.tscn");
		}
	}

	// Center + uniformly scale a preview model to a consistent frame, using its
	// combined mesh bounds. Deferred so global transforms are valid.
	private void PlayPreviewAnimation(Node3D model, string state)
	{
		if (IsInstanceValid(model))
		{
			ExternalModelLibrary.TryPlayActorAnimation(model, state);
		}
	}

	private void FitPreviewModel(Node3D model, float extraScale)
	{
		if (!IsInstanceValid(model) || !TryGetModelBounds(model, out Aabb bounds))
		{
			return;
		}

		Vector3 size = bounds.Size;
		if (Mathf.Max(size.X, Mathf.Max(size.Y, size.Z)) <= 0.0001f)
		{
			return;
		}

		// Uniform (proportional) scale, but normalize by HEIGHT so every character
		// stands the same height on screen — humanoids, quadrupeds and tall bosses
		// all line up. Very wide/deep models are scaled down just enough to stay in
		// frame so they never overflow their card.
		const float targetHeight = 1.9f;
		const float maxWidth = 2.3f;
		const float maxDepth = 2.3f;
		float scale = targetHeight / Mathf.Max(size.Y, 0.001f);
		if (size.X * scale > maxWidth)
		{
			scale = maxWidth / Mathf.Max(size.X, 0.001f);
		}
		if (size.Z * scale > maxDepth)
		{
			scale = maxDepth / Mathf.Max(size.Z, 0.001f);
		}

		scale *= extraScale;
		model.Scale = new Vector3(scale, scale, scale);
		Vector3 center = bounds.Position + size * 0.5f;
		// Put the scaled centre on the camera focus (0, 0.95, 0) and keep it on
		// the pivot origin so the turntable spins it in place.
		model.Position = new Vector3(-center.X * scale, 0.95f - center.Y * scale, -center.Z * scale);
	}

	private static bool TryGetModelBounds(Node3D model, out Aabb bounds)
	{
		bounds = new Aabb();
		bool has = false;
		Transform3D inverse = model.GlobalTransform.AffineInverse();
		var stack = new Stack<Node>();
		stack.Push(model);
		while (stack.Count > 0)
		{
			Node node = stack.Pop();
			if (node is VisualInstance3D visual)
			{
				Aabb transformed = TransformAabb(inverse * visual.GlobalTransform, visual.GetAabb());
				bounds = has ? bounds.Merge(transformed) : transformed;
				has = true;
			}

			foreach (Node child in node.GetChildren())
			{
				stack.Push(child);
			}
		}

		return has;
	}

	private static Aabb TransformAabb(Transform3D transform, Aabb local)
	{
		Vector3 origin = local.Position;
		Vector3 dimensions = local.Size;
		var result = new Aabb(transform * origin, Vector3.Zero);
		for (int corner = 1; corner < 8; corner++)
		{
			var point = origin + new Vector3(
				(corner & 1) != 0 ? dimensions.X : 0.0f,
				(corner & 2) != 0 ? dimensions.Y : 0.0f,
				(corner & 4) != 0 ? dimensions.Z : 0.0f);
			result = result.Expand(transform * point);
		}

		return result;
	}

	private static Button MakeButton(string text)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(170.0f, 34.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 15);
		return button;
	}
}
