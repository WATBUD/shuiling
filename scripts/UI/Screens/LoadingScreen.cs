using Godot;

public partial class LoadingScreen : CanvasLayer
{
	private const string OverlayName = "GameLoadingScreen";
	private const string PolarBearModelPath = "res://assets/models/pets/cube_pets/animal-polar.glb";
	private Control _root = null!;
	private Label _loadingLabel = null!;
	private Node3D? _bearPivot;
	private Node3D? _bearModel;
	private float _time;
	private ulong _shownAtMsec;
	private bool _hideStarted;

	public static async void ChangeSceneToFile(Node context, string scenePath)
	{
		if (context == null || !GodotObject.IsInstanceValid(context))
		{
			return;
		}

		SceneTree tree = context.GetTree();
		Show(context);
		// Give the renderer time to present the overlay before synchronous scene
		// construction occupies the main thread.
		await context.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		await context.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

		Error request = ResourceLoader.LoadThreadedRequest(scenePath);
		if (request != Error.Ok)
		{
			await WaitForMinimumDisplayTime(context, tree);
			tree.ChangeSceneToFile(scenePath);
			return;
		}

		while (GodotObject.IsInstanceValid(context))
		{
			ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(scenePath);
			if (status == ResourceLoader.ThreadLoadStatus.Loaded)
			{
				await WaitForMinimumDisplayTime(context, tree);
				if (!GodotObject.IsInstanceValid(context))
				{
					return;
				}

				PackedScene? scene = ResourceLoader.LoadThreadedGet(scenePath) as PackedScene;
				if (scene != null)
				{
					tree.ChangeSceneToPacked(scene);
				}
				else
				{
					tree.ChangeSceneToFile(scenePath);
				}
				return;
			}

			if (status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource)
			{
				await WaitForMinimumDisplayTime(context, tree);
				if (!GodotObject.IsInstanceValid(context))
				{
					return;
				}
				tree.ChangeSceneToFile(scenePath);
				return;
			}

			await context.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
	}

	private static async System.Threading.Tasks.Task WaitForMinimumDisplayTime(Node context, SceneTree tree)
	{
		while (GodotObject.IsInstanceValid(context))
		{
			LoadingScreen? overlay = tree.Root.GetNodeOrNull<LoadingScreen>(OverlayName);
			if (overlay != null &&
				overlay._shownAtMsec > 0 &&
				(Time.GetTicksMsec() - overlay._shownAtMsec) / 1000.0 >= LoadingScreenConfig.MinimumVisibleSeconds)
			{
				return;
			}

			await context.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
	}

	public static void Show(Node context)
	{
		Node root = context.GetTree().Root;
		if (root.GetNodeOrNull<LoadingScreen>(OverlayName) != null)
		{
			return;
		}

		root.AddChild(new LoadingScreen { Name = OverlayName, Layer = 500 });
	}

	public static void Hide(Node context)
	{
		LoadingScreen? overlay = context.GetTree().Root.GetNodeOrNull<LoadingScreen>(OverlayName);
		if (overlay == null || overlay._root == null)
		{
			return;
		}
		if (overlay._hideStarted)
		{
			return;
		}
		overlay._hideStarted = true;

		Tween fade = overlay.CreateTween();
		double elapsedSeconds = (Time.GetTicksMsec() - overlay._shownAtMsec) / 1000.0;
		double remainingSeconds = Mathf.Max(LoadingScreenConfig.MinimumVisibleSeconds - elapsedSeconds, 0.0);
		if (remainingSeconds > 0.0)
		{
			fade.TweenInterval(remainingSeconds);
		}
		fade.TweenProperty(overlay._root, "modulate:a", 0.0f, 0.28f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);
		fade.TweenCallback(Callable.From(overlay.QueueFree));
	}

	public override void _Ready()
	{
		_shownAtMsec = Time.GetTicksMsec();
		_root = new Control
		{
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var background = new ColorRect
		{
			Color = new Color(0.018f, 0.028f, 0.048f, 1.0f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(background);

		_loadingLabel = new Label
		{
			Text = "LOADING...",
			AnchorLeft = LoadingScreenConfig.TextLeftAnchor,
			AnchorTop = LoadingScreenConfig.RowTopAnchor,
			AnchorRight = LoadingScreenConfig.TextRightAnchor,
			AnchorBottom = LoadingScreenConfig.RowBottomAnchor,
			VerticalAlignment = VerticalAlignment.Bottom,
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_loadingLabel.AddThemeFontSizeOverride("font_size", LoadingScreenConfig.LoadingTextFontSize);
		_loadingLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.92f, 1.0f));
		_loadingLabel.AddThemeColorOverride("font_shadow_color", new Color(0.04f, 0.12f, 0.22f, 0.95f));
		_loadingLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		_loadingLabel.AddThemeConstantOverride("shadow_offset_y", 3);
		_root.AddChild(_loadingLabel);

		BuildPolarBearPreview();
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		int dotCount = 1 + Mathf.FloorToInt(_time * 2.2f) % 3;
		_loadingLabel.Text = "LOADING" + new string('.', dotCount);

		if (_bearPivot != null)
		{
			_bearPivot.Position = new Vector3(
				Mathf.Sin(_time * 1.7f) * 0.18f,
				LoadingScreenConfig.BearVerticalOffset + Mathf.Abs(Mathf.Sin(_time * 7.0f)) * 0.035f,
				0.0f);
		}
		if (_bearModel != null)
		{
			ExternalModelLibrary.StabilizeRootMotion(_bearModel, Vector3.Zero, new Vector3(0.0f, LoadingScreenConfig.BearModelYawDegrees, 0.0f));
			ExternalModelLibrary.TryPlayActorAnimation(_bearModel, "run");
		}
	}

	private void BuildPolarBearPreview()
	{
		var viewport = new SubViewport
		{
			Size = new Vector2I(LoadingScreenConfig.BearViewportWidth, LoadingScreenConfig.BearViewportHeight),
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			Msaa3D = Viewport.Msaa.Msaa4X,
			ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Fxaa,
		};
		_root.AddChild(viewport);

		var preview = new TextureRect
		{
			Texture = viewport.GetTexture(),
			AnchorLeft = LoadingScreenConfig.BearLeftAnchor,
			AnchorTop = LoadingScreenConfig.RowTopAnchor,
			AnchorRight = LoadingScreenConfig.BearRightAnchor,
			AnchorBottom = LoadingScreenConfig.RowBottomAnchor,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		preview.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
		_root.AddChild(preview);

		_bearPivot = new Node3D { Position = new Vector3(0.0f, LoadingScreenConfig.BearVerticalOffset, 0.0f) };
		viewport.AddChild(_bearPivot);
		_bearModel = ExternalModelLibrary.InstantiatePreviewModel(PolarBearModelPath);
		if (_bearModel != null)
		{
			_bearModel.Scale = Vector3.One * LoadingScreenConfig.BearModelScale;
			_bearModel.RotationDegrees = new Vector3(0.0f, LoadingScreenConfig.BearModelYawDegrees, 0.0f);
			_bearPivot.AddChild(_bearModel);
			ExternalModelLibrary.TryPlayActorAnimation(_bearModel, "run");
		}

		viewport.AddChild(new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-48.0f, -32.0f, 0.0f),
			LightEnergy = 1.65f,
			LightColor = new Color(0.78f, 0.90f, 1.0f),
		});
		viewport.AddChild(new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-18.0f, 145.0f, 0.0f),
			LightEnergy = 0.75f,
			LightColor = new Color(0.40f, 0.68f, 1.0f),
		});

		var camera = new Camera3D
		{
			Position = new Vector3(0.0f, 1.25f, LoadingScreenConfig.BearCameraDistance),
		};
		viewport.AddChild(camera);
		camera.LookAtFromPosition(camera.Position, new Vector3(0.0f, 0.85f, 0.0f), Vector3.Up);
	}
}
