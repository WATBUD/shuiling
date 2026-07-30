using Godot;
using System;
using System.Collections.Generic;

public partial class World
{
	private float _runtimeCleanupRemaining = WorldDropConfig.CleanupIntervalSeconds;
	private float _memoryPressureCheckRemaining = PerformanceConfig.MemoryPressureCheckIntervalSeconds;
	private float _emergencyFullGcCooldownRemaining;
	private long _managedMemoryBaselineBytes = GC.GetTotalMemory(false);
	private readonly List<WorldDrop> _runtimeDropScratch = new();

	private void UpdateRuntimeCleanup(float step)
	{
		_runtimeCleanupRemaining -= step;
		_memoryPressureCheckRemaining -= step;
		_emergencyFullGcCooldownRemaining = Mathf.Max(_emergencyFullGcCooldownRemaining - step, 0.0f);

		if (_runtimeCleanupRemaining <= 0.0f)
		{
			_runtimeCleanupRemaining = WorldDropConfig.CleanupIntervalSeconds;
			SweepWorldDrops();
		}

		if (_memoryPressureCheckRemaining <= 0.0f)
		{
			_memoryPressureCheckRemaining = PerformanceConfig.MemoryPressureCheckIntervalSeconds;
			long managedBytes = GC.GetTotalMemory(false);
			long growthBytes = managedBytes - _managedMemoryBaselineBytes;

			// Never force collections on a timer during ordinary play. The CLR's
			// generational collector handles short-lived wrappers efficiently. This
			// non-blocking emergency request only runs after sustained, substantial
			// growth above the configured safety threshold.
			if (_emergencyFullGcCooldownRemaining <= 0.0f
				&& managedBytes >= PerformanceConfig.ManagedMemoryPressureBytes
				&& growthBytes >= PerformanceConfig.ManagedMemoryGrowthBytes)
			{
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false, false);
				_managedMemoryBaselineBytes = GC.GetTotalMemory(false);
				_emergencyFullGcCooldownRemaining = PerformanceConfig.EmergencyFullGcCooldownSeconds;
			}
			else if (managedBytes < _managedMemoryBaselineBytes)
			{
				_managedMemoryBaselineBytes = managedBytes;
			}
		}
	}

	private void SweepWorldDrops()
	{
		_runtimeDropScratch.Clear();
		IReadOnlyList<WorldDrop> drops = WorldDrop.ActiveDrops;
		// Iterate backwards: Recycle() removes the drop from the active registry
		// (this same live list) synchronously, so a forward pass would skip the
		// element shifted into the freed slot.
		for (int index = drops.Count - 1; index >= 0; index--)
		{
			WorldDrop drop = drops[index];
			if (!IsInstanceValid(drop) || drop.IsQueuedForDeletion())
			{
				continue;
			}

			// Independent failsafe in case a drop's own process was paused or disabled.
			if (drop.AgeSeconds >= Mathf.Max(drop.LifetimeSeconds, 1.0f))
			{
				drop.Recycle();
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
				drop.Recycle();
			}
		}
	}
}
