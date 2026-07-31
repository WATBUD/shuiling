using Godot;
using System.Collections.Generic;

public partial class World : Node3D
{
	private void CreateMapPortal(string name, Vector3 position, string targetMapId, string labelKey)
	{
		bool isCityGate = targetMapId == "wild_select";
		float portalScale = isCityGate ? 1.55f : 1.0f;
		var portalGlowMaterial = MakeEmissiveMaterial(new Color(0.30f, 0.88f, 1.0f, isCityGate ? 0.70f : 0.58f), isCityGate ? 2.85f : 1.75f, 0.20f);
		var portalCoreMaterial = MakeEmissiveMaterial(new Color(0.62f, 0.42f, 1.0f, isCityGate ? 0.62f : 0.46f), isCityGate ? 2.25f : 1.25f, 0.15f);
		var portalSparkMaterial = MakeEmissiveMaterial(new Color(0.82f, 0.96f, 1.0f, 0.92f), isCityGate ? 3.6f : 2.1f, 0.10f);

		var portal = new StaticBody3D
		{
			Name = name,
			Position = position,
		};
		portal.AddToGroup("map_portal");
		portal.SetMeta("target_map", targetMapId);
		portal.SetMeta("label", labelKey);
		_propsRoot.AddChild(portal);

		AddMesh(portal, "PortalBase", CylinderMeshFor(1.45f * portalScale, 1.45f * portalScale, 0.16f), new Vector3(0.0f, 0.08f, 0.0f), Vector3.Zero, Vector3.One, _matRune);
		var groundAura = AddMesh(portal, "PortalGroundAura", CylinderMeshFor(2.25f * portalScale, 2.25f * portalScale, 0.025f), new Vector3(0.0f, 0.13f, 0.0f), Vector3.Zero, Vector3.One, portalGlowMaterial);
		AddMesh(portal, "PortalOuterRune", CylinderMeshFor(1.62f * portalScale, 1.62f * portalScale, 0.030f), new Vector3(0.0f, 0.155f, 0.0f), Vector3.Zero, Vector3.One, portalGlowMaterial);
		AddMesh(portal, "PortalGrandRune", CylinderMeshFor(2.88f * portalScale, 2.88f * portalScale, 0.018f), new Vector3(0.0f, 0.145f, 0.0f), Vector3.Zero, Vector3.One, portalCoreMaterial);
		var outerRing = AddMesh(portal, "PortalOuterHalo", CylinderMeshFor(1.12f * portalScale, 1.12f * portalScale, 0.028f), new Vector3(0.0f, 0.19f, 0.0f), Vector3.Zero, Vector3.One, portalCoreMaterial);
		var innerRing = AddMesh(portal, "PortalInnerHalo", CylinderMeshFor(0.68f * portalScale, 0.68f * portalScale, 0.032f), new Vector3(0.0f, 0.225f, 0.0f), Vector3.Zero, Vector3.One, portalGlowMaterial);
		AddMesh(portal, "PortalCenterGlow", new SphereMesh { Radius = 0.50f * portalScale, Height = 0.26f * portalScale }, new Vector3(0.0f, 0.32f, 0.0f), Vector3.Zero, new Vector3(1.25f, 0.24f, 1.25f), portalCoreMaterial);
		// City gate uses the same hexagram/ring/particle design as the wild
		// portals (just a larger, richer version) — no separate light column.
		AddPortalRuneStones(portal, portalGlowMaterial, portalScale);
		AddPortalHexagram(portal, portalSparkMaterial, portalScale);
		AddPortalParticles(portal, portalSparkMaterial, portalScale, isCityGate);

		var portalLight = new OmniLight3D
		{
			Name = "PortalLight",
			LightColor = new Color(0.45f, 0.86f, 1.0f),
			LightEnergy = isCityGate ? 4.2f : 1.8f,
			OmniRange = isCityGate ? 15.0f : 8.5f,
			Position = new Vector3(0.0f, isCityGate ? 1.6f : 0.75f, 0.0f),
		};
		portal.AddChild(portalLight);

		var effect = new MapPortalEffect
		{
			Name = "PortalEffect",
			OuterRing = outerRing,
			InnerRing = innerRing,
			GroundAura = groundAura,
			PortalLight = portalLight,
		};
		portal.AddChild(effect);

		var label = new Label3D
		{
			Name = "PortalLabel",
			Text = LocaleText.T(labelKey),
			Position = new Vector3(0.0f, isCityGate ? 2.72f : 1.65f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = isCityGate ? 28 : 22,
			PixelSize = 0.008f,
			OutlineSize = 6,
			HorizontalAlignment = HorizontalAlignment.Center,
			Width = 300.0f,
		};
		label.OutlineModulate = new Color(0.02f, 0.03f, 0.025f, 0.95f);
		label.Modulate = new Color(0.72f, 0.92f, 1.0f);
		portal.AddChild(label);

		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, 0.35f, 0.0f),
			Shape = new CylinderShape3D { Radius = 1.8f * portalScale, Height = 0.7f },
		};
		portal.AddChild(collisionShape);
	}

	private void RefreshLocalizedWorldLabels()
	{
		foreach (Node node in GetTree().GetNodesInGroup("map_portal"))
		{
			if (node is not Node3D portal || !portal.HasMeta("label"))
			{
				continue;
			}

			Label3D? label = portal.GetNodeOrNull<Label3D>("PortalLabel");
			if (label == null)
			{
				continue;
			}

			string labelKey = portal.GetMeta("label").AsString();
			if (!string.IsNullOrWhiteSpace(labelKey))
			{
				label.Text = LocaleText.T(labelKey);
			}
		}
	}

	private void AddPortalParticles(Node3D portal, Material particleMaterial, float portalScale, bool isCityGate)
	{
		var particleMesh = new SphereMesh { Radius = isCityGate ? 0.065f : 0.045f, Height = isCityGate ? 0.13f : 0.09f };
		particleMesh.SurfaceSetMaterial(0, particleMaterial);

		var processMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 1.1f * portalScale,
			Direction = new Vector3(0.0f, 1.0f, 0.0f),
			Spread = isCityGate ? 76.0f : 58.0f,
			Gravity = new Vector3(0.0f, isCityGate ? 0.34f : 0.18f, 0.0f),
			InitialVelocityMin = isCityGate ? 0.75f : 0.35f,
			InitialVelocityMax = isCityGate ? 2.45f : 1.15f,
			AngularVelocityMin = -90.0f,
			AngularVelocityMax = 90.0f,
			ScaleMin = isCityGate ? 0.75f : 0.55f,
			ScaleMax = isCityGate ? 2.35f : 1.35f,
			Color = new Color(0.72f, 0.94f, 1.0f, 0.86f),
		};

		var risingParticles = new GpuParticles3D
		{
			Name = "PortalRisingParticles",
			Amount = isCityGate ? 180 : 72,
			Lifetime = isCityGate ? 3.4f : 2.2f,
			Randomness = 0.58f,
			Explosiveness = 0.0f,
			VisibilityAabb = isCityGate
				? new Aabb(new Vector3(-5.8f, -0.4f, -5.8f), new Vector3(11.6f, 8.2f, 11.6f))
				: new Aabb(new Vector3(-2.4f, -0.2f, -2.4f), new Vector3(4.8f, 4.2f, 4.8f)),
			ProcessMaterial = processMaterial,
			DrawPass1 = particleMesh,
			Emitting = true,
			Position = new Vector3(0.0f, 0.35f, 0.0f),
		};
		portal.AddChild(risingParticles);

		var orbitMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
			EmissionRingRadius = 1.25f * portalScale,
			EmissionRingInnerRadius = 0.85f * portalScale,
			EmissionRingHeight = isCityGate ? 0.42f : 0.12f,
			Direction = new Vector3(0.0f, 0.35f, 1.0f),
			Spread = 90.0f,
			Gravity = Vector3.Zero,
			InitialVelocityMin = isCityGate ? 0.28f : 0.12f,
			InitialVelocityMax = isCityGate ? 0.86f : 0.45f,
			ScaleMin = isCityGate ? 0.55f : 0.35f,
			ScaleMax = isCityGate ? 1.45f : 0.9f,
			Color = new Color(0.94f, 0.84f, 1.0f, 0.78f),
		};

		var orbitParticles = new GpuParticles3D
		{
			Name = "PortalOrbitParticles",
			Amount = isCityGate ? 110 : 42,
			Lifetime = isCityGate ? 2.4f : 1.7f,
			Randomness = 0.42f,
			VisibilityAabb = isCityGate
				? new Aabb(new Vector3(-5.2f, -0.6f, -5.2f), new Vector3(10.4f, 6.8f, 10.4f))
				: new Aabb(new Vector3(-2.2f, -0.4f, -1.6f), new Vector3(4.4f, 3.4f, 3.2f)),
			ProcessMaterial = orbitMaterial,
			DrawPass1 = particleMesh,
			Emitting = true,
			Position = new Vector3(0.0f, 0.34f, 0.0f),
			RotationDegrees = Vector3.Zero,
		};
		portal.AddChild(orbitParticles);
	}

	// Six-pointed star (hexagram) laid flat on the portal floor: two overlapping
	// triangles + a circumscribing ring, matching the "傳送點為六芒星地板" ask.
	private void AddPortalHexagram(Node3D portal, Material material, float portalScale)
	{
		float radius = 1.98f * portalScale;
		float y = 0.165f;
		AddHexagramTriangle(portal, material, radius, y, 90.0f, portalScale, "A");
		AddHexagramTriangle(portal, material, radius, y, 30.0f, portalScale, "B");
		AddMesh(
			portal,
			"HexRing",
			new TorusMesh { InnerRadius = radius * 0.99f, OuterRadius = radius * 1.03f, RingSegments = 6, Rings = 48 },
			new Vector3(0.0f, y, 0.0f),
			Vector3.Zero,
			Vector3.One,
			material);
	}

	private void AddHexagramTriangle(Node3D portal, Material material, float radius, float y, float startDegrees, float portalScale, string tag)
	{
		var vertices = new Vector3[3];
		for (int index = 0; index < 3; index++)
		{
			float angle = Mathf.DegToRad(startDegrees + index * 120.0f);
			vertices[index] = new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
		}

		for (int index = 0; index < 3; index++)
		{
			Vector3 p0 = vertices[index];
			Vector3 p1 = vertices[(index + 1) % 3];
			Vector3 mid = (p0 + p1) * 0.5f;
			float length = p0.DistanceTo(p1);
			float yaw = Mathf.Atan2(-(p1.Z - p0.Z), p1.X - p0.X);
			AddMesh(
				portal,
				$"HexEdge{tag}{index}",
				BoxMeshFor(new Vector3(length, 0.03f, 0.10f * portalScale)),
				new Vector3(mid.X, y, mid.Z),
				new Vector3(0.0f, Mathf.RadToDeg(yaw), 0.0f),
				Vector3.One,
				material);
		}
	}

	private void AddPortalRuneStones(Node3D portal, Material material, float portalScale)
	{
		for (int index = 0; index < 12; index++)
		{
			float angle = index / 12.0f * Mathf.Tau;
			float radius = (index % 2 == 0 ? 1.42f : 1.18f) * portalScale;
			Vector3 position = new(Mathf.Cos(angle) * radius, 0.245f, Mathf.Sin(angle) * radius);
			AddMesh(
				portal,
				$"PortalRune{index}",
				BoxMeshFor(new Vector3(0.28f * portalScale, 0.026f, 0.07f * portalScale)),
				position,
				new Vector3(0.0f, Mathf.RadToDeg(-angle), 0.0f),
				Vector3.One,
				material
			);
		}
	}
}
