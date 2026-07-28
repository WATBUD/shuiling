using Godot;
using System;
using System.Collections.Generic;

public partial class World
{
	private float _runtimeCleanupRemaining = WorldDropConfig.CleanupIntervalSeconds;
	private float _generationZeroGcRemaining = WorldDropConfig.GenerationZeroGcIntervalSeconds;
	private float _fullGcRemaining = WorldDropConfig.FullGcIntervalSeconds;
	private int _nodesQueuedSinceLastGc;
	private readonly List<WorldDrop> _runtimeDropScratch = new();

	private void UpdateRuntimeCleanup(float step)
	{
		_runtimeCleanupRemaining -= step;
		_generationZeroGcRemaining -= step;
		_fullGcRemaining -= step;

		if (_runtimeCleanupRemaining <= 0.0f)
		{
			_runtimeCleanupRemaining = WorldDropConfig.CleanupIntervalSeconds;
			SweepWorldDrops();
		}

		// Collect only the young managed generation during normal play. This is cheap
		// and clears wrappers left behind by freed short-lived effects/projectiles.
		if (_generationZeroGcRemaining <= 0.0f)
		{
			_generationZeroGcRemaining = WorldDropConfig.GenerationZeroGcIntervalSeconds;
			if (_nodesQueuedSinceLastGc > 0)
			{
				GC.Collect(0, GCCollectionMode.Optimized, false, false);
				_nodesQueuedSinceLastGc = 0;
			}
		}

		// A full collection is deliberately rare so cleanup cannot become a recurring
		// combat hitch during effect-heavy encounters.
		if (_fullGcRemaining <= 0.0f)
		{
			_fullGcRemaining = WorldDropConfig.FullGcIntervalSeconds;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false, false);
		}
	}

	private void SweepWorldDrops()
	{
		_runtimeDropScratch.Clear();
		foreach (Node node in GetTree().GetNodesInGroup("world_drops"))
		{
			if (node is not WorldDrop drop || !IsInstanceValid(drop) || drop.IsQueuedForDeletion())
			{
				continue;
			}

			// Independent failsafe in case a drop's own process was paused or disabled.
			if (drop.AgeSeconds >= Mathf.Max(drop.LifetimeSeconds, 1.0f))
			{
				drop.QueueFree();
				_nodesQueuedSinceLastGc++;
				continue;
			}

			_runtimeDropScratch.Add(drop);
		}

		int overflow = _runtimeDropScratch.Count - WorldDropConfig.MaximumActiveDrops;
		if (overflow <= 0)
		{
			return;
		}

		// Preserve the newest loot and retire the oldest excess drops first.
		_runtimeDropScratch.Sort((left, right) => right.AgeSeconds.CompareTo(left.AgeSeconds));
		for (int index = 0; index < overflow; index++)
		{
			WorldDrop drop = _runtimeDropScratch[index];
			if (IsInstanceValid(drop) && !drop.IsQueuedForDeletion())
			{
				drop.QueueFree();
				_nodesQueuedSinceLastGc++;
			}
		}
	}
}
