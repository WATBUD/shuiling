using Godot;

// Town Portal Scroll (回城卷): an emergency-retreat consumable. Usable in the
// wild only when it's safe — not in a dungeon/cave, not during a boss fight,
// not right after taking damage, and with no monsters bearing down on you.
// Returns the party to the city. New characters start with 5.
public partial class PlayerController
{
	private const int StarterTownPortalScrolls = 5;
	private const ulong TownPortalCombatBlockMsec = 5000;
	private const float TownPortalChannelSeconds = 3.0f;

	private ulong _lastCombatMsec;

	// Channel (3s cast) state. The scroll is consumed on completion, so a combat
	// interrupt costs nothing.
	private bool _townPortalChanneling;
	private float _townPortalRemaining;
	private ulong _townPortalStartMsec;
	private Node3D? _townPortalCastEffect;
	private OmniLight3D? _townPortalCastLight;
	private MeshInstance3D? _townPortalCastRing;
	private MeshInstance3D? _townPortalCastRing2;
	private MeshInstance3D? _townPortalCastColumn;

	// Called from ReceiveDamage — refreshes the "recently in combat" window.
	private void MarkRecentCombat()
	{
		_lastCombatMsec = Time.GetTicksMsec();
	}

	private void GrantStarterTownPortalScrolls()
	{
		AddInventoryItem(BuildCatalog.TownPortalScrollId, StarterTownPortalScrolls);
	}

	// Warms up the cast effect's shaders/particle system during load, so the
	// FIRST real use doesn't stutter (Godot compiles additive/unshaded material
	// variants and initialises GpuParticles on first draw). We render the same
	// shader features once far under the map, then free them.
	public void PrewarmTownPortalCastEffect()
	{
		var warm = new Node3D { Name = "TownPortalWarm", Position = new Vector3(0.0f, -1000.0f, 0.0f) };
		AddChild(warm);

		var beam = new MeshInstance3D
		{
			Name = "WarmBeam",
			Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 1.0f, RadialSegments = 8 },
			MaterialOverride = MakeCastGlow(new Color(0.48f, 0.82f, 1.0f), 0.3f, 2.0f),
		};
		warm.AddChild(beam);
		warm.AddChild(BuildCastMotes(new Color(0.48f, 0.82f, 1.0f)));

		// Free after a few frames — long enough for the shaders to compile.
		SceneTreeTimer timer = GetTree().CreateTimer(0.6f);
		timer.Timeout += () =>
		{
			if (IsInstanceValid(warm))
			{
				warm.QueueFree();
			}
		};
	}

	// Hotkey entry point (guarded so it doesn't fire while a panel is open).
	private void TryUseTownPortalScroll()
	{
		if (_settingsPanel.Visible || _partyPanel.Visible || _inventoryPanel.Visible
			|| _formationPanel.Visible || _npcQuestDialog.Visible
			|| (_mapTravelDialog != null && _mapTravelDialog.Visible))
		{
			return;
		}

		UseTownPortalScroll();
	}

	// Public so the inventory panel can trigger it too. Returns true on success.
	public bool UseTownPortalScroll()
	{
		if (GetInventoryCount(BuildCatalog.TownPortalScrollId) <= 0)
		{
			PostSystemMessage(LocaleText.T("item.town_portal.none"), new Color(1.0f, 0.72f, 0.5f));
			return false;
		}

		if (_townPortalChanneling)
		{
			return false;
		}

		if (!CanUseTownPortalScroll(out string reasonKey))
		{
			// Show WHY it's blocked as a floating tip above the player + log.
			string reason = LocaleText.T(reasonKey);
			SpawnFloatingEffect(reason, new Color(1.0f, 0.72f, 0.5f, 0.95f), 1.1f, 0.6f);
			PostSystemMessage(reason, new Color(1.0f, 0.72f, 0.5f));
			return false;
		}

		// Begin a 3s channel with a light effect; the scroll is only consumed
		// when it completes, and taking damage interrupts it (no waste).
		BeginTownPortalChannel();
		return true;
	}

	private void BeginTownPortalChannel()
	{
		_townPortalChanneling = true;
		_townPortalRemaining = TownPortalChannelSeconds;
		_townPortalStartMsec = Time.GetTicksMsec();
		SpawnTownPortalCastEffect();
		PostSystemMessage(LocaleText.F("item.town_portal.channel", Mathf.CeilToInt(TownPortalChannelSeconds)), new Color(0.72f, 0.92f, 1.0f));
	}

	private void UpdateTownPortalChannel(float step)
	{
		if (!_townPortalChanneling)
		{
			return;
		}

		// Interrupted if we got hit since the channel began, a boss engaged, or
		// we somehow left the wild (e.g. another travel).
		bool damagedSinceStart = _lastCombatMsec >= _townPortalStartMsec;
		bool bossEngaged = _bossHudCombatVisibleRemaining > 0.0f;
		bool leftWild = GetParent() is not World world || world.ActiveMapId == "city" || world.ActiveMapId.Contains("_cave_");
		if (damagedSinceStart || bossEngaged || leftWild)
		{
			CancelTownPortalChannel();
			return;
		}

		_townPortalRemaining -= step;
		float progress = Mathf.Clamp(1.0f - _townPortalRemaining / TownPortalChannelSeconds, 0.0f, 1.0f);
		UpdateTownPortalCastEffect(progress, step);

		if (_townPortalRemaining <= 0.0f)
		{
			CompleteTownPortalChannel();
		}
	}

	private void CompleteTownPortalChannel()
	{
		_townPortalChanneling = false;
		ClearTownPortalCastEffect();
		if (!TryConsumeInventoryItem(BuildCatalog.TownPortalScrollId))
		{
			return;
		}

		PostSystemMessage(LocaleText.T("item.town_portal.used"), new Color(0.72f, 0.92f, 1.0f));
		if (GetParent() is World world)
		{
			world.RequestMapTravel("city");
		}
	}

	private void CancelTownPortalChannel()
	{
		if (!_townPortalChanneling)
		{
			return;
		}

		_townPortalChanneling = false;
		ClearTownPortalCastEffect();
		string message = LocaleText.T("item.town_portal.interrupted");
		SpawnFloatingEffect(message, new Color(1.0f, 0.6f, 0.45f, 0.95f), 1.1f, 0.6f);
		PostSystemMessage(message, new Color(1.0f, 0.6f, 0.45f));
	}

	// ------------------------------------------------------------- cast visuals

	private static StandardMaterial3D MakeCastGlow(Color color, float alpha, float energy)
	{
		// Additive + unshaded so it reads as light (a beam/aura), not a solid
		// plastic mesh; cull disabled so the inside of the beam stays visible.
		return new StandardMaterial3D
		{
			AlbedoColor = new Color(color.R, color.G, color.B, alpha),
			Emission = color,
			EmissionEnabled = true,
			EmissionEnergyMultiplier = energy,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
	}

	// Digimon-style "return beam": a soft additive light column from above,
	// twin spinning ground halos, spiralling motes and a pulsing light.
	private void SpawnTownPortalCastEffect()
	{
		ClearTownPortalCastEffect();

		var beamColor = new Color(0.48f, 0.82f, 1.0f);
		var coreColor = new Color(0.86f, 0.96f, 1.0f);

		_townPortalCastEffect = new Node3D { Name = "TownPortalCast", Position = new Vector3(0.0f, 0.05f, 0.0f) };
		AddChild(_townPortalCastEffect);

		// Outer soft beam + bright inner core, descending through the player.
		var outerBeam = new MeshInstance3D
		{
			Name = "CastBeamOuter",
			Mesh = new CylinderMesh { TopRadius = 1.05f, BottomRadius = 0.85f, Height = 7.0f, RadialSegments = 20 },
			Position = new Vector3(0.0f, 3.5f, 0.0f),
			MaterialOverride = MakeCastGlow(beamColor, 0.16f, 1.8f),
		};
		_townPortalCastEffect.AddChild(outerBeam);

		_townPortalCastColumn = new MeshInstance3D
		{
			Name = "CastBeamCore",
			Mesh = new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.30f, Height = 7.0f, RadialSegments = 16 },
			Position = new Vector3(0.0f, 3.5f, 0.0f),
			MaterialOverride = MakeCastGlow(coreColor, 0.45f, 3.4f),
		};
		_townPortalCastEffect.AddChild(_townPortalCastColumn);

		// Twin ground halos (counter-rotating).
		_townPortalCastRing = new MeshInstance3D
		{
			Name = "CastHaloOuter",
			Mesh = new TorusMesh { InnerRadius = 1.02f, OuterRadius = 1.24f, RingSegments = 8, Rings = 40 },
			MaterialOverride = MakeCastGlow(beamColor, 0.7f, 2.8f),
		};
		_townPortalCastEffect.AddChild(_townPortalCastRing);

		_townPortalCastRing2 = new MeshInstance3D
		{
			Name = "CastHaloInner",
			Mesh = new TorusMesh { InnerRadius = 0.55f, OuterRadius = 0.72f, RingSegments = 8, Rings = 32 },
			Position = new Vector3(0.0f, 0.03f, 0.0f),
			MaterialOverride = MakeCastGlow(coreColor, 0.8f, 3.2f),
		};
		_townPortalCastEffect.AddChild(_townPortalCastRing2);

		_townPortalCastEffect.AddChild(BuildCastMotes(beamColor));

		_townPortalCastLight = new OmniLight3D
		{
			Name = "CastLight",
			LightColor = beamColor,
			LightEnergy = 0.6f,
			OmniRange = 6.0f,
			Position = new Vector3(0.0f, 1.2f, 0.0f),
		};
		_townPortalCastEffect.AddChild(_townPortalCastLight);
	}

	private static GpuParticles3D BuildCastMotes(Color color)
	{
		var moteMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(color.R, color.G, color.B, 0.95f),
			Emission = color,
			EmissionEnabled = true,
			EmissionEnergyMultiplier = 3.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
		};
		var moteMesh = new QuadMesh { Size = new Vector2(0.14f, 0.14f), Material = moteMaterial };

		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
			EmissionRingAxis = Vector3.Up,
			EmissionRingRadius = 1.1f,
			EmissionRingInnerRadius = 0.5f,
			EmissionRingHeight = 0.15f,
			Direction = Vector3.Up,
			Spread = 8.0f,
			InitialVelocityMin = 2.4f,
			InitialVelocityMax = 3.8f,
			// Slight upward buoyancy + tangential swirl for the rising spiral.
			Gravity = new Vector3(0.0f, 1.4f, 0.0f),
			RadialAccelMin = -1.4f,
			RadialAccelMax = -0.6f,
			TangentialAccelMin = 2.2f,
			TangentialAccelMax = 3.4f,
			ScaleMin = 0.5f,
			ScaleMax = 1.15f,
			Color = new Color(color.R, color.G, color.B, 0.95f),
		};

		return new GpuParticles3D
		{
			Name = "CastMotes",
			Amount = 56,
			Lifetime = 1.35f,
			Randomness = 0.5f,
			Explosiveness = 0.0f,
			ProcessMaterial = process,
			DrawPass1 = moteMesh,
			Emitting = true,
			Position = new Vector3(0.0f, 0.1f, 0.0f),
			VisibilityAabb = new Aabb(new Vector3(-2.4f, -0.4f, -2.4f), new Vector3(4.8f, 7.6f, 4.8f)),
		};
	}

	private void UpdateTownPortalCastEffect(float progress, float step)
	{
		if (_townPortalCastEffect == null || !IsInstanceValid(_townPortalCastEffect))
		{
			return;
		}

		float pulse = Mathf.Sin(Time.GetTicksMsec() * 0.012f);

		if (_townPortalCastRing != null)
		{
			_townPortalCastRing.RotationDegrees += new Vector3(0.0f, (90.0f + progress * 260.0f) * step, 0.0f);
			float scale = Mathf.Lerp(1.3f, 0.85f, progress);
			_townPortalCastRing.Scale = new Vector3(scale, 1.0f, scale);
		}

		if (_townPortalCastRing2 != null)
		{
			_townPortalCastRing2.RotationDegrees -= new Vector3(0.0f, (140.0f + progress * 300.0f) * step, 0.0f);
		}

		if (_townPortalCastColumn != null)
		{
			float columnScale = Mathf.Lerp(0.35f, 1.0f, progress) + pulse * 0.05f;
			_townPortalCastColumn.Scale = new Vector3(columnScale, 1.0f, columnScale);
		}

		if (_townPortalCastLight != null)
		{
			_townPortalCastLight.LightEnergy = Mathf.Lerp(0.6f, 3.4f, progress) + pulse * 0.35f;
		}
	}

	private void ClearTownPortalCastEffect()
	{
		if (_townPortalCastEffect != null && IsInstanceValid(_townPortalCastEffect))
		{
			_townPortalCastEffect.QueueFree();
		}

		_townPortalCastEffect = null;
		_townPortalCastRing = null;
		_townPortalCastRing2 = null;
		_townPortalCastColumn = null;
		_townPortalCastLight = null;
	}

	// Returns whether the scroll can be used, plus a locale key explaining why
	// not. Boss "fight" = an active combat window (NotifyBossCombat), NOT merely
	// a boss existing on the map. "In combat" = recently took damage — a wild
	// map always has wandering monsters, so proximity alone must not block.
	private bool CanUseTownPortalScroll(out string reasonKey)
	{
		reasonKey = "item.town_portal.blocked";
		if (GetParent() is not World world)
		{
			return false;
		}

		string mapId = world.ActiveMapId;
		if (mapId == "city")
		{
			reasonKey = "item.town_portal.in_town";
			return false;
		}

		if (mapId.Contains("_cave_"))
		{
			reasonKey = "item.town_portal.in_dungeon";
			return false;
		}

		if (_bossHudCombatVisibleRemaining > 0.0f)
		{
			reasonKey = "item.town_portal.boss";
			return false;
		}

		if (Time.GetTicksMsec() - _lastCombatMsec < TownPortalCombatBlockMsec)
		{
			reasonKey = "item.town_portal.combat";
			return false;
		}

		return true;
	}
}
