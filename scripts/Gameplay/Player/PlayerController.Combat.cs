using Godot;
using System.Collections.Generic;

public partial class PlayerController
{
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

		return _captureRhythmPanel.Begin(actor);
	}

	private void OnCaptureChallengeSucceeded(SimpleActor actor)
	{
		if (!CaptureActor(actor))
		{
			PostSystemMessage(LocaleText.T("system.capture.target_lost"), new Color(1.0f, 0.58f, 0.42f), GameMessageChannel.Party);
		}
	}

	private void OnCaptureChallengeFailed(SimpleActor actor)
	{
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
			Gravity = NetGravity,
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
		int mitigatedDamage = Mathf.Max(rawDamage - Mathf.RoundToInt(Defense * 0.35f), 1);
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
			RecoverFromKnockdown();
		}

		return mitigatedDamage;
	}

	public int ReceiveHealing(int rawHealing)
	{
		int missingHealth = Mathf.Max(MaxHealth - CurrentHealth, 0);
		int healing = Mathf.Min(Mathf.Max(rawHealing, 0), missingHealth);
		if (healing <= 0)
		{
			return 0;
		}

		CurrentHealth += healing;
		SpawnFloatingEffect($"+{healing}", new Color(0.36f, 1.0f, 0.54f, 0.92f), 0.55f, 0.48f);
		return healing;
	}

	public void GrantCombatExperience(int amount)
	{
		int experience = Mathf.Max(amount, 0);
		if (experience <= 0)
		{
			return;
		}

		Experience += experience;
		while (Experience >= ExperienceToNextLevel)
		{
			Experience -= ExperienceToNextLevel;
			Level++;
			MaxHealth += 12;
			CurrentHealth = Mathf.Min(CurrentHealth + 12, MaxHealth);
			Attack += 2;
			Defense += 1;
		}

		foreach (SimpleActor actor in _activeParty)
		{
			if (IsInstanceValid(actor) && actor.IsInActiveParty)
			{
				actor.GrantTraining(experience);
			}
		}

		PostSystemMessage(LocaleText.F("system.exp.party_gain", experience), new Color(0.86f, 0.78f, 1.0f), GameMessageChannel.Combat);
		_partyPanel.RefreshParty();
	}

	private void RecoverFromKnockdown()
	{
		CurrentHealth = Mathf.Max(MaxHealth / 2, 1);
		TeleportToSafePosition();
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
