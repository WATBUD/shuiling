using Godot;
using System.Collections.Generic;

// Monster cards (卡片系統) — thin player-side adapter over the CardAlbum component.
// The album owns the data + rules; this file wires the side-effects that need the
// player/world (system messages, HUD refresh, applying the buff to companions).
public partial class PlayerController
{
	private readonly CardAlbum _cardAlbum = new();

	// Affinity granted when a recruit NPC accepts the specific card it collects.
	// The single card a given recruit NPC will accept, chosen deterministically
	// from the fixed named-card set so it's stable across sessions.
	public string GetNpcWantedCardKey(SimpleActor npc)
	{
		IReadOnlyList<string> keys = ExternalModelLibrary.KnownCardKeys;
		if (npc == null || keys.Count == 0)
		{
			return string.Empty;
		}

		int hash = 0;
		foreach (char c in npc.DisplayName)
		{
			hash = (hash * 31 + c) & 0x7fffffff;
		}

		return keys[hash % keys.Count];
	}

	public int OwnedCardCount => _cardAlbum.Count;

	public bool HasCard(string cardKey) => _cardAlbum.Has(cardKey);

	public IReadOnlyCollection<string> OwnedCards => _cardAlbum.Owned;

	public List<string> GetOwnedCardKeys() => _cardAlbum.GetSortedKeys();

	// Current collection multiplier (1.0 = no cards). Shown in the album panel.
	public float CardCollectionMultiplier => _cardAlbum.CollectionMultiplier;

	// Award the defeated monster's card (one per model). No-op if already owned.
	public void AwardMonsterCard(SimpleActor monster)
	{
		if (monster == null || !IsInstanceValid(monster))
		{
			return;
		}

		AwardMonsterCardByKey(monster.GetCardKey());
	}

	// Add a specific card to the album by its canonical key (used when a physical
	// card drop is picked up). No-op if empty or already owned.
	public bool AwardMonsterCardByKey(string key)
	{
		if (!_cardAlbum.Add(key))
		{
			return false;
		}

		string name = ExternalModelLibrary.LocalizedCardName(key);
		PostSystemMessage(LocaleText.F("system.card.obtained", name), new Color(0.62f, 0.86f, 1.0f));
		OnCardCollectionChanged(refreshHiddenPanel: false);
		return true;
	}

	// Hand a card over (NPC quest exchange). Losing it lowers the team bonus.
	public bool TryConsumeCard(string cardKey)
	{
		if (!_cardAlbum.Remove(cardKey))
		{
			return false;
		}

		OnCardCollectionChanged(refreshHiddenPanel: false);
		return true;
	}

	// Re-apply the collection buff to every deployed companion.
	public void RefreshCardCollectionBonus()
	{
		float multiplier = _cardAlbum.CollectionMultiplier;
		foreach (SimpleActor actor in _activeParty)
		{
			if (IsInstanceValid(actor))
			{
				actor.SetCardCollectionBonus(multiplier, multiplier, multiplier);
			}
		}
	}

	// Shared side-effects after the album changes: re-apply the buff and refresh
	// UI. refreshHiddenPanel rebuilds the album even when it's closed (used after a
	// save load); normal gains only rebuild it while it's open.
	private void OnCardCollectionChanged(bool refreshHiddenPanel)
	{
		RefreshCardCollectionBonus();
		UpdateCardAlbumHud();
		if (_cardAlbumPanel != null && IsInstanceValid(_cardAlbumPanel)
			&& (refreshHiddenPanel || _cardAlbumPanel.Visible))
		{
			_cardAlbumPanel.RefreshAll();
		}
	}

	// --- save round-trip ------------------------------------------------------

	private List<string> ExportCards() => _cardAlbum.Export();

	private void RestoreCards(PlayerSaveData data)
	{
		_cardAlbum.Restore(data.OwnedCards);
		OnCardCollectionChanged(refreshHiddenPanel: true);
	}
}
