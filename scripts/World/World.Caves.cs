using Godot;
using System.Collections.Generic;

public partial class World
{
	private const float CaveHalfWidth = 30.0f;
	private const float CaveHalfLength = 36.0f;
	private const float CaveRespawnInterval = 18.0f;
	private readonly Dictionary<string, Node3D> _caveMapRootsById = new();
	private readonly Dictionary<string, int> _caveTargetCountsById = new();
	private readonly Dictionary<string, Vector3> _caveEntrancePositionsByWildMap = new();
	private string _activeCaveOriginMapId = "wild_forest";
	private int _activeCaveDepth;
	private float _caveRespawnRemaining = CaveRespawnInterval;

	private static bool IsCaveMapId(string mapId)
	{
		return !string.IsNullOrWhiteSpace(mapId) && mapId.Contains("_cave_");
	}

	private static string MakeCaveMapId(string originMapId, int depth)
	{
		return $"{originMapId}_cave_{Mathf.Max(depth, 1)}";
	}

	private static bool TryParseCaveMapId(string mapId, out string originMapId, out int depth)
	{
		originMapId = "wild_forest";
		depth = 0;
		int marker = mapId.LastIndexOf("_cave_", System.StringComparison.Ordinal);
		if (marker <= 0 || !int.TryParse(mapId[(marker + 6)..], out depth) || depth < 1)
		{
			return false;
		}

		originMapId = mapId[..marker];
		return true;
	}

	private string GetCaveMapDisplayName(string mapId)
	{
		return TryParseCaveMapId(mapId, out _, out int depth)
			? LocaleText.F("map.cave.depth", depth)
			: LocaleText.T("map.cave");
	}

	private void CreateWildernessCaveEntrance(string wildMapId)
	{
		Vector3 position = wildMapId switch
		{
			"wild_marsh" => new Vector3(50.0f, 0.0f, -48.0f),
			"wild_badlands" => new Vector3(-51.0f, 0.0f, 47.0f),
			"wild_snow" => new Vector3(49.0f, 0.0f, 47.0f),
			_ => new Vector3(-51.0f, 0.0f, -48.0f),
		};
		_caveEntrancePositionsByWildMap[wildMapId] = position;
		_obstaclePositions.Add(position);

		var entrance = new StaticBody3D
		{
			Name = $"CaveEntrance_{wildMapId}",
			Position = position,
		};
		entrance.AddToGroup("map_portal");
		entrance.SetMeta("target_map", $"cave_enter|{wildMapId}");
		entrance.SetMeta("label", "portal.enter_cave");
		_propsRoot.AddChild(entrance);

		Material dark = MakeMaterial(new Color(0.018f, 0.014f, 0.026f));
		Material stone = MakeMaterial(new Color(0.16f, 0.17f, 0.20f));
		AddMesh(entrance, "CaveMouth", new SphereMesh { Radius = 3.1f, Height = 5.0f }, new Vector3(0.0f, 2.1f, 0.65f), Vector3.Zero, new Vector3(1.28f, 1.0f, 0.42f), dark);
		for (int index = 0; index < 9; index++)
		{
			float angle = Mathf.Lerp(18.0f, 162.0f, index / 8.0f);
			float radians = Mathf.DegToRad(angle);
			Vector3 rockPosition = new(Mathf.Cos(radians) * 3.45f, 0.35f + Mathf.Sin(radians) * 3.0f, 0.0f);
			AddMesh(entrance, $"ArchRock{index}", new SphereMesh { Radius = 0.85f, Height = 1.45f }, rockPosition, new Vector3(index * 13.0f, index * 21.0f, 0.0f), new Vector3(1.25f, 1.0f, 0.85f), stone);
		}
		AddCaveEntranceCollision(entrance, new Vector3(-2.75f, 1.65f, 0.0f), new Vector3(1.6f, 3.3f, 2.0f));
		AddCaveEntranceCollision(entrance, new Vector3(2.75f, 1.65f, 0.0f), new Vector3(1.6f, 3.3f, 2.0f));

		var label = new Label3D
		{
			Name = "PortalLabel",
			Text = LocaleText.T("portal.enter_cave"),
			Position = new Vector3(0.0f, 5.45f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 24,
			PixelSize = 0.008f,
			OutlineSize = 6,
			Width = 320.0f,
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.84f, 0.76f, 1.0f),
		};
		entrance.AddChild(label);
	}

	private static void AddCaveEntranceCollision(Node3D entrance, Vector3 position, Vector3 size)
	{
		entrance.AddChild(new CollisionShape3D
		{
			Position = position,
			Shape = new BoxShape3D { Size = size },
		});
	}

	private bool TryHandleCaveTravel(string targetMapId)
	{
		if (targetMapId.StartsWith("cave_enter|"))
		{
			string originMapId = targetMapId[(targetMapId.IndexOf('|') + 1)..];
			if (!_wildMapRootsById.ContainsKey(originMapId))
			{
				return true;
			}
			_activeCaveOriginMapId = originMapId;
			_activeCaveDepth = 1;
			EnsureCaveDepth(originMapId, 1);
			TravelToCaveMap(MakeCaveMapId(originMapId, 1), new Vector3(0.0f, 0.2f, 27.0f), "system.cave.enter");
			return true;
		}

		if (targetMapId == "cave_deeper" && IsCaveMapId(_activeMapId))
		{
			if (!TryParseCaveMapId(_activeMapId, out string originMapId, out int depth))
			{
				return true;
			}
			_activeCaveOriginMapId = originMapId;
			_activeCaveDepth = depth + 1;
			EnsureCaveDepth(originMapId, _activeCaveDepth);
			TravelToCaveMap(MakeCaveMapId(originMapId, _activeCaveDepth), new Vector3(0.0f, 0.2f, 27.0f), "system.cave.deeper");
			return true;
		}

		if (targetMapId == "cave_back" && IsCaveMapId(_activeMapId))
		{
			if (!TryParseCaveMapId(_activeMapId, out string originMapId, out int depth))
			{
				return true;
			}
			if (depth > 1)
			{
				_activeCaveDepth = depth - 1;
				TravelToCaveMap(MakeCaveMapId(originMapId, depth - 1), new Vector3(0.0f, 0.2f, -27.0f), "system.cave.ascend");
			}
			else
			{
				Vector3 entrance = _caveEntrancePositionsByWildMap.TryGetValue(originMapId, out Vector3 savedEntrance)
					? savedEntrance
					: _wildSpawnPosition;
				SetMapVisibility(originMapId);
				_player.TeleportPartyTo(entrance + new Vector3(0.0f, 0.2f, 5.2f));
				_player.PostSystemMessage(LocaleText.T("system.cave.exit"), new Color(0.76f, 0.88f, 1.0f));
				UpdateActorMapActivity();
				UpdateActiveBossHud(false);
				_player.RefreshBossWorldStatus(true);
			}
			return true;
		}

		return false;
	}

	private void TravelToCaveMap(string mapId, Vector3 arrival, string messageKey)
	{
		SetMapVisibility(mapId);
		_player.TeleportPartyTo(arrival);
		_player.PostSystemMessage(LocaleText.F(messageKey, _activeCaveDepth), new Color(0.76f, 0.74f, 1.0f));
		UpdateActorMapActivity();
		UpdateActiveBossHud(false);
		_player.RefreshBossWorldStatus(true);
	}

	private void EnsureSavedCaveMap(string mapId)
	{
		if (!TryParseCaveMapId(mapId, out string originMapId, out int depth))
		{
			return;
		}
		_activeCaveOriginMapId = _wildMapRootsById.ContainsKey(originMapId) ? originMapId : "wild_forest";
		_activeCaveDepth = depth;
		for (int index = 1; index <= depth; index++)
		{
			EnsureCaveDepth(_activeCaveOriginMapId, index);
		}
	}

	private void EnsureCaveDepth(string originMapId, int depth)
	{
		string mapId = MakeCaveMapId(originMapId, depth);
		if (_caveMapRootsById.ContainsKey(mapId))
		{
			return;
		}

		Node3D previousMapRoot = _mapRoot;
		Node3D previousPropsRoot = _propsRoot;
		var caveRoot = new Node3D { Name = $"Cave_{originMapId}_{depth}" };
		_mapsRoot.AddChild(caveRoot);
		_caveMapRootsById[mapId] = caveRoot;
		_mapRoot = caveRoot;
		_propsRoot = new Node3D { Name = "CaveProps" };
		caveRoot.AddChild(_propsRoot);

		BuildCaveGeometry(depth);
		CreateCaveStonePortal(new Vector3(0.0f, 0.0f, 31.0f), "cave_back", depth == 1 ? "portal.leave_cave" : "portal.cave_back", false);
		CreateCaveStonePortal(new Vector3(0.0f, 0.0f, -31.0f), "cave_deeper", "portal.cave_deeper", true);
		SpawnCaveMonsters(mapId, depth);
		SetMapRootActive(caveRoot, false);
		_mapRoot = previousMapRoot;
		_propsRoot = previousPropsRoot;
	}

	private void BuildCaveGeometry(int depth)
	{
		float depthShade = Mathf.Clamp(depth * 0.008f, 0.0f, 0.08f);
		Material floor = MakeMaterial(new Color(0.105f - depthShade, 0.095f - depthShade * 0.6f, 0.125f));
		Material rock = MakeMaterial(new Color(0.125f, 0.12f, 0.155f));
		Material rockDark = MakeMaterial(new Color(0.065f, 0.06f, 0.085f));
		Material crystal = MakeEmissiveMaterial(new Color(0.30f, 0.24f, 0.72f, 0.88f), 2.5f, 0.12f);

		CreateStaticBox(_mapRoot, "CaveFloor", new Vector3(0.0f, -0.5f, 0.0f), new Vector3(CaveHalfWidth * 2.0f, 1.0f, CaveHalfLength * 2.0f), floor);
		CreateStaticBox(_mapRoot, "CaveWestWall", new Vector3(-CaveHalfWidth, 3.0f, 0.0f), new Vector3(2.0f, 7.0f, CaveHalfLength * 2.0f), rockDark);
		CreateStaticBox(_mapRoot, "CaveEastWall", new Vector3(CaveHalfWidth, 3.0f, 0.0f), new Vector3(2.0f, 7.0f, CaveHalfLength * 2.0f), rockDark);
		CreateStaticBox(_mapRoot, "CaveNorthWall", new Vector3(0.0f, 3.0f, -CaveHalfLength), new Vector3(CaveHalfWidth * 2.0f, 7.0f, 2.0f), rockDark);
		CreateStaticBox(_mapRoot, "CaveSouthWall", new Vector3(0.0f, 3.0f, CaveHalfLength), new Vector3(CaveHalfWidth * 2.0f, 7.0f, 2.0f), rockDark);

		var localRng = new RandomNumberGenerator { Seed = (ulong)(depth * 7919 + _activeCaveOriginMapId.GetHashCode()) };
		for (int index = 0; index < 34; index++)
		{
			float side = index % 2 == 0 ? -1.0f : 1.0f;
			Vector3 position = new(side * localRng.RandfRange(15.0f, 27.0f), 0.0f, localRng.RandfRange(-28.0f, 28.0f));
			float size = localRng.RandfRange(0.7f, 1.8f);
			AddMesh(_propsRoot, $"CaveRock{index}", new SphereMesh { Radius = size, Height = size * 1.5f }, position + Vector3.Up * size * 0.55f, new Vector3(index * 17.0f, index * 31.0f, 0.0f), new Vector3(1.4f, 1.0f, 1.1f), rock);
		}

		for (int index = 0; index < 10; index++)
		{
			float side = index % 2 == 0 ? -1.0f : 1.0f;
			Vector3 position = new(side * localRng.RandfRange(18.0f, 25.0f), localRng.RandfRange(0.35f, 1.1f), localRng.RandfRange(-27.0f, 27.0f));
			AddMesh(_propsRoot, $"CaveCrystal{index}", CylinderMeshFor(0.0f, 0.18f, localRng.RandfRange(0.8f, 1.8f)), position, new Vector3(0.0f, 0.0f, side * localRng.RandfRange(12.0f, 32.0f)), Vector3.One, crystal);
			if (index % 3 == 0)
			{
				_propsRoot.AddChild(new OmniLight3D { Position = position + Vector3.Up * 0.5f, LightColor = new Color(0.40f, 0.32f, 1.0f), LightEnergy = 1.6f, OmniRange = 8.0f, ShadowEnabled = false });
			}
		}

		for (int index = 0; index < 5; index++)
		{
			float z = -22.0f + index * 11.0f;
			AddMesh(_propsRoot, $"CaveArchLeft{index}", new SphereMesh { Radius = 1.8f, Height = 5.8f }, new Vector3(-11.5f, 3.1f, z), Vector3.Zero, new Vector3(1.25f, 1.0f, 0.85f), rockDark);
			AddMesh(_propsRoot, $"CaveArchRight{index}", new SphereMesh { Radius = 1.8f, Height = 5.8f }, new Vector3(11.5f, 3.1f, z), Vector3.Zero, new Vector3(1.25f, 1.0f, 0.85f), rockDark);
		}
	}

	private void CreateCaveStonePortal(Vector3 position, string targetMap, string labelKey, bool deeper)
	{
		Material stone = MakeMaterial(new Color(0.12f, 0.11f, 0.145f));
		Material glow = MakeEmissiveMaterial(deeper ? new Color(0.52f, 0.18f, 0.78f, 0.9f) : new Color(0.22f, 0.54f, 0.72f, 0.9f), 2.0f, 0.14f);
		var portal = new StaticBody3D { Name = deeper ? "CaveDeeperPassage" : "CaveReturnPassage", Position = position };
		portal.AddToGroup("map_portal");
		portal.SetMeta("target_map", targetMap);
		portal.SetMeta("label", labelKey);
		_propsRoot.AddChild(portal);
		AddMesh(portal, "DarkPassage", new SphereMesh { Radius = 2.5f, Height = 4.2f }, new Vector3(0.0f, 1.9f, 0.0f), Vector3.Zero, new Vector3(1.35f, 1.0f, 0.42f), MakeMaterial(new Color(0.008f, 0.006f, 0.014f)));
		for (int index = 0; index < 7; index++)
		{
			float angle = Mathf.Lerp(20.0f, 160.0f, index / 6.0f);
			float radians = Mathf.DegToRad(angle);
			AddMesh(portal, $"PassageRock{index}", new SphereMesh { Radius = 0.72f, Height = 1.2f }, new Vector3(Mathf.Cos(radians) * 2.7f, 0.35f + Mathf.Sin(radians) * 2.45f, 0.0f), Vector3.Zero, Vector3.One, stone);
		}
		AddMesh(portal, "PassageGlow", new SphereMesh { Radius = 0.52f, Height = 0.18f }, new Vector3(0.0f, 0.18f, -0.25f), Vector3.Zero, new Vector3(2.8f, 0.22f, 1.4f), glow);
		portal.AddChild(new CollisionShape3D { Position = new Vector3(0.0f, 0.45f, 0.0f), Shape = new BoxShape3D { Size = new Vector3(4.2f, 0.9f, 1.2f) } });
		portal.AddChild(new Label3D { Name = "PortalLabel", Text = LocaleText.T(labelKey), Position = new Vector3(0.0f, 4.8f, 0.0f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 23, PixelSize = 0.008f, OutlineSize = 6, Width = 340.0f, HorizontalAlignment = HorizontalAlignment.Center, Modulate = deeper ? new Color(0.86f, 0.62f, 1.0f) : new Color(0.62f, 0.88f, 1.0f) });
	}

	private void SpawnCaveMonsters(string mapId, int depth)
	{
		int count = Mathf.Clamp(8 + depth, 9, 22);
		_caveTargetCountsById[mapId] = count;
		for (int index = 0; index < count; index++)
		{
			SpawnCaveMonster(mapId, depth, index);
		}
	}

	private SimpleActor SpawnCaveMonster(string mapId, int depth, int spawnIndex)
	{
		bool spider = spawnIndex % 3 != 0;
		string nameKey = spider ? "name.monster.cave_spider" : "name.monster.cave_bat";
		string role = spider ? "DPS" : "Ranged";
		int level = Mathf.Clamp(2 + depth * 2 + spawnIndex % 3, 3, 60);
		SimpleActor actor = CreateActor(true, mapId, nameKey, role, level);
		int maxHealth = (spider ? 105 : 78) + level * (spider ? 19 : 15);
		int attack = (spider ? 10 : 8) + level * 3;
		int defense = (spider ? 7 : 4) + level * 2;
		actor.ConfigureStats(nameKey, level, maxHealth, attack, defense, level * 11, level * 4);
		actor.ConfigureGrowth(spider ? "ability.monster.poison" : "ability.monster.hide", Mathf.Clamp(1 + depth / 4, 1, 5));
		actor.AttackRange = spider ? 2.0f : 4.6f;
		actor.AttackCooldown = spider ? 1.45f : 1.75f;
		actor.MoveSpeed = spider ? 6.2f : 7.2f;
		actor.DetectionRadius = 15.0f;
		actor.ChaseRadius = 24.0f;
		var localRng = new RandomNumberGenerator { Seed = (ulong)(depth * 104729 + spawnIndex * 1543 + mapId.GetHashCode()) };
		Vector3 position = new(localRng.RandfRange(-12.0f, 12.0f), 0.0f, localRng.RandfRange(-23.0f, 23.0f));
		actor.Position = position;
		actor.HomePosition = position;
		_actorsRoot.AddChild(actor);
		actor.SetWorldMapActive(mapId == _activeMapId);
		return actor;
	}

	private bool TryBuildCaveMonsterVisual(SimpleActor actor)
	{
		if (actor.DisplayName == "name.monster.cave_bat")
		{
			BuildCaveBatVisual(actor);
			return true;
		}
		if (actor.DisplayName == "name.monster.cave_spider")
		{
			BuildCaveSpiderVisual(actor);
			return true;
		}
		return false;
	}

	private void BuildCaveBatVisual(SimpleActor actor)
	{
		Material body = MakeMaterial(new Color(0.12f, 0.075f, 0.17f));
		Material wing = MakeMaterial(new Color(0.23f, 0.10f, 0.30f));
		Material eye = MakeEmissiveMaterial(new Color(0.94f, 0.16f, 0.28f, 1.0f), 3.0f, 0.0f);
		var root = new Node3D { Name = "CaveBatVisual", Position = new Vector3(0.0f, 1.45f, 0.0f) };
		actor.AddChild(root);
		AddMesh(root, "BatBody", new CapsuleMesh { Radius = 0.23f, Height = 0.72f }, Vector3.Zero, new Vector3(90.0f, 0.0f, 0.0f), new Vector3(1.0f, 1.0f, 0.8f), body);
		AddMesh(root, "BatHead", new SphereMesh { Radius = 0.25f, Height = 0.42f }, new Vector3(0.0f, 0.06f, -0.38f), Vector3.Zero, Vector3.One, body);
		AddMesh(root, "LeftWing", new PrismMesh { Size = new Vector3(1.15f, 0.06f, 0.72f) }, new Vector3(-0.62f, 0.02f, 0.0f), new Vector3(4.0f, -8.0f, 10.0f), Vector3.One, wing);
		AddMesh(root, "RightWing", new PrismMesh { Size = new Vector3(1.15f, 0.06f, 0.72f) }, new Vector3(0.62f, 0.02f, 0.0f), new Vector3(-4.0f, 8.0f, -10.0f), Vector3.One, wing);
		AddMesh(root, "LeftEar", CylinderMeshFor(0.0f, 0.075f, 0.30f), new Vector3(-0.12f, 0.28f, -0.39f), new Vector3(0.0f, 0.0f, -14.0f), Vector3.One, wing);
		AddMesh(root, "RightEar", CylinderMeshFor(0.0f, 0.075f, 0.30f), new Vector3(0.12f, 0.28f, -0.39f), new Vector3(0.0f, 0.0f, 14.0f), Vector3.One, wing);
		AddMesh(root, "LeftEye", new SphereMesh { Radius = 0.035f, Height = 0.07f }, new Vector3(-0.09f, 0.10f, -0.60f), Vector3.Zero, Vector3.One, eye);
		AddMesh(root, "RightEye", new SphereMesh { Radius = 0.035f, Height = 0.07f }, new Vector3(0.09f, 0.10f, -0.60f), Vector3.Zero, Vector3.One, eye);
		actor.AddChild(new CaveCreatureAnimator { CreatureVisual = root, IsBat = true });
	}

	private void BuildCaveSpiderVisual(SimpleActor actor)
	{
		Material body = MakeMaterial(new Color(0.11f, 0.025f, 0.045f));
		Material leg = MakeMaterial(new Color(0.18f, 0.055f, 0.075f));
		Material eye = MakeEmissiveMaterial(new Color(1.0f, 0.12f, 0.08f, 1.0f), 3.2f, 0.0f);
		var root = new Node3D { Name = "CaveSpiderVisual", Position = new Vector3(0.0f, 0.42f, 0.0f) };
		actor.AddChild(root);
		AddMesh(root, "SpiderAbdomen", new SphereMesh { Radius = 0.48f, Height = 0.72f }, new Vector3(0.0f, 0.12f, 0.34f), Vector3.Zero, new Vector3(1.15f, 0.78f, 1.35f), body);
		AddMesh(root, "SpiderHead", new SphereMesh { Radius = 0.34f, Height = 0.48f }, new Vector3(0.0f, 0.10f, -0.43f), Vector3.Zero, new Vector3(1.0f, 0.78f, 1.0f), body);
		for (int sideIndex = 0; sideIndex < 2; sideIndex++)
		{
			float side = sideIndex == 0 ? -1.0f : 1.0f;
			for (int legIndex = 0; legIndex < 4; legIndex++)
			{
				float z = -0.38f + legIndex * 0.27f;
				float yaw = (legIndex - 1.5f) * 18.0f;
				AddMesh(root, $"Leg_{sideIndex}_{legIndex}", new CapsuleMesh { Radius = 0.055f, Height = 1.25f }, new Vector3(side * 0.58f, -0.06f, z), new Vector3(0.0f, yaw, side * 66.0f), new Vector3(1.0f, 1.0f, 0.8f), leg);
			}
		}
		for (int eyeIndex = 0; eyeIndex < 4; eyeIndex++)
		{
			float x = (eyeIndex - 1.5f) * 0.09f;
			AddMesh(root, $"SpiderEye{eyeIndex}", new SphereMesh { Radius = 0.032f, Height = 0.064f }, new Vector3(x, 0.20f + (eyeIndex % 2) * 0.07f, -0.72f), Vector3.Zero, Vector3.One, eye);
		}
		actor.AddChild(new CaveCreatureAnimator { CreatureVisual = root, IsBat = false });
	}

	private void UpdateCaveRespawns(float step)
	{
		_caveRespawnRemaining -= step;
		if (_caveRespawnRemaining > 0.0f)
		{
			return;
		}
		_caveRespawnRemaining = CaveRespawnInterval;
		foreach (KeyValuePair<string, int> entry in _caveTargetCountsById)
		{
			int living = CountLivingMonsters(entry.Key, false);
			if (living >= Mathf.Max(entry.Value / 2, 3) || !TryParseCaveMapId(entry.Key, out _, out int depth))
			{
				continue;
			}
			int amount = Mathf.Min(entry.Value - living, 4);
			for (int index = 0; index < amount; index++)
			{
				SpawnCaveMonster(entry.Key, depth, living + index + _rng.RandiRange(0, 2000));
			}
		}
	}
}
