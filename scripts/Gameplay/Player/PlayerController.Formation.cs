using Godot;
using System.Collections.Generic;

public partial class PlayerController
{
	private const int FormationGridSideLength = 5;
	private const int FormationCenterSlotIndex = 12;
	private const float FormationMinCompanionDistance = 3.6f;
	private const int FormationRingSlotCount = 8;
	private const float FormationRingSpacing = 1.75f;

	private static readonly int[] FormationFillOrder =
	{
		7, 11, 13, 17,
		6, 8, 16, 18,
		2, 10, 14, 22,
		1, 3, 5, 9, 15, 19, 21, 23,
		0, 4, 20, 24,
	};
	private static readonly int[] FormationFrontRow = { 6, 7, 8 };
	private static readonly int[] FormationBackRow = { 16, 17, 18 };

	private readonly Dictionary<int, SimpleActor> _formationActorsBySlot = new();
	private readonly Dictionary<SimpleActor, int> _formationSlotsByActor = new();

	public int FormationGridSide => FormationGridSideLength;
	public int FormationPlayerSlotIndex => FormationCenterSlotIndex;
	public int FormationAssignedCount => _formationSlotsByActor.Count;

	public SimpleActor? GetFormationActor(int slotIndex)
	{
		if (!IsValidFormationSlot(slotIndex) || !_formationActorsBySlot.TryGetValue(slotIndex, out SimpleActor? actor))
		{
			return null;
		}

		if (!IsInstanceValid(actor) || !actor.IsCaptured || !actor.IsInActiveParty)
		{
			_formationActorsBySlot.Remove(slotIndex);
			_formationSlotsByActor.Remove(actor);
			return null;
		}

		return actor;
	}

	public int GetFormationSlot(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !_formationSlotsByActor.TryGetValue(actor, out int slotIndex))
		{
			return -1;
		}

		return GetFormationActor(slotIndex) == actor ? slotIndex : -1;
	}

	public bool CanAssignCompanionToFormation(SimpleActor actor, int slotIndex)
	{
		if (!IsInstanceValid(actor) || !actor.IsCaptured || !IsValidCompanionFormationSlot(slotIndex))
		{
			return false;
		}

		if (_activeParty.Contains(actor))
		{
			return true;
		}

		if (_activeParty.Count < ActivePartyLimit)
		{
			return true;
		}

		SimpleActor? target = GetFormationActor(slotIndex);
		return target != null && target != actor;
	}

	public bool AssignCompanionToFormation(SimpleActor actor, int slotIndex)
	{
		if (!CanAssignCompanionToFormation(actor, slotIndex))
		{
			return false;
		}

		SimpleActor? targetBeforeDeploy = GetFormationActor(slotIndex);
		if (!_activeParty.Contains(actor) && _activeParty.Count >= ActivePartyLimit && targetBeforeDeploy != null && targetBeforeDeploy != actor)
		{
			StoreCompanion(targetBeforeDeploy);
		}

		if (!_activeParty.Contains(actor) && !DeployCompanion(actor, false))
		{
			return false;
		}

		int previousSlot = GetFormationSlot(actor);
		SimpleActor? target = GetFormationActor(slotIndex);
		if (target == actor)
		{
			RefreshFormationViews();
			return true;
		}

		if (previousSlot >= 0)
		{
			_formationActorsBySlot.Remove(previousSlot);
		}

		if (target != null)
		{
			_formationSlotsByActor.Remove(target);
			if (previousSlot >= 0)
			{
				SetFormationAssignment(target, previousSlot);
			}

			target.OnFormationLayoutChanged();
		}

		SetFormationAssignment(actor, slotIndex);
		actor.OnFormationLayoutChanged();
		RecalculateFormationBonuses();
		RefreshFormationViews();
		return true;
	}

	public bool ClearFormationSlot(int slotIndex)
	{
		if (!IsValidCompanionFormationSlot(slotIndex))
		{
			return false;
		}

		SimpleActor? actor = GetFormationActor(slotIndex);
		if (actor == null)
		{
			return false;
		}

		ClearFormationAssignment(actor);
		actor.OnFormationLayoutChanged();
		RecalculateFormationBonuses();
		RefreshFormationViews();
		return true;
	}

	public Vector3 GetFormationLocalOffset(SimpleActor actor)
	{
		int slotIndex = GetFormationSlot(actor);
		if (slotIndex >= 0)
		{
			return GetFormationSlotLocalOffset(slotIndex);
		}

		return GetFallbackFormationOffset(actor);
	}

	private void EnsureFormationSlotForActor(SimpleActor actor)
	{
		if (GetFormationSlot(actor) >= 0)
		{
			return;
		}

		int slotIndex = FindFirstOpenFormationSlot();
		if (slotIndex >= 0)
		{
			SetFormationAssignment(actor, slotIndex);
		}
	}

	private int FindFirstOpenFormationSlot()
	{
		foreach (int slotIndex in FormationFillOrder)
		{
			if (IsValidCompanionFormationSlot(slotIndex) && GetFormationActor(slotIndex) == null)
			{
				return slotIndex;
			}
		}

		return -1;
	}

	private void SetFormationAssignment(SimpleActor actor, int slotIndex)
	{
		if (!IsValidCompanionFormationSlot(slotIndex))
		{
			return;
		}

		ClearFormationAssignment(actor);
		if (GetFormationActor(slotIndex) is SimpleActor previousActor)
		{
			_formationSlotsByActor.Remove(previousActor);
		}

		_formationActorsBySlot[slotIndex] = actor;
		_formationSlotsByActor[actor] = slotIndex;
	}

	public void RecalculateFormationBonuses()
	{
		bool tankFrontAura = AreSlotsFilledByRole(FormationFrontRow, "Tank");
		bool rangedBackAura = AreSlotsFilledByRole(FormationBackRow, "Ranged");
		foreach (KeyValuePair<int, SimpleActor> entry in _formationActorsBySlot)
		{
			SimpleActor actor = entry.Value;
			if (!IsInstanceValid(actor) || !actor.IsCaptured || !actor.IsInActiveParty)
			{
				continue;
			}

			int adjacentTanks = 0;
			int adjacentSupports = 0;
			int sameElementNeighbors = 0;
			string elementId = BuildCatalog.GetAttributeGem(actor.BuildLoadout.AttributeGemId).ElementId;
			foreach (int neighborSlot in GetAdjacentSlots(entry.Key))
			{
				SimpleActor? neighbor = GetFormationActor(neighborSlot);
				if (neighbor == null)
				{
					continue;
				}

				adjacentTanks += neighbor.CombatRole == "Tank" ? 1 : 0;
				adjacentSupports += neighbor.CombatRole == "Support" ? 1 : 0;
				string neighborElement = BuildCatalog.GetAttributeGem(neighbor.BuildLoadout.AttributeGemId).ElementId;
				sameElementNeighbors += elementId != "physical" && neighborElement == elementId ? 1 : 0;
			}

			float attackMultiplier = 1.0f + Mathf.Min(sameElementNeighbors, 3) * 0.08f;
			float defenseMultiplier = tankFrontAura ? 1.12f : 1.0f;
			float cooldownMultiplier = Mathf.Max(1.0f - adjacentSupports * 0.08f, 0.80f);
			float incomingMultiplier = Mathf.Max(1.0f - adjacentTanks * 0.10f, 0.80f);
			float rangeBonus = rangedBackAura ? 1.25f : 0.0f;
			var bonuses = new List<string>();
			if (sameElementNeighbors > 0) bonuses.Add(LocaleText.T("formation.bonus.resonance"));
			if (adjacentTanks > 0) bonuses.Add(LocaleText.T("formation.bonus.guard"));
			if (adjacentSupports > 0) bonuses.Add(LocaleText.T("formation.bonus.support"));
			if (tankFrontAura) bonuses.Add(LocaleText.T("formation.bonus.frontline"));
			if (rangedBackAura) bonuses.Add(LocaleText.T("formation.bonus.backline"));
			actor.SetFormationBonuses(attackMultiplier, defenseMultiplier, cooldownMultiplier, incomingMultiplier, rangeBonus, string.Join(" / ", bonuses));
		}
	}

	private bool AreSlotsFilledByRole(int[] slots, string role)
	{
		foreach (int slot in slots)
		{
			if (GetFormationActor(slot)?.CombatRole != role)
			{
				return false;
			}
		}
		return true;
	}

	private static IEnumerable<int> GetAdjacentSlots(int slotIndex)
	{
		int row = slotIndex / FormationGridSideLength;
		int column = slotIndex % FormationGridSideLength;
		for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
		{
			for (int columnOffset = -1; columnOffset <= 1; columnOffset++)
			{
				if ((rowOffset == 0 && columnOffset == 0) || row + rowOffset < 0 || row + rowOffset >= FormationGridSideLength || column + columnOffset < 0 || column + columnOffset >= FormationGridSideLength)
				{
					continue;
				}
				yield return (row + rowOffset) * FormationGridSideLength + column + columnOffset;
			}
		}
	}

	private void ClearFormationAssignment(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !_formationSlotsByActor.TryGetValue(actor, out int slotIndex))
		{
			return;
		}

		_formationSlotsByActor.Remove(actor);
		if (_formationActorsBySlot.TryGetValue(slotIndex, out SimpleActor? assignedActor) && assignedActor == actor)
		{
			_formationActorsBySlot.Remove(slotIndex);
		}
	}

	private bool IsValidFormationSlot(int slotIndex)
	{
		return slotIndex >= 0 && slotIndex < FormationGridSideLength * FormationGridSideLength;
	}

	private bool IsValidCompanionFormationSlot(int slotIndex)
	{
		return IsValidFormationSlot(slotIndex) && slotIndex != FormationCenterSlotIndex;
	}

	private Vector3 GetFormationSlotLocalOffset(int slotIndex)
	{
		int orderIndex = System.Array.IndexOf(FormationFillOrder, slotIndex);
		if (orderIndex < 0)
		{
			orderIndex = Mathf.Max(slotIndex - (slotIndex > FormationCenterSlotIndex ? 1 : 0), 0);
		}

		return GetFormationRingOffset(orderIndex);
	}

	private Vector3 GetFallbackFormationOffset(SimpleActor actor)
	{
		int index = Mathf.Max(_activeParty.IndexOf(actor), 0);
		return GetFormationRingOffset(index);
	}

	private static Vector3 GetFormationRingOffset(int orderIndex)
	{
		int ring = Mathf.Clamp(orderIndex / FormationRingSlotCount, 0, 2);
		int ringSlot = orderIndex % FormationRingSlotCount;
		float radius = FormationMinCompanionDistance + ring * FormationRingSpacing;
		float angle = Mathf.Pi * 0.5f - ringSlot * (Mathf.Pi * 2.0f / FormationRingSlotCount);
		return new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
	}

	private void RefreshFormationViews()
	{
		if (_partyPanel != null)
		{
			_partyPanel.RefreshParty();
		}

		if (_formationPanel != null)
		{
			_formationPanel.RefreshAll();
		}
	}
}
