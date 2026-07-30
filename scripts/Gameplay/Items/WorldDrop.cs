using Godot;
using System.Collections.Generic;

// A physical loot pickup in the world. WorldDrop is pure gameplay logic now:
// it holds the drop's data, drives its lifetime and pickup, and delegates all
// visuals to an authored scene (see DropVisual + the *Drop.tscn files) that it
// instantiates as a child. Creation and recycling go through WorldDropFactory /
// WorldDropPool — do not `new` one directly.
public partial class WorldDrop : Node3D
{
	public enum DropKind
	{
		Gold,
		Item,
		Card,
	}

	private static readonly Dictionary<DropKind, string> ScenePaths = new()
	{
		{ DropKind.Gold, "res://assets/scenes/props/drops/GoldDrop.tscn" },
		{ DropKind.Item, "res://assets/scenes/props/drops/ItemDrop.tscn" },
		{ DropKind.Card, "res://assets/scenes/props/drops/CardDrop.tscn" },
	};

	private static readonly Dictionary<DropKind, PackedScene?> SceneCache = new();

	private static readonly List<WorldDrop> ActiveDropRegistry = new();
	public static IReadOnlyList<WorldDrop> ActiveDrops => ActiveDropRegistry;

	private static readonly Color CardTint = new(1.0f, 0.82f, 0.34f, 1.0f);

	[Export] public string ItemId { get; set; } = string.Empty;
	[Export] public int Amount { get; set; } = 1;
	[Export] public int GoldAmount { get; set; }
	[Export] public string CardKey { get; set; } = string.Empty;
	[Export] public float PickupRadius { get; set; } = WorldDropConfig.PickupRadius;
	[Export] public float LifetimeSeconds { get; set; } = WorldDropConfig.LifetimeSeconds;

	private DropKind _kind;
	private DropVisual? _visual;
	private float _age;
	private bool _registered;

	public DropKind Kind => _kind;
	public bool IsGoldDrop => _kind == DropKind.Gold;
	public bool IsCardDrop => _kind == DropKind.Card;
	public bool IsCollected { get; private set; }
	public float AgeSeconds => _age;

	// The pool creates instances through here so the kind is fixed for the
	// lifetime of the node (its authored visual scene never changes).
	public static WorldDrop CreateForKind(DropKind kind)
	{
		return new WorldDrop { _kind = kind };
	}

	public override void _ExitTree()
	{
		// Failsafe: if the node is hard-freed (e.g. scene teardown) while active,
		// make sure it leaves the registry. Normal recycling unregisters first,
		// so this is idempotent.
		Unregister();
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		if (_age >= LifetimeSeconds)
		{
			Recycle();
		}
	}

	// Brings a freshly-acquired (or reused) drop to life: builds/reuses its
	// visual, applies the dynamic label + tint, and registers it for collection.
	public void Activate()
	{
		_age = 0.0f;
		IsCollected = false;
		Visible = true;
		SetProcess(true);

		EnsureVisual();
		_visual?.Configure(GetDisplayText(), GetTint());
		_visual?.OnActivated();
		Register();
	}

	private void EnsureVisual()
	{
		if (_visual != null && GodotObject.IsInstanceValid(_visual))
		{
			return;
		}

		PackedScene? scene = LoadScene(_kind);
		if (scene?.Instantiate() is DropVisual visual)
		{
			visual.Name = "Visual";
			AddChild(visual);
			_visual = visual;
		}
	}

	private static PackedScene? LoadScene(DropKind kind)
	{
		if (SceneCache.TryGetValue(kind, out PackedScene? cached))
		{
			return cached;
		}

		string path = ScenePaths[kind];
		PackedScene? scene = ResourceLoader.Exists(path)
			? ResourceLoader.Load<PackedScene>(path)
			: null;
		SceneCache[kind] = scene;
		return scene;
	}

	public bool TryCollect(PlayerController player)
	{
		if (IsCollected || !IsInstanceValid(player) || GlobalPosition.DistanceTo(player.GlobalPosition) > PickupRadius)
		{
			return false;
		}

		IsCollected = true;
		if (IsCardDrop)
		{
			player.AwardMonsterCardByKey(CardKey);
		}
		else if (IsGoldDrop)
		{
			player.AddGold(GoldAmount);
		}
		else if (!string.IsNullOrWhiteSpace(ItemId))
		{
			player.AddInventoryItem(ItemId, Mathf.Max(Amount, 1));
		}

		Recycle();
		return true;
	}

	// Returns the drop to the pool for reuse. Replaces the old QueueFree path so
	// a burst of loot doesn't churn node allocations and rebuild materials.
	public void Recycle()
	{
		_visual?.OnRecycled();
		Unregister();
		SetProcess(false);
		WorldDropPool.Release(this);
	}

	private void Register()
	{
		if (_registered)
		{
			return;
		}

		ActiveDropRegistry.Add(this);
		AddToGroup("world_drops");
		_registered = true;
	}

	private void Unregister()
	{
		if (!_registered)
		{
			return;
		}

		ActiveDropRegistry.Remove(this);
		if (IsInGroup("world_drops"))
		{
			RemoveFromGroup("world_drops");
		}

		_registered = false;
	}

	private Color GetTint()
	{
		return _kind switch
		{
			DropKind.Gold => DropPalette.Gold,
			DropKind.Card => CardTint,
			_ => DropPalette.ForItem(ItemId),
		};
	}

	private string GetDisplayText()
	{
		if (IsCardDrop)
		{
			return LocaleText.F("drop.card", ExternalModelLibrary.LocalizedCardName(CardKey));
		}

		if (IsGoldDrop)
		{
			return LocaleText.F("drop.gold", GoldAmount);
		}

		string name = MonsterLootCatalog.IsMonsterLoot(ItemId)
			? LocaleText.T(MonsterLootCatalog.GetNameKey(ItemId))
			: LocaleText.T(BuildCatalog.GetItemNameKey(ItemId));
		return Amount > 1 ? $"{name} x{Amount}" : name;
	}
}
