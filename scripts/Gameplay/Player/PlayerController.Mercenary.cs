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
		actor.ClearBuildLoadout();
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
		if (Gold < RecruitmentConfig.ManualRefreshGoldCost)
		{
			PostSystemMessage(LocaleText.F("system.mercenary.refresh_not_enough_gold", RecruitmentConfig.ManualRefreshGoldCost, Gold), new Color(1.0f, 0.62f, 0.48f));
			return false;
		}

		if (_contractCompanionOffers.Count >= RecruitmentConfig.OfferCapacityPerCategory)
		{
			return false;
		}

		Gold -= RecruitmentConfig.ManualRefreshGoldCost;
		AddOneMercenaryOffer();
		PostSystemMessage(LocaleText.F("system.mercenary.refreshed", RecruitmentConfig.ManualRefreshGoldCost, Gold), new Color(0.82f, 0.94f, 1.0f));
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
		// 每 3 小時累積 1 隻到上限；首次初始化先給 1 隻並排定下一次。
		if (_mercenaryNextRefreshUnix <= 0.0)
		{
			AddOneMercenaryOffer();
			_mercenaryNextRefreshUnix = Time.GetUnixTimeFromSystem() + RecruitmentConfig.RefillSeconds;
			return;
		}

		AdvanceMercenaryOffers(false);
	}

	private void UpdateMercenaryOfferRefresh()
	{
		AdvanceMercenaryOffers(true);
	}

	private void AdvanceMercenaryOffers(bool announce)
	{
		if (_mercenaryNextRefreshUnix <= 0.0)
		{
			return;
		}

		double now = Time.GetUnixTimeFromSystem();
		bool added = false;
		while (now >= _mercenaryNextRefreshUnix && _contractCompanionOffers.Count < RecruitmentConfig.OfferCapacityPerCategory)
		{
			AddOneMercenaryOffer();
			added = true;
			_mercenaryNextRefreshUnix += RecruitmentConfig.RefillSeconds;
		}

		// 已達上限：把下一次時間貼齊到未來，避免一次灌爆或無限迴圈。
		if (_contractCompanionOffers.Count >= RecruitmentConfig.OfferCapacityPerCategory)
		{
			_mercenaryNextRefreshUnix = now + RecruitmentConfig.RefillSeconds;
		}

		if (added)
		{
			if (announce)
			{
				PostSystemMessage(LocaleText.T("system.mercenary.auto_refreshed"), new Color(0.82f, 0.94f, 1.0f));
			}

			if (_mercenaryShopPanel != null && _mercenaryShopPanel.Visible)
			{
				_mercenaryShopPanel.RefreshAll();
			}
		}
	}

	private void AddOneMercenaryOffer()
	{
		if (_contractCompanionOffers.Count >= RecruitmentConfig.OfferCapacityPerCategory)
		{
			return;
		}

		ContractCompanionOffer template = ContractCompanionOfferTemplates[_mercenaryRng.RandiRange(0, ContractCompanionOfferTemplates.Length - 1)];
		_contractCompanionOffers.Add(CreateRandomMercenaryOffer(template, _contractCompanionOffers.Count));
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
			if (_contractCompanionOffers.Count >= RecruitmentConfig.OfferCapacityPerCategory)
			{
				break;
			}
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
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!IsMercenaryBroker(actor) || !actor.IsActiveWorldTarget)
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
