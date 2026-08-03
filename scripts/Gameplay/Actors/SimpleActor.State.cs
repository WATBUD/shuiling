using Godot;

public partial class SimpleActor : CharacterBody3D
{
	public void Capture(PlayerController followTarget)
	{
		EnsureLevelOneStats();
		_isCaptured = true;
		_isDefeated = false;
		_isAwaitingRecovery = false;
		_fallenRecoveryExpiresAtUnixSeconds = 0;
		_fallenMapId = string.Empty;
		_followTarget = followTarget;
		_isInActiveParty = false;
		AttackModeId = BuildCatalog.AiManualOnly;
		_waitTime = 0.0f;
		_captureLocked = false;
		_captureProtectionRemaining = 0.0f;
		_captureProtectionSynced = false;
		RefreshCaptureShield(false);
		ResetSquadActivity();
		Velocity = Vector3.Zero;
		CurrentHealth = Mathf.Max(CurrentHealth, Mathf.RoundToInt(EffectiveMaxHealth * 0.45f));
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		AddToGroup("captured_actors");
		// Captured wild monsters arrive with no equipment (they keep their innate
		// skill cores / element); the player gears them up themselves.
		StripEquipment();
		RefreshNameplate();

		// Celebrate a rare capture — a real power spike worth showing off.
		if (Rarity > MonsterRarity.Common && IsInstanceValid(followTarget))
		{
			string rarity = LocaleText.T(MonsterRarity.NameKey(Rarity));
			followTarget.PostSystemMessage(LocaleText.F("system.capture.rare", rarity, LocalizedDisplayName), MonsterRarity.Color(Rarity), GameMessageChannel.Party);
			SpawnCombatEffect(LocaleText.T("system.capture.rare_pop"), MonsterRarity.Color(Rarity), GlobalPosition + new Vector3(0.0f, 1.6f, 0.0f), 1.3f, 0.9f);
		}
	}

	public void Recruit(PlayerController followTarget)
	{
		Capture(followTarget);
	}

	public void SetWorldMapActive(bool active)
	{
		SetWorldMapState(active, active);
	}

	// Simulation (physics/AI/collision) and visibility are decoupled so the host
	// can keep another group's instance running (simulate) without showing it to
	// the local player (visible). Clients never simulate (simulate=false).
	public void SetWorldMapState(bool simulate, bool visible)
	{
		if (_isCaptured)
		{
			return;
		}

		_isWorldMapActive = simulate;
		_engagesLocalPlayer = visible;
		Visible = visible;
		SetPhysicsProcess(simulate && !_isDefeated);
		if (simulate && !_isDefeated)
		{
			CollisionLayer = _defaultCollisionLayer;
			CollisionMask = _defaultCollisionMask;
		}
		else
		{
			CollisionLayer = 0;
			CollisionMask = 0;
			_combatTarget = null;
			_retaliationTarget = null;
			_retaliationTargetRemaining = 0.0f;
		}
	}

	public void DeployToParty(PlayerController followTarget, int followSlot)
	{
		_followTarget = followTarget;
		_followSlot = followSlot;
		_isInWarehouseCollection = false;
		_isInActiveParty = true;
		Visible = true;
		SetPhysicsProcess(!_isDefeated);
		CollisionLayer = _defaultCollisionLayer;
		CollisionMask = _defaultCollisionMask;
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		ResetSquadActivity();
		if (!_isDefeated)
		{
			GlobalPosition = GetFollowDestination();
			ApplyLivingPose();
		}
		Velocity = Vector3.Zero;
		RefreshNameplate();
	}

	public void StoreInCollection()
	{
		_isInActiveParty = false;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);
		RefreshNameplate();
	}

	public void SetWarehouseCollectionState(bool stored)
	{
		_isInWarehouseCollection = stored;
		if (stored)
		{
			StoreInCollection();
		}
	}

	public bool TryRecoverFallenCompanion(PlayerController followTarget, float pickupRadius)
	{
		if (!_isCaptured
			|| !_isDefeated
			|| !_isAwaitingRecovery
			|| _followTarget != followTarget
			|| followTarget.GetParent() is not World world
			|| world.ActiveMapId != _fallenMapId
			|| GlobalPosition.DistanceTo(followTarget.GlobalPosition) > pickupRadius)
		{
			return false;
		}

		_isAwaitingRecovery = false;
		_isInActiveParty = false;
		_fallenRecoveryExpiresAtUnixSeconds = 0;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);
		RefreshNameplate();
		return true;
	}

	// 開發測試用：把一隻已捕捉的夥伴直接設為「已死亡且已回收」狀態
	// （等同倒地後已被撿回），使其顯示在 U 面板「已死亡」區並可於水池復活。
	public void MarkDefeatedForTest()
	{
		_isCaptured = true;
		_isDefeated = true;
		_isAwaitingRecovery = false;
		_isInActiveParty = false;
		_fallenMapId = string.Empty;
		_fallenRecoveryExpiresAtUnixSeconds = 0;
		CurrentHealth = 0;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);
		RefreshNameplate();
	}

	public void UpdateFallenMapVisibility(string activeMapId)
	{
		if (_isCaptured && _isDefeated && _isAwaitingRecovery)
		{
			Visible = activeMapId == _fallenMapId;
		}
	}

	public void RestoreCapturedState(PlayerController followTarget, ActorSaveData data)
	{
		Capture(followTarget);
		ApplySaveData(data);
		_followTarget = followTarget;
		_isCaptured = true;
		_isInActiveParty = false;
		if (_isDefeated)
		{
			CurrentHealth = 0;
			Velocity = Vector3.Zero;
			Visible = _isAwaitingRecovery;
			CollisionLayer = _isAwaitingRecovery ? _defaultCollisionLayer : 0;
			CollisionMask = _isAwaitingRecovery ? _defaultCollisionMask : 0;
			SetPhysicsProcess(false);
			ApplyDefeatedPose();
			RefreshNameplate();
			return;
		}

		StoreInCollection();
	}

	public void SetFormationBonuses(float attackMultiplier, float defenseMultiplier, float cooldownMultiplier, float incomingDamageMultiplier, float rangeBonus, string summary)
	{
		_formationAttackMultiplier = attackMultiplier;
		_formationDefenseMultiplier = defenseMultiplier;
		_formationCooldownMultiplier = cooldownMultiplier;
		_formationIncomingDamageMultiplier = incomingDamageMultiplier;
		_formationRangeBonus = rangeBonus;
		_formationBonusSummary = summary;
		_buildStatsDirty = true;
		if (_buildConfigured)
		{
			RecalculateBuildStats();
		}
	}

	// Applied to every deployed companion; scales with unique cards collected.
	public void SetCardCollectionBonus(float attackMultiplier, float defenseMultiplier, float healthMultiplier)
	{
		_cardAttackMultiplier = attackMultiplier;
		_cardDefenseMultiplier = defenseMultiplier;
		_cardHealthMultiplier = healthMultiplier;
		_buildStatsDirty = true;
		if (_buildConfigured)
		{
			RecalculateBuildStats();
		}
	}

	// The res:// path of the currently-instantiated external model, so the network
	// layer can tell peers which model to render for this companion (empty if the
	// actor is using the primitive fallback body).
	public string GetExternalModelPath()
	{
		Node3D? model = GetNodeOrNull<Node3D>("ExternalModel");
		return model?.SceneFilePath ?? string.Empty;
	}

	// Canonical card identity for this actor's model (one card per model), with a
	// fallback to the species DisplayName key when no external model is present.
	public string GetCardKey()
	{
		Node3D? model = GetNodeOrNull<Node3D>("ExternalModel");
		string path = model?.SceneFilePath ?? string.Empty;
		if (!string.IsNullOrEmpty(path))
		{
			string key = ExternalModelLibrary.CardKeyFromModelPath(path);
			if (!string.IsNullOrWhiteSpace(key))
			{
				return key;
			}
		}

		return DisplayName;
	}
}
