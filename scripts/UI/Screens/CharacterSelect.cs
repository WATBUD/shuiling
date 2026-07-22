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
	private Button _startButton = null!;
	private int _selectedIndex;

	public override void _Ready()
	{
		_models = ExternalModelLibrary.GetAvailableCharacterModels();
		BuildUi();
	}

	public override void _Process(double delta)
	{
		// Turntable spin for every preview.
		foreach (Node3D pivot in _previewPivots)
		{
			if (IsInstanceValid(pivot))
			{
				pivot.RotationDegrees += new Vector3(0.0f, (float)delta * 45.0f, 0.0f);
			}
		}
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

		var root = new VBoxContainer
		{
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -430.0f,
			OffsetRight = 430.0f,
			OffsetTop = -320.0f,
			OffsetBottom = 320.0f,
		};
		root.AddThemeConstantOverride("separation", 14);
		AddChild(root);

		var title = new Label
		{
			Text = LocaleText.T("character.select.title"),
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		title.AddThemeFontSizeOverride("font_size", 32);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.76f));
		root.AddChild(title);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(860.0f, 420.0f),
		};
		root.AddChild(scroll);

		var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", 10);
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
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.9f, 1.0f));
		nameRow.AddChild(nameLabel);

		_nameEdit = new LineEdit
		{
			Text = LocaleText.T("player.default_name"),
			CustomMinimumSize = new Vector2(280.0f, 40.0f),
			MaxLength = 24,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_nameEdit.TextChanged += _ => UpdateStartEnabled();
		nameRow.AddChild(_nameEdit);

		var buttons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		buttons.AddThemeConstantOverride("separation", 12);
		root.AddChild(buttons);

		var backButton = MakeButton(LocaleText.T("dialog.button.cancel"));
		backButton.Pressed += () => GetTree().ChangeSceneToFile("res://main_menu.tscn");
		buttons.AddChild(backButton);

		_startButton = MakeButton(LocaleText.T("character.select.start"));
		_startButton.Pressed += StartGame;
		buttons.AddChild(_startButton);

		SelectModel(0);
		UpdateStartEnabled();
	}

	private Control BuildModelCell(int index)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(158.0f, 214.0f),
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
		column.AddThemeConstantOverride("separation", 4);
		button.AddChild(column);

		var viewportContainer = new SubViewportContainer
		{
			Stretch = true,
			CustomMinimumSize = new Vector2(150.0f, 176.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		column.AddChild(viewportContainer);

		var viewport = new SubViewport
		{
			Size = new Vector2I(150, 176),
			OwnWorld3D = true,
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		viewportContainer.AddChild(viewport);

		var pivot = new Node3D();
		viewport.AddChild(pivot);
		_previewPivots.Add(pivot);

		Node3D? model = ExternalModelLibrary.InstantiatePreviewModel(_models[index].Path);
		if (model != null)
		{
			model.Position = Vector3.Zero;
			pivot.AddChild(model);
			// Auto-fit so every model (tiny rats, huge dragons, off-origin
			// meshes) is centered and framed the same in the preview.
			CallDeferred(nameof(FitPreviewModel), model);
		}

		var light = new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-42.0f, -34.0f, 0.0f),
			LightEnergy = 1.4f,
		};
		viewport.AddChild(light);

		var camera = new Camera3D { Position = new Vector3(0.0f, 1.05f, 3.1f) };
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
		_startButton.Disabled = _models.Count == 0 || _nameEdit.Text.Trim().Length == 0;
	}

	private void StartGame()
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

		GameLaunchOptions.NewGamePlayerModelPath = _models[_selectedIndex].Path;
		GameLaunchOptions.NewGamePlayerName = name;
		GameLaunchOptions.StartNewGame();
		GetTree().ChangeSceneToFile("res://node_3d.tscn");
	}

	// Center + uniformly scale a preview model to a consistent frame, using its
	// combined mesh bounds. Deferred so global transforms are valid.
	private void FitPreviewModel(Node3D model)
	{
		if (!IsInstanceValid(model) || !TryGetModelBounds(model, out Aabb bounds))
		{
			return;
		}

		Vector3 size = bounds.Size;
		float maxDim = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
		if (maxDim <= 0.0001f)
		{
			return;
		}

		float scale = 1.7f / maxDim;
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
			CustomMinimumSize = new Vector2(220.0f, 46.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 20);
		return button;
	}
}
