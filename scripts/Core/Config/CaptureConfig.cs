using Godot;

// Frequently tuned capture-rhythm difficulty rules.
public static class CaptureConfig
{
	public const int MinimumRhythmCommands = 5;
	public const int FirstCommandIncreaseLevel = 10;
	public const int LevelsPerCommandIncrease = 5;
	public const int CommandsPerIncrease = 2;
	public const float BaseRhythmTimeSeconds = 4.5f;
	public const float TimePerCommandSeconds = 0.55f;
	public const float MinimumRhythmTimeSeconds = 6.5f;

	public static int GetRhythmCommandCount(int monsterLevel)
	{
		int safeLevel = Mathf.Max(monsterLevel, 1);
		if (safeLevel < FirstCommandIncreaseLevel)
		{
			return MinimumRhythmCommands;
		}

		int increases = (safeLevel - FirstCommandIncreaseLevel) / LevelsPerCommandIncrease + 1;
		return MinimumRhythmCommands + increases * CommandsPerIncrease;
	}

	public static float GetRhythmTimeLimit(int commandCount)
	{
		return Mathf.Max(
			BaseRhythmTimeSeconds + Mathf.Max(commandCount, MinimumRhythmCommands) * TimePerCommandSeconds,
			MinimumRhythmTimeSeconds);
	}
}
