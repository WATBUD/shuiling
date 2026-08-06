using Godot;

public partial class SimpleActor : CharacterBody3D
{
	public static void SetNameplateScale(float scale)
	{
		NameplateScale = Mathf.Clamp(scale, MinNameplateScale, MaxNameplateScale);
	}

	// Public so a settings change can re-apply the scale to a live actor.
	public void RefreshNameplateDisplay()
	{
		RefreshNameplate();
	}

	private void CreateNameplate()
	{
		_nameplate = new Label3D
		{
			Name = "Nameplate",
			Position = new Vector3(0.0f, 2.35f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FixedSize = false,
			NoDepthTest = false,
			FontSize = 20,
			PixelSize = 0.0075f,
			OutlineSize = 6,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Width = 320.0f,
		};
		AddChild(_nameplate);

		// Shaded so the marker reads as a real 3D ball from every angle rather
		// than a flat unshaded disc; kept self-lit via emission so it still pops
		// in dark biomes.
		_nameplateMarkerMaterial = MakeMarkerBallMaterial(new Color(1.0f, 0.28f, 0.20f, 0.92f));
		_nameplateHaloMaterial = MakeMarkerMaterial(new Color(1.0f, 0.28f, 0.20f, 0.34f), 0.35f);
		_nameplateMarker = new MeshInstance3D
		{
			Name = "NameplateMarker",
			// Full, smooth sphere (Height = 2 x Radius) — round from all 360°.
			Mesh = new SphereMesh { Radius = 0.085f, Height = 0.17f, RadialSegments = 24, Rings = 12 },
			MaterialOverride = _nameplateMarkerMaterial,
		};
		_nameplateHalo = new MeshInstance3D
		{
			Name = "NameplateHalo",
			Mesh = new TorusMesh { InnerRadius = 0.018f, OuterRadius = 0.34f },
			RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
			MaterialOverride = _nameplateHaloMaterial,
		};
		AddChild(_nameplateHalo);
		AddChild(_nameplateMarker);
		RefreshNameplate();
	}

	private void RefreshNameplate()
	{
		if (_nameplate == null)
		{
			return;
		}

		string capturedText = _isAwaitingRecovery
			? LocaleText.T("actor.nameplate.awaiting_recovery")
			: _isDefeated
			? LocaleText.T("actor.nameplate.defeated")
			: _isCaptured
			? _isInActiveParty ? LocaleText.T("actor.nameplate.active") : LocaleText.T("actor.nameplate.stored")
			: string.Empty;
		// Rebirth count (轉生) shown as ✦xN next to a companion's level.
		string rebirthSuffix = _isCaptured && RebirthCount > 0 ? $" ✦x{RebirthCount}" : string.Empty;
		// Wild monster ready to be netted (weakened or staggered) gets a tag.
		string captureTag = CaptureReady ? " " + LocaleText.T(IsStaggered ? "mob.capture_stagger" : "mob.capture_ready") : string.Empty;
		if (ActorKind == "monster")
		{
			string colorName = LocaleText.T(IsBoss ? "rarity.color.gold" : MonsterRarity.ColorNameKey(Rarity));
			_nameplate.Text = $"[{colorName}][LV{Level}][{LocalizedDisplayName}]{rebirthSuffix}{captureTag}{capturedText}";
		}
		else if (ActorKind == "npc")
		{
			// Town service NPCs (merchants, refiner, gacha, ...) are non-combat, so
			// their nameplate shows only the name. Recruitable quest NPCs additionally
			// expose affinity above the name so it does not occupy quest tracking UI.
			_nameplate.Text = ShowsRecruitQuestAffinity()
				? $"{LocaleText.F("quest.log.affinity", Affinity, 80)}\n{LocalizedDisplayName}"
				: LocalizedDisplayName;
		}
		else
		{
			_nameplate.Text = $"{LocaleText.T("actor.level_prefix")}{Level} {LocalizedDisplayName}{rebirthSuffix}{captureTag}{capturedText}";
		}
		_nameplate.FontSize = Mathf.RoundToInt((IsTrainingDummy ? 40 : IsBoss ? 28 : 20) * NameplateScale);
		Color markerColor = GetNameplateStatusColor();
		_nameplate.Modulate = markerColor;
		_nameplate.OutlineModulate = new Color(0.02f, 0.025f, 0.03f, 0.96f);
		if (_nameplateMarkerMaterial != null)
		{
			_nameplateMarkerMaterial.AlbedoColor = markerColor;
			_nameplateMarkerMaterial.Emission = markerColor;
		}

		if (_nameplateHaloMaterial != null)
		{
			_nameplateHaloMaterial.AlbedoColor = new Color(markerColor.R, markerColor.G, markerColor.B, _isCaptured ? 0.45f : 0.30f);
			_nameplateHaloMaterial.Emission = markerColor;
		}
		if (_nameplateHalo != null)
		{
			_nameplateHalo.Visible = !IsBoss;
		}

		UpdateNameplatePosition();
	}

	private void UpdateNameplatePosition()
	{
		if (_nameplate == null)
		{
			return;
		}

		float visualTop = GetVisualTopY(this);
		float fallbackTop = ActorKind == "monster" ? 2.2f : 2.05f;
		float labelY = Mathf.Max(visualTop + 0.38f, fallbackTop);
		_nameplate.Position = new Vector3(0.0f, labelY, 0.0f);
		if (_nameplateMarker != null)
		{
			_nameplateMarker.Position = new Vector3(0.0f, labelY + (ShowsRecruitQuestAffinity() ? 0.56f : 0.34f), 0.0f);
			float markerScale = IsBoss ? 1.65f : _isCaptured ? 1.18f : 1.0f;
			_nameplateMarker.Scale = Vector3.One * markerScale;
		}

		if (_nameplateHalo != null)
		{
			_nameplateHalo.Position = new Vector3(0.0f, labelY + (ShowsRecruitQuestAffinity() ? 0.50f : 0.28f), 0.0f);
			float haloScale = IsBoss ? 2.15f : _isCaptured ? 1.18f : 1.0f;
			_nameplateHalo.Scale = Vector3.One * haloScale;
		}
	}

	private bool ShowsRecruitQuestAffinity()
	{
		if (!IsNpcRecruitCandidate || MapId != "city")
		{
			return false;
		}

		return DisplayName is not (
			"name.npc.blacksmith" or
			"name.npc.item_merchant" or
			"name.npc.pet_trainer" or
			"name.npc.mercenary_broker" or
			"name.npc.warehouse_keeper" or
			"name.npc.refiner" or
			"name.npc.core_enhancer" or
			"name.npc.gacha");
	}

	private Color GetNameplateStatusColor()
	{
		if (_isDefeated)
		{
			return new Color(0.62f, 0.66f, 0.70f, 0.88f);
		}

		if (ActorKind != "monster")
		{
			return new Color(0.64f, 0.86f, 1.0f, 0.94f);
		}

		if (IsBoss)
		{
			return _bossEnraged
				? new Color(1.0f, 0.22f, 0.08f, 0.98f)
				: new Color(1.0f, 0.76f, 0.18f, 0.98f);
		}

		// Every monster name uses the same colour represented by its rarity tag:
		// [白], [藍], [紫], [橙]. Captured monsters retain their original rarity,
		// while active/stored state remains explicit in the trailing text.
		return MonsterRarity.Color(Rarity);
	}

	private static StandardMaterial3D MakeMarkerMaterial(Color color, float emissionEnergy)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = emissionEnergy,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			NoDepthTest = true,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
	}

	// Marker ball: lit (per-pixel) so its curvature is visible as a rounded
	// gradient from any direction, with mild emission so it self-illuminates.
	private static StandardMaterial3D MakeMarkerBallMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = 0.35f,
			Roughness = 0.4f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
		};
	}

	private float GetVisualTopY(Node node)
	{
		float topY = 0.0f;
		foreach (Node child in node.GetChildren())
		{
			if (child == _nameplate || child == _nameplateMarker || child == _nameplateHalo || child is CollisionShape3D)
			{
				continue;
			}

			if (child is MeshInstance3D meshInstance && meshInstance.Mesh != null)
			{
				topY = Mathf.Max(topY, GetMeshTopY(meshInstance));
			}

			topY = Mathf.Max(topY, GetVisualTopY(child));
		}

		return topY;
	}

	private float GetMeshTopY(MeshInstance3D meshInstance)
	{
		Aabb aabb = meshInstance.GetAabb();
		float topY = 0.0f;
		for (int x = 0; x <= 1; x++)
		{
			for (int y = 0; y <= 1; y++)
			{
				for (int z = 0; z <= 1; z++)
				{
					var corner = new Vector3(
						x == 0 ? aabb.Position.X : aabb.Position.X + aabb.Size.X,
						y == 0 ? aabb.Position.Y : aabb.Position.Y + aabb.Size.Y,
						z == 0 ? aabb.Position.Z : aabb.Position.Z + aabb.Size.Z
					);
					Vector3 actorLocalCorner = ToLocal(meshInstance.ToGlobal(corner));
					topY = Mathf.Max(topY, actorLocalCorner.Y);
				}
			}
		}

		return topY;
	}
}
