using Godot;

// Town Portal Scroll (回城卷): an emergency-retreat consumable. Usable in the
// wild only when it's safe — not in a dungeon/cave, not during a boss fight,
// not right after taking damage, and with no monsters bearing down on you.
// Returns the party to the city. New characters start with 5.
public partial class PlayerController
{
	private const int StarterTownPortalScrolls = 5;
	private const ulong TownPortalCombatBlockMsec = 5000;

	private ulong _lastCombatMsec;

	// Called from ReceiveDamage — refreshes the "recently in combat" window.
	private void MarkRecentCombat()
	{
		_lastCombatMsec = Time.GetTicksMsec();
	}

	private void GrantStarterTownPortalScrolls()
	{
		AddInventoryItem(BuildCatalog.TownPortalScrollId, StarterTownPortalScrolls);
	}

	// Hotkey entry point (guarded so it doesn't fire while a panel is open).
	private void TryUseTownPortalScroll()
	{
		if (_settingsPanel.Visible || _partyPanel.Visible || _inventoryPanel.Visible
			|| _formationPanel.Visible || _npcQuestDialog.Visible
			|| (_mapTravelDialog != null && _mapTravelDialog.Visible))
		{
			return;
		}

		UseTownPortalScroll();
	}

	// Public so the inventory panel can trigger it too. Returns true on success.
	public bool UseTownPortalScroll()
	{
		if (GetInventoryCount(BuildCatalog.TownPortalScrollId) <= 0)
		{
			PostSystemMessage(LocaleText.T("item.town_portal.none"), new Color(1.0f, 0.72f, 0.5f));
			return false;
		}

		if (!CanUseTownPortalScroll(out string reasonKey))
		{
			// Show WHY it's blocked as a floating tip above the player + log.
			string reason = LocaleText.T(reasonKey);
			SpawnFloatingEffect(reason, new Color(1.0f, 0.72f, 0.5f, 0.95f), 1.1f, 0.6f);
			PostSystemMessage(reason, new Color(1.0f, 0.72f, 0.5f));
			return false;
		}

		if (!TryConsumeInventoryItem(BuildCatalog.TownPortalScrollId))
		{
			return false;
		}

		PostSystemMessage(LocaleText.T("item.town_portal.used"), new Color(0.72f, 0.92f, 1.0f));
		if (GetParent() is World world)
		{
			world.RequestMapTravel("city");
		}

		return true;
	}

	// Returns whether the scroll can be used, plus a locale key explaining why
	// not. Boss "fight" = an active combat window (NotifyBossCombat), NOT merely
	// a boss existing on the map. "In combat" = recently took damage — a wild
	// map always has wandering monsters, so proximity alone must not block.
	private bool CanUseTownPortalScroll(out string reasonKey)
	{
		reasonKey = "item.town_portal.blocked";
		if (GetParent() is not World world)
		{
			return false;
		}

		string mapId = world.ActiveMapId;
		if (mapId == "city")
		{
			reasonKey = "item.town_portal.in_town";
			return false;
		}

		if (mapId.Contains("_cave_"))
		{
			reasonKey = "item.town_portal.in_dungeon";
			return false;
		}

		if (_bossHudCombatVisibleRemaining > 0.0f)
		{
			reasonKey = "item.town_portal.boss";
			return false;
		}

		if (Time.GetTicksMsec() - _lastCombatMsec < TownPortalCombatBlockMsec)
		{
			reasonKey = "item.town_portal.combat";
			return false;
		}

		return true;
	}
}
