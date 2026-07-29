// Loading screen tuning values.
// Edit these values to adjust the loading presentation without touching UI logic.
public static class LoadingScreenConfig
{
	// The loading overlay stays visible for at least this many seconds.
	public const double MinimumVisibleSeconds = 3.0;

	// Main visual sizes.
	public const int LoadingTextFontSize = 46;
	public const float BearModelScale = 1.15f;

	// Bear framing. Larger scale or a smaller camera distance makes the bear larger.
	public const float BearCameraDistance = 3.30f;
	public const float BearVerticalOffset = -0.90f;

	// Shared row anchors. Text and bear use the same top/bottom values.
	public const float RowTopAnchor = 0.78f;
	public const float RowBottomAnchor = 0.94f;
	public const float TextLeftAnchor = 0.28f;
	public const float TextRightAnchor = 0.60f;
	public const float BearLeftAnchor = 0.595f;
	public const float BearRightAnchor = 0.73f;

	// High-resolution 3D preview texture. Increase only if a very large bear is needed.
	public const int BearViewportWidth = 720;
	public const int BearViewportHeight = 480;
}
