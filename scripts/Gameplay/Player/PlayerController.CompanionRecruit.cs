using Godot;
using System.Collections.Generic;

// 夥伴招募所「夥伴」分頁：與傭兵相同節奏，每 6 小時累積 1 隻 1 等候選夥伴到上限。
public partial class PlayerController
{
	private void EnsureCompanionRecruitOffers()
	{
		if (_companionRecruitNextRefreshUnix <= 0.0)
		{
			AddOneCompanionRecruitOffer();
			_companionRecruitNextRefreshUnix = Time.GetUnixTimeFromSystem() + MercenaryRefreshSeconds;
			return;
		}

		AdvanceCompanionRecruitOffers(false);
	}

	private void UpdateCompanionRecruitRefresh()
	{
		AdvanceCompanionRecruitOffers(true);
	}

	private void AdvanceCompanionRecruitOffers(bool announce)
	{
		if (_companionRecruitNextRefreshUnix <= 0.0)
		{
			return;
		}

		double now = Time.GetUnixTimeFromSystem();
		bool added = false;
		while (now >= _companionRecruitNextRefreshUnix && _companionRecruitOffers.Count < RecruitOfferCap)
		{
			AddOneCompanionRecruitOffer();
			added = true;
			_companionRecruitNextRefreshUnix += MercenaryRefreshSeconds;
		}

		if (_companionRecruitOffers.Count >= RecruitOfferCap)
		{
			_companionRecruitNextRefreshUnix = now + MercenaryRefreshSeconds;
		}

		if (added)
		{
			if (announce)
			{
				PostSystemMessage(LocaleText.T("system.recruit.companion_arrived"), new Color(0.64f, 1.0f, 0.82f), GameMessageChannel.Party);
			}

			if (_mercenaryShopPanel != null && _mercenaryShopPanel.Visible)
			{
				_mercenaryShopPanel.RefreshAll();
			}
		}
	}

	private void AddOneCompanionRecruitOffer()
	{
		if (_companionRecruitOffers.Count >= RecruitOfferCap)
		{
			return;
		}

		PetShopOffer template = PetShopOffers[_mercenaryRng.RandiRange(0, PetShopOffers.Length - 1)];
		string role = MonsterSpeciesCatalog.Current.GetDefaultRole(template.MonsterNameKey);
		int cost = Mathf.Clamp(template.Price, 120, 900);
		string id = $"companion.{template.MonsterNameKey}.{Time.GetTicksMsec()}.{_companionRecruitOffers.Count}.{_mercenaryRng.Randi()}";
		_companionRecruitOffers.Add(new ContractCompanionOffer(
			id,
			template.MonsterNameKey,
			string.Empty,
			role,
			string.Empty,
			1,
			cost,
			template.MaxHealth,
			template.Attack,
			template.Defense,
			"companion"));
	}

	// 招募一隻夥伴（消耗金幣，直接加入收藏／出戰），與購買寵物相同流程。
	public bool TryRecruitCompanion(ContractCompanionOffer offer)
	{
		if (!_companionRecruitOffers.Contains(offer))
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

		SimpleActor actor = world.SpawnPurchasedPet(offer.NameKey, offer.Level, offer.MaxHealth, offer.Attack, offer.Defense);
		Gold = Mathf.Max(Gold - offer.Cost, 0);
		actor.ClearBuildLoadout();
		if (!_capturedCollection.Contains(actor))
		{
			_capturedCollection.Add(actor);
		}

		actor.Capture(this);
		PostSystemMessage(LocaleText.F("system.recruit.companion_hired", actor.LocalizedDisplayName, offer.Cost, Gold), new Color(0.64f, 1.0f, 0.82f), GameMessageChannel.Party);

		if (_activeParty.Count < ActivePartyLimit)
		{
			DeployCompanion(actor, false);
		}
		else
		{
			actor.StoreInCollection();
		}

		_companionRecruitOffers.Remove(offer);
		_partyPanel.RefreshParty();
		_formationPanel.RefreshAll();
		_mercenaryShopPanel.RefreshAll();
		return true;
	}
}
