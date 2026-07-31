using Godot;

public static partial class ExternalModelLibrary
{
	private static void ApplyFallbackMaterials(Node root, string sourcePath)
	{
		ApplyFallbackMaterialsRecursive(root, sourcePath.ToLowerInvariant(), root.Name.ToString().ToLowerInvariant());
	}

	private static void ApplyFallbackMaterialsRecursive(Node node, string sourcePath, string inheritedName)
	{
		string nodeName = node.Name.ToString().ToLowerInvariant();
		string materialKey = $"{inheritedName} {nodeName}";
		if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
		{
			int surfaceCount = meshInstance.Mesh.GetSurfaceCount();
			for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
			{
				Material? material = meshInstance.GetSurfaceOverrideMaterial(surfaceIndex) ?? meshInstance.Mesh.SurfaceGetMaterial(surfaceIndex);
				if (HasUsefulMaterialColor(material))
				{
					continue;
				}

				meshInstance.SetSurfaceOverrideMaterial(surfaceIndex, PickFallbackMaterial(sourcePath, materialKey, surfaceIndex));
			}
		}

		foreach (Node child in node.GetChildren())
		{
			ApplyFallbackMaterialsRecursive(child, sourcePath, materialKey);
		}
	}

	private static bool HasUsefulMaterialColor(Material? material)
	{
		if (material == null)
		{
			return false;
		}

		if (material is not StandardMaterial3D standardMaterial)
		{
			return true;
		}

		if (standardMaterial.AlbedoTexture != null)
		{
			return true;
		}

		Color color = standardMaterial.AlbedoColor;
		float max = Mathf.Max(color.R, Mathf.Max(color.G, color.B));
		float min = Mathf.Min(color.R, Mathf.Min(color.G, color.B));
		float saturation = max <= 0.001f ? 0.0f : (max - min) / max;
		bool nearlyWhite = max > 0.78f && saturation < 0.12f;
		bool nearlyFlatGray = max > 0.28f && saturation < 0.08f;
		return !nearlyWhite && !nearlyFlatGray;
	}

	private static StandardMaterial3D PickFallbackMaterial(string sourcePath, string meshName, int surfaceIndex)
	{
		string key = $"{sourcePath} {meshName}";

		if (key.Contains("leaf") || key.Contains("leaves") || key.Contains("tree") || key.Contains("hedge") || key.Contains("grass"))
		{
			return ModelMaterial(new Color(0.18f, 0.50f, 0.18f));
		}

		if (key.Contains("trunk") || key.Contains("wood") || key.Contains("fence") || key.Contains("cart") || key.Contains("door") || key.Contains("staff") || key.Contains("bow"))
		{
			return ModelMaterial(new Color(0.42f, 0.26f, 0.13f));
		}

		if (key.Contains("roof") || key.Contains("stall-red") || key.Contains("banner-red"))
		{
			return ModelMaterial(new Color(0.66f, 0.16f, 0.14f));
		}

		if (key.Contains("stall-green") || key.Contains("banner-green"))
		{
			return ModelMaterial(new Color(0.16f, 0.48f, 0.22f));
		}

		if (key.Contains("window") || key.Contains("glass") || key.Contains("crystal"))
		{
			return ModelMaterial(new Color(0.36f, 0.78f, 0.96f, 0.82f), 0.18f, true);
		}

		if (key.Contains("water") || key.Contains("fountain"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.34f, 0.62f, 0.78f, 0.62f) : new Color(0.52f, 0.52f, 0.47f), 0.16f, surfaceIndex % 2 == 0);
		}

		if (key.Contains("rock") || key.Contains("stone") || key.Contains("wall") || key.Contains("chimney") || key.Contains("stairs"))
		{
			return ModelMaterial(new Color(0.48f, 0.47f, 0.42f));
		}

		if (key.Contains("sword") || key.Contains("shield") || key.Contains("armor") || key.Contains("metal") || key.Contains("blade"))
		{
			return ModelMaterial(new Color(0.68f, 0.72f, 0.74f), 0.34f);
		}

		if (key.Contains("skin") || key.Contains("head") || key.Contains("face") || key.Contains("hand"))
		{
			return ModelMaterial(new Color(0.82f, 0.58f, 0.40f));
		}

		if (key.Contains("mage"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.32f, 0.22f, 0.66f) : new Color(0.86f, 0.66f, 0.26f));
		}

		if (key.Contains("rogue"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.12f, 0.18f, 0.22f) : new Color(0.44f, 0.24f, 0.14f));
		}

		if (key.Contains("knight") || key.Contains("guard"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.20f, 0.34f, 0.62f) : new Color(0.70f, 0.72f, 0.68f), surfaceIndex % 2 == 1 ? 0.36f : 0.78f);
		}

		if (key.Contains("barbarian") || key.Contains("warrior"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.48f, 0.22f, 0.10f) : new Color(0.24f, 0.13f, 0.08f));
		}

		if (key.Contains("street_rat"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.38f, 0.28f, 0.22f) : new Color(0.72f, 0.58f, 0.48f));
		}

		if (key.Contains("animal-fox"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.92f, 0.34f, 0.12f) : new Color(0.96f, 0.84f, 0.70f));
		}

		if (key.Contains("animal-deer") || key.Contains("animal-beaver") || key.Contains("animal-hog"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.54f, 0.32f, 0.16f) : new Color(0.78f, 0.58f, 0.36f));
		}

		if (key.Contains("animal-bunny"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.90f, 0.80f, 0.72f) : new Color(1.0f, 0.74f, 0.82f));
		}

		if (key.Contains("animal-crab"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.86f, 0.18f, 0.12f) : new Color(1.0f, 0.42f, 0.24f));
		}

		if (key.Contains("animal-fish"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.22f, 0.66f, 0.92f) : new Color(0.76f, 0.92f, 1.0f));
		}

		if (key.Contains("animal-caterpillar"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.32f, 0.70f, 0.18f) : new Color(0.78f, 0.94f, 0.34f));
		}

		if (key.Contains("animal-bee"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(1.0f, 0.78f, 0.12f) : new Color(0.10f, 0.10f, 0.08f));
		}

		if (key.Contains("animal-lion") || key.Contains("animal-tiger"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.90f, 0.46f, 0.12f) : new Color(0.18f, 0.12f, 0.08f));
		}

		if (key.Contains("animal-polar"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.86f, 0.92f, 1.0f) : new Color(0.56f, 0.64f, 0.72f));
		}

		if (key.Contains("animal-elephant"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.50f, 0.56f, 0.60f) : new Color(0.74f, 0.76f, 0.74f));
		}

		if (key.Contains("monster") || key.Contains("orc") || key.Contains("demon") || key.Contains("imp") || key.Contains("beast") || key.Contains("wolf"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.64f, 0.15f, 0.12f) : new Color(0.25f, 0.08f, 0.07f));
		}

		if (key.Contains("slime") || key.Contains("ghost") || key.Contains("spitter"))
		{
			return ModelMaterial(new Color(0.24f, 0.78f, 0.74f, 0.76f), 0.16f, true);
		}

		return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.54f, 0.42f, 0.28f) : new Color(0.76f, 0.58f, 0.32f));
	}

	private static StandardMaterial3D ModelMaterial(Color color, float roughness = 0.78f, bool transparent = false)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness,
		};

		if (transparent || color.A < 1.0f)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		}

		return material;
	}
}
