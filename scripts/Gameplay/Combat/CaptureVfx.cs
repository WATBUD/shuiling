using Godot;

// Self-animating, self-freeing visual effects for the capture orb: a flight
// trail, a landing shockwave ring, and a "收束" convergence burst on a hit.
public static class CaptureVfx
{
	private static StandardMaterial3D GlowMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G, color.B),
			EmissionEnergyMultiplier = 2.6f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
	}

	// A fading dot left behind the moving orb (called repeatedly along the path).
	public static void SpawnTrailDot(Node parent, Vector3 position, Color color)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
		{
			return;
		}

		var material = GlowMaterial(new Color(color.R, color.G, color.B, 0.7f));
		var dot = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 8, Rings = 4 },
		};
		dot.SetSurfaceOverrideMaterial(0, material);
		parent.AddChild(dot);
		dot.GlobalPosition = position;

		Tween tween = dot.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(dot, "scale", Vector3.One * 0.1f, 0.35f);
		tween.TweenProperty(material, "albedo_color", new Color(color.R, color.G, color.B, 0.0f), 0.35f);
		tween.Finished += dot.QueueFree;
	}

	// Expanding ground ring when the orb lands / hits a wall.
	public static void SpawnShockwave(Node parent, Vector3 position, Color color)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
		{
			return;
		}

		var material = GlowMaterial(new Color(color.R, color.G, color.B, 0.85f));
		var ring = new MeshInstance3D
		{
			// TorusMesh already lies flat in the XZ plane (hole facing up), so it
			// must NOT be rotated — rotating it stands the ring up in the floor.
			Mesh = new TorusMesh { InnerRadius = 0.18f, OuterRadius = 0.30f },
		};
		ring.SetSurfaceOverrideMaterial(0, material);
		parent.AddChild(ring);
		ring.GlobalPosition = position with { Y = position.Y - 0.4f };
		ring.Scale = Vector3.One * 0.4f;

		Tween tween = ring.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(ring, "scale", new Vector3(9.0f, 0.4f, 9.0f), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(material, "albedo_color", new Color(color.R, color.G, color.B, 0.0f), 0.5f);
		tween.Finished += ring.QueueFree;
	}

	// Inward "收束" burst on a monster when the net connects: a ring of shards
	// rushing to the centre plus a quick flash.
	public static void SpawnConvergence(Node parent, Vector3 center, Color color)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
		{
			return;
		}

		var root = new Node3D();
		parent.AddChild(root);
		root.GlobalPosition = center;

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		const int shardCount = 12;
		for (int i = 0; i < shardCount; i++)
		{
			float angle = i / (float)shardCount * Mathf.Tau + rng.RandfRange(-0.2f, 0.2f);
			float radius = rng.RandfRange(1.0f, 1.6f);
			var start = new Vector3(Mathf.Cos(angle) * radius, rng.RandfRange(0.2f, 1.6f), Mathf.Sin(angle) * radius);

			var material = GlowMaterial(new Color(color.R, color.G, color.B, 0.9f));
			var shard = new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f, RadialSegments = 6, Rings = 3 },
				Position = start,
			};
			shard.SetSurfaceOverrideMaterial(0, material);
			root.AddChild(shard);

			Tween tween = shard.CreateTween();
			tween.SetParallel(true);
			tween.TweenProperty(shard, "position", Vector3.Zero, 0.32f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			tween.TweenProperty(material, "albedo_color", new Color(color.R, color.G, color.B, 0.0f), 0.32f);
		}

		// Central flash: swell then collapse.
		var flashMaterial = GlowMaterial(new Color(1.0f, 1.0f, 1.0f, 0.9f));
		var flash = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f, RadialSegments = 16, Rings = 8 },
			Scale = Vector3.One * 0.2f,
		};
		flash.SetSurfaceOverrideMaterial(0, flashMaterial);
		root.AddChild(flash);

		Tween flashTween = flash.CreateTween();
		flashTween.TweenProperty(flash, "scale", Vector3.One * 1.3f, 0.16f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		flashTween.TweenProperty(flash, "scale", Vector3.One * 0.1f, 0.18f);
		flashTween.Parallel().TweenProperty(flashMaterial, "albedo_color", new Color(1.0f, 1.0f, 1.0f, 0.0f), 0.18f);

		// Free the whole burst shortly after the animations finish.
		SceneTreeTimer timer = root.GetTree().CreateTimer(0.5);
		timer.Timeout += root.QueueFree;
	}
}
