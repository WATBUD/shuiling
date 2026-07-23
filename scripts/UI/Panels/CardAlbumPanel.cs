using Godot;
using System.Collections.Generic;

// Card album (卡冊) — the monster-card collection. Each owned card shows a live
// rotating 3D preview of the creature (same turntable style as the character
// creation screen), plus the resulting team stat bonus.
public partial class CardAlbumPanel : PanelContainer
{
	private PlayerController? _player;
	private Label _titleLabel = null!;
	private Label _summaryLabel = null!;
	private Label _emptyLabel = null!;
	private GridContainer _grid = null!;
	private readonly List<Node3D> _previewPivots = new();

	public System.Action? CloseRequested { get; set; }

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += RefreshAll;
		SetPanelVisible(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshAll;
	}

	public override void _Process(double delta)
	{
		if (!Visible)
		{
			return;
		}

		// Turntable spin for every card preview.
		foreach (Node3D pivot in _previewPivots)
		{
			if (IsInstanceValid(pivot))
			{
				pivot.RotationDegrees += new Vector3(0.0f, (float)delta * 45.0f, 0.0f);
			}
		}
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		RefreshAll();
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshAll();
		}
		else
		{
			// Free the preview viewports so nothing renders while hidden.
			ClearGrid();
		}
	}

	private void BuildPanel()
	{
		Name = "CardAlbumPanel";
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -440.0f;
		OffsetRight = 440.0f;
		OffsetTop = -320.0f;
		OffsetBottom = 320.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.09f, 0.96f),
			BorderColor = new Color(0.68f, 0.82f, 1.0f, 0.78f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		AddChild(margin);

		var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 8);
		margin.AddChild(root);

		_titleLabel = new Label { Text = LocaleText.T("card.title"), HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 24);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.94f, 1.0f));
		root.AddChild(_titleLabel);

		_summaryLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_summaryLabel.AddThemeFontSizeOverride("font_size", 15);
		_summaryLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.68f));
		root.AddChild(_summaryLabel);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(840.0f, 430.0f),
		};
		root.AddChild(scroll);

		_grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_grid.AddThemeConstantOverride("h_separation", 8);
		_grid.AddThemeConstantOverride("v_separation", 8);
		scroll.AddChild(_grid);

		_emptyLabel = new Label { Text = LocaleText.T("card.empty"), HorizontalAlignment = HorizontalAlignment.Center };
		_emptyLabel.AddThemeFontSizeOverride("font_size", 15);
		_emptyLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.80f));
		root.AddChild(_emptyLabel);

		var closeButton = new Button { Text = LocaleText.T("dialog.button.close"), CustomMinimumSize = new Vector2(0.0f, 40.0f) };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	public void RefreshAll()
	{
		if (_grid == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("card.title");

		ClearGrid();
		if (_player == null)
		{
			return;
		}

		int count = _player.OwnedCardCount;
		int bonusPercent = Mathf.RoundToInt((_player.CardCollectionMultiplier - 1.0f) * 100.0f);
		_summaryLabel.Text = LocaleText.F("card.summary", count, bonusPercent);
		_emptyLabel.Visible = count == 0;

		foreach (string key in _player.GetOwnedCardKeys())
		{
			_grid.AddChild(BuildCardCell(key));
		}
	}

	private Control BuildCardCell(string cardKey)
	{
		var cell = new PanelContainer { CustomMinimumSize = new Vector2(152.0f, 196.0f) };
		var cellStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.14f, 0.22f, 0.96f),
			BorderColor = new Color(1.0f, 0.84f, 0.42f, 0.85f),
		};
		cellStyle.SetBorderWidthAll(2);
		cellStyle.SetCornerRadiusAll(6);
		cell.AddThemeStyleboxOverride("panel", cellStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 6);
		margin.AddThemeConstantOverride("margin_right", 6);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		cell.AddChild(margin);

		var box = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 4);
		margin.AddChild(box);

		var tag = new Label { Text = LocaleText.T("card.tag"), HorizontalAlignment = HorizontalAlignment.Center };
		tag.AddThemeFontSizeOverride("font_size", 11);
		tag.AddThemeColorOverride("font_color", new Color(0.72f, 0.82f, 0.94f));
		box.AddChild(tag);

		BuildPreview(box, cardKey);

		var name = new Label
		{
			Text = ExternalModelLibrary.LocalizedCardName(cardKey),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		name.AddThemeFontSizeOverride("font_size", 15);
		name.AddThemeColorOverride("font_color", new Color(1.0f, 0.96f, 0.86f));
		box.AddChild(name);

		return cell;
	}

	// A rotating 3D preview of the card's creature (same setup as CharacterSelect).
	private void BuildPreview(VBoxContainer parent, string cardKey)
	{
		var viewportContainer = new SubViewportContainer
		{
			Stretch = true,
			CustomMinimumSize = new Vector2(138.0f, 130.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		parent.AddChild(viewportContainer);

		var viewport = new SubViewport
		{
			Size = new Vector2I(138, 130),
			OwnWorld3D = true,
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		viewportContainer.AddChild(viewport);

		var pivot = new Node3D();
		viewport.AddChild(pivot);
		_previewPivots.Add(pivot);

		string modelPath = ExternalModelLibrary.GetModelPathForCardKey(cardKey);
		Node3D? model = string.IsNullOrEmpty(modelPath) ? null : ExternalModelLibrary.InstantiatePreviewModel(modelPath);
		if (model != null)
		{
			model.Position = Vector3.Zero;
			pivot.AddChild(model);
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
	}

	private void ClearGrid()
	{
		_previewPivots.Clear();
		if (_grid == null)
		{
			return;
		}

		foreach (Node child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.QueueFree();
		}
	}

	// Center + uniformly scale a preview model to a consistent frame.
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
}
