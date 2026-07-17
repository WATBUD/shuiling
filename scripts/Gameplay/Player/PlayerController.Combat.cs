using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	private void ThrowCaptureNet()
	{
		if (_captureCooldownRemaining > 0.0f || _captureNetCharges <= 0)
		{
			return;
		}

		_captureCooldownRemaining = CaptureCooldown;
		_captureNetCharges = Mathf.Max(_captureNetCharges - 1, 0);
		Vector3 direction = GetCaptureThrowDirection();
		Vector3 spawnPosition = GlobalPosition + new Vector3(0.0f, 1.18f, 0.0f) + direction * 1.05f;
		var net = new CaptureNet
		{
			OwnerPlayer = this,
			Direction = direction,
		};

		Node projectileParent = GetTree().CurrentScene ?? GetParent();
		projectileParent.AddChild(net);
		net.GlobalPosition = spawnPosition;
		net.AlignToDirection();
		UpdateCaptureAmmoHud();
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
		PostSystemMessage(LocaleText.F("system.capture.success", actor.LocalizedDisplayName), new Color(0.62f, 0.90f, 1.0f));

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
		Color hitColor = attacker?.AttackFxColor ?? new Color(1.0f, 0.18f, 0.14f, 0.92f);
		SpawnWorldCombatEffect($"-{mitigatedDamage}", hitColor, GlobalPosition + new Vector3(0.0f, 1.45f, 0.0f), 0.78f, 0.88f);
		SpawnIncomingAttackCue(attacker, hitColor);
		TriggerDamageFlash();

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

		PostSystemMessage(LocaleText.F("system.exp.party_gain", experience), new Color(0.86f, 0.78f, 1.0f));
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
