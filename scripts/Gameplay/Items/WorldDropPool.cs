using Godot;
using System.Collections.Generic;

// Reuses WorldDrop nodes instead of freeing them on collect/expiry. A defeated
// boss can emit 8+ drops in a single frame and they are collected in quick
// bursts; churning node allocations there was the documented death-frame hitch.
//
// Idle instances are parked (hidden) under a holder node inside the current
// scene, so they are freed automatically on a scene change — no manual teardown
// and no dangling static references. All access is main-thread only (drops are
// spawned and collected from _Process), so no locking is required.
public static class WorldDropPool
{
	private const int MaxIdlePerKind = 48;
	private const int WarmItemCount = 8;
	private const int WarmGoldCount = 3;
	private const int WarmCardCount = 1;

	private static readonly Dictionary<WorldDrop.DropKind, Stack<WorldDrop>> Idle = new();
	private static Node? _holder;

	public static void Prewarm(Node context)
	{
		PrewarmKind(WorldDrop.DropKind.Item, WarmItemCount, context);
		PrewarmKind(WorldDrop.DropKind.Gold, WarmGoldCount, context);
		PrewarmKind(WorldDrop.DropKind.Card, WarmCardCount, context);
	}

	private static void PrewarmKind(WorldDrop.DropKind kind, int count, Node context)
	{
		var warmed = new List<WorldDrop>(count);
		for (int index = 0; index < count; index++)
		{
			WorldDrop drop = Acquire(kind, context);
			context.AddChild(drop);
			drop.WarmUp();
			warmed.Add(drop);
		}

		foreach (WorldDrop drop in warmed)
		{
			Release(drop);
		}
	}

	// Pulls a reusable drop of the requested kind, or creates one. The returned
	// node is detached from the tree; the caller adds it where it belongs.
	public static WorldDrop Acquire(WorldDrop.DropKind kind, Node context)
	{
		// Refresh the holder first so a scene change clears stale idle entries
		// before we hand one out.
		ResolveHolder(context);

		if (Idle.TryGetValue(kind, out Stack<WorldDrop>? stack))
		{
			while (stack.Count > 0)
			{
				WorldDrop candidate = stack.Pop();
				if (GodotObject.IsInstanceValid(candidate))
				{
					candidate.GetParent()?.RemoveChild(candidate);
					return candidate;
				}
			}
		}

		return WorldDrop.CreateForKind(kind);
	}

	// Parks a drop for reuse, or frees it if the pool is full / has nowhere to
	// live. Must be called while the drop is still inside the tree.
	public static void Release(WorldDrop drop)
	{
		if (!GodotObject.IsInstanceValid(drop))
		{
			return;
		}

		Node? holder = ResolveHolder(drop);
		drop.GetParent()?.RemoveChild(drop);

		if (holder == null)
		{
			drop.QueueFree();
			return;
		}

		if (!Idle.TryGetValue(drop.Kind, out Stack<WorldDrop>? stack))
		{
			stack = new Stack<WorldDrop>();
			Idle[drop.Kind] = stack;
		}

		if (stack.Count >= MaxIdlePerKind)
		{
			drop.QueueFree();
			return;
		}

		drop.Visible = false;
		holder.AddChild(drop);
		stack.Push(drop);
	}

	private static Node? ResolveHolder(Node context)
	{
		if (_holder != null && GodotObject.IsInstanceValid(_holder) && _holder.IsInsideTree())
		{
			return _holder;
		}

		SceneTree? tree = context.GetTree();
		Node? scene = tree?.CurrentScene;
		if (scene == null || !GodotObject.IsInstanceValid(scene))
		{
			return null;
		}

		// A new scene means every previously pooled instance was freed with the
		// old one; drop the stale references before creating a fresh holder.
		Idle.Clear();

		var holder = new Node { Name = "WorldDropPoolHolder" };
		scene.AddChild(holder);
		_holder = holder;
		return _holder;
	}
}
