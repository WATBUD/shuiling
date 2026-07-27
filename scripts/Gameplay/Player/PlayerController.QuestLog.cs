using Godot;
using System.Collections.Generic;

// 任務清單資料：把目前已接受的 NPC 招募任務整理成可顯示的進度條目，供 QuestLogPanel（Q 開啟）使用。
public partial class PlayerController
{
	public readonly struct QuestLogEntry
	{
		public string NpcName { get; init; }
		public string QuestItemName { get; init; }
		public int ItemCount { get; init; }
		public int ItemRequired { get; init; }
		public int Affinity { get; init; }
		public int AffinityRequired { get; init; }
		public string StatusKey { get; init; }
	}

	public List<QuestLogEntry> GetAcceptedQuestEntries()
	{
		var entries = new List<QuestLogEntry>();
		foreach (SimpleActor npc in _acceptedNpcQuests)
		{
			if (!IsInstanceValid(npc))
			{
				continue;
			}

			string questItemId = GetNpcQuestItemId(npc);
			int itemCount = GetInventoryCount(questItemId);
			bool delivered = _completedNpcQuests.Contains(npc);

			string statusKey;
			if (!delivered)
			{
				statusKey = itemCount >= NpcRecruitQuestItemCount
					? "quest.status.ready_deliver"
					: "quest.status.gathering";
			}
			else
			{
				statusKey = npc.Affinity >= NpcRecruitAffinityRequirement
					? "quest.status.ready_invite"
					: "quest.status.need_affinity";
			}

			entries.Add(new QuestLogEntry
			{
				NpcName = npc.LocalizedDisplayName,
				QuestItemName = GetInventoryItemDisplayName(questItemId),
				ItemCount = Mathf.Min(itemCount, NpcRecruitQuestItemCount),
				ItemRequired = NpcRecruitQuestItemCount,
				Affinity = npc.Affinity,
				AffinityRequired = NpcRecruitAffinityRequirement,
				StatusKey = statusKey,
			});
		}

		return entries;
	}
}
