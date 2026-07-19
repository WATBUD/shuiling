using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	public SimpleActor? MountedCompanion => IsMountedCompanionValid() ? _mountedCompanion : null;
	private bool RecruitNpc(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !actor.IsNpcRecruitCandidate || _capturedCollection.Contains(actor))
		{
			return false;
		}

		_capturedCollection.Add(actor);
		actor.Recruit(this);
		PostSystemMessage(LocaleText.F("system.npc.joined", actor.LocalizedDisplayName), new Color(0.62f, 1.0f, 0.78f), GameMessageChannel.Party);

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

	public bool IsInActiveParty(SimpleActor actor)
	{
		return _activeParty.Contains(actor);
	}

	public bool IsMountedCompanion(SimpleActor actor)
	{
		return MountedCompanion == actor;
	}

	public bool ToggleMountCompanion(SimpleActor actor)
	{
		if (!_activeParty.Contains(actor) || actor.IsDefeated)
		{
			return false;
		}

		if (IsMountedCompanion(actor))
		{
			actor.SetMountedByPlayer(false);
			_mountedCompanion = null;
		}
		else
		{
			if (MountedCompanion != null)
			{
				MountedCompanion.SetMountedByPlayer(false);
			}
			_mountedCompanion = actor;
			actor.SetMountedByPlayer(true);
		}
		UpdateMountedVisualOffset();
		_partyPanel.RefreshParty();
		return true;
	}

	private bool IsMountedCompanionValid()
	{
		return _mountedCompanion != null
			&& IsInstanceValid(_mountedCompanion)
			&& _activeParty.Contains(_mountedCompanion)
			&& !_mountedCompanion.IsDefeated;
	}

	public bool DeployCompanion(SimpleActor actor, bool replaceLastIfFull)
	{
		if (!_capturedCollection.Contains(actor) || actor.IsDefeated || actor.IsAwaitingRecovery)
		{
			return false;
		}

		if (_activeParty.Contains(actor))
		{
			return true;
		}

		if (_activeParty.Count >= ActivePartyLimit)
		{
			if (!replaceLastIfFull || _activeParty.Count == 0)
			{
				return false;
			}

			StoreCompanion(_activeParty[_activeParty.Count - 1]);
		}

		_activeParty.Add(actor);
		actor.DeployToParty(this, _activeParty.Count - 1);
		EnsureFormationSlotForActor(actor);
		actor.OnFormationLayoutChanged();
		RecalculateFormationBonuses();
		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		return true;
	}

	public bool StoreCompanion(SimpleActor actor)
	{
		if (!_capturedCollection.Contains(actor))
		{
			return false;
		}

		bool removed = _activeParty.Remove(actor);
		if (_mountedCompanion == actor)
		{
			actor.SetMountedByPlayer(false);
			_mountedCompanion = null;
			UpdateMountedVisualOffset();
		}
		ClearFormationAssignment(actor);
		actor.StoreInCollection();
		RecalculateFormationBonuses();
		if (removed)
		{
			ReassignFollowSlots();
		}

		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		return true;
	}

	public void OnCompanionFallen(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !_capturedCollection.Contains(actor))
		{
			return;
		}

		bool removed = _activeParty.Remove(actor);
		if (_mountedCompanion == actor)
		{
			actor.SetMountedByPlayer(false);
			_mountedCompanion = null;
			UpdateMountedVisualOffset();
		}

		ClearFormationAssignment(actor);
		if (removed)
		{
			ReassignFollowSlots();
		}
		RecalculateFormationBonuses();
		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		PostSystemMessage(
			LocaleText.F("system.companion.fallen_recover", actor.LocalizedDisplayName),
			new Color(1.0f, 0.58f, 0.42f),
			GameMessageChannel.Party);
	}

	private void CollectNearbyFallenCompanions()
	{
		foreach (SimpleActor actor in _capturedCollection)
		{
			if (!IsInstanceValid(actor)
				|| !actor.TryRecoverFallenCompanion(this, FallenCompanionPickupRadius))
			{
				continue;
			}

			PostSystemMessage(
				LocaleText.F("system.companion.recovered", actor.LocalizedDisplayName),
				new Color(0.56f, 1.0f, 0.76f),
				GameMessageChannel.Party);
			_partyPanel.RefreshParty();
			_formationPanel.RefreshAll();
			_inventoryPanel.RefreshAll();
		}
	}

	public void RefreshFallenCompanionMapVisibility(string activeMapId)
	{
		foreach (SimpleActor actor in _capturedCollection)
		{
			if (IsInstanceValid(actor))
			{
				actor.UpdateFallenMapVisibility(activeMapId);
			}
		}
	}

	public int ReviveDefeatedCompanions()
	{
		int fallenCount = 0;
		int awaitingRecoveryCount = 0;
		foreach (SimpleActor actor in _capturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsDefeated)
			{
				if (actor.IsAwaitingRecovery)
				{
					awaitingRecoveryCount++;
				}
				else
				{
					fallenCount++;
				}
			}
		}

		if (fallenCount <= 0)
		{
			PostSystemMessage(
				LocaleText.T(awaitingRecoveryCount > 0 ? "system.revive.retrieve_first" : "system.revive.no_fallen"),
				new Color(0.78f, 0.88f, 1.0f),
				GameMessageChannel.Party);
			return 0;
		}

		int totalCost = fallenCount * PetReviveGoldCost;
		if (Gold < totalCost)
		{
			PostSystemMessage(LocaleText.F("system.revive.not_enough_gold", totalCost, Gold), new Color(1.0f, 0.62f, 0.48f), GameMessageChannel.Party);
			return 0;
		}

		int revivedCount = 0;
		foreach (SimpleActor actor in _capturedCollection)
		{
			if (!IsInstanceValid(actor) || !actor.IsDefeated || actor.IsAwaitingRecovery)
			{
				continue;
			}

			if (actor.ReviveFromCaretaker(this))
			{
				revivedCount++;
			}
		}

		if (revivedCount > 0)
		{
			int paidGold = revivedCount * PetReviveGoldCost;
			Gold = Mathf.Max(Gold - paidGold, 0);
			ReassignFollowSlots();
			_partyPanel.RefreshParty();
			_formationPanel.RefreshAll();
			_inventoryPanel.RefreshAll();
			PostSystemMessage(LocaleText.F("system.revive.count_paid", revivedCount, paidGold, Gold), new Color(0.54f, 1.0f, 0.70f), GameMessageChannel.Party);
		}

		return revivedCount;
	}

	public void TeleportPartyTo(Vector3 position)
	{
		GlobalPosition = position;
		Velocity = Vector3.Zero;
		_lastSafePosition = position + Vector3.Up * 0.18f;
		for (int index = 0; index < _activeParty.Count; index++)
		{
			SimpleActor actor = _activeParty[index];
			if (!IsInstanceValid(actor) || actor.IsDefeated)
			{
				continue;
			}

			Vector3 offset = GetFormationLocalOffset(actor);
			actor.GlobalPosition = position + new Vector3(offset.X, 0.0f, offset.Z);
			actor.Velocity = Vector3.Zero;
		}
	}

	private void ReassignFollowSlots()
	{
		for (int index = 0; index < _activeParty.Count; index++)
		{
			_activeParty[index].SetFollowSlot(index);
		}
	}

	private void GrantStarterBunny()
	{
		if (_capturedCollection.Count > 0 || GetParent() is not World world)
		{
			return;
		}

		// High level so every core slot (1 main + 6 support) is already unlocked, then
		// deck it out with a full fire-mage core chain to showcase the system.
		SimpleActor bunny = world.SpawnPurchasedPet("name.monster.bunny", 40, 1100, 150, 80);
		CaptureActor(bunny);
		EquipFullCoreShowcase(bunny);
	}

	private static void EquipFullCoreShowcase(SimpleActor actor)
	{
		if (!IsInstanceValid(actor))
		{
			return;
		}

		// Main core (attack element).
		actor.EquipAttributeGem("gem.attribute.fire");

		// Support core chain (max 6). The ranged active skill (fireball) must go first so
		// the projectile-support cores that follow are accepted.
		string[] supportCores =
		{
			"gem.skill.fireball",
			"gem.skill.explosion",
			"gem.skill.split",
			"gem.skill.multishot",
			"gem.skill.chain",
			"gem.skill.piercing",
		};
		for (int index = 0; index < supportCores.Length; index++)
		{
			actor.EquipSkillGem(index, supportCores[index]);
		}
	}

}
