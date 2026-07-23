using Godot;
using System.Collections.Generic;

// Monster cards (卡片系統) — every monster drops its own exclusive name card, and
// the album keeps at most one per model (deduped by canonical model key). Cards
// grant a global team stat bonus that scales with how many are collected, and
// can be handed over to certain NPCs to complete their quests.
public partial class PlayerController
{
	private readonly HashSet<string> _ownedCards = new();

	// +1% ATK / DEF / HP to every deployed companion per unique card owned.
	private const float CardBonusPerCard = 0.01f;

	// Affinity granted when a recruit NPC accepts the specific card it collects.
	private const int NpcCardExchangeAffinityReward = 40;

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

	public int OwnedCardCount => _ownedCards.Count;

	public bool HasCard(string cardKey) => !string.IsNullOrEmpty(cardKey) && _ownedCards.Contains(cardKey);

	public IReadOnlyCollection<string> OwnedCards => _ownedCards;

	public List<string> GetOwnedCardKeys()
	{
		var keys = new List<string>(_ownedCards);
		keys.Sort((a, b) => string.Compare(ExternalModelLibrary.LocalizedCardName(a), ExternalModelLibrary.LocalizedCardName(b), System.StringComparison.CurrentCulture));
		return keys;
	}

	// Current collection multiplier (1.0 = no cards). Shown in the album panel.
	public float CardCollectionMultiplier => 1.0f + _ownedCards.Count * CardBonusPerCard;

	// Award the defeated monster's card (one per model). No-op if already owned.
	public void AwardMonsterCard(SimpleActor monster)
	{
		if (monster == null || !IsInstanceValid(monster))
		{
			return;
		}

		string key = monster.GetCardKey();
		if (string.IsNullOrWhiteSpace(key) || _ownedCards.Contains(key))
		{
			return;
		}

		_ownedCards.Add(key);
		string name = ExternalModelLibrary.LocalizedCardName(key);
		PostSystemMessage(LocaleText.F("system.card.obtained", name), new Color(0.62f, 0.86f, 1.0f));
		RefreshCardCollectionBonus();
		UpdateCardAlbumHud();
		if (_cardAlbumPanel != null && IsInstanceValid(_cardAlbumPanel) && _cardAlbumPanel.Visible)
		{
			_cardAlbumPanel.RefreshAll();
		}
	}

	// Hand a card over (NPC quest exchange). Losing it lowers the team bonus.
	public bool TryConsumeCard(string cardKey)
	{
		if (string.IsNullOrEmpty(cardKey) || !_ownedCards.Remove(cardKey))
		{
			return false;
		}

		RefreshCardCollectionBonus();
		UpdateCardAlbumHud();
		if (_cardAlbumPanel != null && IsInstanceValid(_cardAlbumPanel) && _cardAlbumPanel.Visible)
		{
			_cardAlbumPanel.RefreshAll();
		}

		return true;
	}

	// Re-apply the collection buff to every deployed companion.
	public void RefreshCardCollectionBonus()
	{
		float multiplier = CardCollectionMultiplier;
		foreach (SimpleActor actor in _activeParty)
		{
			if (IsInstanceValid(actor))
			{
				actor.SetCardCollectionBonus(multiplier, multiplier, multiplier);
			}
		}
	}

	// --- save round-trip ------------------------------------------------------

	private List<string> ExportCards()
	{
		return new List<string>(_ownedCards);
	}

	private void RestoreCards(PlayerSaveData data)
	{
		_ownedCards.Clear();
		if (data.OwnedCards != null)
		{
			foreach (string key in data.OwnedCards)
			{
				if (!string.IsNullOrWhiteSpace(key))
				{
					_ownedCards.Add(key);
				}
			}
		}

		RefreshCardCollectionBonus();
		UpdateCardAlbumHud();
		if (_cardAlbumPanel != null && IsInstanceValid(_cardAlbumPanel))
		{
			_cardAlbumPanel.RefreshAll();
		}
	}
}
