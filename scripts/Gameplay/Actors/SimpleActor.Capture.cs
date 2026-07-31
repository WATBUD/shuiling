using Godot;

// Capture-protection and mourning behaviour for SimpleActor, split out of the
// core file (Stage-0 separation — see docs/ARCHITECTURE_REVIEW.md). Pure code
// move: shared state stays in SimpleActor.cs and is reached as the same class.
public partial class SimpleActor : CharacterBody3D
{
	private void UpdateCaptureState(float step)
	{
		if (_staggerRemaining > 0.0f)
		{
			_staggerRemaining -= step;
			if (_staggerRemaining <= 0.0f)
			{
				_staggerRemaining = 0.0f;
				RefreshNameplate();
			}
		}
		else if (_staggerValue > 0.0f)
		{
			// Guard recovers if you stop comboing (~5s to fully drain).
			_staggerValue = Mathf.Max(0.0f, _staggerValue - MaxStagger * 0.2f * step);
		}

		if (_captureProtectionRemaining > 0.0f)
		{
			_captureProtectionRemaining = Mathf.Max(0.0f, _captureProtectionRemaining - step);
		}
		SyncCaptureProtection();
	}

	// A capture orb landed: protect the target from dying for a while. Refreshed by
	// each subsequent orb hit so the whole weaken/capture sequence is safe.
	public void GrantCaptureProtection(float seconds)
	{
		_captureProtectionRemaining = Mathf.Max(_captureProtectionRemaining, seconds);
		SyncCaptureProtection();
	}

	// Held true for the duration of the rhythm challenge (which pauses the world),
	// so the target stays protected even though its timer isn't ticking.
	public void SetCaptureLocked(bool locked)
	{
		_captureLocked = locked;
		SyncCaptureProtection();
	}

	// The capture attempt ended (success or failure): normal combat rules resume.
	public void EndCaptureProtection()
	{
		_captureLocked = false;
		_captureProtectionRemaining = 0.0f;
		SyncCaptureProtection();
	}

	// Client-side entry point: mirror the host's protected flag for the shield VFX.
	public void SetCaptureProtectedVisual(bool on)
	{
		RefreshCaptureShield(on);
	}

	// Broadcast protection changes to clients (host authority) and refresh the local
	// shield visual. HP no-death itself is already synced because the host owns HP.
	private void SyncCaptureProtection()
	{
		bool now = IsCaptureProtected;
		if (now == _captureProtectionSynced)
		{
			return;
		}

		_captureProtectionSynced = now;
		RefreshCaptureShield(now);
		if (NetworkManager.Instance is { IsHost: true } net && NetworkMonsterId >= 0)
		{
			net.BroadcastMonsterCaptureProtection(NetworkMonsterId, now);
		}
	}

	private void RefreshCaptureShield(bool on)
	{
		if (on)
		{
			if (_captureShield == null || !IsInstanceValid(_captureShield))
			{
				var material = new StandardMaterial3D
				{
					AlbedoColor = new Color(0.42f, 0.82f, 1.0f, 0.22f),
					Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					EmissionEnabled = true,
					Emission = new Color(0.42f, 0.82f, 1.0f) * 0.5f,
					CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				};
				_captureShield = new MeshInstance3D
				{
					Name = "CaptureShield",
					Mesh = new SphereMesh { Radius = 1.15f, Height = 2.3f },
					Position = new Vector3(0.0f, 1.0f, 0.0f),
				};
				_captureShield.SetSurfaceOverrideMaterial(0, material);
				AddChild(_captureShield);
			}

			_captureShield.Visible = true;
		}
		else if (_captureShield != null && IsInstanceValid(_captureShield))
		{
			_captureShield.Visible = false;
		}
	}

	// Owner died: enter an invincible grieving state (no combat, can't be hit) and
	// float a crying bubble above the companion until the owner returns.
	public void EnterMourning()
	{
		if (!_isCaptured)
		{
			return;
		}

		_isMourning = true;
		_combatTarget = null;
		_retaliationTarget = null;
		_retaliationTargetRemaining = 0.0f;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		ShowMournBubble();
	}

	public void ExitMourning()
	{
		if (!_isMourning)
		{
			return;
		}

		_isMourning = false;
		if (_mournBubble != null && IsInstanceValid(_mournBubble))
		{
			_mournBubble.Visible = false;
		}

		if (_isInActiveParty && !_isDefeated)
		{
			CollisionLayer = _defaultCollisionLayer;
			CollisionMask = _defaultCollisionMask;
		}
	}

	private void ShowMournBubble()
	{
		if (_mournBubble == null || !IsInstanceValid(_mournBubble))
		{
			_mournBubble = new Label3D
			{
				Name = "MournBubble",
				Position = new Vector3(0.4f, 2.1f, 0.0f),
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				FontSize = 40,
				OutlineSize = 10,
				Modulate = new Color(0.72f, 0.86f, 1.0f),
				NoDepthTest = true,
			};
			AddChild(_mournBubble);
		}

		string[] keys = { "system.mourn.1", "system.mourn.2", "system.mourn.3" };
		_mournBubble.Text = LocaleText.T(keys[(int)(GD.Randi() % (uint)keys.Length)]);
		_mournBubble.Visible = true;
	}
}
