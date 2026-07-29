// Runtime performance tuning. Keep frequently adjusted update rates and cleanup
// thresholds in one place so gameplay code does not grow scattered magic numbers.
public static class PerformanceConfig
{
	// UI does not need to rebuild text at render-frame frequency.
	public const float HudRefreshIntervalSeconds = 0.10f;
	public const float TargetHudRefreshIntervalSeconds = 0.10f;
	public const float PartyDetailsRefreshIntervalSeconds = 0.20f;
	public const float MinimapRefreshIntervalSeconds = 0.10f;
	public const float BackgroundSystemsRefreshIntervalSeconds = 1.0f;

	// World-drop animation remains smooth while avoiding 60 updates per second for
	// every item left on the ground.
	public const float WorldDropVisualRefreshIntervalSeconds = 1.0f / 30.0f;
	public const int MaximumVisibleCombatEffects = 96;
	public const int MaximumVisibleSkillEffects = 72;

	// The CLR already performs generational GC. We only request a collection when
	// managed memory has both grown substantially and crossed a safety threshold.
	public const float MemoryPressureCheckIntervalSeconds = 15.0f;
	public const float EmergencyFullGcCooldownSeconds = 180.0f;
	public const long ManagedMemoryPressureBytes = 384L * 1024L * 1024L;
	public const long ManagedMemoryGrowthBytes = 48L * 1024L * 1024L;
}
