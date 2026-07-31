using Godot;

public static partial class ExternalModelLibrary
{
	private static bool HasInvalidImportRemap(string path)
	{
		string importPath = $"{path}.import";
		if (!FileAccess.FileExists(importPath))
		{
			return false;
		}

		string importText = FileAccess.GetFileAsString(importPath);
		return importText.Contains("valid=false");
	}
}
