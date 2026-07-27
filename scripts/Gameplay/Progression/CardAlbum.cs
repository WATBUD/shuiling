using System;
using System.Collections.Generic;

// Owns the player's monster-card collection (卡片系統): the set of unique cards
// and the team stat multiplier they grant. This is a pure state + rules
// component — no UI, no combat, no Godot node. PlayerController.Cards holds one
// and wires the side-effects (system messages, HUD refresh, applying the buff to
// companions), so the collection's data and rules live in exactly one place.
public sealed class CardAlbum
{
	// +1% ATK / DEF / HP to every deployed companion per unique card owned.
	private const float BonusPerCard = 0.01f;

	private readonly HashSet<string> _owned = new();

	public int Count => _owned.Count;

	// 1.0 = no cards. Shown in the album panel and applied to companions.
	public float CollectionMultiplier => 1.0f + _owned.Count * BonusPerCard;

	public IReadOnlyCollection<string> Owned => _owned;

	public bool Has(string cardKey) => !string.IsNullOrEmpty(cardKey) && _owned.Contains(cardKey);

	// Owned card keys sorted by localized name (for the album display).
	public List<string> GetSortedKeys()
	{
		var keys = new List<string>(_owned);
		keys.Sort((a, b) => string.Compare(
			ExternalModelLibrary.LocalizedCardName(a),
			ExternalModelLibrary.LocalizedCardName(b),
			StringComparison.CurrentCulture));
		return keys;
	}

	// Add a card (one per model). Returns true only if it was newly added.
	public bool Add(string cardKey)
	{
		return !string.IsNullOrWhiteSpace(cardKey) && _owned.Add(cardKey);
	}

	// Remove a card (NPC quest exchange). Returns true if it was present.
	public bool Remove(string cardKey)
	{
		return !string.IsNullOrEmpty(cardKey) && _owned.Remove(cardKey);
	}

	public List<string> Export() => new(_owned);

	public void Restore(IEnumerable<string>? keys)
	{
		_owned.Clear();
		if (keys == null)
		{
			return;
		}

		foreach (string key in keys)
		{
			if (!string.IsNullOrWhiteSpace(key))
			{
				_owned.Add(key);
			}
		}
	}
}
