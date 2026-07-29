using Godot;
using System.Collections.Generic;

public partial class PlayerController
{
	private float _meleeCooldownRemaining;
	private float _playerAttackAnimRemaining;
	private const float PlayerAttackAnimationSeconds = 0.45f;
	// Keep the player's whirlwind identical to companions: three full turns at
	// twice the previous angular speed (2 turns / 0.55 s -> 3 turns / 0.4125 s).
	private const float PlayerWhirlwindSpinSeconds = 0.4125f;
	private const float PlayerWhirlwindSpinRadians = Mathf.Tau * 3.0f;

	// Death: the player is downed on the spot while their pets grieve, and stays
	// down (with a return prompt) until they choose to go back to town.
	private bool _isDead;
	private CanvasLayer? _deathPromptLayer;
	public bool IsPlayerDead => _isDead;

	// Out-of-combat regeneration: after this long without taking damage, the
	// player recovers a fraction of max HP per second. This is the main healing
	// means — survive an encounter, back off, and you heal back up.
	private const ulong RegenCombatDelayMsec = 4000;
	private const float RegenFractionPerSecond = 0.06f;
	private float _regenAccumulator;

	// Player melee: swing at hostile monsters in a frontal arc within reach. The
	// clicked/focused target is always included (even slightly off-arc) so a click
	// reliably hits what you aimed at.
	private void PerformMeleeAttack()
	{
		if (_meleeCooldownRemaining > 0.0f || CurrentHealth <= 0)
		{
			return;
		}

		BuildStats stats = CurrentBuildStats;
		_meleeCooldownRemaining = AttackCooldown * stats.AttackCooldownMultiplier;
		PlayPlayerAttackAnimation(BuildLoadout.GetSkillGemId(0) == "gem.skill.whirlwind");

		Vector3 origin = GlobalPosition;
		Vector3 forward = -GlobalTransform.Basis.Z;
		forward.Y = 0.0f;
		forward = forward.LengthSquared() < 0.0001f ? Vector3.Forward : forward.Normalized();

		float reach = AttackRange + stats.AttackRangeBonus + 0.8f;

		if (BuildCatalog.HasMainAttackCore(BuildLoadout) && FocusedTarget is SimpleActor coreTarget
			&& IsInstanceValid(coreTarget) && !coreTarget.IsDefeated)
		{
			LaunchPlayerCoreAttack(coreTarget, stats);
			MarkRecentCombat();
			return;
		}

		bool hitAny = false;
		SimpleActor? focused = FocusedTarget;
		foreach (SimpleActor monster in SimpleActor.ActiveActors)
		{
			if (!IsInstanceValid(monster) || !monster.IsHostileToPlayer)
			{
				continue;
			}

			Vector3 toMonster = monster.GlobalPosition - origin;
			toMonster.Y = 0.0f;
			float distance = toMonster.Length();
			if (distance > reach)
			{
				continue;
			}

			// Frontal arc (~110°) for cleave, but the focused target always connects.
			bool inArc = distance <= 0.15f || forward.Dot(toMonster / distance) >= 0.34f;
			if (!inArc && monster != focused)
			{
				continue;
			}

			// null attacker: for a client this forwards the hit to the host, which
			// owns the monster's HP (same path companions/net use).
			monster.ReceiveDamage(stats.Attack, null, this);
			SpawnImpactEffect(monster.GlobalPosition + Vector3.Up * 0.9f);
			hitAny = true;
		}

		if (hitAny)
		{
			MarkRecentCombat();
		}
	}

	private void LaunchPlayerCoreAttack(SimpleActor target, BuildStats stats)
	{
		Vector3 forward = target.GlobalPosition - GlobalPosition;
		forward.Y = 0.0f;
		forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : -GlobalTransform.Basis.Z;
		string skillId = BuildLoadout.GetSkillGemId(0);
		int coreDamage = GetPlayerCoreDamage(stats, skillId);
		bool isMelee = !BuildCatalog.HasRangedActiveSkill(BuildLoadout);
		Node parent = GetTree().CurrentScene ?? GetParent();
		SkillAttackVfx.SpawnCast(parent, GlobalPosition + Vector3.Up * 1.1f + forward * 0.35f, forward,
			skillId, stats.DamageElementId, stats.AttackColor, stats.Behavior, stats.LifeStealPercent > 0.0f);

		if (skillId is "gem.skill.lightning" or "gem.skill.meteor" or "gem.skill.laser")
		{
			ResolvePlayerTargetedCoreStrike(target, stats, parent, skillId);
			return;
		}

		int count = skillId == "gem.skill.whirlwind" ? 3 : 1 + Mathf.Max(stats.Behavior.ExtraProjectiles, 0);
		for (int index = 0; index < count; index++)
		{
			float angle = (index - (count - 1) / 2.0f) * Mathf.DegToRad(14.0f);
			Vector3 direction = forward.Rotated(Vector3.Up, angle);
			var projectile = new CombatProjectile
			{
				PlayerAttacker = this,
				Damage = coreDamage,
				EffectColor = stats.AttackColor,
				IsMelee = isMelee,
				VisualSkillId = skillId,
				ElementId = stats.DamageElementId,
				HasLifeSteal = stats.LifeStealPercent > 0.0f,
				Speed = (isMelee ? 26.0f : 18.0f) * stats.ProjectileSpeedMultiplier,
				MaxRange = isMelee ? 3.0f : Mathf.Max(AttackRange + stats.AttackRangeBonus, 9.0f) * 1.6f,
				HitRadius = isMelee ? 1.35f : 1.0f,
				InitialTarget = Mathf.Abs(angle) < 0.001f ? target : null,
				LaunchDirection = direction,
				SpawnOrigin = GlobalPosition + Vector3.Up * 1.2f + direction * 0.5f,
				Behavior = stats.Behavior.Clone(),
			};
			parent.AddChild(projectile);
		}
	}

	private void ResolvePlayerTargetedCoreStrike(SimpleActor target, BuildStats stats, Node parent, string skillId)
	{
		if (!IsInstanceValid(target) || target.IsDefeated)
		{
			return;
		}

		Vector3 targetPosition = target.GlobalPosition + Vector3.Up * 0.08f;
		if (skillId == "gem.skill.laser")
		{
			Vector3 beamOrigin = GlobalPosition + Vector3.Up * 1.1f;
			SkillAttackVfx.SpawnSpecial(
				parent,
				SkillAttackVfx.ChainEvent,
				beamOrigin,
				targetPosition - beamOrigin,
				skillId,
				stats.DamageElementId,
				stats.AttackColor,
				0.9f,
				new ProjectileBehaviorProfile(),
				stats.LifeStealPercent > 0.0f);
		}

		float radius = skillId == "gem.skill.meteor" ? 1.65f : 1.15f;
		SkillAttackVfx.SpawnImpact(
			parent,
			targetPosition,
			Vector3.Down,
			skillId,
			stats.DamageElementId,
			stats.AttackColor,
			radius,
			new ProjectileBehaviorProfile(),
			stats.LifeStealPercent > 0.0f);
		ResolvePlayerProjectileHit(target, GetPlayerCoreDamage(stats, skillId));
	}

	private static int GetPlayerCoreDamage(BuildStats stats, string skillId)
	{
		float multiplier = BuildCatalog.GetSkillGem(skillId).IsSpell ? stats.SpellDamageMultiplier : 1.0f;
		return Mathf.Max(Mathf.RoundToInt(stats.Attack * multiplier), 1);
	}

	public void FindPlayerProjectileTargets(Vector3 center, float radius, ICollection<SimpleActor> exclude, List<SimpleActor> results)
	{
		results.Clear();
		center.Y = 0.0f;
		float radiusSquared = radius * radius;
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!IsInstanceValid(actor) || !actor.IsHostileToPlayer
				|| actor.IsDefeated || (exclude != null && exclude.Contains(actor)))
			{
				continue;
			}
			Vector3 position = actor.GlobalPosition;
			position.Y = 0.0f;
			if (center.DistanceSquaredTo(position) <= radiusSquared)
			{
				results.Add(actor);
			}
		}
	}

	public int ResolvePlayerProjectileHit(SimpleActor target, int baseDamage)
	{
		if (!IsInstanceValid(target) || target.IsDefeated)
		{
			return 0;
		}
		BuildStats stats = CurrentBuildStats;
		int damage = Mathf.Max(baseDamage, 1);
		if (GD.Randf() < stats.CritChance)
		{
			damage = Mathf.RoundToInt(damage * 1.55f);
		}
		int dealt = target.ReceiveDamage(damage, null, this);
		if (dealt > 0 && GD.Randf() < stats.ControlChance)
		{
			target.ApplyElementStatusFromPlayer(stats.DamageElementId);
		}
		if (dealt > 0 && stats.LifeStealPercent > 0.0f)
		{
			ReceiveHealing(Mathf.RoundToInt(dealt * stats.LifeStealPercent));
		}
		MarkRecentCombat();
		return dealt;
	}

	// Click-to-attack pathing: when a monster is focused and out of melee range,
	// report the planar direction to walk toward it. Returns false once in range
	// (auto-attack takes over) or when there's no valid focused target.
	private bool TryGetAutoApproachDirection(out Vector3 direction)
	{
		direction = Vector3.Zero;
		if (_isDead)
		{
			return false;
		}

		SimpleActor? focused = FocusedTarget;
		if (focused == null || !IsInstanceValid(focused) || focused.IsDefeated)
		{
			return false;
		}

		Vector3 toTarget = focused.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		float effectiveRange = BuildCatalog.HasRangedActiveSkill(BuildLoadout)
			? Mathf.Max(AttackRange + CurrentBuildStats.AttackRangeBonus, 9.0f)
			: AttackRange + CurrentBuildStats.AttackRangeBonus + 0.8f;
		if (toTarget.Length() <= effectiveRange * 0.9f)
		{
			return false; // already in range
		}

		direction = toTarget.Normalized();
		return true;
	}

	// Auto-attack: once you've clicked (focused) a monster, keep swinging at it on
	// cooldown while it stays in reach — no need to keep clicking.
	private void UpdateAutoAttack(float step)
	{
		if (_meleeCooldownRemaining > 0.0f || CurrentHealth <= 0)
		{
			return;
		}

		SimpleActor? focused = FocusedTarget;
		if (focused == null || !IsInstanceValid(focused) || focused.IsDefeated)
		{
			return;
		}

		Vector3 toTarget = focused.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		float effectiveRange = BuildCatalog.HasRangedActiveSkill(BuildLoadout)
			? Mathf.Max(AttackRange + CurrentBuildStats.AttackRangeBonus, 9.0f)
			: AttackRange + CurrentBuildStats.AttackRangeBonus + 0.8f;
		if (toTarget.Length() > effectiveRange)
		{
			return; // move closer to keep auto-attacking
		}

		PerformMeleeAttack();
	}

	private void UpdateMeleeCooldown(float step)
	{
		if (_meleeCooldownRemaining > 0.0f)
		{
			_meleeCooldownRemaining = Mathf.Max(0.0f, _meleeCooldownRemaining - step);
		}
	}

	private void UpdateHealthRegen(float step)
	{
		int effectiveMaxHealth = EffectiveMaxHealth;
		if (CurrentHealth <= 0 || CurrentHealth >= effectiveMaxHealth)
		{
			_regenAccumulator = 0.0f;
			return;
		}

		if (Time.GetTicksMsec() - _lastCombatMsec < RegenCombatDelayMsec)
		{
			return;
		}

		_regenAccumulator += effectiveMaxHealth * RegenFractionPerSecond * step;
		if (_regenAccumulator >= 1.0f)
		{
			int amount = Mathf.FloorToInt(_regenAccumulator);
			_regenAccumulator -= amount;
			CurrentHealth = Mathf.Min(CurrentHealth + amount, effectiveMaxHealth);
		}
	}

	// A short impact burst where a melee hit lands: a bright spark that pops and
	// fades on the struck monster (replaces the old swing halo).
	private void SpawnImpactEffect(Vector3 point)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		SkillAttackVfx.SpawnSpecial(
			parent,
			SkillAttackVfx.ImpactEvent,
			point,
			GetPlayerProjectileDirection(),
			"gem.skill.whirlwind",
			"physical",
			new Color(1.0f, 0.78f, 0.28f, 0.94f),
			0.62f,
			new ProjectileBehaviorProfile());
	}

	private void CreateCaptureRhythmPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "CaptureRhythmLayer",
			Layer = 90,
		};
		AddChild(layer);

		_captureRhythmPanel = new CaptureRhythmPanel();
		_captureRhythmPanel.ChallengeSucceeded += OnCaptureChallengeSucceeded;
		_captureRhythmPanel.ChallengeFailed += OnCaptureChallengeFailed;
		layer.AddChild(_captureRhythmPanel);
	}

	// Net hit dispatch: ready monsters open the capture challenge; healthy ones
	// only take stagger (weaken/combo first). Returns true if the net is consumed.
	public bool HandleCaptureNetHit(SimpleActor actor)
	{
		if (!IsInstanceValid(actor))
		{
			return false;
		}

		if (actor.IsNetworkPuppet)
		{
			PostSystemMessage(LocaleText.T("system.net.capture_blocked"), new Color(1.0f, 0.72f, 0.5f));
			return true;
		}

		if (!actor.CanBeCaptured)
		{
			return false; // not a capture target — let the net keep flying
		}

		// The orb landed on a capture target: protect it from dying while the
		// weaken/capture sequence plays out (refreshed by each subsequent hit).
		actor.GrantCaptureProtection(CaptureProtectionSeconds);

		if (actor.CaptureReady)
		{
			return BeginCaptureChallenge(actor);
		}

		// Not weakened enough yet: chip its guard and hint the player.
		actor.AddCaptureStagger(actor.MaxStagger * 0.25f);
		PostSystemMessage(LocaleText.F("system.capture.not_ready", actor.LocalizedDisplayName), new Color(1.0f, 0.82f, 0.5f), GameMessageChannel.Party);
		return true;
	}

	public bool BeginCaptureChallenge(SimpleActor actor)
	{
		if (IsInstanceValid(actor) && actor.IsNetworkPuppet)
		{
			// Multiplayer phase 1: host-owned monsters can't be captured yet.
			PostSystemMessage(LocaleText.T("system.net.capture_blocked"), new Color(1.0f, 0.72f, 0.5f));
			return false;
		}

		if (!IsInstanceValid(actor)
			|| !actor.CanBeCaptured
			|| !actor.CaptureReady
			|| _capturedCollection.Contains(actor)
			|| _captureRhythmPanel == null)
		{
			return false;
		}

		bool began = _captureRhythmPanel.Begin(actor);
		if (began)
		{
			// Locked for the whole challenge (which pauses the world), so the target
			// can't be killed by anyone before the attempt resolves.
			actor.SetCaptureLocked(true);
		}
		return began;
	}

	private void OnCaptureChallengeSucceeded(SimpleActor actor)
	{
		if (IsInstanceValid(actor))
		{
			actor.EndCaptureProtection();
		}
		if (!CaptureActor(actor))
		{
			PostSystemMessage(LocaleText.T("system.capture.target_lost"), new Color(1.0f, 0.58f, 0.42f), GameMessageChannel.Party);
		}
	}

	private void OnCaptureChallengeFailed(SimpleActor actor)
	{
		if (IsInstanceValid(actor))
		{
			actor.EndCaptureProtection();
		}
		PostSystemMessage(
			LocaleText.F("system.capture.rhythm_failed", actor.LocalizedDisplayName),
			new Color(1.0f, 0.58f, 0.42f),
			GameMessageChannel.Party);
	}

	// charge in 0..1 (from how long the throw was held) scales speed + range.
	private void ThrowCaptureNet(float charge)
	{
		if (_captureCooldownRemaining > 0.0f || _captureNetCharges <= 0)
		{
			return;
		}

		charge = Mathf.Clamp(charge, 0.0f, 1.0f);
		_captureCooldownRemaining = CaptureCooldown;
		_captureNetCharges = Mathf.Max(_captureNetCharges - 1, 0);
		Vector3 launch = ComputeNetLaunchVelocity(charge);
		var net = new CaptureNet
		{
			OwnerPlayer = this,
			LaunchVelocity = launch,
			FallGravity = NetGravity,
		};

		Node projectileParent = GetTree().CurrentScene ?? GetParent();
		projectileParent.AddChild(net);
		net.GlobalPosition = NetLaunchOrigin;
		UpdateCaptureAmmoHud();
	}

	private Vector3 NetLaunchOrigin => GlobalPosition + new Vector3(0.0f, 1.4f, 0.0f);

	// Parabolic launch velocity. In God View the arc lands exactly on the cursor's
	// ground point (distance follows the mouse); otherwise it uses facing + charge.
	private Vector3 ComputeNetLaunchVelocity(float charge)
	{
		Vector3 start = NetLaunchOrigin;
		if (_cameraMode == CameraViewMode.GodView
			&& TryGetMouseGroundPoint(GetViewport().GetMousePosition(), out Vector3 target))
		{
			// Solve v so the projectile reaches `target` in `flight` seconds under
			// gravity: v = (target - start)/flight + 0.5*g*flight (upward).
			float horizontalDist = new Vector2(target.X - start.X, target.Z - start.Z).Length();
			float flight = Mathf.Clamp(horizontalDist * 0.09f, 0.45f, 1.6f);
			Vector3 velocity = (target - start) / flight;
			velocity.Y += 0.5f * NetGravity * flight;
			return velocity;
		}

		Vector3 direction = GetCaptureThrowDirection();
		direction.Y = 0.0f;
		direction = direction.LengthSquared() > 0.001f ? direction.Normalized() : -GlobalTransform.Basis.Z;
		float horizontalSpeed = 12.0f + charge * 16.0f;
		float verticalSpeed = 6.0f + charge * 2.5f;
		return direction * horizontalSpeed + Vector3.Up * verticalSpeed;
	}

	// --- aimed / charged throw (hold R to aim, release to throw) ---------------

	private void BeginAimingNet()
	{
		if (_captureCooldownRemaining > 0.0f || _captureNetCharges <= 0)
		{
			return;
		}

		_isAimingNet = true;
		_netAimCharge = 0.0f;
		EnsureNetAimIndicator();
		_netAimIndicator.Visible = true;
		UpdateNetAimIndicator();
	}

	private void ReleaseCaptureNet()
	{
		if (!_isAimingNet)
		{
			return;
		}

		_isAimingNet = false;
		if (_netAimIndicator != null && IsInstanceValid(_netAimIndicator))
		{
			_netAimIndicator.Visible = false;
		}

		ThrowCaptureNet(_netAimCharge);
	}

	private void UpdateNetAiming(float step)
	{
		if (!_isAimingNet)
		{
			return;
		}

		_netAimCharge = Mathf.Min(1.0f, _netAimCharge + step / NetChargeTime);
		UpdateNetAimIndicator();
	}

	private const int NetAimDotCount = 20;

	private void EnsureNetAimIndicator()
	{
		if (_netAimIndicator != null && IsInstanceValid(_netAimIndicator))
		{
			return;
		}

		_netAimIndicator = new Node3D { Name = "NetAimIndicator", Visible = false };
		(GetTree().CurrentScene ?? GetParent()).AddChild(_netAimIndicator);

		_netAimDotMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.6f, 0.92f, 1.0f, 0.85f),
			EmissionEnabled = true,
			Emission = new Color(0.5f, 0.85f, 1.0f),
			EmissionEnergyMultiplier = 2.4f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};

		_netAimDots.Clear();
		for (int i = 0; i < NetAimDotCount; i++)
		{
			// Nearer dots are a touch larger so the arc reads with depth.
			float radius = Mathf.Lerp(0.11f, 0.05f, i / (float)NetAimDotCount);
			var dot = new MeshInstance3D
			{
				Name = $"AimDot{i}",
				Mesh = new SphereMesh { Radius = radius, Height = radius * 2.0f, RadialSegments = 8, Rings = 4 },
			};
			dot.SetSurfaceOverrideMaterial(0, _netAimDotMaterial);
			_netAimIndicator.AddChild(dot);
			_netAimDots.Add(dot);
		}
	}

	private void UpdateNetAimIndicator()
	{
		if (_netAimIndicator == null || !IsInstanceValid(_netAimIndicator) || _netAimDots.Count == 0)
		{
			return;
		}

		// Charge tints the whole arc blue -> gold.
		_netAimDotMaterial.AlbedoColor = new Color(0.6f, 0.92f, 1.0f, 0.85f).Lerp(new Color(1.0f, 0.86f, 0.35f, 0.95f), _netAimCharge);
		_netAimDotMaterial.Emission = new Color(0.5f, 0.85f, 1.0f).Lerp(new Color(1.0f, 0.8f, 0.3f), _netAimCharge);

		Vector3 start = NetLaunchOrigin;
		Vector3 velocity = ComputeNetLaunchVelocity(_netAimCharge);
		const float dt = 0.07f;
		bool landed = false;
		for (int i = 0; i < _netAimDots.Count; i++)
		{
			MeshInstance3D dot = _netAimDots[i];
			if (landed)
			{
				dot.Visible = false;
				continue;
			}

			float t = i * dt;
			Vector3 pos = start + velocity * t + 0.5f * Vector3.Down * NetGravity * t * t;
			if (i > 2 && pos.Y < start.Y - 1.4f)
			{
				landed = true;
				dot.Visible = false;
				continue;
			}

			dot.Visible = true;
			dot.GlobalPosition = pos;
		}
	}

	public bool CaptureActor(SimpleActor actor)
	{
		if (actor.ActorKind == "npc")
		{
			PostSystemMessage(LocaleText.F("system.npc.requires_task", actor.LocalizedDisplayName), new Color(0.82f, 0.88f, 1.0f));
			return false;
		}

		if (!actor.CanBeCaptured || _capturedCollection.Contains(actor))
		{
			return false;
		}

		// 收藏上限 20（含已死亡的夥伴）；額滿就無法再撿取。
		if (_capturedCollection.Count >= ActivePartyLimit)
		{
			PostSystemMessage(LocaleText.F("system.capture.collection_full", ActivePartyLimit), new Color(1.0f, 0.72f, 0.5f), GameMessageChannel.Party);
			return false;
		}

		_capturedCollection.Add(actor);
		actor.Capture(this);
		PostSystemMessage(LocaleText.F("system.capture.success", actor.LocalizedDisplayName), new Color(0.62f, 0.90f, 1.0f), GameMessageChannel.Party);

		if (_activeParty.Count < ActivePartyLimit)
		{
			DeployCompanion(actor, false);
		}
		else
		{
			actor.StoreInCollection();
		}

		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		return true;
	}

	public int ReceiveDamage(int rawDamage, SimpleActor? attacker = null)
	{
		int mitigatedDamage = Mathf.Max(rawDamage - Mathf.RoundToInt(EffectiveDefense * 0.35f), 1);
		CurrentHealth = Mathf.Max(CurrentHealth - mitigatedDamage, 0);
		MarkRecentCombat();
		Color hitColor = attacker?.AttackFxColor ?? new Color(1.0f, 0.18f, 0.14f, 0.92f);
		SpawnWorldCombatEffect($"-{mitigatedDamage}", hitColor, GlobalPosition + new Vector3(0.0f, 1.45f, 0.0f), 0.78f, 0.88f);
		SpawnIncomingAttackCue(attacker, hitColor);
		TriggerDamageFlash();
		if (attacker?.IsBoss == true)
		{
			NotifyBossCombat(attacker);
		}

		if (CurrentHealth <= 0)
		{
			HandlePlayerDeath();
		}

		return mitigatedDamage;
	}

	public int ReceiveHealing(int rawHealing)
	{
		int missingHealth = Mathf.Max(EffectiveMaxHealth - CurrentHealth, 0);
		int healing = Mathf.Min(Mathf.Max(rawHealing, 0), missingHealth);
		if (healing <= 0)
		{
			return 0;
		}

		CurrentHealth += healing;
		SpawnFloatingEffect($"+{healing}", new Color(0.36f, 1.0f, 0.54f, 0.92f), 0.55f, 0.48f);
		return healing;
	}

	public void GrantCombatExperience(int amount, int sourceLevel = 1)
	{
		int amountBase = Mathf.Max(amount, 0);
		if (amountBase <= 0)
		{
			return;
		}

		// The player and each companion scale the reward by their OWN level gap to
		// the defeated monster — over-leveled earners gain almost nothing.
		int playerXp = Level >= MaxPlayerLevel ? 0 : ExperienceTable.ScaleReward(amountBase, Level, sourceLevel);
		int playerLevelBefore = Level;
		Experience += playerXp;
		while (Level < MaxPlayerLevel && Experience >= ExperienceToNextLevel)
		{
			Experience -= ExperienceToNextLevel;
			Level++;
			UnspentAttributePoints += AttributePointsPerLevel;
		}
		if (Level >= MaxPlayerLevel)
		{
			Experience = 0;
		}

		if (Level > playerLevelBefore)
		{
			MarkPlayerBuildStatsDirty();
			ShowPlayerLevelUpFeedback();
		}

		foreach (SimpleActor actor in _activeParty)
		{
			if (IsInstanceValid(actor) && actor.IsInActiveParty)
			{
				actor.GrantTraining(ExperienceTable.ScaleReward(amountBase, actor.Level, sourceLevel));
			}
		}

		PostSystemMessage(LocaleText.F("system.exp.party_gain", playerXp), new Color(0.86f, 0.78f, 1.0f), GameMessageChannel.Combat);
		_partyPanel.RefreshParty();
	}

	// Real death (no more invincible knockdown): the player is downed on the spot
	// and their deployed pets stand guard and grieve (invincible, can't fight). The
	// player stays down — with a "you died / return?" prompt — until THEY choose to
	// return to the city. No auto-respawn.
	private void HandlePlayerDeath()
	{
		if (_isDead)
		{
			return;
		}

		_isDead = true;
		CurrentHealth = 0;
		Velocity = Vector3.Zero;
		PostSystemMessage(
			LocaleText.T("system.player.defeated"),
			new Color(1.0f, 0.42f, 0.36f),
			GameMessageChannel.Combat);

		foreach (SimpleActor actor in _activeParty)
		{
			if (IsInstanceValid(actor))
			{
				actor.EnterMourning();
			}
		}

		ShowDeathPrompt();
	}

	private void ShowDeathPrompt()
	{
		EnsureDeathPrompt();
		if (_deathPromptLayer != null)
		{
			_deathPromptLayer.Visible = true;
		}
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void EnsureDeathPrompt()
	{
		if (_deathPromptLayer != null && IsInstanceValid(_deathPromptLayer))
		{
			return;
		}

		_deathPromptLayer = new CanvasLayer { Name = "DeathPromptLayer", Layer = 95 };
		AddChild(_deathPromptLayer);

		var center = new CenterContainer { AnchorRight = 1.0f, AnchorBottom = 1.0f };
		_deathPromptLayer.AddChild(center);

		var panel = new PanelContainer();
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.02f, 0.03f, 0.94f),
			BorderColor = new Color(0.85f, 0.28f, 0.26f, 0.95f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(26.0f);
		panel.AddThemeStyleboxOverride("panel", style);
		center.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 16);
		panel.AddChild(vbox);

		var title = new Label
		{
			Text = LocaleText.T("system.player.defeated"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.AddThemeFontSizeOverride("font_size", 26);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.5f, 0.44f));
		vbox.AddChild(title);

		var prompt = new Label
		{
			Text = LocaleText.T("system.player.death_prompt"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		prompt.AddThemeFontSizeOverride("font_size", 16);
		prompt.AddThemeColorOverride("font_color", new Color(0.92f, 0.9f, 0.9f));
		vbox.AddChild(prompt);

		var returnButton = new Button
		{
			Text = LocaleText.T("button.return_town"),
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		returnButton.AddThemeFontSizeOverride("font_size", 18);
		returnButton.Pressed += ConfirmReturnToTown;
		vbox.AddChild(returnButton);
	}

	// The player chose to return: revive, send them to the city, and let the pets
	// stop grieving and rejoin.
	private void ConfirmReturnToTown()
	{
		if (!_isDead)
		{
			return;
		}

		_isDead = false;
		if (_deathPromptLayer != null && IsInstanceValid(_deathPromptLayer))
		{
			_deathPromptLayer.Visible = false;
		}
		Input.MouseMode = Input.MouseModeEnum.Captured;

		CurrentHealth = EffectiveMaxHealth;
		_lastCombatMsec = 0;

		foreach (SimpleActor actor in _activeParty)
		{
			if (IsInstanceValid(actor))
			{
				actor.ExitMourning();
			}
		}

		PostSystemMessage(LocaleText.T("system.player.revived"), new Color(0.7f, 1.0f, 0.8f));
		if (GetParent() is World world && world.ActiveMapId != "city")
		{
			world.RequestMapTravel("city");
		}
		else
		{
			TeleportToSafePosition();
		}
	}

	private void SpawnFloatingEffect(string text, Color color, float lifetime, float radius)
	{
		SpawnWorldCombatEffect(text, color, GlobalPosition + new Vector3(0.0f, 1.15f, 0.0f), lifetime, radius);
	}

	private void SpawnIncomingAttackCue(SimpleActor? attacker, Color color)
	{
		if (attacker == null || !IsInstanceValid(attacker))
		{
			return;
		}

		Vector3 midpoint = attacker.GlobalPosition + (GlobalPosition - attacker.GlobalPosition) * 0.62f;
		midpoint.Y = Mathf.Max(attacker.GlobalPosition.Y, GlobalPosition.Y) + 1.15f;
		SpawnWorldCombatEffect("!", color, midpoint, 0.42f, 0.72f);
	}

	private void SpawnWorldCombatEffect(string text, Color color, Vector3 position, float lifetime, float radius)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		var effect = new CombatEffect
		{
			Text = text,
			EffectColor = color,
			Lifetime = lifetime,
			Radius = radius,
		};
		parent.AddChild(effect);
		effect.GlobalPosition = position;
	}

	// 玩家升等文字提示 + 金色特效（每次獲得經驗只顯示一次，顯示最終等級）。
	private void ShowPlayerLevelUpFeedback()
	{
		SpawnWorldCombatEffect(LocaleText.F("effect.level_up", Level), new Color(1.0f, 0.9f, 0.42f, 0.95f), GlobalPosition + new Vector3(0.0f, 1.9f, 0.0f), 1.1f, 0.9f);

		var effect = new LevelUpEffect();
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent != null)
		{
			parent.AddChild(effect);
			effect.GlobalPosition = new Vector3(GlobalPosition.X, GlobalPosition.Y + 0.05f, GlobalPosition.Z);
		}
	}

	private void InitializeCaptureNetAmmo()
	{
		CaptureNetCapacity = Mathf.Max(CaptureNetCapacity, 1);
		_captureNetCharges = CaptureNetCapacity;
		_captureNetRechargeRemaining = CaptureNetRechargeSeconds;
	}

	private void UpdateCaptureNetRecharge(float step)
	{
		float rechargeSeconds = Mathf.Max(CaptureNetRechargeSeconds, 0.05f);
		if (_captureNetCharges >= CaptureNetCapacity)
		{
			_captureNetRechargeRemaining = rechargeSeconds;
			return;
		}

		_captureNetRechargeRemaining -= step;
		while (_captureNetRechargeRemaining <= 0.0f && _captureNetCharges < CaptureNetCapacity)
		{
			_captureNetCharges++;
			_captureNetRechargeRemaining += rechargeSeconds;
		}

		if (_captureNetCharges >= CaptureNetCapacity)
		{
			_captureNetRechargeRemaining = rechargeSeconds;
		}
	}

	private void TriggerDamageFlash()
	{
		_damageFlashRemaining = Mathf.Max(DamageFlashDuration, 0.05f);
	}

	private void UpdateDamageFlash(float step)
	{
		if (_damageFlashOverlay == null)
		{
			return;
		}

		_damageFlashRemaining = Mathf.Max(_damageFlashRemaining - step, 0.0f);
		float duration = Mathf.Max(DamageFlashDuration, 0.05f);
		float alpha = _damageFlashRemaining <= 0.0f
			? 0.0f
			: Mathf.Clamp((_damageFlashRemaining / duration) * 0.28f, 0.0f, 0.28f);
		_damageFlashOverlay.Visible = alpha > 0.01f;
		_damageFlashOverlay.Color = new Color(1.0f, 0.06f, 0.02f, alpha);
	}

}
