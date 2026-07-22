using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	public bool TryHireContractCompanion(ContractCompanionOffer offer)
	{
		if (!_contractCompanionOffers.Contains(offer))
		{
			return false;
		}

		if (Gold < offer.Cost)
		{
			PostSystemMessage(LocaleText.F("system.mercenary.not_enough_gold", offer.Cost, Gold), new Color(1.0f, 0.62f, 0.48f));
			return false;
		}

		if (GetParent() is not World world)
		{
			return false;
		}

		SimpleActor actor = world.SpawnContractCompanion(offer);
		Gold = Mathf.Max(Gold - offer.Cost, 0);
		PostSystemMessage(LocaleText.F("system.mercenary.hired", LocaleText.T(offer.NameKey), offer.Cost, Gold), new Color(1.0f, 0.86f, 0.46f), GameMessageChannel.Party);
		_contractCompanionOffers.Remove(offer);
		RecruitNpc(actor);
		_inventoryPanel.RefreshAll();
		_mercenaryShopPanel.RefreshAll();
		return true;
	}

	public bool TryRefreshMercenaryOffersManually()
	{
		if (Gold < MercenaryRefreshCost)
		{
			PostSystemMessage(LocaleText.F("system.mercenary.refresh_not_enough_gold", MercenaryRefreshCost, Gold), new Color(1.0f, 0.62f, 0.48f));
			return false;
		}

		Gold -= MercenaryRefreshCost;
		GenerateMercenaryOffers();
		PostSystemMessage(LocaleText.F("system.mercenary.refreshed", MercenaryRefreshCost, Gold), new Color(0.82f, 0.94f, 1.0f));
		_inventoryPanel.RefreshAll();
		_mercenaryShopPanel.RefreshAll();
		return true;
	}

	public string GetMercenaryRefreshCountdownText()
	{
		double remaining = Mathf.Max((float)(_mercenaryNextRefreshUnix - Time.GetUnixTimeFromSystem()), 0.0f);
		int totalSeconds = Mathf.CeilToInt((float)remaining);
		int hours = totalSeconds / 3600;
		int minutes = (totalSeconds % 3600) / 60;
		int seconds = totalSeconds % 60;
		return LocaleText.F("mercenary.refresh.countdown", hours, minutes, seconds);
	}

	private void EnsureMercenaryOffers()
	{
		// Only generate on first init or when the refresh time is due — do NOT
		// refill just because the list was emptied by hiring. Bought-out slots
		// stay empty until the timer elapses or the player pays to refresh.
		if (_mercenaryNextRefreshUnix <= 0.0 || Time.GetUnixTimeFromSystem() >= _mercenaryNextRefreshUnix)
		{
			GenerateMercenaryOffers();
		}
	}

	private void UpdateMercenaryOfferRefresh()
	{
		if (_mercenaryNextRefreshUnix > 0.0 && Time.GetUnixTimeFromSystem() >= _mercenaryNextRefreshUnix)
		{
			GenerateMercenaryOffers();
			PostSystemMessage(LocaleText.T("system.mercenary.auto_refreshed"), new Color(0.82f, 0.94f, 1.0f));
		}
	}

	private void GenerateMercenaryOffers()
	{
		_contractCompanionOffers.Clear();
		var available = new List<ContractCompanionOffer>(ContractCompanionOfferTemplates);
		for (int index = 0; index < MercenaryOfferCount; index++)
		{
			if (available.Count == 0)
			{
				available.AddRange(ContractCompanionOfferTemplates);
			}

			int templateIndex = _mercenaryRng.RandiRange(0, available.Count - 1);
			ContractCompanionOffer template = available[templateIndex];
			available.RemoveAt(templateIndex);
			_contractCompanionOffers.Add(CreateRandomMercenaryOffer(template, index));
		}

		_mercenaryNextRefreshUnix = Time.GetUnixTimeFromSystem() + MercenaryRefreshSeconds;
	}

	private ContractCompanionOffer CreateRandomMercenaryOffer(ContractCompanionOffer template, int index)
	{
		// Hired mercenaries start at level 1 (they grow with the player).
		int level = 1;
		float quality = (float)_mercenaryRng.RandfRange(0.88f, 1.22f);
		int maxHealth = Mathf.RoundToInt((template.MaxHealth + level * 17) * quality);
		int attack = Mathf.RoundToInt((template.Attack + level * 3) * quality);
		int defense = Mathf.RoundToInt((template.Defense + level * 2) * quality);
		int cost = Mathf.RoundToInt((template.Cost + level * 38 + attack * 4 + defense * 3) * quality / 10.0f) * 10;
		string id = $"{template.Id}.{Time.GetTicksMsec()}.{index}.{_mercenaryRng.Randi()}";
		return template with
		{
			Id = id,
			Level = level,
			Cost = Mathf.Clamp(cost, 160, 720),
			MaxHealth = Mathf.Max(maxHealth, 80),
			Attack = Mathf.Max(attack, 8),
			Defense = Mathf.Max(defense, 5),
		};
	}

	private void RestoreMercenaryOffers(PlayerSaveData data)
	{
		_contractCompanionOffers.Clear();
		foreach (MercenaryOfferSaveData offer in data.MercenaryOffers)
		{
			if (offer.Cost <= 0 || string.IsNullOrWhiteSpace(offer.NameKey))
			{
				continue;
			}

			bool isLegacyMender = offer.NameKey == "name.mercenary.mender"
				|| offer.Id.StartsWith("mercenary.offer.mender", System.StringComparison.Ordinal);
			_contractCompanionOffers.Add(new ContractCompanionOffer(
				isLegacyMender ? offer.Id.Replace("mercenary.offer.mender", "mercenary.offer.arcane_healer", System.StringComparison.Ordinal) : offer.Id,
				isLegacyMender ? "name.mercenary.arcane_healer" : offer.NameKey,
				offer.RoleNameKey,
				offer.CombatRole,
				isLegacyMender ? "mercenary.summary.arcane_healer" : offer.SummaryKey,
				Mathf.Max(offer.Level, 1),
				Mathf.Max(offer.Cost, 1),
				Mathf.Max(offer.MaxHealth, 1),
				Mathf.Max(offer.Attack, 1),
				Mathf.Max(offer.Defense, 0)
			));
		}

		_mercenaryNextRefreshUnix = data.MercenaryNextRefreshUnix;
		EnsureMercenaryOffers();
	}

	private SimpleActor? GetNearestMercenaryBroker()
	{
		if (!IsInCityMap())
		{
			return null;
		}

		SimpleActor? nearest = null;
		float bestDistance = MercenaryBrokerInteractRange;
		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is not SimpleActor actor || !IsMercenaryBroker(actor) || !actor.IsActiveWorldTarget)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance <= bestDistance)
			{
				nearest = actor;
				bestDistance = distance;
			}
		}

		return nearest;
	}

	private static bool IsMercenaryBroker(SimpleActor actor)
	{
		return actor.DisplayName == "name.npc.mercenary_broker";
	}

}
