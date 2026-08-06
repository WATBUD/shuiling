using Godot;
using System.Collections.Generic;

public partial class SimpleActor : CharacterBody3D
{
	// Player peers that have dealt damage to this monster — the eligible pool for
	// party loot. Non-contributors get nothing; drops are diced among these.
	private readonly HashSet<long> _lootContributors = new();

	public IReadOnlyCollection<long> LootContributors => _lootContributors;

	public void RegisterLootContributor(long peerId)
	{
		if (peerId != 0)
		{
			_lootContributors.Add(peerId);
		}
	}

	public int ReceiveDamage(int rawDamage, SimpleActor? attacker, PlayerController? playerAttacker = null, bool isCrit = false)
	{
		if (IsInvulnerable)
		{
			return 0;
		}

		// Network puppet: the host owns this monster's health. Forward the raw
		// damage and show a local hit flash; real damage syncs back via state.
		if (_isNetworkPuppet)
		{
			NetworkManager.Instance?.SendMonsterDamageRequest(_networkId, Mathf.Max(rawDamage, 1));
			SpawnCombatEffect(rawDamage, attacker?.GetAttackColor() ?? new Color(1.0f, 0.5f, 0.22f, 0.92f));
			return 0;
		}

		float elementMultiplier = attacker == null
			? 1.0f
			: ElementChart.GetMultiplier(attacker.CurrentBuildStats.DamageElementId, CurrentBuildStats.DamageElementId);
		int elementalDamage = Mathf.Max(Mathf.RoundToInt(rawDamage * elementMultiplier * CurrentBuildStats.IncomingDamageMultiplier), 1);
		int mitigatedDamage = Mathf.Max(elementalDamage - Mathf.RoundToInt(EffectiveDefense * 0.35f), 1);
		if (IsTrainingDummy)
		{
			// 稻草人：顯示傷害數字與命中特效，但永遠不扣血、不會死亡、也不會反擊。
			if (isCrit)
			{
				SpawnCritEffect(mitigatedDamage);
			}
			else
			{
				SpawnCombatEffect(mitigatedDamage, attacker?.GetAttackColor() ?? new Color(1.0f, 0.5f, 0.22f, 0.92f));
			}
			RefreshNameplate();
			return mitigatedDamage;
		}

		if (CurrentBuildStats.HasShieldSkill)
		{
			mitigatedDamage = Mathf.Max(Mathf.RoundToInt(mitigatedDamage * 0.78f), 1);
			SpawnCombatEffect(string.Empty, new Color(0.35f, 0.78f, 1.0f, 0.78f), GlobalPosition + new Vector3(0.0f, 1.0f, 0.0f), 0.28f, 0.82f);
		}
		RememberAttacker(attacker);
		// A local player-side hit (own attack or a companion's) credits the local
		// peer as a loot participant. Remote peers are credited in
		// World.ApplyNetworkMonsterDamage, which calls ReceiveDamage with both null.
		if (attacker != null || playerAttacker != null)
		{
			RegisterLootContributor(NetworkManager.Instance?.LocalPeerId ?? 1L);
		}

		// Attacking a passive newbie monster provokes it into fighting back.
		if (_isPassive)
		{
			_provokeRemaining = PassiveProvokeSeconds;
		}
		CurrentHealth = Mathf.Max(CurrentHealth - mitigatedDamage, 0);
		// A capture in progress keeps the target alive (min 1 HP) so it can't die
		// mid-capture. Damage still lands and still builds the stagger meter.
		if (CurrentHealth <= 0 && IsCaptureProtected)
		{
			CurrentHealth = 1;
		}
		if (isCrit)
		{
			SpawnCritEffect(mitigatedDamage);
		}
		else
		{
			SpawnCombatEffect(mitigatedDamage, attacker?.GetAttackColor() ?? new Color(1.0f, 0.5f, 0.22f, 0.92f));
		}
		// Hits build the capture stagger meter (combo finisher path).
		AddCaptureStagger(mitigatedDamage);
		if (IsBoss && attacker?._followTarget != null && IsInstanceValid(attacker._followTarget))
		{
			attacker._followTarget.NotifyBossCombat(this);
		}
		else if (IsBoss && playerAttacker != null && IsInstanceValid(playerAttacker))
		{
			playerAttacker.NotifyBossCombat(this);
		}
		else if (attacker?.IsBoss == true && _followTarget != null && IsInstanceValid(_followTarget))
		{
			_followTarget.NotifyBossCombat(attacker);
		}
		if (IsBoss && !_bossEnraged && CurrentHealth > 0 && HealthRatio <= 0.50f)
		{
			TriggerBossEnrage(attacker);
		}

		if (CurrentHealth <= 0)
		{
			Defeat(attacker, playerAttacker);
		}

		RefreshNameplate();
		return mitigatedDamage;
	}

	private void TriggerBossEnrage(SimpleActor? attacker)
	{
		_bossEnraged = true;
		Node effectParent = GetTree().CurrentScene ?? GetParent();
		SkillAttackVfx.SpawnSpecial(
			effectParent,
			SkillAttackVfx.ExplosionEvent,
			GlobalPosition + Vector3.Up * 0.35f,
			Vector3.Up,
			"gem.skill.meteor",
			"fire",
			new Color(1.0f, 0.16f, 0.04f, 0.94f),
			4.2f,
			new ProjectileBehaviorProfile());
		if (attacker?._followTarget != null && IsInstanceValid(attacker._followTarget))
		{
			attacker._followTarget.ShowBossEnraged(this);
		}
		RefreshNameplate();
	}

	public int ReceiveHealing(int rawHealing)
	{
		if (_isDefeated)
		{
			return 0;
		}

		int missingHealth = Mathf.Max(EffectiveMaxHealth - CurrentHealth, 0);
		int healing = Mathf.Min(Mathf.Max(rawHealing, 0), missingHealth);
		if (healing <= 0)
		{
			return 0;
		}

		CurrentHealth += healing;
		SpawnCombatEffect($"+{healing}", new Color(0.36f, 1.0f, 0.54f, 0.92f), GlobalPosition + new Vector3(0.0f, 1.3f, 0.0f), 0.58f, 0.46f);
		RefreshNameplate();
		return healing;
	}

	private void AttackActor(SimpleActor target)
	{
		if (_attackCooldownRemaining > 0.0f)
		{
			return;
		}

		// Only captured companions use the gem-driven projectile system. Wild monsters
		// (e.g. retaliating against a companion) keep the direct instant-hit path, whose
		// targeting differs from the companion "hostile monster" search.
		if (_isCaptured)
		{
			LaunchAttack(target);
		}
		else
		{
			LegacyAttackActor(target);
		}

		_attackCooldownRemaining = EffectiveAttackCooldown;
	}

	private void LegacyAttackActor(SimpleActor target)
	{
		BuildStats stats = CurrentBuildStats;
		int roleBonus = CombatRole == "DPS" ? 4 : CombatRole == "Tank" ? 1 : CombatRole == "Ranged" ? 2 : 0;
		int affinityBonus = Affinity >= 80 ? 2 : Affinity >= 55 ? 1 : 0;
		int damage = Mathf.Max(stats.Attack + roleBonus + affinityBonus, 1);
		bool crit = _rng.Randf() < stats.CritChance;
		if (crit)
		{
			damage = Mathf.RoundToInt(damage * stats.CritDamageMultiplier);
		}

		PlayAttackAction(target.GlobalPosition, false);
		int dealtDamage = target.ReceiveDamage(damage, this, isCrit: crit);
		if (dealtDamage > 0 && _rng.Randf() < stats.ControlChance)
		{
			target.ApplyElementStatus(stats.DamageElementId, this);
		}

		if (stats.LifeStealPercent > 0.0f && dealtDamage > 0)
		{
			ReceiveHealing(Mathf.RoundToInt(dealtDamage * stats.LifeStealPercent));
		}

		AdvanceBossAttackPattern(target);
	}

	// Base attack of the pet, shaped by whatever behavior gems the build carries.
	// Damage is no longer applied instantly; each spawned projectile carries it and
	// resolves on impact through ResolveProjectileHit.
	private void LaunchAttack(SimpleActor target)
	{
		BuildStats stats = CurrentBuildStats;
		int roleBonus = CombatRole == "DPS" ? 4 : CombatRole == "Tank" ? 1 : CombatRole == "Ranged" ? 2 : 0;
		int affinityBonus = Affinity >= 80 ? 2 : Affinity >= 55 ? 1 : 0;
		int baseDamage = Mathf.Max(stats.Attack + roleBonus + affinityBonus, 1);

		SetExternalAnimationState(GetExternalAttackAnimationState(false), 0.48f);
		AnimateAttackPose();

		bool isMelee = !UsesProjectileAttack(false);
		if (isMelee)
		{
			SpawnSwingEffect(target.GlobalPosition);
		}

		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		Vector3 forward = toTarget.LengthSquared() > 0.001f ? toTarget.Normalized() : -GlobalTransform.Basis.Z;
		string visualSkillId = BuildLoadout.GetSkillGemId(0);
		if (BuildCatalog.IsMainAttackCore(visualSkillId))
		{
			Node effectParent = GetTree().CurrentScene ?? GetParent();
			SkillAttackVfx.SpawnCast(
				effectParent,
				GlobalPosition + Vector3.Up * 1.05f + forward * 0.34f,
				forward,
				visualSkillId,
				stats.DamageElementId,
				GetAttackColor(),
				stats.Behavior,
				stats.LifeStealPercent > 0.0f);
		}

		if (visualSkillId is "gem.skill.lightning" or "gem.skill.meteor" or "gem.skill.laser")
		{
			ResolveTargetedCoreStrike(target, baseDamage, stats, visualSkillId);
			return;
		}

		bool usesWhirlwind = isMelee && BuildLoadout.HasSkill("gem.skill.whirlwind");
		if (usesWhirlwind)
		{
			BeginWhirlwindSpin();
		}
		int projectileCount = usesWhirlwind ? 3 : 1 + Mathf.Max(stats.Behavior.ExtraProjectiles, 0);
		float spreadStep = Mathf.DegToRad(usesWhirlwind ? 32.0f : 14.0f);
		for (int index = 0; index < projectileCount; index++)
		{
			float angle = (index - (projectileCount - 1) / 2.0f) * spreadStep;
			Vector3 direction = forward.Rotated(Vector3.Up, angle);
			SimpleActor? homing = Mathf.Abs(angle) < 0.001f ? target : null;
			SpawnCombatProjectile(direction, homing, baseDamage, stats, isMelee);
		}
	}

	private void ResolveTargetedCoreStrike(SimpleActor target, int damage, BuildStats stats, string skillId)
	{
		if (!IsInstanceValid(target) || !target.IsActiveWorldTarget)
		{
			return;
		}

		Node? parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		Vector3 targetPosition = target.GlobalPosition + Vector3.Up * 0.08f;
		if (skillId == "gem.skill.laser")
		{
			Vector3 beamOrigin = GlobalPosition + Vector3.Up * 1.05f;
			SkillAttackVfx.SpawnSpecial(
				parent,
				SkillAttackVfx.ChainEvent,
				beamOrigin,
				targetPosition - beamOrigin,
				skillId,
				stats.DamageElementId,
				GetAttackColor(),
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
			GetAttackColor(),
			radius,
			new ProjectileBehaviorProfile(),
			stats.LifeStealPercent > 0.0f);
		ResolveProjectileHit(target, Mathf.Max(damage, 1));
	}

	private void SpawnCombatProjectile(Vector3 direction, SimpleActor? homingTarget, int baseDamage, BuildStats stats, bool isMelee)
	{
		Node? parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		var projectile = new CombatProjectile
		{
			Attacker = this,
			Damage = baseDamage,
			EffectColor = GetAttackColor(),
			IsMelee = isMelee,
			IsArrow = UsesArrowProjectile(false),
			VisualSkillId = BuildCatalog.IsMainAttackCore(BuildLoadout.GetSkillGemId(0))
				? BuildLoadout.GetSkillGemId(0)
				: stats.ActiveRangedSkillId,
			ElementId = stats.DamageElementId,
			HasLifeSteal = stats.LifeStealPercent > 0.0f,
			Speed = (isMelee ? 26.0f : 17.0f) * stats.ProjectileSpeedMultiplier,
			MaxRange = Mathf.Max(EffectiveAttackRange * 1.6f, isMelee ? 3.0f : 9.0f),
			HitRadius = isMelee ? 1.35f : 1.0f,
			InitialTarget = homingTarget,
			LaunchDirection = direction,
			SpawnOrigin = GlobalPosition + Vector3.Up * (isMelee ? 1.04f : 1.22f) + direction * 0.5f,
			Behavior = stats.Behavior.Clone(),
		};
		parent.AddChild(projectile);
	}

	// Called by a CombatProjectile when it strikes a target. Centralizes the crit roll,
	// elemental damage, on-hit control status, and life steal so all combat math stays here.
	public int ResolveProjectileHit(SimpleActor target, int baseDamage)
	{
		if (target == null || !IsInstanceValid(target) || !target.IsActiveWorldTarget)
		{
			return 0;
		}

		BuildStats stats = CurrentBuildStats;
		int damage = Mathf.Max(baseDamage, 1);
		bool crit = _rng.Randf() < stats.CritChance;
		if (crit)
		{
			damage = Mathf.RoundToInt(damage * stats.CritDamageMultiplier);
		}

		int dealtDamage = target.ReceiveDamage(damage, this, isCrit: crit);
		if (dealtDamage > 0 && _rng.Randf() < stats.ControlChance)
		{
			target.ApplyElementStatus(stats.DamageElementId, this);
		}

		if (stats.LifeStealPercent > 0.0f && dealtDamage > 0)
		{
			ReceiveHealing(Mathf.RoundToInt(dealtDamage * stats.LifeStealPercent));
		}

		return dealtDamage;
	}

	// Hostile actors within radius of a point, skipping any already struck by the
	// projectile. Used for chain retargeting, split fan-out, and explosion splash.
	public void FindProjectileTargets(Vector3 center, float radius, ICollection<SimpleActor> exclude, List<SimpleActor> results)
	{
		results.Clear();
		float radiusSquared = radius * radius;
		center.Y = 0.0f;
		foreach (SimpleActor actor in ActiveActorRegistry)
		{
			if (!IsInstanceValid(actor) || !actor.IsHostileToPlayer)
			{
				continue;
			}

			if (exclude != null && exclude.Contains(actor))
			{
				continue;
			}

			Vector3 actorPosition = actor.GlobalPosition;
			actorPosition.Y = 0.0f;
			if (center.DistanceSquaredTo(actorPosition) <= radiusSquared)
			{
				results.Add(actor);
			}
		}
	}

	// Pick the nearest valid player this monster should hunt within its own
	// instance. The local player counts only when the monster shares the local
	// player's instance (_engagesLocalPlayer); remote players are resolved on the
	// host so host-simulated monsters can attack clients too.
	private Node3D? ResolveHostileTarget(Node3D? localPlayer, out bool isRemote, out long remotePeerId)
	{
		isRemote = false;
		remotePeerId = 0;
		Node3D? best = null;
		float bestDistance = float.MaxValue;

		if (SharesLocalInstance && localPlayer != null)
		{
			best = localPlayer;
			bestDistance = GlobalPosition.DistanceTo(localPlayer.GlobalPosition);
		}

		if (NetworkManager.Instance is { IsHost: true } net)
		{
			Node3D? puppet = net.FindNearestRemotePlayer(MapId, WorldTier, GroupId, GlobalPosition, out long peerId, out float distance);
			if (puppet != null && distance < bestDistance)
			{
				best = puppet;
				bestDistance = distance;
				isRemote = true;
				remotePeerId = peerId;
			}
		}

		return best;
	}

	// Attack a remote player's puppet: the host deals the damage on that client via
	// an RPC (the puppet itself is a display-only stand-in with no combat state).
	private bool TryAttackRemotePlayer(Node3D puppet, long peerId, Vector3 velocity, float step)
	{
		Vector3 toTarget = puppet.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		if (toTarget.Length() > EffectiveAttackRange)
		{
			return false;
		}

		Velocity = SlowToStop(velocity, step);
		FaceDirection(toTarget, step);
		if (_attackCooldownRemaining <= 0.0f)
		{
			SpawnPlayerAttackCue(puppet.GlobalPosition);
			PlayAttackAction(puppet.GlobalPosition, false);
			NetworkManager.Instance?.SendMonsterAttackToPlayer(peerId, EffectiveAttack);
			_attackCooldownRemaining = EffectiveAttackCooldown;
		}

		MoveAndSlideWithEffects(step);
		return true;
	}

	private bool TryAttackPlayer(Node3D player, Vector3 velocity, float step)
	{
		Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
		toPlayer.Y = 0.0f;
		if (toPlayer.Length() > EffectiveAttackRange)
		{
			return false;
		}

		Velocity = SlowToStop(velocity, step);
		FaceDirection(toPlayer, step);
		if (_attackCooldownRemaining <= 0.0f && player is PlayerController playerController)
		{
			SpawnPlayerAttackCue(player.GlobalPosition);
			PlayAttackAction(player.GlobalPosition, false);
			playerController.ReceiveDamage(EffectiveAttack, this);
			if (DisplayName == "name.monster.cave_spider"
				&& _specialControlCooldownRemaining <= 0.0f
				&& _rng.Randf() <= 0.38f
				&& playerController.TryApplySpiderWebSuspension(this))
			{
				_specialControlCooldownRemaining = 8.0f;
			}
			AdvanceBossAttackPattern(playerController);
			_attackCooldownRemaining = EffectiveAttackCooldown;
		}

		MoveAndSlideWithEffects(step);
		return true;
	}

	private void AdvanceBossAttackPattern(Node primaryTarget)
	{
		if (!IsBoss || _isDefeated)
		{
			return;
		}

		_bossAttackCounter++;
		int attackInterval = _bossEnraged ? 2 : 3;
		if (_bossAttackCounter % attackInterval != 0)
		{
			return;
		}

		const float novaRadius = 4.8f;
		Node effectParent = GetTree().CurrentScene ?? GetParent();
		SkillAttackVfx.SpawnSpecial(
			effectParent,
			SkillAttackVfx.ExplosionEvent,
			GlobalPosition + Vector3.Up * 0.24f,
			Vector3.Up,
			"gem.skill.explosion",
			"fire",
			_bossEnraged ? new Color(1.0f, 0.18f, 0.035f, 0.94f) : new Color(1.0f, 0.68f, 0.12f, 0.92f),
			novaRadius,
			new ProjectileBehaviorProfile { ExplosionRadius = novaRadius });

		int splashDamage = Mathf.Max(Mathf.RoundToInt(EffectiveAttack * (_bossEnraged ? 0.68f : 0.52f)), 1);
		foreach (SimpleActor companion in ActiveActorRegistry)
		{
			if (IsInstanceValid(companion)
				&& companion.IsCaptured
				&& companion != primaryTarget
				&& companion.IsInActiveParty
				&& !companion.IsDefeated
				&& companion.GlobalPosition.DistanceTo(GlobalPosition) <= novaRadius)
			{
				companion.ReceiveDamage(splashDamage, this);
			}
		}

		Node3D? player = GetCachedPlayerNode();
		if (player is PlayerController playerController
			&& playerController != primaryTarget
			&& playerController.GlobalPosition.DistanceTo(GlobalPosition) <= novaRadius)
		{
			playerController.ReceiveDamage(Mathf.Max(Mathf.RoundToInt(splashDamage * 0.72f), 1), this);
		}
	}

	private bool TryAttackActorTarget(SimpleActor target, Vector3 velocity, float step)
	{
		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		if (toTarget.Length() > EffectiveAttackRange)
		{
			return false;
		}

		Velocity = SlowToStop(velocity, step);
		FaceDirection(toTarget, step);
		AttackActor(target);
		MoveAndSlideWithEffects(step);
		return true;
	}

	private void RememberAttacker(SimpleActor? attacker)
	{
		if (ActorKind != "monster" || !IsActiveWorldTarget || !IsValidRetaliationTarget(attacker))
		{
			return;
		}

		_retaliationTarget = attacker;
		_retaliationTargetRemaining = 8.0f;
		_combatTarget = attacker;
	}

	private bool TryGetRetaliationTarget(out SimpleActor target)
	{
		target = null!;
		SimpleActor? retaliationTarget = _retaliationTarget;
		if (_retaliationTargetRemaining <= 0.0f || !IsValidRetaliationTarget(retaliationTarget))
		{
			_retaliationTarget = null;
			return false;
		}

		if (GlobalPosition.DistanceTo(retaliationTarget!.GlobalPosition) > ChaseRadius * 1.65f)
		{
			_retaliationTarget = null;
			_retaliationTargetRemaining = 0.0f;
			return false;
		}

		target = retaliationTarget;
		return true;
	}

	private bool IsValidRetaliationTarget(SimpleActor? actor)
	{
		if (actor == null || !IsInstanceValid(actor) || actor == this || actor.IsDefeated || !actor.IsVisibleInTree())
		{
			return false;
		}

		return actor.IsInActiveParty || actor.IsActiveWorldTarget;
	}

	private void BeginWhirlwindSpin()
	{
		if (_whirlwindSpinRemaining > 0.0f)
		{
			Vector3 previousRotation = Rotation;
			previousRotation.Y = _whirlwindSpinBaseYaw;
			Rotation = previousRotation;
		}

		_whirlwindSpinBaseYaw = Rotation.Y;
		_whirlwindSpinAngle = 0.0f;
		_whirlwindSpinRemaining = WhirlwindSpinSeconds;
	}

	private void UpdateWhirlwindSpin(float step)
	{
		if (_whirlwindSpinRemaining <= 0.0f)
		{
			return;
		}

		float appliedStep = Mathf.Min(step, _whirlwindSpinRemaining);
		_whirlwindSpinRemaining -= appliedStep;
		_whirlwindSpinAngle += WhirlwindSpinRadians * appliedStep / WhirlwindSpinSeconds;
	}

	private void ApplyWhirlwindSpinRotation()
	{
		if (_whirlwindSpinRemaining > 0.0f)
		{
			Vector3 rotation = Rotation;
			rotation.Y = _whirlwindSpinBaseYaw + _whirlwindSpinAngle;
			Rotation = rotation;
			return;
		}

		if (_whirlwindSpinAngle > 0.0f)
		{
			Vector3 rotation = Rotation;
			rotation.Y = _whirlwindSpinBaseYaw;
			Rotation = rotation;
			_whirlwindSpinAngle = 0.0f;
		}
	}
}
