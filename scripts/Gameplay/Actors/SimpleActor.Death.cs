using Godot;

public partial class SimpleActor : CharacterBody3D
{
	private void Defeat(SimpleActor? attacker, PlayerController? playerAttacker)
	{
		_isDefeated = true;
		CurrentHealth = 0;
		Velocity = Vector3.Zero;
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		_retaliationTarget = null;
		_retaliationTargetRemaining = 0.0f;
		_combatTarget = null;

		if (_isCaptured)
		{
			Affinity = Mathf.Max(Affinity - 12, -100);
			UpdateNegativeMoodAfterDefeat();
			_isAwaitingRecovery = true;
			_fallenMapId = _followTarget?.GetParent() is World world ? world.ActiveMapId : string.Empty;
			_isInActiveParty = false;
			CollisionLayer = _defaultCollisionLayer;
			CollisionMask = _defaultCollisionMask;
			Visible = true;
			SetPhysicsProcess(false);
			ApplyDefeatedPose();
			SpawnCombatEffect(LocaleText.F("effect.affinity_loss", 12), new Color(1.0f, 0.28f, 0.22f, 0.92f), GlobalPosition + new Vector3(0.0f, 1.15f, 0.0f), 0.95f, 0.72f);
			RefreshNameplate();
			if (_followTarget != null && IsInstanceValid(_followTarget))
			{
				_followTarget.OnCompanionFallen(this);
			}
			return;
		}

		_isInActiveParty = false;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);

		PlayerController? creditedPlayer = playerAttacker;
		if (creditedPlayer == null && attacker?._followTarget != null && IsInstanceValid(attacker._followTarget))
		{
			creditedPlayer = attacker._followTarget;
		}
		if (creditedPlayer != null && IsInstanceValid(creditedPlayer))
		{
			string attackerName = playerAttacker != null ? playerAttacker.LocalizedPlayerName : attacker!.LocalizedDisplayName;
			creditedPlayer.PostSystemMessage(LocaleText.F("system.combat.defeated", attackerName, LocalizedDisplayName), new Color(1.0f, 0.70f, 0.42f), GameMessageChannel.Combat);
			creditedPlayer.GrantCombatExperience(ExperienceReward, Level);
			if (ActorKind == "monster")
			{
				DropMonsterLoot(creditedPlayer);
				// A monster's exclusive name card is a rare physical drop (5%). We
				// only bother rolling if the player doesn't already own it.
				MaybeDropMonsterCard(creditedPlayer);
			}
			if (IsBoss)
			{
				creditedPlayer.ShowBossDefeated(this);
			}
		}

		// Tier unlock is per-player: only the LOCAL player's kill counts here.
		// Remote players' kills are credited on their own machine via RPC
		// (World.ApplyNetworkMonsterDamage → ClientReceiveBossDefeat).
		if (IsBoss
			&& ActorKind == "monster"
			&& creditedPlayer != null
			&& IsInstanceValid(creditedPlayer)
			&& FindOwningWorld() is World bossWorld)
		{
			bossWorld.OnWildBossDefeated(this);
		}

		// Multiplayer: tell the host to broadcast this monster's removal now,
		// instead of waiting for the next periodic state sweep (removes the
		// death lag/pop clients would otherwise see).
		if (ActorKind == "monster" && NetworkMonsterId >= 0)
		{
			FindOwningWorld()?.OnNetworkMonsterDefeated(this);
		}

		// Wild enemies are not recoverable corpses. They used to remain as invisible
		// CharacterBody3D nodes forever while the respawner kept creating replacements,
		// eventually making long sessions progressively slower.
		CallDeferred(Node.MethodName.QueueFree);
	}

	private World? FindOwningWorld()
	{
		Node? node = GetParent();
		while (node != null && node is not World)
		{
			node = node.GetParent();
		}

		return node as World;
	}

	private void UpdateNegativeMoodAfterDefeat()
	{
		if (Affinity >= 0)
		{
			MoodStateId = string.Empty;
			return;
		}

		if (Affinity <= -60)
		{
			MoodStateId = "actor.mood.wants_to_escape";
			return;
		}

		if (Affinity <= -30)
		{
			MoodStateId = _rng.Randf() < 0.55f ? "actor.mood.depressed" : "actor.mood.wants_to_escape";
			return;
		}

		string[] mildNegativeMoods =
		{
			"actor.mood.depressed",
			"actor.mood.afraid",
			"actor.mood.sulking",
		};
		MoodStateId = mildNegativeMoods[_rng.RandiRange(0, mildNegativeMoods.Length - 1)];
	}

	private void ApplyDefeatedPose()
	{
		SetExternalAnimationState("death");
		ResetAttackVisualScale();
		RotationDegrees = new Vector3(0.0f, RotationDegrees.Y, 88.0f);
		SetChildRotation("Head", ActorKind == "monster" ? new Vector3(22.0f, 0.0f, -16.0f) : new Vector3(28.0f, 0.0f, -12.0f));
		SetChildRotation("TailBase", new Vector3(82.0f, 0.0f, 0.0f));
	}

	private void ApplyLivingPose()
	{
		RotationDegrees = new Vector3(0.0f, RotationDegrees.Y, 0.0f);
		ApplyEvolutionAppearance();
		ResetAttackVisualScale();
		SetExternalAnimationState("idle");
	}
}
