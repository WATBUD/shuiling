using Godot;
using System.Collections.Generic;

// 主城中央水池復活：站在水池旁按 E，把已死亡（且已回收、非待回收）的夥伴從天而降丟進水池，
// 播放類似回城卷的光柱／光環特效，特效結束後夥伴復活。此復活免費，水池的儀式就是它的代價。
public partial class PlayerController
{
	private bool _fountainReviving;
	private static readonly Color FountainReviveColor = new(0.52f, 0.86f, 1.0f);

	public bool TryFountainRevive(Vector3 fountainPos)
	{
		if (_fountainReviving)
		{
			return false;
		}

		var toRevive = new List<SimpleActor>();
		int awaitingRecovery = 0;
		foreach (SimpleActor actor in _capturedCollection)
		{
			if (!IsInstanceValid(actor) || !actor.IsDefeated)
			{
				continue;
			}

			if (actor.IsAwaitingRecovery)
			{
				awaitingRecovery++;
			}
			else
			{
				toRevive.Add(actor);
			}
		}

		if (toRevive.Count == 0)
		{
			PostSystemMessage(
				LocaleText.T(awaitingRecovery > 0 ? "system.revive.retrieve_first" : "system.revive.no_fallen"),
				new Color(0.78f, 0.88f, 1.0f),
				GameMessageChannel.Party);
			return false;
		}

		_fountainReviving = true;
		PlayFountainReviveSequence(fountainPos, toRevive);
		return true;
	}

	private void PlayFountainReviveSequence(Vector3 fountainPos, List<SimpleActor> toRevive)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		var vfx = new Node3D { Name = "FountainReviveVfx", Position = fountainPos };
		parent.AddChild(vfx);

		// 光柱
		var column = new MeshInstance3D
		{
			Name = "ReviveColumn",
			Mesh = new CylinderMesh { TopRadius = 1.15f, BottomRadius = 1.15f, Height = 9.0f, RadialSegments = 16 },
			MaterialOverride = MakeCastGlow(FountainReviveColor, 0.20f, 2.2f),
			Position = new Vector3(0.0f, 4.5f, 0.0f),
		};
		vfx.AddChild(column);

		// 地面光環（平躺）
		var ring = new MeshInstance3D
		{
			Name = "ReviveRing",
			Mesh = new TorusMesh { InnerRadius = 1.4f, OuterRadius = 2.1f },
			MaterialOverride = MakeCastGlow(FountainReviveColor, 0.42f, 2.6f),
			Position = new Vector3(0.0f, 0.35f, 0.0f),
			RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
		};
		vfx.AddChild(ring);

		var light = new OmniLight3D
		{
			Name = "ReviveLight",
			LightColor = FountainReviveColor,
			OmniRange = 10.0f,
			LightEnergy = 3.2f,
			Position = new Vector3(0.0f, 1.6f, 0.0f),
		};
		vfx.AddChild(light);

		// 光環擴張淡出
		Tween ringTween = CreateTween();
		ringTween.TweenProperty(ring, "scale", new Vector3(1.6f, 1.6f, 1.6f), 1.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

		// 每隻夥伴一顆從天而降、落入水池的光球（錯開時間）。
		float lastLandTime = 0.0f;
		for (int index = 0; index < toRevive.Count; index++)
		{
			float angle = index * (Mathf.Pi * 2.0f / Mathf.Max(toRevive.Count, 1));
			float radius = toRevive.Count > 1 ? 0.9f : 0.0f;
			var landPos = new Vector3(Mathf.Cos(angle) * radius, 0.7f, Mathf.Sin(angle) * radius);
			var orb = new MeshInstance3D
			{
				Name = $"ReviveOrb{index}",
				Mesh = new SphereMesh { Radius = 0.42f, Height = 0.84f },
				MaterialOverride = MakeCastGlow(FountainReviveColor, 0.9f, 3.2f),
				Position = new Vector3(landPos.X, 9.5f + index * 1.1f, landPos.Z),
			};
			vfx.AddChild(orb);

			float delay = index * 0.28f;
			float fallTime = 0.85f;
			lastLandTime = Mathf.Max(lastLandTime, delay + fallTime);
			Tween fall = CreateTween();
			fall.TweenInterval(delay);
			fall.TweenProperty(orb, "position", landPos, fallTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			// 落水後縮小消失（水花感）。
			fall.TweenProperty(orb, "scale", new Vector3(0.1f, 0.1f, 0.1f), 0.25f);
		}

		// 特效總長：最後一顆落水 + 一點餘韻。
		float totalDuration = Mathf.Max(lastLandTime + 0.5f, 2.0f);
		SceneTreeTimer timer = GetTree().CreateTimer(totalDuration);
		timer.Timeout += () =>
		{
			FinishFountainRevive(toRevive);
			if (IsInstanceValid(vfx))
			{
				vfx.QueueFree();
			}
		};
	}

	private void FinishFountainRevive(List<SimpleActor> toRevive)
	{
		_fountainReviving = false;
		int revived = 0;
		foreach (SimpleActor actor in toRevive)
		{
			if (IsInstanceValid(actor) && actor.ReviveFromCaretaker(this))
			{
				revived++;
			}
		}

		if (revived <= 0)
		{
			return;
		}

		ReassignFollowSlots();
		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		_inventoryPanel.RefreshAll();
		PostSystemMessage(LocaleText.F("system.revive.fountain_done", revived), new Color(0.54f, 1.0f, 0.70f), GameMessageChannel.Party);
	}
}
