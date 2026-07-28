using Godot;
using System.Collections.Generic;

// 升等特效：金色的地面光環擴散 + 一圈向上升起的能量環 + 短暫光暈，播完自動釋放。
// 用法：spawn 後把 GlobalPosition 設在角色腳邊即可（fire-and-forget）。
public partial class LevelUpEffect : Node3D
{
	private const float Lifetime = 1.1f;
	private static readonly Color Gold = new(1.0f, 0.86f, 0.36f, 0.95f);

	private readonly List<(MeshInstance3D Mesh, StandardMaterial3D Mat, float StartScale, float EndScale, float RiseY)> _rings = new();
	private OmniLight3D? _light;
	private float _age;

	public override void _Ready()
	{
		// 地面擴散環（不旋轉 → TorusMesh 本來就水平）。
		AddRing(0.35f, 1.9f, 0.10f, 0.34f, 0.06f, 0.0f);
		// 向上升起、同時擴張的能量環。
		AddRing(0.55f, 1.2f, 0.06f, 0.24f, 0.10f, 2.2f);

		_light = new OmniLight3D
		{
			LightColor = Gold,
			OmniRange = 5.5f,
			LightEnergy = 2.6f,
			Position = new Vector3(0.0f, 1.1f, 0.0f),
		};
		AddChild(_light);
	}

	private void AddRing(float startScale, float endScale, float innerRadius, float outerRadius, float yOffset, float riseY)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = Gold,
			Emission = Gold,
			EmissionEnabled = true,
			EmissionEnergyMultiplier = 2.2f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			NoDepthTest = true,
			RenderPriority = 8,
		};

		var mesh = new MeshInstance3D
		{
			Name = "LevelUpRing",
			Mesh = new TorusMesh { InnerRadius = innerRadius, OuterRadius = outerRadius, RingSegments = 6, Rings = 32 },
			Position = new Vector3(0.0f, yOffset, 0.0f),
			Scale = Vector3.One * startScale,
			MaterialOverride = mat,
		};
		AddChild(mesh);
		_rings.Add((mesh, mat, startScale, endScale, riseY));
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		float t = Mathf.Clamp(_age / Lifetime, 0.0f, 1.0f);
		float expand = 1.0f - (1.0f - t) * (1.0f - t); // ease-out
		float alpha = 1.0f - t;

		foreach ((MeshInstance3D mesh, StandardMaterial3D mat, float startScale, float endScale, float riseY) in _rings)
		{
			float scale = Mathf.Lerp(startScale, endScale, expand);
			mesh.Scale = new Vector3(scale, 1.0f, scale);
			// 地面環（riseY=0）貼地；升起環從腳邊升到 riseY 高度。
			float targetY = riseY <= 0.0f ? 0.06f : riseY;
			mesh.Position = new Vector3(0.0f, Mathf.Lerp(0.06f, targetY, expand), 0.0f);
			mat.AlbedoColor = new Color(Gold.R, Gold.G, Gold.B, Gold.A * alpha);
			mat.EmissionEnergyMultiplier = 2.2f * alpha;
		}

		if (_light != null)
		{
			_light.LightEnergy = 2.6f * alpha;
		}

		if (_age >= Lifetime)
		{
			QueueFree();
		}
	}
}
