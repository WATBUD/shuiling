using Godot;

// Single entry point for spawning world loot. Callers describe *what* dropped;
// the factory handles pooling, parenting, placement and activation. This keeps
// drop-creation policy in one place instead of scattered `new WorldDrop`.
public static class WorldDropFactory
{
	// Drops rest just above the ground plane; keep this in sync with the spawn
	// height the loot callers previously hard-coded.
	private const float GroundY = 0.04f;

	public static WorldDrop SpawnGold(Node parent, Vector3 position, int goldAmount)
	{
		return Spawn(parent, position, WorldDrop.DropKind.Gold, drop =>
		{
			drop.GoldAmount = Mathf.Max(goldAmount, 0);
			drop.ItemId = string.Empty;
			drop.CardKey = string.Empty;
			drop.Amount = 1;
		});
	}

	public static WorldDrop SpawnItem(Node parent, Vector3 position, string itemId, int amount)
	{
		return Spawn(parent, position, WorldDrop.DropKind.Item, drop =>
		{
			drop.ItemId = itemId;
			drop.Amount = Mathf.Max(amount, 1);
			drop.GoldAmount = 0;
			drop.CardKey = string.Empty;
		});
	}

	public static WorldDrop SpawnCard(Node parent, Vector3 position, string cardKey)
	{
		return Spawn(parent, position, WorldDrop.DropKind.Card, drop =>
		{
			drop.CardKey = cardKey;
			drop.ItemId = string.Empty;
			drop.GoldAmount = 0;
			drop.Amount = 1;
		});
	}

	private static WorldDrop Spawn(Node parent, Vector3 position, WorldDrop.DropKind kind, System.Action<WorldDrop> applyData)
	{
		WorldDrop drop = WorldDropPool.Acquire(kind, parent);
		applyData(drop);
		parent.AddChild(drop);
		drop.GlobalPosition = new Vector3(position.X, GroundY, position.Z);
		drop.Activate();
		return drop;
	}
}
