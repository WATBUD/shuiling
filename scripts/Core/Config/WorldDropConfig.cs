// Physical pickup lifecycle, polling and runtime cleanup limits.
public static class WorldDropConfig
{
	public const float PickupRadius = 1.65f;
	public const float LifetimeSeconds = 75.0f;
	public const float CollectionRefreshSeconds = 0.10f;
	public const float CleanupIntervalSeconds = 5.0f;
	public const int MaximumActiveDrops = 180;
	public const float GenerationZeroGcIntervalSeconds = 60.0f;
	public const float FullGcIntervalSeconds = 300.0f;
}
