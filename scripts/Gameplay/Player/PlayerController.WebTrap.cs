using Godot;

public partial class PlayerController
{
	private const float SpiderWebSuspensionDuration = 2.65f;
	private const float SpiderWebLiftHeight = 2.25f;
	private float _spiderWebSuspensionRemaining;
	private Vector3 _spiderWebGroundPosition;
	private Node3D? _spiderWebVisual;
	private ulong _nextSpiderWebAllowedMsec;

	public bool IsSpiderWebSuspended => _spiderWebSuspensionRemaining > 0.0f;

	public bool TryApplySpiderWebSuspension(SimpleActor spider)
	{
		ulong now = Time.GetTicksMsec();
		if (_spiderWebSuspensionRemaining > 0.0f || now < _nextSpiderWebAllowedMsec || CurrentHealth <= 0)
		{
			return false;
		}

		_spiderWebSuspensionRemaining = SpiderWebSuspensionDuration;
		_spiderWebGroundPosition = GlobalPosition;
		_nextSpiderWebAllowedMsec = now + 7600;
		Velocity = Vector3.Zero;
		CreateSpiderWebTrapVisual(spider);
		PostSystemMessage(LocaleText.T("system.cave.spider_web"), new Color(0.92f, 0.72f, 1.0f), GameMessageChannel.Combat);
		return true;
	}

	private bool UpdateSpiderWebSuspension(float step)
	{
		if (_spiderWebSuspensionRemaining <= 0.0f)
		{
			return false;
		}

		_spiderWebSuspensionRemaining = Mathf.Max(_spiderWebSuspensionRemaining - step, 0.0f);
		float elapsed = SpiderWebSuspensionDuration - _spiderWebSuspensionRemaining;
		float lift;
		if (elapsed < 0.48f)
		{
			float progress = Mathf.Clamp(elapsed / 0.48f, 0.0f, 1.0f);
			lift = Mathf.SmoothStep(0.0f, SpiderWebLiftHeight, progress);
		}
		else if (_spiderWebSuspensionRemaining < 0.52f)
		{
			float progress = 1.0f - _spiderWebSuspensionRemaining / 0.52f;
			lift = Mathf.SmoothStep(SpiderWebLiftHeight, 0.0f, progress);
		}
		else
		{
			lift = SpiderWebLiftHeight + Mathf.Sin(elapsed * 8.0f) * 0.08f;
		}

		Velocity = Vector3.Zero;
		GlobalPosition = _spiderWebGroundPosition + Vector3.Up * lift;
		if (_spiderWebSuspensionRemaining > 0.0f)
		{
			return true;
		}

		GlobalPosition = _spiderWebGroundPosition + Vector3.Up * 0.06f;
		if (_spiderWebVisual != null && IsInstanceValid(_spiderWebVisual))
		{
			_spiderWebVisual.QueueFree();
		}
		_spiderWebVisual = null;
		PostSystemMessage(LocaleText.T("system.cave.spider_web_escape"), new Color(0.72f, 0.92f, 1.0f), GameMessageChannel.Combat);
		return true;
	}

	private void CreateSpiderWebTrapVisual(SimpleActor spider)
	{
		if (_spiderWebVisual != null && IsInstanceValid(_spiderWebVisual))
		{
			_spiderWebVisual.QueueFree();
		}

		_spiderWebVisual = new Node3D { Name = "SpiderWebSuspension" };
		AddChild(_spiderWebVisual);
		var silkMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.86f, 0.88f, 0.96f, 0.82f),
			EmissionEnabled = true,
			Emission = new Color(0.44f, 0.48f, 0.62f),
			EmissionEnergyMultiplier = 1.35f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};

		AddWebMesh("CeilingSilk", new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.028f, Height = 5.4f }, new Vector3(0.0f, 4.15f, 0.0f), Vector3.Zero, silkMaterial);
		for (int index = 0; index < 6; index++)
		{
			float y = 0.35f + index * 0.27f;
			float radius = 0.42f - index * 0.018f;
			AddWebMesh($"CocoonBand{index}", new TorusMesh { InnerRadius = radius - 0.025f, OuterRadius = radius + 0.025f, Rings = 20, RingSegments = 6 }, new Vector3(0.0f, y, 0.0f), new Vector3(index % 2 == 0 ? 7.0f : -7.0f, index * 13.0f, 0.0f), silkMaterial);
		}
		for (int index = 0; index < 4; index++)
		{
			float angle = index / 4.0f * Mathf.Tau;
			AddWebMesh($"CocoonThread{index}", new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = 1.65f }, new Vector3(Mathf.Cos(angle) * 0.34f, 1.02f, Mathf.Sin(angle) * 0.34f), new Vector3(0.0f, 0.0f, Mathf.Sin(angle) * 8.0f), silkMaterial);
		}

		_spiderWebVisual.AddChild(new GpuParticles3D
		{
			Name = "WebDust",
			Position = new Vector3(0.0f, 1.0f, 0.0f),
			Amount = 18,
			Lifetime = 1.2f,
			Preprocess = 0.4f,
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
				EmissionSphereRadius = 0.65f,
				Gravity = new Vector3(0.0f, 0.12f, 0.0f),
				InitialVelocityMin = 0.05f,
				InitialVelocityMax = 0.22f,
				ScaleMin = 0.35f,
				ScaleMax = 0.8f,
				Color = new Color(0.86f, 0.88f, 1.0f, 0.64f),
			},
			DrawPass1 = new SphereMesh { Radius = 0.025f, Height = 0.05f, Material = silkMaterial },
			Emitting = true,
		});
	}

	private void AddWebMesh(string name, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Material material)
	{
		if (_spiderWebVisual == null)
		{
			return;
		}
		var instance = new MeshInstance3D { Name = name, Mesh = mesh, Position = position, RotationDegrees = rotationDegrees };
		instance.SetSurfaceOverrideMaterial(0, material);
		_spiderWebVisual.AddChild(instance);
	}
}
